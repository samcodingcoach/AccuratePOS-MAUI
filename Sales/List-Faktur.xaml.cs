using System.Text;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Net.Http.Headers;

namespace MyPosAccurate2026.Sales;

public partial class List_Faktur : ContentPage
{
    public ObservableCollection<InvoiceData> InvoiceList { get; set; } = new ObservableCollection<InvoiceData>();

    // Variabel state untuk akumulasi Grand Total & Paginasi
    private double _grandTotalAmount = 0;
    private int _currentPage = 1;
    private bool _isFetching = false;
    private bool _hasMoreData = true;

    // Menyimpan state filter agar paginasi ke halaman berikutnya tetap sinkron
    private string _activeStartDate = "";
    private string _activeEndDate = "";
    private string _activeSearch = "";

    // Properti ini dibinding ke UI Label Total Transaksi
    public string FormattedGrandTotal => $"Rp {_grandTotalAmount:N0}";

    public List_Faktur()
    {
        InitializeComponent();

        // Wajib diset agar {Binding FormattedGrandTotal} dapat terbaca oleh layar utama
        BindingContext = this;
        InvoiceCollectionView.ItemsSource = InvoiceList;

        // Set default nilai input tanggal ke Hari Ini menggunakan x:Name Anda
        DP_startdate.Date = DateTime.Today;
        DP_enddate.Date = DateTime.Today;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Membaca data pertama kali halaman dibuka (Otomatis Filter Hari Ini)
        if (InvoiceList.Count == 0)
        {
            ResetFilterState();
            await LoadDataFromServer(isRefresh: true);
        }
    }

    
    private void ResetFilterState()
    {
        DP_startdate.Date = DateTime.Today;
        DP_enddate.Date = DateTime.Today;
        Search_FakturKonsumen.Text = string.Empty;

        _activeStartDate = $"{DateTime.Today:yyyy-MM-dd}";
        _activeEndDate = $"{DateTime.Today:yyyy-MM-dd}";
        _activeSearch = string.Empty;
    }

    
    private async void B_Filter_Clicked(object sender, EventArgs e)
    {
        // Mengambil nilai dari x:Name XAML Anda
        // Menggunakan String Interpolation untuk menghindari error ToString()
        _activeStartDate = $"{DP_startdate.Date:yyyy-MM-dd}";
        _activeEndDate = $"{DP_enddate.Date:yyyy-MM-dd}";

        _activeSearch = Search_FakturKonsumen.Text?.Trim() ?? string.Empty;

        // Tarik data dengan mode Refresh (kembali ke page 1)
        await LoadDataFromServer(isRefresh: true);
    }

    // EVENT HANDLER: Tombol Reset Ditekan
    private async void B_Reset_Clicked(object sender, EventArgs e)
    {
        ResetFilterState();
        await LoadDataFromServer(isRefresh: true);
    }

    // EVENT HANDLER: Scroll bawah pada CollectionView
    private async void OnLoadMoreItems(object sender, EventArgs e)
    {
        // Tarik data mode Load More (lanjut ke page berikutnya)
        await LoadDataFromServer(isRefresh: false);
    }

    // Core Logic Pemanggilan API dengan HttpClient
    private async Task LoadDataFromServer(bool isRefresh)
    {
        // Cegah pemanggilan ganda / cegah load jika data sudah habis
        if (_isFetching || (!_hasMoreData && !isRefresh)) return;

        _isFetching = true;

        if (isRefresh)
        {
            _currentPage = 1;
            _grandTotalAmount = 0;
            _hasMoreData = true;
            InvoiceList.Clear();
            OnPropertyChanged(nameof(FormattedGrandTotal)); // Beritahu UI kalau nilai jadi 0 sementara
        }

        try
        {
            // Susun Endpoint beserta paging
            var urlBuilder = new StringBuilder($"{App.API_HOST}penjualan/list-invoice.php?page={_currentPage}&limit=100");

            // Filter Parameter logic
            if (!string.IsNullOrEmpty(_activeSearch))
            {
                urlBuilder.Append($"&search={Uri.EscapeDataString(_activeSearch)}");
            }
            else
            {
                urlBuilder.Append($"&start_date={_activeStartDate}&end_date={_activeEndDate}");
            }

            string apiUrl = urlBuilder.ToString();

            string secureToken = Preferences.Get("TOKEN_KEY", "");
            string cleanToken = secureToken.Replace("Bearer ", "").Trim();

            if (string.IsNullOrEmpty(cleanToken))
            {
                await DisplayAlertAsync("Sesi Habis", "Anda harus login kembali.", "OK");
                _isFetching = false;
                return;
            }

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);

                var response = await client.GetAsync(apiUrl);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (responseContent.StartsWith("<"))
                {
                    await DisplayAlertAsync("Error Server", "Gagal membaca format data dari server.", "OK");
                    _isFetching = false;
                    return;
                }

                var responseObject = JsonConvert.DeserializeObject<InvoiceListResponse>(responseContent);

                if (responseObject != null && responseObject.status == "success")
                {
                    var data = responseObject.data;

                    if (data == null || data.Count == 0)
                    {
                        _hasMoreData = false; // Tandai data sudah habis dari API
                    }
                    else
                    {
                        foreach (var invoice in data)
                        {
                            InvoiceList.Add(invoice);

                            // Tambahkan total uang ke Grand Total
                            _grandTotalAmount += invoice.totalAmount;
                        }

                        // Beri tahu halaman agar text Grand Total ter-update di layar UI
                        OnPropertyChanged(nameof(FormattedGrandTotal));

                        // Jika data yg didapat < limit(100), maka tidak ada lagi halaman berikutnya
                        if (data.Count < 100)
                        {
                            _hasMoreData = false;
                        }
                        else
                        {
                            _currentPage++;
                        }
                    }
                }
                else
                {
                    string errorServer = responseObject?.message ?? "Format respons server tidak sesuai.";
                    await DisplayAlertAsync("Gagal (Respon Server)", errorServer, "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Koneksi Gagal", ex.Message, "OK");
        }
        finally
        {
            _isFetching = false;
        }
    }

    // =========================================================
    // CLASS MODEL MAPPING JSON -> C#
    // =========================================================
    public class InvoiceListResponse
    {
        public string status { get; set; }
        public string message { get; set; }
        public List<InvoiceData> data { get; set; }
    }

    public class InvoiceData
    {
        public string number { get; set; }
        public double totalAmount { get; set; }
        public string transDate { get; set; }
        public string statusName { get; set; }
        public int id { get; set; }
        public string transDateView { get; set; }
        public CustomerData customer { get; set; }

        public string FormattedTotalAmount => $"Rp {totalAmount:N0}";
        public string CustomerDisplay => $"{customer?.customerNo} - {customer?.name}";

        public Color StatusTextColor =>
            statusName?.Trim() == "Lunas"
            ? Color.FromArgb("#155724")
            : Color.FromArgb("#ff4f4f");

        public Color StatusBgColor =>
            statusName?.Trim() == "Lunas"
            ? Color.FromArgb("#d4edda")
            : Color.FromArgb("#ff9191");
    }

    public class CustomerData
    {
        public string name { get; set; }
        public int id { get; set; }
        public string customerNo { get; set; }
    }

    private async void B_NewFak_Clicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new Sales.New_Faktur());
    }
}