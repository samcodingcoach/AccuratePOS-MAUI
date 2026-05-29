using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Globalization;
namespace MyPosAccurate2026.Sales;

public partial class ItemAdd : ContentPage
{
    public double ItemBalance { get; set; }
    public string CustomerCode { get; set; }
    public string SelectedSalesNumber { get; private set; }

    public List<SerialData> AvailableSerialNumbers { get; set; } = new List<SerialData>();

    public ItemAdd(string itemNo, string name, double balance, string konsumenValue)
    {
        InitializeComponent();

        cek_token();
        
        CustomerCode = konsumenValue;
        ItemBalance = balance;
        FormNoItem.Text = itemNo;
        FormNamaBarang.Text = name;
        FormPriceCategory.Text = CustomerCode;

        _ = LoadItemStockPrice(itemNo, konsumenValue);

        LoadSalesData();

        _ = LoadSerialNumber(itemNo);
    }


    private async Task LoadItemStockPrice(string itemNo, string customerCode)
    {
        try
        {
            string cleanToken = Preferences.Get("TOKEN_KEY", "").Replace("Bearer ", "").Trim();

            if (string.IsNullOrEmpty(cleanToken))
            {
                System.Diagnostics.Debug.WriteLine("Token tidak ditemukan.");
                return;
            }

            // Susun URL API (Gunakan Uri.EscapeDataString untuk menghindari error jika ada spasi pada string)
            string apiUrl = $"{App.API_HOST}item/stokharga.php?no={Uri.EscapeDataString(itemNo)}&priceCategoryName={Uri.EscapeDataString(customerCode)}";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);

                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();

                    // Deserialize data dari server
                    var apiResult = JsonConvert.DeserializeObject<ItemStockPriceResponse>(responseContent);

                    if (apiResult != null && apiResult.data != null)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            // 1. Update Harga Jual (Otomatis ditambah format titik ribuan)
                            FormHargaJual.Text = apiResult.data.unitPrice.ToString("N0", new CultureInfo("id-ID"));

                            // 2. Update Nilai Balance (Stok Terkini dari API)
                            ItemBalance = apiResult.data.availableStock;

                            System.Diagnostics.Debug.WriteLine($"Harga: {apiResult.data.unitPrice}, Stok: {apiResult.data.availableStock}");

                            // [OPSIONAL] Jika di ItemAdd.xaml Anda membuat Label untuk stok, 
                            // tambahkan x:Name="LabelStokInfo" lalu buka kode di bawah ini:
                            // LabelStokInfo.Text = apiResult.data.availableStock.ToString();
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Gagal memuat harga dan stok: {ex.Message}");
        }
    }

    private async Task LoadSerialNumber(string itemNo)
    {
        try
        {
            string cleanToken = Preferences.Get("TOKEN_KEY", "").Replace("Bearer ", "").Trim();
            if (string.IsNullOrEmpty(cleanToken)) return;

            string apiUrl = $"{App.API_HOST}item/serial_byNo.php?no={Uri.EscapeDataString(itemNo)}";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);

                var response = await client.GetAsync(apiUrl);
                string responseContent = await response.Content.ReadAsStringAsync();

                // Parsing JSON yang masuk
                var apiResult = JsonConvert.DeserializeObject<SerialResponse>(responseContent);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (apiResult != null && apiResult.status == "success")
                    {
                        // Akses list melalui apiResult.data.d
                        if (apiResult.data != null && apiResult.data.d != null)
                        {
                            AvailableSerialNumbers = apiResult.data.d;
                            GridNoSeri.IsVisible = true;

                            System.Diagnostics.Debug.WriteLine($"Form SN Dimunculkan. Ada {AvailableSerialNumbers.Count} SN tersedia.");
                        }
                    }
                    else
                    {
                        // Otomatis tereksekusi jika status == "error" 
                        // (Barang yang dicari bukan merupakan barang dengan Serial Number)
                        AvailableSerialNumbers.Clear();
                        GridNoSeri.IsVisible = false;

                        System.Diagnostics.Debug.WriteLine($"Form SN Disembunyikan. Pesan: {apiResult?.message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() => GridNoSeri.IsVisible = false);
            System.Diagnostics.Debug.WriteLine($"CRASH di LoadSerialNumber: {ex.Message}");
        }
    }

    public class ItemStockPriceResponse
    {
        public string status { get; set; }
        public string message { get; set; }

        // Perhatikan ini BUKAN List<T>, melainkan langsung satu objek
        public ItemStockPriceData data { get; set; }
    }

    public class ItemStockPriceData
    {
        public string no { get; set; }
        public string name { get; set; }
        public double unitPrice { get; set; }
        public double availableStock { get; set; }
    }

    private async void cek_token()
    {
        string token = Preferences.Get("TOKEN_KEY", string.Empty);

        // Cek dengan if
        if (!string.IsNullOrEmpty(token))
        {
            System.Diagnostics.Debug.WriteLine($"Token ditemukan: {token}");
        }
        else
        {

            await DisplayAlertAsync("Alert","Token Tidak Ditemukan", "OK");
            System.Diagnostics.Debug.WriteLine("Token tidak ditemukan");

        }

    }

    public class EmployeeResponse
    {
        // Asumsi balasan API Anda menggunakan struktur standar dengan data berupa List
        public string status { get; set; }
        public string message { get; set; }
        public List<EmployeeData> data { get; set; }
    }

    public class EmployeeData
    {
        public string number { get; set; }
        public string name { get; set; }

        // Properti khusus untuk digabungkan dan ditampilkan ke Picker (ItemDisplayBinding)
        public string DisplayName => $"{number} - {name}";
    }


    // =========================================================
    // CLASS MODEL MAPPING SERIAL NUMBER
    // =========================================================
    public class SerialResponse
    {
        public string status { get; set; }
        public string message { get; set; }

        // Sekarang data adalah objek wrapper, bukan list langsung
        public SerialDataWrapper data { get; set; }
    }

    public class SerialDataWrapper
    {
        public bool s { get; set; }
        // Properti 'd' inilah yang menyimpan list array Serial Number-nya
        public List<SerialData> d { get; set; }
    }

    public class SerialData
    {
        public WarehouseData warehouse { get; set; }
        public SerialNumberInfo serialNumber { get; set; }
        public double quantity { get; set; }
    }

    public class WarehouseData
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class SerialNumberInfo
    {
        public int id { get; set; }
        public string number { get; set; }
        public string createDate { get; set; }
        public string expiredDate { get; set; }
    }

    private void BClose_Clicked(object sender, EventArgs e)
    {

    }

    private void BSimpan_Clicked(object sender, EventArgs e)
    {

    }

    private void PickerSales_SelectedIndexChanged(object sender, EventArgs e)
    {
        // Tangkap objek dari item yang dipilih
        if (PickerSales.SelectedItem is EmployeeData selectedSales)
        {
            // Simpan 'number' ke dalam variabel global halaman ini
            SelectedSalesNumber = selectedSales.number;

            System.Diagnostics.Debug.WriteLine($"Sales Dipilih: {SelectedSalesNumber} - {selectedSales.name}");
        }
    }

    private async Task LoadSalesData()
    {
        try
        {
            // 1. Ambil token dari Preferences
            string cleanToken = Preferences.Get("TOKEN_KEY", "").Replace("Bearer ", "").Trim();

            if (string.IsNullOrEmpty(cleanToken))
            {
                System.Diagnostics.Debug.WriteLine("Token tidak ditemukan.");
                return;
            }

            // 2. Susun URL API untuk mengambil list karyawan yang merupakan sales
            string apiUrl = $"{App.API_HOST}karyawan/list.php?sales=true";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);

                // 3. Tarik data dari server
                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();

                    // 4. Konversi JSON ke object C#
                    var apiResult = JsonConvert.DeserializeObject<EmployeeResponse>(responseContent);

                    if (apiResult != null && apiResult.data != null)
                    {
                        // 5. Masukkan data ke dalam Picker di Main Thread
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            PickerSales.ItemsSource = apiResult.data;
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Gagal memuat data sales: " + ex.Message);
        }
    }
}