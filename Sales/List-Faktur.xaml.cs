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

    // Variabel state untuk akumulasi nilai & Paginasi
    private double _grandTotalAmount = 0;
    private int _currentPage = 1;
    private bool _isFetching = false;
    private bool _hasMoreData = true;

    // Properti yang dibinding ke UI untuk Box Total (otomatis notify saat nilainya berubah)
    public string FormattedGrandTotal => $"Rp {_grandTotalAmount:N0}";

    public List_Faktur()
    {
        InitializeComponent();

        // WAJIB: Agar {Binding FormattedGrandTotal} di halaman utama (bukan di list) bisa terbaca
        BindingContext = this;
        InvoiceCollectionView.ItemsSource = InvoiceList;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Hanya load saat pertama kali dibuka atau list kosong
        if (InvoiceList.Count == 0)
        {
            await LoadDataFromServer(isRefresh: true);
        }
    }

    // Event saat user men-scroll list mendekati akhir
    private async void OnLoadMoreItems(object sender, EventArgs e)
    {
        await LoadDataFromServer(isRefresh: false);
    }

    private async Task LoadDataFromServer(bool isRefresh)
    {
        // Cegah request ganda atau jika data sudah mentok (habis)
        if (_isFetching || (!_hasMoreData && !isRefresh)) return;

        _isFetching = true;

        if (isRefresh)
        {
            _currentPage = 1;
            _grandTotalAmount = 0;
            _hasMoreData = true;
            InvoiceList.Clear();
            OnPropertyChanged(nameof(FormattedGrandTotal));
        }

        try
        {
            // Set Format Tanggal Hari Ini (Contoh: "2026-05-21")
            string todayDate = DateTime.Now.ToString("yyyy-MM-dd");

            // Susun URL endpoint beserta parameter filter default & paginasi
            string apiUrl = $"{App.API_HOST}penjualan/list-invoice.php?start_date={todayDate}&end_date={todayDate}&page={_currentPage}&limit=100";

            string secureToken = Preferences.Get("TOKEN_KEY", "");
            string cleanToken = secureToken.Replace("Bearer ", "").Trim();

            if (string.IsNullOrEmpty(cleanToken))
            {
                await DisplayAlertAsync("Sesi Habis", "Anda harus login kembali.", "OK");
                return;
            }

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);

                var response = await client.GetAsync(apiUrl);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (responseContent.StartsWith("<"))
                {
                    System.Diagnostics.Debug.WriteLine($"HTML Detected: {responseContent}");
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
                        // Data habis
                        _hasMoreData = false;
                    }
                    else
                    {
                        foreach (var invoice in data)
                        {
                            InvoiceList.Add(invoice);

                            // Akumulasi nilai uang
                            _grandTotalAmount += invoice.totalAmount;
                        }

                        // Beri tahu UI bahwa nilai Grand Total berubah agar ter-update di layar
                        OnPropertyChanged(nameof(FormattedGrandTotal));

                        // Jika data yang dikembalikan kurang dari 100 baris, artinya kita sudah di halaman terakhir
                        if (data.Count < 100)
                        {
                            _hasMoreData = false;
                        }
                        else
                        {
                            _currentPage++; // Siapkan untuk request halaman berikutnya
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
            System.Diagnostics.Debug.WriteLine($"Exception: {ex.Message}");
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
}