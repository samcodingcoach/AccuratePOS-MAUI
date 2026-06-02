using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using static MyPosAccurate2026.Sales.New_Faktur;
namespace MyPosAccurate2026.Sales;

public partial class ItemAdd : ContentPage
{
    public double ItemBalance { get; set; }
    public double balanceQty { get; set; }
    public string CustomerCode { get; set; }
    public string SelectedSalesNumber { get; private set; }
    public List<SerialData> AvailableSerialNumbers { get; set; } = new List<SerialData>();
    public ObservableCollection<AddedSerialModel> AddedSerialNumbers { get; set; } = new ObservableCollection<AddedSerialModel>();

    public event EventHandler<CartItemModel> OnItemSaved;

    public ItemAdd(string itemNo, string name, double balance, string konsumenValue)
    {
        InitializeComponent();

        cek_token();
        
        CustomerCode = konsumenValue;
        ItemBalance = balance;
        FormNoItem.Text = itemNo;
        FormNamaBarang.Text = name;
        FormPriceCategory.Text = CustomerCode;

        BindableLayout.SetItemsSource(ListSnContainer, AddedSerialNumbers);

        UpdateSerialCounter();

        _ = LoadItemStockPrice(itemNo, konsumenValue);

        _= LoadSalesData();

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

                            

                            // [OPSIONAL] Jika di ItemAdd.xaml Anda membuat Label untuk stok, 
                            // tambahkan x:Name="LabelStokInfo" lalu buka kode di bawah ini:
                            balanceQty = apiResult.data.availableStock;
                            FormLabelStokAvailable.Text = "Stock tersedia: " + balanceQty;

                            HitungTotalHarga();

                            System.Diagnostics.Debug.WriteLine($"Harga: {apiResult.data.unitPrice}, Stok: {apiResult.data.availableStock}");
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
                        FormSerialCounter.IsVisible = false;
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

    // =========================================================
    // CLASS MODEL PENAMPUNG SN INPUTAN USER
    // =========================================================
    public class AddedSerialModel
    {
        public string SerialNumber { get; set; }
        public double Qty { get; set; }
        public string WarehouseName { get; set; }
    }



    private async void BSimpan_Clicked(object sender, EventArgs e)
    {
        // 1. Validasi Qty
        if (!int.TryParse(FormQty.Text, out int qty) || qty <= 0)
        {
            await DisplayAlertAsync("Peringatan", "Kuantitas tidak valid.", "OK");
            return;
        }

        // 2. Validasi Serial Number (Jika form SN muncul, SN harus diisi penuh)
        if (GridNoSeri.IsVisible && AddedSerialNumbers.Count < qty)
        {
            await DisplayAlertAsync("Peringatan", $"Barang ini butuh {qty} Serial Number, Anda baru memasukkan {AddedSerialNumbers.Count}.", "OK");
            return;
        }

        // 3. Ambil Nilai Harga Bersih
        string cleanHarga = FormHargaJual.Text?.Replace("Rp", "")?.Replace(".", "")?.Trim() ?? "0";
        if (!double.TryParse(cleanHarga, out double harga))
        {
            await DisplayAlertAsync("Peringatan", "Harga jual tidak valid.", "OK");
            return;
        }

        // ==========================================================
        // 4. TAMBAHAN: Validasi Harga Tidak Boleh 0
        // ==========================================================
        if (harga <= 0)
        {
            await DisplayAlertAsync("Peringatan", "Barang belum diberi harga jual.", "OK");
            return;
        }

        // 5. SUSUN JSON / DATA OBJECT 
        var cartItem = new CartItemModel
        {
            itemNo = FormNoItem.Text,
            itemName = FormNamaBarang.Text,
            unitPrice = harga,
            quantity = qty,
            warehouseName = FormNamaGudang.Text ?? "Gudang Utama",
            salesmanListNumber = SelectedSalesNumber ?? "",
            detailSerialNumber = AddedSerialNumbers.Select(sn => new DetailSerialNumber
            {
                serialNumberNo = sn.SerialNumber,
                quantity = (int)sn.Qty
            }).ToList()
        };

        // 6. TEMBAKKAN DATA KE HALAMAN NEW-FAKTUR
        OnItemSaved?.Invoke(this, cartItem);

        // 7. TUTUP HALAMAN ADD ITEM
        await Navigation.PopAsync();
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

    private async void BTambahSN_Clicked(object sender, EventArgs e)
    {
        string snInput = FormNomorSerial.Text?.Trim();

        // 1. Validasi Input Kosong
        if (string.IsNullOrEmpty(snInput))
        {
            await DisplayAlertAsync("Peringatan", "Kolom Nomor Serial tidak boleh kosong.", "OK");
            return;
        }

        // 2. Baca batas maksimal dari Qty
        int batasQty = 1;
        if (int.TryParse(FormQty.Text, out int parsedQty))
        {
            batasQty = parsedQty;
        }

        // 3. Validasi Batas Kuantitas
        if (AddedSerialNumbers.Count >= batasQty)
        {
            await DisplayAlertAsync("Peringatan", $"Anda hanya memasukkan Qty {batasQty}. Tidak dapat menambah SN melebihi kuantitas.", "OK");
            return;
        }

        // 4. Cegah Duplikasi Input SN yang sama
        if (AddedSerialNumbers.Any(x => x.SerialNumber.Equals(snInput, StringComparison.OrdinalIgnoreCase)))
        {
            await DisplayAlertAsync("Peringatan", "Nomor Serial ini sudah Anda tambahkan ke daftar.", "OK");
            FormNomorSerial.Text = string.Empty;
            return;
        }

        // 5. Pencocokan dengan Stok di API Accurate
        var matchedSn = AvailableSerialNumbers.FirstOrDefault(x =>
            x.serialNumber != null &&
            x.serialNumber.number.Equals(snInput, StringComparison.OrdinalIgnoreCase));

        if (matchedSn != null)
        {
            // Validasi sukses, masukkan ke keranjang SN
            AddedSerialNumbers.Add(new AddedSerialModel
            {
                SerialNumber = matchedSn.serialNumber.number,
                Qty = 1,
                WarehouseName = matchedSn.warehouse?.name ?? "Gudang Default"
            });

            // Bersihkan kolom input agar siap untuk ketikan SN berikutnya
            FormNomorSerial.Text = string.Empty;
        }
        else
        {
            await DisplayAlertAsync("Gagal", "Nomor Serial tidak ditemukan di sistem atau stok sudah habis.", "OK");
        }
    }

    private void HapusSN_Tapped(object sender, TappedEventArgs e)
    {
        // Menghapus SN jika tulisan "Hapus" diklik
        if (sender is Label label && label.BindingContext is AddedSerialModel snData)
        {
            AddedSerialNumbers.Remove(snData);
        }
    }

    private async void FormQty_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.NewTextValue))
            return;

        string cleanText = new string(e.NewTextValue.Where(char.IsDigit).ToArray());

        if (string.IsNullOrEmpty(cleanText))
        {
            FormQty.Text = string.Empty;
            return;
        }

        if (double.TryParse(cleanText, out double qty))
        {
            if (balanceQty <= 0)
            {
                if (qty != 0) FormQty.Text = "0";
                return;
            }

            if (qty == 0)
            {
                FormQty.Text = "1";
                return;
            }

            if (qty > balanceQty)
            {
                FormQty.Text = balanceQty.ToString();
                Dispatcher.Dispatch(async () =>
                {
                    await DisplayAlertAsync("Stok Terbatas", $"Kuantitas tidak boleh melebihi stok yang tersedia ({balanceQty}).", "OK");
                });
                return;
            }

            // ==========================================================
            // TAMBAHAN VALIDASI: Cegah Qty lebih kecil dari SN yang sudah diinput
            // ==========================================================
            if (qty < AddedSerialNumbers.Count)
            {
                FormQty.Text = AddedSerialNumbers.Count.ToString();
                Dispatcher.Dispatch(async () =>
                {
                    await DisplayAlertAsync("Peringatan", $"Kuantitas tidak boleh kurang dari jumlah Nomor Serial yang sudah dimasukkan ({AddedSerialNumbers.Count}). Hapus SN terlebih dahulu.", "OK");
                });
                return;
            }

            if (FormQty.Text != cleanText)
            {
                FormQty.Text = cleanText;
            }

            // TAMBAHKAN INI: Update label counter jika Qty berubah
            UpdateSerialCounter();

            HitungTotalHarga();
        }
    }

    private void UpdateSerialCounter()
    {
        int batasQty = 1;
        if (int.TryParse(FormQty.Text, out int parsedQty))
        {
            batasQty = parsedQty;
        }

        // Hitung sisa kebutuhan SN
        int sisa = batasQty - AddedSerialNumbers.Count;

        // Pastikan angka tidak negatif
        if (sisa < 0) sisa = 0;

        // Update teks ke Label UI
        FormSerialCounter.Text = $"Butuh serial: {sisa}";
    }

    private void HitungTotalHarga()
    {
        // 1. Ambil nilai Qty (Default 1 jika kosong/error)
        double qty = 1;
        if (double.TryParse(FormQty.Text, out double parsedQty))
        {
            qty = parsedQty;
        }

        // 2. Ambil nilai Harga Jual dan bersihkan dari titik atau tulisan "Rp"
        double hargaJual = 0;
        string cleanHarga = FormHargaJual.Text?.Replace("Rp", "")?.Replace(".", "")?.Trim() ?? "0";

        if (double.TryParse(cleanHarga, out double parsedHarga))
        {
            hargaJual = parsedHarga;
        }

        // 3. Kalikan
        double totalHarga = qty * hargaJual;

        // 4. Tampilkan ke Label dengan format pemisah ribuan ala Indonesia
        FormTotalHarga.Text = $"Rp {totalHarga.ToString("N0", new CultureInfo("id-ID"))}";
    }
}