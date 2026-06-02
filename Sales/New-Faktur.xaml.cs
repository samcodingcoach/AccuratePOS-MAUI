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

public partial class New_Faktur : ContentPage
{
    public string? SelectedKonsumenValue { get; private set; }
    public record KonsumenOption(string Text, string Value);

    public ObservableCollection<ItemModel> AutoCompleteResults { get; set; } = new ObservableCollection<ItemModel>();
    public ObservableCollection<SelectedBiayaModel> SelectedBiayaList { get; set; } = new ObservableCollection<SelectedBiayaModel>();

    public ObservableCollection<CartItemModel> CartItems { get; set; } = new ObservableCollection<CartItemModel>();

    // 1. Deklarasikan HttpClient tanpa langsung mengisi string URL di sini
    private readonly HttpClient _httpClient;

    private bool _isFormattingBiaya = false;

    public New_Faktur()
	{
		InitializeComponent();
        //cek_token();
        _httpClient = new HttpClient { BaseAddress = new Uri(App.API_HOST) };

        var listKonsumen = new List<KonsumenOption>
        {
            new("Free - MB003", "Free"),
            new("Shopee - C.00001", "Shopee"),
            new("Membership - MB002", "Membership"),
            new("Non Member - MB001", "Umum")
        };

        // 2. Bind ke Picker
        PickerKonsumen.ItemsSource = listKonsumen;

        List_AutoComplete.ItemsSource = AutoCompleteResults;

       
        BindableLayout.SetItemsSource(ListBiayaContainer, SelectedBiayaList);

        // Tambahkan di dalam constructor New_Faktur()
        BindableLayout.SetItemsSource(CartContainer, CartItems);


        _ = LoadCoaData();
    }

    private async void cek_token()
    {
        string token = Preferences.Get("TOKEN_KEY", string.Empty);

       

        // Cek dengan if
        if (!string.IsNullOrEmpty(token))
        {

            await DisplayAlertAsync("TES",token,"OK");
            System.Diagnostics.Debug.WriteLine($"Token ditemukan: {token}");
          
        }
        else
        {
           
            System.Diagnostics.Debug.WriteLine("Token tidak ditemukan");
            
        }

    }

    public class ApiResponse
    {
        public string status { get; set; }
        public string message { get; set; }
        public List<ItemModel> data { get; set; }
    }

    public class ItemModel
    {
        public string item_no { get; set; }
        public string name { get; set; }
        public double balance { get; set; }

        public Color StockColor => balance > 0 ? Color.FromArgb("#006400") : Color.FromArgb("#FF0000");
    }

    public class CoaResponse
    {
        public string status { get; set; }
        public string message { get; set; }
        public List<CoaData> data { get; set; }
    }

    public class CoaData
    {
        public string no { get; set; }
        public string name { get; set; }
        public int id { get; set; }
        public string DisplayName => $"{no} - {name}";
    }

    public class SelectedBiayaModel
    {
        public string No { get; set; }
        public string Name { get; set; }
        public double Nominal { get; set; }
        public string FormattedNominal => $"Rp {Nominal:N0}";
    }


    // =========================================================
    // MODEL KERANJANG BELANJA (CART ITEM)
    // Sesuai dengan spesifikasi JSON API
    // =========================================================
    public class CartItemModel
    {
        public string itemNo { get; set; }
        public string itemName { get; set; }
        public double unitPrice { get; set; }
        public int quantity { get; set; }
        public string warehouseName { get; set; }
        public string salesmanListNumber { get; set; }
        public List<DetailSerialNumber> detailSerialNumber { get; set; }

        // Properti Khusus UI (Tidak ikut diparsing API, hanya untuk tampilan)
        public string FormattedUnitPrice => $"Rp {unitPrice.ToString("N0", new CultureInfo("id-ID"))}";
        public string FormattedTotalPrice => $"Rp {(unitPrice * quantity).ToString("N0", new CultureInfo("id-ID"))}";
        public string DisplayQty => $"x{quantity}";
        public bool HasSerialNumbers => detailSerialNumber != null && detailSerialNumber.Count > 0;
        public string SerialNumbersDisplay => HasSerialNumbers ? "SN: " + string.Join(", ", detailSerialNumber.Select(x => x.serialNumberNo)) : "";
        public string InfoGudangSales => $"Gudang: {warehouseName} | Sales: {salesmanListNumber}";
    }

    public class DetailSerialNumber
    {
        public string serialNumberNo { get; set; }
        public int quantity { get; set; }
    }

    private async void SearchBar_Item_TextChanged(object sender, TextChangedEventArgs e)
    {
        string keyword = e.NewTextValue;

        if (string.IsNullOrWhiteSpace(keyword) || keyword.Length < 2)
        {
            Border_AutoComplete.IsVisible = false;
            AutoCompleteResults.Clear();
            return;
        }

        await Task.Delay(400); // Debounce

        // Pastikan teks tidak berubah lagi saat kita menunggu
        if (keyword != SearchBar_Item.Text) return;

        await FetchItemsFromApi(keyword);
    }

    private async Task FetchItemsFromApi(string keyword)
    {
        try
        {
            string cleanToken = Preferences.Get("TOKEN_KEY", "").Replace("Bearer ", "").Trim();

            // Bangun URL persis seperti yang Anda tes di browser
            string apiUrl = $"{App.API_HOST}item/list-lokal.php?search={Uri.EscapeDataString(keyword)}&limit=10";

            using (var client = new HttpClient())
            {
                if (!string.IsNullOrEmpty(cleanToken))
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);

                // Bypass blokir keamanan hosting
                client.DefaultRequestHeaders.Add("User-Agent", "AccuratePOS-App/1.0");

                var response = await client.GetAsync(apiUrl);
                string responseContent = await response.Content.ReadAsStringAsync();

                // -------------------------------------------------------------
                // BUKA KOMENTAR INI JIKA MASIH KOSONG UNTUK MELIHAT RAW JSON DI HP
                // await DisplayAlert("JSON DITERIMA", responseContent, "OK");
                // -------------------------------------------------------------

                if (!response.IsSuccessStatusCode)
                {
                    MainThread.BeginInvokeOnMainThread(() => Border_AutoComplete.IsVisible = false);
                    return;
                }

                var apiResult = JsonConvert.DeserializeObject<ApiResponse>(responseContent);

                // Pastikan UI diupdate di Main Thread agar CollectionView kerender
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AutoCompleteResults.Clear();

                    if (apiResult != null && apiResult.status == "success")
                    {
                        if (apiResult.data != null && apiResult.data.Count > 0)
                        {
                            foreach (var item in apiResult.data)
                            {
                                AutoCompleteResults.Add(item);
                            }
                        }
                        // Tetap true, jika Count > 0 maka data muncul. 
                        // Jika Count 0 maka EmptyView "Tidak ada barang" muncul.
                        Border_AutoComplete.IsVisible = true;
                    }
                });
            }
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() => Border_AutoComplete.IsVisible = false);
            System.Diagnostics.Debug.WriteLine("Fetch API Error: " + ex.Message);
        }
    }

    private async void List_AutoComplete_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ItemModel selectedItem)
        {
            // ==============================================================
            // TAMBAHAN: Validasi Stok Tidak Boleh 0
            // ==============================================================
            if (selectedItem.balance <= 0)
            {
                // Lepas pilihan dan beri peringatan
                List_AutoComplete.SelectedItem = null;
                await DisplayAlertAsync("Stok Habis", "Barang ini tidak dapat dipilih karena stok kosong (0).", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedKonsumenValue))
            {
                // Reset pilihan auto-complete agar tidak menggantung
                List_AutoComplete.SelectedItem = null;
                Border_AutoComplete.IsVisible = false;

                await DisplayAlertAsync("Peringatan", "Silakan pilih Konsumen / Pelanggan terlebih dahulu!", "OK");
                PickerKonsumen.Focus();
                return;
            }

            // Jika lolos validasi, sembunyikan dropdown pencarian
            SearchBar_Item.Text = selectedItem.item_no;
            Border_AutoComplete.IsVisible = false;

            System.Diagnostics.Debug.WriteLine($"Barang Dipilih: {selectedItem.name} - {selectedItem.item_no}");

            var itemAddPage = new ItemAdd(selectedItem.item_no, selectedItem.name, selectedItem.balance, SelectedKonsumenValue);

            // Tangkap Data yang dikirim dari BSimpan_Clicked
            itemAddPage.OnItemSaved += (s, cartItem) =>
            {
                CartItems.Add(cartItem);
                UpdateSubtotal();
            };

            await Navigation.PushAsync(itemAddPage);

            List_AutoComplete.SelectedItem = null;
        }
    }

    private void B_ShipmentTapGesture_Tapped(object sender, TappedEventArgs e)
    {
        ViewBarang.IsVisible = false;
        ViewDiskon.IsVisible = false;
        ViewPengirim.IsVisible = true;
        ViewBiaya.IsVisible = false;
        BClose.IsVisible = true;
    }

    private void B_BiayaTapGesture_Tapped(object sender, TappedEventArgs e)
    {
        ViewBarang.IsVisible = false;
        ViewDiskon.IsVisible = false;
        ViewPengirim.IsVisible = false;
        ViewBiaya.IsVisible = true;
        BClose.IsVisible =true;
    }

    private void B_NewFaktur_Clicked(object sender, EventArgs e)
    {

    }

    private void BClose_Clicked(object sender, EventArgs e)
    {
        ViewBarang.IsVisible = true;
        ViewDiskon.IsVisible = false;
        ViewPengirim.IsVisible = false;
        ViewBiaya.IsVisible = false;
        BClose.IsVisible = false;

    }

    private void B_Diskon_Tapped(object sender, TappedEventArgs e)
    {
        ViewBarang.IsVisible = false;
        ViewDiskon.IsVisible = true;
        ViewPengirim.IsVisible = false;
        ViewBiaya.IsVisible = false;
        BClose.IsVisible = true;
    }

    private void OnKonsumenSelected(object sender, EventArgs e)
    {
        var picker = sender as Picker;

        // Cek apakah ada yang dipilih (hindari null saat user batal pilih)
        if (picker?.SelectedItem is KonsumenOption selected)
        {
            SelectedKonsumenValue = selected.Value;
           // DisplayAlertAsync("Tes", SelectedKonsumenValue, "OK");
        }
    }

    private async Task LoadCoaData()
    {
        try
        {
            string cleanToken = Preferences.Get("TOKEN_KEY", "").Replace("Bearer ", "").Trim();
            if (string.IsNullOrEmpty(cleanToken)) return;

            string apiUrl = $"{App.API_HOST}coa/list.php";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);
                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    var apiResult = JsonConvert.DeserializeObject<CoaResponse>(responseContent);

                    if (apiResult != null && apiResult.status == "success" && apiResult.data != null)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            PickerBiaya.ItemsSource = apiResult.data;
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Gagal memuat COA: {ex.Message}");
        }
    }

    private void EntryHargaBiaya_TextChanged(object sender, TextChangedEventArgs e)
    {
        // 1. Jika sistem sedang memformat teks, hentikan proses untuk mencegah infinite loop
        if (_isFormattingBiaya) return;

        if (string.IsNullOrWhiteSpace(e.NewTextValue))
            return;

        // 2. Buang semua karakter yang BUKAN angka
        string cleanText = new string(e.NewTextValue.Where(char.IsDigit).ToArray());

        if (string.IsNullOrEmpty(cleanText))
        {
            _isFormattingBiaya = true;
            EntryHargaBiaya.Text = string.Empty;
            _isFormattingBiaya = false;
            return;
        }

        // 3. Ubah ke format angka dan titik ribuan
        if (long.TryParse(cleanText, out long value))
        {
            string formatted = value.ToString("N0", new CultureInfo("id-ID"));

            if (EntryHargaBiaya.Text != formatted)
            {
                _isFormattingBiaya = true;

                // 4. BUNGKUS DENGAN DISPATCHER (Solusi Anti-Crash Android)
                Dispatcher.Dispatch(() =>
                {
                    EntryHargaBiaya.Text = formatted;

                    // Pindahkan posisi kursor ke paling kanan agar tidak kembali ke kiri
                    EntryHargaBiaya.CursorPosition = formatted.Length;

                    _isFormattingBiaya = false;
                });
            }
        }
    }

    private void UpdateTotalBiaya()
    {
        // Jumlahkan semua nilai 'Nominal' yang ada di list menggunakan LINQ
        double totalBiaya = SelectedBiayaList.Sum(x => x.Nominal);

        // Tampilkan ke Label dengan format Rupiah (titik ribuan)
        EntryTotalBiaya.Text = $"Rp {totalBiaya.ToString("N0", new CultureInfo("id-ID"))}";
    }

    private async void BTambahBiaya_Clicked(object sender, EventArgs e)
    {
        if (PickerBiaya.SelectedItem is CoaData selectedCoa)
        {
            // HILANGKAN TITIK SEBELUM DI-PARSE JADI ANGKA
            string cleanNominal = EntryHargaBiaya.Text?.Replace(".", "") ?? "";

            if (string.IsNullOrWhiteSpace(cleanNominal) || !double.TryParse(cleanNominal, out double nominal))
            {
                await DisplayAlertAsync("Validasi", "Masukkan nominal harga yang valid.", "OK");
                return;
            }

            if (SelectedBiayaList.Any(x => x.No == selectedCoa.no))
            {
               await DisplayAlertAsync("Validasi", "Biaya ini sudah ditambahkan sebelumnya.", "OK");
                return;
            }

            SelectedBiayaList.Add(new SelectedBiayaModel
            {
                No = selectedCoa.no,
                Name = selectedCoa.name,
                Nominal = nominal
            });

            PickerBiaya.SelectedItem = null;
            EntryHargaBiaya.Text = string.Empty;

            // PANGGIL UPDATE TOTAL BIAYA DI SINI
            UpdateTotalBiaya();
        }
        else
        {
           await DisplayAlertAsync("Peringatan", "Pilih jenis biaya terlebih dahulu dari dropdown.", "OK");
        }
    }

    private void HapusBiaya_Tapped(object sender, TappedEventArgs e)
    {
        if (sender is Label label && label.BindingContext is SelectedBiayaModel biayaData)
        {
            SelectedBiayaList.Remove(biayaData);

            
            UpdateTotalBiaya();
        }
    }

    private void UpdateSubtotal()
    {
        double subtotal = CartItems.Sum(x => x.unitPrice * x.quantity);
        EntrySubtotal.Text = $"Rp {subtotal.ToString("N0", new CultureInfo("id-ID"))}";
    }

    // =========================================================
    // FUNGSI HAPUS ITEM DARI KERANJANG BELANJA
    // =========================================================
    private void HapusCartItem_Tapped(object sender, TappedEventArgs e)
    {
        if (sender is Label label && label.BindingContext is CartItemModel cartItem)
        {
            // Hapus dari koleksi, UI akan otomatis hilang
            CartItems.Remove(cartItem);

            // Hitung ulang subtotal setelah barang dihapus
            UpdateSubtotal();
        }
    }
}