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

public partial class Pembayaran_Faktur : ContentPage
{
    string bankNo { get; set; }
    string nomor_faktur { get; set; }

    string DiskonAccountNo = "400004";

    string nomor_pelanggan { get; set; }

    string paymentMethodVal { get; set; }

    string charFieldString2 { get; set; }

    // Nilai total faktur asli (sebelum diskon pembayaran) — dipakai sebagai batas maksimal diskon
    double _totalAmountFaktur = 0;
    // Nilai diskon pembayaran yang sedang aktif
    double _diskonPembayaran = 0;
    // Guard agar TextChanged tidak infinite-loop saat menyisipkan pemisah ribuan
    bool _isFormattingDiskon = false;

    // Total yang harus dibayar konsumen (setelah diskon & pembulatan) — acuan kembalian
    double _totalTagihan = 0;
    // Nominal yang dibayarkan konsumen
    double _nominalBayarKonsumen = 0;
    // Guard format ribuan untuk nominal bayar konsumen
    bool _isFormattingBayar = false;

    public Pembayaran_Faktur(string nomorFaktur)
	{
		InitializeComponent();

        // Simpan nomor faktur yang dikirim dari List-Faktur
        nomor_faktur = nomorFaktur;

        // Atur tanggal default = hari ini dan izinkan memilih tanggal ke depan (mis. besok)
        PickerTanggalBayar.MinimumDate = new DateTime(2000, 1, 1);
        PickerTanggalBayar.MaximumDate = DateTime.Today.AddYears(5);
        PickerTanggalBayar.Date = DateTime.Today;

        PickerBank.ItemDisplayBinding = new Binding("name");

        // Panggil fungsi pengambilan data saat halaman dibuka
        _ = LoadKasBankData();
        _ = LoadDetailFaktur();

        // Tampilkan nomor faktur pada form
        FormNumber.Text = nomor_faktur;

        //id user
        string idUser = Preferences.Get("ID_USER", "");
        string userName = Preferences.Get("USERNAME", "");
        charFieldString2 = $"{idUser} - {userName}";

    }


    
    public class DetailPembayaranResponse
    {
        public string status { get; set; }
        public DetailPembayaranData data { get; set; }
    }

    public class DetailPembayaranData
    {
        public double subTotal { get; set; }
        public double cashDiscount { get; set; }
        public double totalExpense { get; set; }
        public double tax1Amount { get; set; }
        public double totalAmount { get; set; }
        public string number { get; set; }
        public string description { get; set; }
        public string transDate { get; set; }
        public CustomerInvoiceData customer { get; set; }
        public List<DetailItemPembayaran> detailItem { get; set; }
    }

    public class CustomerInvoiceData
    {
        public string name { get; set; }
        public string customerNo { get; set; }
    }

    public class DetailItemPembayaran
    {
        public ItemDetailPembayaran item { get; set; }
        public double itemCashDiscount { get; set; }
        public double totalPrice { get; set; }
        public double quantity { get; set; }

        // Properti pembantu (Helper) untuk ditampilkan langsung di BindableLayout XAML
        public string QtyAndPrice => $"{quantity} x Rp {item?.unitPrice.ToString("N0", new CultureInfo("id-ID"))}";
        public string FormattedItemDiscount => itemCashDiscount > 0 ? $"- Rp {itemCashDiscount.ToString("N0", new CultureInfo("id-ID"))}" : "Rp 0";
        public string FormattedTotalPrice => $"Rp {totalPrice.ToString("N0", new CultureInfo("id-ID"))}";
    }

    public class ItemDetailPembayaran
    {
        public double unitPrice { get; set; }
        public string name { get; set; }
        public string no { get; set; }
    }

    private async Task LoadDetailFaktur()
    {
        try
        {
            string cleanToken = Preferences.Get("TOKEN_KEY", "").Replace("Bearer ", "").Trim();
            if (string.IsNullOrEmpty(cleanToken)) return;

            // Memanggil endpoint berdasarkan variabel nomor_faktur
            string apiUrl = $"{App.API_HOST}penjualan/detail-invoice.php?number={Uri.EscapeDataString(nomor_faktur)}";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);
                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    var apiResult = JsonConvert.DeserializeObject<DetailPembayaranResponse>(responseContent);

                    if (apiResult != null && apiResult.data != null)
                    {
                        var inv = apiResult.data;

                        // Simpan total faktur asli sebagai batas maksimal diskon pembayaran
                        _totalAmountFaktur = inv.totalAmount;

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            // 1. Mapping Informasi Konsumen

                            FormNameCustomerNo.Text = $"{inv.customer?.customerNo} - {inv.customer?.name}";
                            nomor_pelanggan = inv.customer?.customerNo;

                            // 2. Mapping Jumlah Pembayaran Default
                            FormtotalAmount.Text = inv.totalAmount.ToString("N0", new CultureInfo("id-ID"));

                            // 3. Mapping Daftar Barang ke BindableLayout UI
                            ListBarangContainer.ItemsSource = inv.detailItem;

                            // 4. Hitung jumlah item
                            double totalQty = inv.detailItem?.Sum(x => x.quantity) ?? 0;
                            LabelHeaderDetail.Text = $"Detail Barang ({totalQty} items)";
                            LabelItemCount.Text = $"{totalQty} item";

                            // 5. Mapping Ringkasan Uang
                            LabelTotalDiskon.Text = $"Rp {inv.cashDiscount.ToString("N0", new CultureInfo("id-ID"))}";
                            LabelTotalBiaya.Text = $"Rp {inv.totalExpense.ToString("N0", new CultureInfo("id-ID"))}";
                            LabelTotalPajak.Text = $"Rp {inv.tax1Amount.ToString("N0", new CultureInfo("id-ID"))}";
                            LabelGrandTotal.Text = $"Rp {inv.totalAmount.ToString("N0", new CultureInfo("id-ID"))}";

                            // 6. Hitung Pembulatan ke Bawah (Kelipatan Ratusan)
                            double pembulatan = Math.Floor(inv.totalAmount / 100) * 100;
                            LabelPembulatan.Text = $"Rp {pembulatan.ToString("N0", new CultureInfo("id-ID"))}";
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Gagal memuat detail faktur: {ex.Message}");
        }
    }

    private void PickerBank_SelectedIndexChanged(object sender, EventArgs e)
    {
        // Tangkap objek dari item yang dipilih pengguna
        if (PickerBank.SelectedItem is KasBankData selectedBank)
        {
            System.Diagnostics.Debug.WriteLine($"Bank Dipilih: {selectedBank.name} dengan ID: {selectedBank.id} & No: {selectedBank.no}");
            bankNo = selectedBank.no;

            // Tentukan metode pembayaran berdasarkan nama Kas/Bank
            string namaBank = selectedBank.name ?? "";
            if (namaBank.IndexOf("QRIS", StringComparison.OrdinalIgnoreCase) >= 0)
                paymentMethodVal = "QRIS";
            else if (namaBank.IndexOf("Tunai", StringComparison.OrdinalIgnoreCase) >= 0)
                paymentMethodVal = "CASH_OTHER";
            else if (namaBank.IndexOf("BANK", StringComparison.OrdinalIgnoreCase) >= 0)
                paymentMethodVal = "BANK_TRANSFER";

            System.Diagnostics.Debug.WriteLine($"paymentMethodVal: {paymentMethodVal}");
        }
    }

    private async Task LoadKasBankData()
    {
        try
        {
            string cleanToken = Preferences.Get("TOKEN_KEY", "").Replace("Bearer ", "").Trim();
            if (string.IsNullOrEmpty(cleanToken)) return;

            string apiUrl = $"{App.API_HOST}coa/list-kasbank.php";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);
                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    var apiResult = JsonConvert.DeserializeObject<KasBankResponse>(responseContent);

                    if (apiResult != null && apiResult.data != null)
                    {
                        // Sembunyikan item dengan value "Kas Kecil"
                        var filteredData = apiResult.data
                            .Where(b => b.name != null && b.name.IndexOf("Kas Kecil", StringComparison.OrdinalIgnoreCase) < 0)
                            .ToList();

                        // Eksekusi perubahan UI wajib menggunakan MainThread
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            PickerBank.ItemsSource = filteredData;
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Gagal memuat daftar Kas/Bank: {ex.Message}");
        }
    }


    public class KasBankResponse
    {
        public List<KasBankData> data { get; set; }
    }

    public class KasBankData
    {
        public string no { get; set; }
        public string name { get; set; }
        public int id { get; set; }
    }

    private void B_SimpanPembayaran_Clicked(object sender, EventArgs e)
    {

    }

    private void TapViewDiskon_Tapped(object sender, TappedEventArgs e)
    {
        ViewNominalPembayaran.IsVisible = false;
        ViewDiskon.IsVisible = true;

    }

    private void EntryDiskonNominal_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Cegah infinite-loop saat kita menulis ulang .Text dengan pemisah ribuan
        if (_isFormattingDiskon) return;

        var culture = new CultureInfo("id-ID");

        // Ambil angka murni: buang pemisah ribuan, "Rp", "%", dan spasi
        string raw = (e.NewTextValue ?? "")
            .Replace(".", "")
            .Replace("Rp", "")
            .Replace("%", "")
            .Trim();

        // Kosong → diskon 0
        if (string.IsNullOrEmpty(raw))
        {
            _diskonPembayaran = 0;
            KalkulasiUlangTotal();
            return;
        }

        if (!double.TryParse(raw, out double diskon) || diskon < 0)
            return;

        // Batasi agar tidak melebihi total faktur (LabelGrandTotal / FormtotalAmount awal)
        if (diskon > _totalAmountFaktur)
            diskon = _totalAmountFaktur;

        _diskonPembayaran = diskon;

        // Tampilkan ulang dengan pemisah ribuan
        string formatted = diskon.ToString("N0", culture);
        if (formatted != (e.NewTextValue ?? ""))
        {
            // Gunakan Dispatcher.Dispatch untuk manipulasi elemen UI secara aman
            Dispatcher.Dispatch(() =>
            {
                _isFormattingDiskon = true;
                EntryDiskonNominal.Text = formatted;

                // =========================================================
                // TAMBAHKAN BARIS INI: Obat penangkal crash di Android
                // Memaksa kursor selalu diamankan ke posisi paling akhir teks
                // =========================================================
                EntryDiskonNominal.CursorPosition = formatted.Length;

                _isFormattingDiskon = false;
            });
        }

        // Hitung ulang semua total
        KalkulasiUlangTotal();
    }

    // Hitung ulang Grand Total, Nilai Faktur, Diskon Pembayaran, dan Pembulatan
    private void KalkulasiUlangTotal()
    {
        var culture = new CultureInfo("id-ID");

        double grandTotal = _totalAmountFaktur - _diskonPembayaran;
        if (grandTotal < 0) grandTotal = 0;

        // Diskon pembayaran tampil dinamis
        LabelDiskonPembayaran.Text = $"Rp {_diskonPembayaran.ToString("N0", culture)}";

        // Grand total & nilai faktur menyesuaikan diskon
        LabelGrandTotal.Text = $"Rp {grandTotal.ToString("N0", culture)}";
        FormtotalAmount.Text = grandTotal.ToString("N0", culture);

        // Pembulatan ke bawah (kelipatan ratusan)
        double pembulatan = Math.Floor(grandTotal / 100) * 100;
        LabelPembulatan.Text = $"Rp {pembulatan.ToString("N0", culture)}";

        // Total tagihan akhir = nilai setelah pembulatan; jadi acuan kembalian
        _totalTagihan = pembulatan;
        HitungKembalian();
    }

    // Kembalian = nominal bayar konsumen - total tagihan akhir
    private void HitungKembalian()
    {
        var culture = new CultureInfo("id-ID");

        double kembalian = _nominalBayarKonsumen - _totalTagihan;
        if (kembalian < 0) kembalian = 0; // belum/kurang bayar → tidak ada kembalian

        KembalianKonsumen.Text = $"Rp {kembalian.ToString("N0", culture)}";
    }

    private void NominalBayarKonsumen_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Cegah infinite-loop saat menulis ulang .Text dengan pemisah ribuan
        if (_isFormattingBayar) return;

        var culture = new CultureInfo("id-ID");

        // Ambil angka murni: buang pemisah ribuan, "Rp", "%", dan spasi
        string raw = (e.NewTextValue ?? "")
            .Replace(".", "")
            .Replace("Rp", "")
            .Replace("%", "")
            .Trim();

        // Kosong → nominal 0
        if (string.IsNullOrEmpty(raw))
        {
            _nominalBayarKonsumen = 0;
            HitungKembalian();
            return;
        }

        if (!double.TryParse(raw, out double nominal) || nominal < 0)
            return;

        _nominalBayarKonsumen = nominal;

        // Tampilkan ulang dengan pemisah ribuan; tunda agar Android tidak crash
        string formatted = nominal.ToString("N0", culture);
        if (formatted != (e.NewTextValue ?? ""))
        {
            Dispatcher.Dispatch(() =>
            {
                _isFormattingBayar = true;
                NominalBayarKonsumen.Text = formatted;
                NominalBayarKonsumen.CursorPosition = formatted.Length;
                _isFormattingBayar = false;
            });
        }

        // Hitung ulang kembalian
        HitungKembalian();
    }

    private void TapCloseDiskon_Tapped(object sender, TappedEventArgs e)
    {
        ViewNominalPembayaran.IsVisible = true;
        ViewDiskon.IsVisible = false;
    }

    private void TapViewKeterangan_Tapped(object sender, TappedEventArgs e)
    {
        ViewKeterangan.IsVisible = false;
        ViewNominalPembayaran.IsVisible = true;
    }

    private void TapViewKet_Tapped(object sender, TappedEventArgs e)
    {
        ViewKeterangan.IsVisible = true;
        ViewNominalPembayaran.IsVisible = false;
    }
}