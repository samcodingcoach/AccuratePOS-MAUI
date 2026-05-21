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
    public List_Faktur()
	{
		InitializeComponent();

        InvoiceCollectionView.ItemsSource = InvoiceList;
    }

    public class InvoiceListResponse
    {
        public string status { get; set; }
        public string message { get; set; } // <--- Tambahkan baris ini
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

        // --- HELPER PROPERTY UNTUK UI BINDING ---

        // Format "Rp 10.000.000"
        public string FormattedTotalAmount => $"Rp {totalAmount:N0}";

        // Tampilan "MB001 - Non Member"
        public string CustomerDisplay => $"{customer?.customerNo} - {customer?.name}";

        // Warna Teks Status
        public Color StatusTextColor =>
            statusName?.Trim() == "Lunas"
            ? Color.FromArgb("#155724") // Hijau Gelap
            : Color.FromArgb("#ff4f4f"); // Merah asli desain Anda

        // Warna Latar (Background) Status
        public Color StatusBgColor =>
            statusName?.Trim() == "Lunas"
            ? Color.FromArgb("#d4edda") // Hijau Terang
            : Color.FromArgb("#ff9191"); // Pink asli desain Anda
    }

    public class CustomerData
    {
        public string name { get; set; }
        public int id { get; set; }
        public string customerNo { get; set; }
    }

    

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadInvoicesFromServer();
    }

    private async Task LoadInvoicesFromServer()
    {
        try
        {
            // Tentukan URL API sesuai setting global Anda
            string apiUrl = App.API_HOST + "penjualan/list-invoice.php";

            // Ambil token otorisasi dari perangkat (Token ini sudah ada kata "Bearer " di depannya)
            string secureToken = Preferences.Get("TOKEN_KEY", "");

            if (string.IsNullOrEmpty(secureToken))
            {
                await DisplayAlertAsync("Sesi Habis", "Anda harus login kembali.", "OK");
                return;
            }

            using (var client = new HttpClient())
            {
                // Masukkan token ke header Authorization
                client.DefaultRequestHeaders.Add("Authorization", secureToken);

                var response = await client.GetAsync(apiUrl);
                var responseContent = await response.Content.ReadAsStringAsync();

                // Cegat pesan error berupa HTML dari PHP (jika terjadi error backend)
                if (responseContent.StartsWith("<"))
                {
                    System.Diagnostics.Debug.WriteLine($"HTML Detected: {responseContent}");
                    await DisplayAlertAsync("Error Server", "Gagal membaca data karena server merespons dengan format tidak valid.", "OK");
                    return;
                }

                // Terjemahkan JSON ke objek C#
                var responseObject = JsonConvert.DeserializeObject<InvoiceListResponse>(responseContent);

                if (responseObject != null && responseObject.status == "success")
                {
                    InvoiceList.Clear();

                    // Loop dan masukkan ke dalam List UI
                    foreach (var invoice in responseObject.data)
                    {
                        InvoiceList.Add(invoice);
                    }
                }
                else
                {
                    // TAMPILKAN PESAN ERROR ASLI DARI SERVER PHP
                    string errorServer = responseObject?.message ?? "Format respons server tidak sesuai.";

                    // Tampilkan popup ke layar HP
                    await DisplayAlertAsync("Gagal (Respon Server)", errorServer, "OK");

                    // Cetak ke Output Console Visual Studio untuk Anda analisis
                    System.Diagnostics.Debug.WriteLine("=== RAW RESPONSE ERROR ===");
                    System.Diagnostics.Debug.WriteLine(responseContent);
                    System.Diagnostics.Debug.WriteLine("==========================");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exception: {ex.Message}");
            await DisplayAlertAsync("Koneksi Gagal", ex.Message, "OK");
        }
    }
}