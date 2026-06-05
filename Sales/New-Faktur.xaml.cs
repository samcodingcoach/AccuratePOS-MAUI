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

    private bool _isFormattingDiskonNominal = false;
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
        //BindableLayout.SetItemsSource(CartContainer, CartItems);

        CartContainer.ItemsSource = CartItems;
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

        public double price { get; set; }
        public string image { get; set; }


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
    // =========================================================
    public class CartItemModel
    {
        public string itemNo { get; set; }
        public int id_promo { get; set; }
        public string itemName { get; set; }
        public double unitPrice { get; set; }
        public int quantity { get; set; }
        public string warehouseName { get; set; }
        public string salesmanListNumber { get; set; }
        public string imagePath { get; set; }
        public double itemDiscPercent { get; set; } // Pastikan properti diskon ini ada dari tahap sebelumnya

        public string DisplayImage
        {
            get
            {
                if (string.IsNullOrWhiteSpace(imagePath)) return "nophotoproduct150.jpg";
                if (imagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return imagePath;
                string baseHost = App.API_HOST.Replace("api/", "");
                string cleanFileName = imagePath.Replace("../", "").Replace("images/", "").TrimStart('/');
                return $"{baseHost}images/{cleanFileName}";
            }
        }

        public List<DetailSerialNumber> detailSerialNumber { get; set; }

        // Properti Khusus UI (Sesuai Gambar Desain Baru)
        public string FormattedUnitPrice => $"Rp {unitPrice.ToString("N0", new CultureInfo("id-ID"))}";
        public string FormattedTotalPrice => $"Rp {(unitPrice * quantity).ToString("N0", new CultureInfo("id-ID"))}";
        public string DisplayQty => $"x {quantity}";
        public bool HasSerialNumbers => detailSerialNumber != null && detailSerialNumber.Count > 0;
        public string SerialNumbersDisplay => HasSerialNumbers ? "SN: " + string.Join(", ", detailSerialNumber.Select(x => x.serialNumberNo)) : "";

        // GABUNGAN INFO (Baris ke-2)
        public string ItemInfoDisplay => $"{itemNo} | Gudang: {warehouseName} | Sales: {salesmanListNumber}";

        // GABUNGAN HARGA & QTY (Baris ke-3)
        public string PriceAndQtyDisplay => $"{FormattedUnitPrice} {DisplayQty}";

        // LOGIKA MUNCULNYA TEKS DISKON (Baris ke-5)
        public string TotalHargaLabel => itemDiscPercent > 0 ? $"Total Harga - Diskon {itemDiscPercent}% :" : "Total Harga :";
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
            // 1. Validasi Stok Tidak Boleh 0
            if (selectedItem.balance <= 0)
            {
                List_AutoComplete.SelectedItem = null;
                await DisplayAlertAsync("Stok Habis", "Barang ini tidak dapat dipilih karena stok kosong (0).", "OK");
                return;
            }

            // 2. Validasi Harga Tidak Boleh 0
            if (selectedItem.price <= 0)
            {
                List_AutoComplete.SelectedItem = null;
                await DisplayAlertAsync("Harga Tidak Valid", "Barang ini belum memiliki harga jual (Rp 0). Silakan perbarui harga master barang terlebih dahulu.", "OK");
                return;
            }

            // 3. Validasi Konsumen (Harus dipilih dulu)
            if (string.IsNullOrWhiteSpace(SelectedKonsumenValue))
            {
                List_AutoComplete.SelectedItem = null;
                Border_AutoComplete.IsVisible = false;

                await DisplayAlertAsync("Peringatan", "Silakan pilih Konsumen / Pelanggan terlebih dahulu!", "OK");
                PickerKonsumen.Focus();
                return;
            }

            // Jika semua lolos validasi, proses navigasi
            SearchBar_Item.Text = selectedItem.item_no;
            Border_AutoComplete.IsVisible = false;

            System.Diagnostics.Debug.WriteLine($"Barang Dipilih: {selectedItem.name} - Harga: {selectedItem.price}");

            var itemAddPage = new ItemAdd(selectedItem.item_no, selectedItem.name, selectedItem.balance, SelectedKonsumenValue, selectedItem.image);

            // Tangkap Data yang dikirim dari BSimpan_Clicked
            itemAddPage.OnItemSaved += (s, cartItem) =>
            {
                CartItems.Add(cartItem);
                KalkulasiSemuaTotal();

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

    private async void B_NewFaktur_Clicked(object sender, EventArgs e)
    {
        // 1. Validasi Data Wajib
        if (CartItems.Count == 0)
        {
            await DisplayAlertAsync("Peringatan", "Keranjang belanja masih kosong. Tambahkan barang terlebih dahulu.", "OK");
            return;
        }

        // ======================================================================
        // 2. AMBIL TEKS LANGSUNG DARI PICKER DAN POTONG STRING
        // ======================================================================
        string finalCustomerCode = "";

        // Tarik objek yang sedang dipilih secara langsung dari elemen Picker UI Anda
        if (PickerKonsumen.SelectedItem is KonsumenOption selectedOption)
        {
            // Ambil parameter "Text" yang tampil di layar (Isinya: "Membership - MB002")
            string teksYangTampil = selectedOption.Text;

            // Lakukan pemotongan dengan spesifik strip dan spasi "- "
            if (teksYangTampil.Contains("- "))
            {
                var splitArray = teksYangTampil.Split(new string[] { "- " }, StringSplitOptions.None);
                finalCustomerCode = splitArray.Last().Trim(); // Hasil akhir murni: "MB002"
            }
            else
            {
                finalCustomerCode = teksYangTampil.Trim();
            }
        }
        else
        {
            await DisplayAlertAsync("Peringatan", "Silakan pilih Konsumen / Pelanggan terlebih dahulu.", "OK");
            return;
        }

        // 3. Bersihkan dan Ambil Nilai Total Diskon
        string cleanDiskon = EntryTotalDiskon.Text?.Replace("Rp", "")?.Replace(".", "")?.Trim() ?? "0";
        if (!double.TryParse(cleanDiskon, out double totalDiskon)) totalDiskon = 0;

        // 4. Susun Payload JSON sesuai struktur save-invoice.php
        var payload = new
        {
            customerNo = finalCustomerCode, // <--- SEKARANG PASTI BERISI KODE PELANGGAN
            transDate = DateTime.Now.ToString("yyyy-MM-dd"), // Tanggal sekarang
            cashDiscount = totalDiskon,
            taxable = CheckBoxPPN.IsChecked == true,
            shipmentName = PickerPengirim.SelectedItem?.ToString() ?? "",
            toAddress = EntryAlamat.Text ?? "",
            description = EntryKeterangan.Text ?? "",
            poNumber = EntryNoPO.Text ?? "",

            // Susun array barang (detailItem)
            detailItem = CartItems.Select(item => new
            {
                itemNo = item.itemNo,
                unitPrice = item.unitPrice,
                quantity = item.quantity,
                warehouseName = item.warehouseName,
                salesmanListNumber = item.salesmanListNumber,
                itemDiscPercent = item.itemDiscPercent,

                // Susun array serial number jika ada
                detailSerialNumber = item.detailSerialNumber?.Select(sn => new
                {
                    serialNumberNo = sn.serialNumberNo,
                    quantity = sn.quantity
                }).ToList()
            }).ToList(),

            // Susun array biaya tambahan (detailExpense)
            detailExpense = SelectedBiayaList.Select(biaya => new
            {
                accountNo = biaya.No,
                expenseAmount = biaya.Nominal
            }).ToList()
        };

        // 5. Proses Pengiriman via HTTP POST
        try
        {
            B_NewFaktur.IsEnabled = false;
            B_NewFaktur.Text = "MENYIMPAN...";

            string cleanToken = Preferences.Get("TOKEN_KEY", "").Replace("Bearer ", "").Trim();
            string apiUrl = $"{App.API_HOST}penjualan/save-invoice.php";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);

                string jsonPayload = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(apiUrl, content);
                string responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlertAsync("Sukses", "Faktur Penjualan berhasil disimpan ke sistem.", "OK");

                    // Kembali ke halaman sebelumnya (List-Faktur)
                    await Navigation.PushAsync(new List_Faktur());
                }
                else
                {
                    await DisplayAlertAsync("Gagal Menyimpan", $"Sistem merespons: {responseString}", "OK");
                    System.Diagnostics.Debug.WriteLine($"Payload yang dikirim: {jsonPayload}");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error Koneksi", $"Terjadi kesalahan saat menghubungi server: {ex.Message}", "OK");
        }
        finally
        {
            B_NewFaktur.IsEnabled = true;
            B_NewFaktur.Text = "BUAT FAKTUR";
        }
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

            // 1. Kunci Picker agar tidak bisa diklik/diubah secara langsung lagi
            PickerKonsumen.IsEnabled = false;

            // 2. Sembunyikan ikon pencarian, munculkan ikon cancel (silang merah)
            ImageSearchKonsumen.IsVisible = false;
            ImageCancelKonsumen.IsVisible = true;
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

            KalkulasiSemuaTotal();

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
            KalkulasiSemuaTotal();


        }
    }

    private async void BHapusDiskon_Clicked(object sender, EventArgs e)
    {
        // Kosongkan input dan kembalikan output ke Rp 0
        EntryDiskonNominal.Text = string.Empty;
        EntryDiskonPersen.Text = string.Empty;
        EntryTotalDiskon.Text = "Rp 0";

        KalkulasiSemuaTotal(); KalkulasiSemuaTotal();
    }

    private async void BTambahkanDiskon_Clicked(object sender, EventArgs e)
    {

        KalkulasiSemuaTotal();

    }

    private void CheckBoxPPN_CheckedChanged(object sender, CheckedChangedEventArgs e) => KalkulasiSemuaTotal();

    private async void HapusCartItem_Tapped(object sender, TappedEventArgs e)
    {
        if (sender is Label label && label.BindingContext is CartItemModel cartItem)
        {
            // Cek apakah barang yang dihapus dari keranjang memiliki ID Promo
            if (cartItem.id_promo > 0)
            {
                // Eksekusi API cancel kuota sejumlah qty barang yang dihapus
                await CancelPromoKuotaAsync(cartItem.id_promo, cartItem.quantity);
            }

            // Hapus dari koleksi, UI akan otomatis hilang
            CartItems.Remove(cartItem);

            // Hitung ulang semua total yang ada di bawah
            KalkulasiSemuaTotal();
        }
    }

    // =========================================================
    // FUNGSI API UNTUK MEMBATALKAN & MENGEMBALIKAN KUOTA PROMO
    // =========================================================
    private async Task CancelPromoKuotaAsync(int idPromo, int canceledKuota)
    {
        try
        {
            string cleanToken = Preferences.Get("TOKEN_KEY", "").Replace("Bearer ", "").Trim();

            // Menggunakan endpoint baru khusus untuk pembatalan
            string apiUrl = $"{App.API_HOST}promo/cancel-kuota.php";

            // Susun payload menggunakan angka murni (positif) dari qty
            var payload = new
            {
                kuota = canceledKuota,
                id_promo = idPromo
            };

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);
                string jsonPayload = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Eksekusi POST
                var response = await client.PostAsync(apiUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Gagal membatalkan Kuota Promo ID: {idPromo}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Sukses membatalkan Kuota Promo ID: {idPromo} sejumlah {canceledKuota}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Koneksi Cancel Promo Gagal: {ex.Message}");
        }
    }

    private void EntryDiskonNominal_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Cegah infinite loop saat sistem sedang menyisipkan titik
        if (_isFormattingDiskonNominal) return;

        if (!string.IsNullOrEmpty(e.NewTextValue))
        {
            // Hilangkan semua titik yang sudah ada
            string cleanInput = e.NewTextValue.Replace(".", "");

            // Validasi apakah benar-benar angka
            if (double.TryParse(cleanInput, out double parsedNumber))
            {
                _isFormattingDiskonNominal = true;

                // Format ulang ke ribuan (titik)
                EntryDiskonNominal.Text = parsedNumber.ToString("N0", new CultureInfo("id-ID"));

                _isFormattingDiskonNominal = false;
            }
            else
            {
                // Jika user mengetik huruf, tolak dan kembalikan ke teks lama
                _isFormattingDiskonNominal = true;
                EntryDiskonNominal.Text = e.OldTextValue;
                _isFormattingDiskonNominal = false;
            }
        }

        // Panggil fungsi master kalkulasi ke bawah
        KalkulasiSemuaTotal();
    }

    private async void TapCancelKonsumen_Tapped(object sender, TappedEventArgs e)
    {
        // 1. Jika ada barang di keranjang, beri peringatan terlebih dahulu
        if (CartItems.Count > 0)
        {
            bool confirm = await DisplayAlertAsync("Konfirmasi", "Membatalkan Konsumen akan menghapus seluruh barang di keranjang belanja Anda. Lanjutkan?", "Ya, Hapus Semua", "Batal");

            // Jika user memilih Batal, hentikan proses pembatalan
            if (!confirm) return;

            // 2. Jika lanjut, kembalikan (cancel) semua kuota promo yang sedang menempel di keranjang
            foreach (var item in CartItems.ToList())
            {
                if (item.id_promo > 0)
                {
                    await CancelPromoKuotaAsync(item.id_promo, item.quantity);
                }
            }

            // Bersihkan isi keranjang
            CartItems.Clear();
        }

        // 3. Reset Picker, buka kembali kuncinya agar user bisa memilih konsumen baru
        PickerKonsumen.SelectedItem = null;
        SelectedKonsumenValue = null;
        PickerKonsumen.IsEnabled = true;

        // 4. Kembalikan posisi ikon seperti semula
        ImageSearchKonsumen.IsVisible = true;
        ImageCancelKonsumen.IsVisible = false;

        // 5. Kalkulasi ulang untuk mereset seluruh angka uang kembali ke Rp 0
        KalkulasiSemuaTotal();
    }

    private void KalkulasiSemuaTotal()
    {
        // 1. HITUNG SUBTOTAL (Langsung dari keranjang memori, sangat aman)
        double subtotal = CartItems.Sum(x => x.unitPrice * x.quantity);
        EntrySubtotal.Text = $"Rp {subtotal.ToString("N0", new CultureInfo("id-ID"))}";

        // 2. HITUNG DISKON (Aman dari inputan ngawur seperti simbol % atau titik)
        double totalDiskon = 0;
        string cleanNominal = EntryDiskonNominal.Text?.Replace(".", "")?.Trim();
        string cleanPersen = EntryDiskonPersen.Text?.Replace("%", "")?.Trim();

        if (!string.IsNullOrWhiteSpace(cleanNominal) && double.TryParse(cleanNominal, out double diskonNominal))
        {
            totalDiskon = diskonNominal;
            KeteranganDiskon.Text = $"Diskon diterapkan: Rp {diskonNominal.ToString("N0", new CultureInfo("id-ID"))}";
        }
        else if (!string.IsNullOrWhiteSpace(cleanPersen) && double.TryParse(cleanPersen, out double diskonPersen))
        {
            totalDiskon = (diskonPersen / 100) * subtotal; // Dinamis ikut subtotal baru
            KeteranganDiskon.Text = $"Diskon diterapkan: {cleanPersen}%";
        }

        if (totalDiskon > subtotal) totalDiskon = subtotal; // Pengaman
        if (totalDiskon < 0) totalDiskon = 0;


       

        EntryTotalDiskon.Text = $"Rp {totalDiskon.ToString("N0", new CultureInfo("id-ID"))}";


        // 3. HITUNG BIAYA LAIN (Langsung dari list biaya)
        double totalBiaya = SelectedBiayaList.Sum(x => x.Nominal);
        EntryTotalBiaya.Text = $"Rp {totalBiaya.ToString("N0", new CultureInfo("id-ID"))}";

        // 4. HITUNG PPN 11%
        double totalPajak = 0;
        if (CheckBoxPPN != null && CheckBoxPPN.IsChecked)
        {
            double nilaiSetelahDiskon = subtotal - totalDiskon;
            if (nilaiSetelahDiskon < 0) nilaiSetelahDiskon = 0;
            totalPajak = nilaiSetelahDiskon * 0.11;
        }
        EntryTotalPajak.Text = $"Rp {totalPajak.ToString("N0", new CultureInfo("id-ID"))}";

        // 5. HITUNG GRAND TOTAL MURNI
        double grandTotal = (subtotal - totalDiskon) + totalBiaya + totalPajak;
        if (grandTotal < 0) grandTotal = 0;
        EntryGrandTotal.Text = $"Rp {grandTotal.ToString("N0", new CultureInfo("id-ID"))}";

        // 6. PEMBULATAN KE BAWAH (KELIPATAN 100 PERAK)
        double grandTotalRounded = Math.Floor(grandTotal / 100) * 100;
        EntryGrandTotalRounded.Text = $"Rp {grandTotalRounded.ToString("N0", new CultureInfo("id-ID"))}";
    }


}