using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Text;
using System.Linq;

#if ANDROID
using Android.Bluetooth;
using Java.Util;
#endif

namespace MyPosAccurate2026.Sales;

public partial class Print : ContentPage
{
    private static readonly CultureInfo IdCulture = new CultureInfo("id-ID");

    private string _receiptNumber = "";   // nomor struk (110103.2026.06.xxxxx)
    private string _invoiceNumber = "";   // nomor faktur SI (fallback untuk detail-invoice)
    private double _nominalBayar = 0;     // khusus tunai: nominal uang yang dibayarkan konsumen

    private DetailReceiptData _receipt;
    private DetailInvoiceData _invoice;
    private CompanyProfileData _company;
    private string _invoiceNoStr;

#if ANDROID
    private static readonly UUID SPP_UUID = UUID.FromString("00001101-0000-1000-8000-00805f9b34fb");
#endif

    public Print()
    {
        InitializeComponent();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    // Dipanggil dari alur pembayaran (Tunai/QRIS/VA) setelah save-receipt sukses.
    // nominalBayar hanya relevan untuk Tunai (uang yang diserahkan konsumen); 0 untuk QRIS/VA.
    public Print(string receiptNumber, string invoiceNumber, double nominalBayar = 0) : this()
    {
        _receiptNumber = receiptNumber ?? "";
        _invoiceNumber = invoiceNumber ?? "";
        _nominalBayar = nominalBayar;
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

            // 3. Data Company Profile
            var companyData = await FetchCompanyProfileAsync(cleanToken);

            _receipt = receipt;
            _invoice = invoice;
            _invoiceNoStr = invoiceNo;
            _company = companyData;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                TampilkanStruk(receipt, invoice, invoiceNo, companyData);
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

    private async Task<CompanyProfileData> FetchCompanyProfileAsync(string token)
    {
        string url = $"{App.API_HOST}profile/company.php";
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await client.GetAsync(url);
            string responseContent = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(responseContent) || responseContent.TrimStart().StartsWith("<"))
                return null;

            var result = JsonConvert.DeserializeObject<CompanyProfileResponse>(responseContent);
            return result?.data;
        }
        catch
        {
            return null;
        }
    }

    private void TampilkanStruk(DetailReceiptData receipt, DetailInvoiceData invoice, string invoiceNo, CompanyProfileData companyData)
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
        double total = invoice?.totalAmount ?? receipt?.totalPayment ?? 0;
        LabelTotal.Text = FormatRupiah(total);

        // ===== Metode =====
        string metode = invoice?.receiptHistory?.FirstOrDefault()?.historyPaymentName;
        if (string.IsNullOrWhiteSpace(metode))
            metode = LabelMetodeFromCode(receipt?.paymentMethod);
        LabelMetode.Text = metode;

        // ===== Khusus Tunai: nominal dibayar & kembalian =====
        bool isTunai = string.Equals(receipt?.paymentMethod, "CASH_OTHER", StringComparison.OrdinalIgnoreCase);
        BoxTunai.IsVisible = isTunai;
        if (isTunai)
        {
            // Dasar tagihan = nilai yang diterima (pembulatan) bila ada, jika tidak pakai total faktur
            double tagihan = receipt?.totalPayment > 0 ? receipt.totalPayment : total;

            // Nominal dibayar: utamakan yang dibawa dari halaman pembayaran;
            // fallback ke kembalian tersimpan (numericField1) + tagihan
            double bayar = _nominalBayar > 0 ? _nominalBayar : tagihan + (receipt?.numericField1 ?? 0);

            double kembalian = bayar - tagihan;
            if (kembalian < 0) kembalian = 0;

            LabelBayar.Text = FormatRupiah(bayar);
            LabelKembalian.Text = FormatRupiah(kembalian);
        }

        // ===== Catatan =====
        string catatan = receipt?.description;
        RowCatatan.IsVisible = !string.IsNullOrWhiteSpace(catatan);
        LabelCatatan.Text = catatan ?? "";

        // ===== Footer Company & Header Company =====
        if (companyData != null)
        {
            LabelHeaderCompanyName.Text = companyData.name ?? "-";
            LabelCompanyName.Text = companyData.name ?? "-";
            
            string addressCity = "";
            if (!string.IsNullOrWhiteSpace(companyData.address)) addressCity += companyData.address;
            if (!string.IsNullOrWhiteSpace(companyData.city)) 
            {
                if (!string.IsNullOrEmpty(addressCity)) addressCity += ", ";
                addressCity += companyData.city;
            }
            LabelCompanyAddressCity.Text = string.IsNullOrEmpty(addressCity) ? "-" : $"📍 {addressCity}";
            
            string contact = "";
            if (!string.IsNullOrWhiteSpace(companyData.phone)) contact += $"📞 {companyData.phone}";
            if (!string.IsNullOrWhiteSpace(companyData.email)) 
            {
                if (!string.IsNullOrEmpty(contact)) contact += "   ";
                contact += $"✉ {companyData.email}";
            }
            LabelCompanyContact.Text = string.IsNullOrEmpty(contact) ? "-" : contact;
        }
        
        LabelPrintDate.Text = $"Dicetak pada: {DateTime.Now.ToString("dd MMM yyyy HH:mm:ss", IdCulture)}";
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
#if ANDROID
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Bluetooth>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Bluetooth>();
            }

            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("Izin Ditolak", "Izin Bluetooth diperlukan untuk menghubungkan ke printer.", "OK");
                return;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error Permission", ex.Message, "OK");
            return;
        }

        BluetoothAdapter bluetoothAdapter = BluetoothAdapter.DefaultAdapter;
        if (bluetoothAdapter == null || !bluetoothAdapter.IsEnabled)
        {
            await DisplayAlert("Error", "Bluetooth tidak tersedia atau belum diaktifkan.", "OK");
            return;
        }

        var bondedDevices = bluetoothAdapter.BondedDevices;
        if (bondedDevices == null || bondedDevices.Count == 0)
        {
            await DisplayAlert("Error", "Tidak ada printer Bluetooth yang di-pairing.", "OK");
            return;
        }

        var deviceNames = bondedDevices.Select(d => d.Name).ToArray();
        string selectedPrinter = await DisplayActionSheet("Pilih Printer Bluetooth", "Batal", null, deviceNames);

        if (selectedPrinter == "Batal" || string.IsNullOrEmpty(selectedPrinter))
            return;

        string paperSizeStr = await DisplayActionSheet("Pilih Ukuran Kertas", "Batal", null, "58mm", "80mm");
        if (paperSizeStr == "Batal" || string.IsNullOrEmpty(paperSizeStr))
            return;
        
        int paperSize = paperSizeStr == "58mm" ? 32 : 48;
        
        BluetoothDevice device = bondedDevices.FirstOrDefault(d => d.Name == selectedPrinter);
        if (device != null)
        {
            await ExecutePrint(device, paperSize);
        }
#else
        await DisplayAlert("Error", "Bluetooth hanya didukung di Android!", "OK");
#endif
    }

    private string AlignRight(string label, string value, int totalLength)
    {
        if (label == null) label = "";
        if (value == null) value = "";
        
        int spacing = totalLength - (label.Length + value.Length);
        if (spacing < 1) spacing = 1;
        return label + new string(' ', spacing) + value;
    }

    private string CenterText(string text, int totalLength)
    {
        if (text == null) text = "";
        if (text.Length >= totalLength) return text;
        int padding = (totalLength - text.Length) / 2;
        return new string(' ', padding) + text;
    }

#if ANDROID
    private async Task ExecutePrint(BluetoothDevice device, int paperSize)
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            string line = new string('-', paperSize) + "\n";
            string lineEq = new string('=', paperSize) + "\n";

            // ESC POS Init
            sb.Append("\x1B\x40");

            // Header
            sb.Append("\x1B\x61\x01"); // Center align
            sb.Append("\x1B\x21\x08"); // Bold
            sb.Append("STRUK PEMBAYARAN\n");
            sb.Append("\x1B\x21\x00"); // Normal
            sb.Append($"{_company?.name ?? "POS ACCURATE"}\n");
            sb.Append("\x1B\x61\x00"); // Left align
            sb.Append(line);

            // Info transaksi
            string tgl = _receipt?.transDate ?? _invoice?.transDate ?? "-";
            string kasir = string.IsNullOrWhiteSpace(_receipt?.charField2) ? "-" : _receipt.charField2;
            string konsumen = _receipt?.customer != null ? $"{_receipt.customer.customerNo} - {_receipt.customer.name}" : "-";
            
            sb.Append(AlignRight("No. Struk", string.IsNullOrWhiteSpace(_receiptNumber) ? "-" : _receiptNumber, paperSize) + "\n");
            sb.Append(AlignRight("No. Faktur", string.IsNullOrWhiteSpace(_invoiceNoStr) ? "-" : _invoiceNoStr, paperSize) + "\n");
            sb.Append(AlignRight("Tanggal", tgl, paperSize) + "\n");
            sb.Append(AlignRight("Kasir", kasir, paperSize) + "\n");
            sb.Append(AlignRight("Konsumen", konsumen, paperSize) + "\n");
            
            string sales = _invoice?.masterSalesmanName;
            if (!string.IsNullOrWhiteSpace(sales)) sb.Append(AlignRight("Sales", sales, paperSize) + "\n");
            
            string pengiriman = _invoice?.shipment?.name;
            if (!string.IsNullOrWhiteSpace(pengiriman)) sb.Append(AlignRight("Pengiriman", pengiriman, paperSize) + "\n");
            
            sb.Append(line);

            // Items header
            sb.Append(AlignRight("ITEM", "TOTAL", paperSize) + "\n");

            // Items
            var items = _invoice?.detailItem ?? new List<InvItem>();
            foreach (var itm in items)
            {
                sb.Append($"{itm.item?.name}\n");
                string qtyPrice = $"{itm.quantity.ToString("0.##", IdCulture)} {itm.itemUnit?.name} x {FormatRupiah(itm.unitPrice)}";
                string totalLn = FormatRupiah(itm.quantity * itm.unitPrice);
                sb.Append(AlignRight(qtyPrice, totalLn, paperSize) + "\n");
            }

            sb.Append(line);

            // Ringkasan
            sb.Append(AlignRight("Subtotal", FormatRupiah(_invoice?.subTotal ?? 0), paperSize) + "\n");
            
            double diskonFaktur = _invoice?.cashDiscount ?? 0;
            if (diskonFaktur > 0)
                sb.Append(AlignRight("Diskon Faktur", $"- {FormatRupiah(diskonFaktur)}", paperSize) + "\n");

            var expenses = _invoice?.detailExpense ?? new List<InvExpense>();
            if (expenses.Count > 0)
            {
                sb.Append("Biaya-biaya\n");
                foreach (var exp in expenses)
                {
                    sb.Append(AlignRight($"  {exp.detailName}", FormatRupiah(exp.expenseAmount), paperSize) + "\n");
                }
            }

            sb.Append(AlignRight("Total Pajak (PPN)", FormatRupiah(_invoice?.tax1AmountBase ?? 0), paperSize) + "\n");
            
            sb.Append(lineEq);
            
            double totalAll = _invoice?.totalAmount ?? _receipt?.totalPayment ?? 0;
            sb.Append("\x1B\x21\x08"); // Bold
            sb.Append(AlignRight("TOTAL", FormatRupiah(totalAll), paperSize) + "\n");
            sb.Append("\x1B\x21\x00"); // Normal
            
            sb.Append(lineEq);

            // Tunai Dibayar & Kembalian
            bool isTunai = string.Equals(_receipt?.paymentMethod, "CASH_OTHER", StringComparison.OrdinalIgnoreCase);
            if (isTunai)
            {
                double tagihan = _receipt?.totalPayment > 0 ? _receipt.totalPayment : totalAll;
                double bayar = _nominalBayar > 0 ? _nominalBayar : tagihan + (_receipt?.numericField1 ?? 0);
                double kembalian = bayar - tagihan;
                if (kembalian < 0) kembalian = 0;

                sb.Append(AlignRight("Tunai Dibayar", FormatRupiah(bayar), paperSize) + "\n");
                sb.Append(AlignRight("Kembalian", FormatRupiah(kembalian), paperSize) + "\n");
            }

            // Metode
            string metode = _invoice?.receiptHistory?.FirstOrDefault()?.historyPaymentName;
            if (string.IsNullOrWhiteSpace(metode))
                metode = LabelMetodeFromCode(_receipt?.paymentMethod);
            sb.Append(AlignRight("Metode", metode, paperSize) + "\n");

            string catatan = _receipt?.description;
            if (!string.IsNullOrWhiteSpace(catatan))
            {
                sb.Append($"Catatan:\n{catatan}\n");
            }

            sb.Append(line);

            // Footer
            sb.Append("\x1B\x61\x01"); // Center align
            sb.Append("Terima kasih telah berbelanja\n\n");
            
            if (_company != null)
            {
                sb.Append($"{_company.name ?? "-"}\n");
                
                string addressCity = "";
                if (!string.IsNullOrWhiteSpace(_company.address)) addressCity += _company.address;
                if (!string.IsNullOrWhiteSpace(_company.city)) 
                {
                    if (!string.IsNullOrEmpty(addressCity)) addressCity += ", ";
                    addressCity += _company.city;
                }
                if (!string.IsNullOrEmpty(addressCity)) sb.Append($"{addressCity}\n");

                string contact = "";
                if (!string.IsNullOrWhiteSpace(_company.phone)) contact += $"{_company.phone}";
                if (!string.IsNullOrWhiteSpace(_company.email)) 
                {
                    if (!string.IsNullOrEmpty(contact)) contact += "  ";
                    contact += $"{_company.email}";
                }
                if (!string.IsNullOrEmpty(contact)) sb.Append($"{contact}\n");
            }

            sb.Append($"Dicetak pada: {DateTime.Now.ToString("dd MMM yyyy HH:mm:ss", IdCulture)}\n\n\n\n");

            string struk = sb.ToString();
            byte[] buffer = Encoding.GetEncoding(437).GetBytes(struk);

            await Task.Delay(500); // Stabilkan koneksi
            using (BluetoothSocket bluetoothSocket = device.CreateRfcommSocketToServiceRecord(SPP_UUID))
            {
                bluetoothSocket.Connect();
                for (int i = 0; i < buffer.Length; i += 512)
                {
                    int size = Math.Min(512, buffer.Length - i);
                    bluetoothSocket.OutputStream.Write(buffer, i, size);
                    await Task.Delay(10);
                }
                bluetoothSocket.OutputStream.Flush();
                bluetoothSocket.Close();
                await DisplayAlert("Sukses", "Print berhasil.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Gagal print: {ex.Message}", "OK");
        }
    }
#endif

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

    // ===================== Response profile/company.php =====================
    private class CompanyProfileResponse
    {
        public string status { get; set; }
        public CompanyProfileData data { get; set; }
    }

    private class CompanyProfileData
    {
        public string name { get; set; }
        public string city { get; set; }
        public string email { get; set; }
        public string address { get; set; }
        public string phone { get; set; }
    }
}
