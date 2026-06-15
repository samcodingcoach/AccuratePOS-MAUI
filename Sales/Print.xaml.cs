using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace MyPosAccurate2026.Sales;

public partial class Print : ContentPage
{
    private static readonly CultureInfo IdCulture = new CultureInfo("id-ID");

    private string _receiptNumber = "";   // nomor struk (110103.2026.06.xxxxx)
    private string _invoiceNumber = "";   // nomor faktur SI (fallback untuk detail-invoice)

    public Print()
    {
        InitializeComponent();
    }

    // Dipanggil dari alur pembayaran (Tunai/QRIS/VA) setelah save-receipt sukses
    public Print(string receiptNumber, string invoiceNumber) : this()
    {
        _receiptNumber = receiptNumber ?? "";
        _invoiceNumber = invoiceNumber ?? "";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadStrukAsync();
    }

    private static string FormatRupiah(double nilai) => $"Rp {nilai.ToString("N0", IdCulture)}";

    private async Task LoadStrukAsync()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StrukLoading.IsRunning = true;
            StrukLoading.IsVisible = true;
        });

        try
        {
            string cleanToken = Preferences.Get("TOKEN_KEY", "").Replace("Bearer ", "").Trim();

            DetailReceiptData receipt = null;

            // 1. Data pembayaran (struk) — butuh nomor struk
            if (!string.IsNullOrWhiteSpace(_receiptNumber))
                receipt = await FetchReceiptAsync(cleanToken, _receiptNumber);

            // Nomor faktur: utamakan dari respon struk, fallback ke yang dibawa konstruktor
            string invoiceNo = receipt?.detailInvoice?.FirstOrDefault()?.invoice?.number;
            if (string.IsNullOrWhiteSpace(invoiceNo))
                invoiceNo = _invoiceNumber;

            // 2. Detail barang (faktur)
            DetailInvoiceData invoice = null;
            if (!string.IsNullOrWhiteSpace(invoiceNo))
                invoice = await FetchInvoiceAsync(cleanToken, invoiceNo);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                TampilkanStruk(receipt, invoice, invoiceNo);
                StrukLoading.IsRunning = false;
                StrukLoading.IsVisible = false;
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StrukLoading.IsRunning = false;
                StrukLoading.IsVisible = false;
            });
            await DisplayAlertAsync("Gagal Memuat Struk", $"Terjadi kesalahan: {ex.Message}", "OK");
        }
    }

    private async Task<DetailReceiptData> FetchReceiptAsync(string token, string number)
    {
        string url = $"{App.API_HOST}penjualan/detail-receipt.php?number={Uri.EscapeDataString(number)}";

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(url);
        string responseContent = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(responseContent) || responseContent.TrimStart().StartsWith("<"))
            return null;

        var result = JsonConvert.DeserializeObject<DetailReceiptResponse>(responseContent);
        return result?.status == "success" ? result.data : null;
    }

    private async Task<DetailInvoiceData> FetchInvoiceAsync(string token, string number)
    {
        string url = $"{App.API_HOST}penjualan/detail-invoice.php?number={Uri.EscapeDataString(number)}";

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(url);
        string responseContent = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(responseContent) || responseContent.TrimStart().StartsWith("<"))
            return null;

        var result = JsonConvert.DeserializeObject<DetailInvoiceResponse>(responseContent);
        return result?.data;
    }

    private void TampilkanStruk(DetailReceiptData receipt, DetailInvoiceData invoice, string invoiceNo)
    {
        // ===== Info transaksi =====
        LabelNoStruk.Text = string.IsNullOrWhiteSpace(_receiptNumber) ? "-" : _receiptNumber;
        LabelNoFaktur.Text = string.IsNullOrWhiteSpace(invoiceNo) ? "-" : invoiceNo;
        LabelTanggal.Text = receipt?.transDate ?? invoice?.transDate ?? "-";
        LabelKasir.Text = string.IsNullOrWhiteSpace(receipt?.charField2) ? "-" : receipt.charField2;

        if (receipt?.customer != null)
            LabelKonsumen.Text = $"{receipt.customer.customerNo} - {receipt.customer.name}";
        else
            LabelKonsumen.Text = "-";

        // Sales, pengiriman, alamat (dari detail-invoice)
        string sales = invoice?.masterSalesmanName;
        RowSales.IsVisible = !string.IsNullOrWhiteSpace(sales);
        LabelSales.Text = sales ?? "-";

        string pengiriman = invoice?.shipment?.name;
        RowPengiriman.IsVisible = !string.IsNullOrWhiteSpace(pengiriman);
        LabelPengiriman.Text = pengiriman ?? "-";

        string alamat = invoice?.toAddress;
        RowAlamat.IsVisible = !string.IsNullOrWhiteSpace(alamat);
        LabelAlamat.Text = alamat ?? "";

        // ===== Daftar item =====
        var items = (invoice?.detailItem ?? new List<InvItem>())
            .Select(d => new StrukItem
            {
                NamaBarang = d.item?.name,
                quantity = d.quantity,
                unitPrice = d.unitPrice,
                satuan = d.itemUnit?.name
            })
            .ToList();
        ListItemContainer.BindingContext = items;

        // ===== Subtotal & diskon faktur =====
        LabelSubtotal.Text = FormatRupiah(invoice?.subTotal ?? 0);

        double diskonFaktur = invoice?.cashDiscount ?? 0;
        RowDiskonFaktur.IsVisible = diskonFaktur > 0;
        LabelDiskonFaktur.Text = $"- {FormatRupiah(diskonFaktur)}";

        // ===== Biaya-biaya (detailExpense) =====
        var expenses = (invoice?.detailExpense ?? new List<InvExpense>())
            .Select(x => new StrukExpense { detailName = x.detailName, expenseAmount = x.expenseAmount })
            .ToList();
        bool adaBiaya = expenses.Count > 0;
        LabelBiayaHeader.IsVisible = adaBiaya;
        ListExpenseContainer.IsVisible = adaBiaya;
        ListExpenseContainer.BindingContext = expenses;

        // ===== Pajak & total =====
        LabelPajak.Text = FormatRupiah(invoice?.tax1AmountBase ?? 0);
        LabelTotal.Text = FormatRupiah(invoice?.totalAmount ?? receipt?.totalPayment ?? 0);

        // ===== Metode & kembalian =====
        string metode = invoice?.receiptHistory?.FirstOrDefault()?.historyPaymentName;
        if (string.IsNullOrWhiteSpace(metode))
            metode = LabelMetodeFromCode(receipt?.paymentMethod);
        LabelMetode.Text = metode;

        bool isTunai = string.Equals(receipt?.paymentMethod, "CASH_OTHER", StringComparison.OrdinalIgnoreCase);
        RowKembalian.IsVisible = isTunai;
        if (isTunai)
            LabelKembalian.Text = FormatRupiah(receipt?.numericField1 ?? 0);

        // ===== Catatan =====
        string catatan = receipt?.description;
        RowCatatan.IsVisible = !string.IsNullOrWhiteSpace(catatan);
        LabelCatatan.Text = catatan ?? "";
    }

    private static string LabelMetodeFromCode(string code)
    {
        switch ((code ?? "").ToUpperInvariant())
        {
            case "CASH_OTHER": return "Tunai";
            case "QRIS": return "QRIS";
            case "BANK_TRANSFER": return "Transfer Bank / VA";
            default: return string.IsNullOrWhiteSpace(code) ? "-" : code;
        }
    }

    // ===== Tombol bawah =====
    private async void B_Print_Clicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Print", "Fitur cetak struk akan segera hadir.", "OK");
    }

    private async void B_Whatsapp_Clicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("WhatsApp", "Fitur kirim struk via WhatsApp akan segera hadir.", "OK");
    }

    private void B_Skip_Clicked(object sender, EventArgs e)
    {
        // Kembali ke List-Faktur (instance baru agar status faktur ter-refresh)
        Application.Current.MainPage = new NavigationPage(new List_Faktur());
    }

    // ===================== Model tampilan item =====================
    private class StrukItem
    {
        public string NamaBarang { get; set; }
        public double quantity { get; set; }
        public double unitPrice { get; set; }
        public string satuan { get; set; }

        public string QtyAndPrice =>
            $"{quantity.ToString("0.##", IdCulture)} {satuan} x Rp {unitPrice.ToString("N0", IdCulture)}";
        public string FormattedLineTotal =>
            $"Rp {(quantity * unitPrice).ToString("N0", IdCulture)}";
    }

    // Model tampilan biaya (detailExpense)
    private class StrukExpense
    {
        public string detailName { get; set; }
        public double expenseAmount { get; set; }
        public string FormattedAmount => $"Rp {expenseAmount.ToString("N0", IdCulture)}";
    }

    // ===================== Response detail-receipt.php =====================
    private class DetailReceiptResponse
    {
        public string status { get; set; }
        public DetailReceiptData data { get; set; }
    }

    private class DetailReceiptData
    {
        public string number { get; set; }
        public double totalPayment { get; set; }
        public string charField2 { get; set; }   // kasir
        public string charField1 { get; set; }   // referensi (QRIS/VA)
        public double totalDiscount { get; set; }
        public string paymentMethod { get; set; }
        public string description { get; set; }
        public string transDate { get; set; }
        public double numericField2 { get; set; } // nomor VA
        public double numericField1 { get; set; } // kembalian (tunai)
        public ReceiptCustomer customer { get; set; }
        public List<ReceiptDetailInvoice> detailInvoice { get; set; }
    }

    private class ReceiptCustomer
    {
        public string name { get; set; }
        public string customerNo { get; set; }
    }

    private class ReceiptDetailInvoice
    {
        public ReceiptInvoice invoice { get; set; }
        public List<ReceiptDetailDiscount> detailDiscount { get; set; }
    }

    private class ReceiptInvoice
    {
        public string number { get; set; }
    }

    private class ReceiptDetailDiscount
    {
        public double amount { get; set; }
        public ReceiptDiscountAccount account { get; set; }
    }

    private class ReceiptDiscountAccount
    {
        public string name { get; set; }
    }

    // ===================== Response detail-invoice.php =====================
    private class DetailInvoiceResponse
    {
        public string status { get; set; }
        public DetailInvoiceData data { get; set; }
    }

    private class DetailInvoiceData
    {
        public string toAddress { get; set; }      // alamat
        public Shipment shipment { get; set; }     // mode pengiriman
        public double tax1AmountBase { get; set; } // total pajak
        public string transDate { get; set; }
        public double cashDiscount { get; set; }   // total diskon faktur
        public string number { get; set; }
        public List<InvItem> detailItem { get; set; }
        public List<InvExpense> detailExpense { get; set; } // biaya-biaya
        public string status { get; set; }
        public double subTotal { get; set; }
        public string masterSalesmanName { get; set; }
        public double totalAmount { get; set; }
        public List<ReceiptHistory> receiptHistory { get; set; }
    }

    private class Shipment
    {
        public string name { get; set; }
    }

    private class InvExpense
    {
        public string detailName { get; set; }
        public double expenseAmount { get; set; }
    }

    private class InvItem
    {
        public ItemUnit itemUnit { get; set; }
        public double unitPrice { get; set; }
        public string salesmanName { get; set; }
        public InvItemInfo item { get; set; }
        public double quantity { get; set; }
    }

    private class ItemUnit
    {
        public string name { get; set; }
    }

    private class InvItemInfo
    {
        public int id { get; set; }
        public string shortName { get; set; }
        public string name { get; set; }
        public string no { get; set; }
    }

    private class ReceiptHistory
    {
        public string historyNumber { get; set; }
        public string historyPaymentName { get; set; }
    }
}
