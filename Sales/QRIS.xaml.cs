using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace MyPosAccurate2026.Sales;

public partial class QRIS : ContentPage
{
    private IDispatcherTimer _countdownTimer;
    private IDispatcherTimer _statusTimer;
    private TimeSpan _sisaWaktu = TimeSpan.FromMinutes(10);
    private bool _isExpired = false;
    private bool _isPaid = false;
    private bool _isCheckingStatus = false;

    private static readonly CultureInfo IdCulture = new CultureInfo("id-ID");

    private string _orderId = ""; // nantinya diambil dari pembayaran-faktur
    private double _grossAmount = 0; //nantinya diambil dari pembayaran-faktur

    // Data pembayaran yang dieksekusi (save-receipt.php) saat status settlement
    private PaymentReceiptData _receiptData;

    public QRIS()
    {
        InitializeComponent();
        LabelOrderId.Text = _orderId;
        LabelTotalPembayaran.Text = FormatRupiah(_grossAmount);
    }

    // Konstruktor terima data transaksi dari halaman pembayaran
    public QRIS(string orderId, double grossAmount) : this()
    {
        SetDataTransaksi(orderId, grossAmount);
    }

    // Konstruktor lengkap: bawa juga data untuk simpan pembayaran setelah settlement
    public QRIS(string orderId, double grossAmount, PaymentReceiptData receiptData) : this()
    {
        SetDataTransaksi(orderId, grossAmount);
        _receiptData = receiptData;
    }

    public void SetDataTransaksi(string orderId, double grossAmount)
    {
        _orderId = orderId;
        _grossAmount = grossAmount;

        LabelOrderId.Text = orderId;
        LabelTotalPembayaran.Text = FormatRupiah(grossAmount);
    }

    private static string FormatRupiah(double nilai) => $"Rp {nilai.ToString("N0", IdCulture)}";

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_grossAmount <= 0)
        {
            await DisplayAlertAsync("QRIS Gagal",
                "Nilai pembayaran tidak valid. Total harus lebih dari Rp 0.", "OK");
            await Navigation.PopAsync();
            return;
        }

        StartCountdown();
        await CreateQrisAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopCountdown();
        StopStatusPolling();
    }

    // === Generate QRIS via Midtrans (create_qris.php) ===
    private async Task CreateQrisAsync()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            QrLoading.IsRunning = true;
            QrLoading.IsVisible = true;
        });

        try
        {
            using var client = new HttpClient();

            var payload = new
            {
                order_id = _orderId,
                gross_amount = (long)Math.Round(_grossAmount)
            };

            var content = new StringContent(
                JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            var response = await client.PostAsync(App.API_MIDTRANS + "create_qris.php", content);
            string responseContent = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(responseContent) || responseContent.TrimStart().StartsWith("<"))
            {
                await ShowQrErrorAsync("Server mengembalikan respons tidak valid.");
                return;
            }

            var result = JsonConvert.DeserializeObject<QrisResponse>(responseContent);

            if (result != null && result.status == "success" && !string.IsNullOrWhiteSpace(result.qris_url))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    QrImage.Source = result.qris_url;
                    QrLoading.IsRunning = false;
                    QrLoading.IsVisible = false;
                });

                StartStatusPolling();
            }
            else
            {
                await ShowQrErrorAsync(result?.message ?? "Gagal membuat QRIS.");
            }
        }
        catch (Exception ex)
        {
            await ShowQrErrorAsync(ex.Message);
        }
    }

    private async Task ShowQrErrorAsync(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            QrLoading.IsRunning = false;
            QrLoading.IsVisible = false;
        });
        await DisplayAlertAsync("QRIS Gagal", message, "OK");
    }

    // === Hitung mundur 10 menit ===
    private void StartCountdown()
    {
        StopCountdown();

        _countdownTimer = Dispatcher.CreateTimer();
        _countdownTimer.Interval = TimeSpan.FromSeconds(1);
        _countdownTimer.Tick += CountdownTimer_Tick;
        _countdownTimer.Start();

        UpdateCountdownLabel();
    }

    private void StopCountdown()
    {
        if (_countdownTimer != null)
        {
            _countdownTimer.Stop();
            _countdownTimer.Tick -= CountdownTimer_Tick;
            _countdownTimer = null;
        }
    }

    private void CountdownTimer_Tick(object sender, EventArgs e)
    {
        _sisaWaktu = _sisaWaktu.Subtract(TimeSpan.FromSeconds(1));

        if (_sisaWaktu <= TimeSpan.Zero)
        {
            _sisaWaktu = TimeSpan.Zero;
            StopCountdown();
            OnExpired();
        }

        UpdateCountdownLabel();
    }

    private void UpdateCountdownLabel()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LabelCountdown.Text = $"{_sisaWaktu.Minutes:D2}:{_sisaWaktu.Seconds:D2}";
        });
    }

    private void OnExpired()
    {
        _isExpired = true;
        StopStatusPolling();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LabelCountdown.Text = "00:00";
            LabelStatus.Text = "Kadaluarsa";
            LabelStatus.TextColor = Color.FromArgb("#C0392B");
            QrImage.Opacity = 0.25;
            B_CekStatus.IsEnabled = false;
            B_CekStatus.Text = "QRIS KADALUARSA";
            B_CekStatus.BackgroundColor = Color.FromArgb("#B0B0B0");
        });
    }

    // === Polling cek status pembayaran tiap 5 detik ===
    private void StartStatusPolling()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StopStatusPolling();

            _statusTimer = Dispatcher.CreateTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(5);
            _statusTimer.Tick += async (s, e) => await CekStatusPembayaranAsync();
            _statusTimer.Start();
        });
    }

    private void StopStatusPolling()
    {
        if (_statusTimer != null)
        {
            _statusTimer.Stop();
            _statusTimer = null;
        }
    }

    private async Task CekStatusPembayaranAsync()
    {
        if (_isExpired || _isPaid || _isCheckingStatus)
            return;

        _isCheckingStatus = true;
        try
        {
            using var client = new HttpClient();
            string url = App.API_MIDTRANS + "midtrans_status.php?order_id=" + Uri.EscapeDataString(_orderId);

            var response = await client.GetAsync(url);
            string responseContent = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(responseContent) || responseContent.TrimStart().StartsWith("<"))
                return;

            var list = JsonConvert.DeserializeObject<List<StatusResponse>>(responseContent);
            if (list == null || list.Count == 0)
                return;

            string status = (list[0].transaction_status ?? "").ToLowerInvariant();

            if (status == "settlement" || status == "capture")
            {
                OnPaid();
            }
            else if (status == "expire" || status == "deny" || status == "cancel" || status == "failure")
            {
                StopStatusPolling();
                StopCountdown();
                OnExpired();
            }
            // "pending" => tetap menunggu, polling lanjut
        }
        catch
        {
            // abaikan error sementara, polling lanjut di tick berikutnya
        }
        finally
        {
            _isCheckingStatus = false;
        }
    }

    private void OnPaid()
    {
        _isPaid = true;
        StopStatusPolling();
        StopCountdown();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            LabelStatus.Text = "Pembayaran Berhasil";
            LabelStatus.TextColor = Color.FromArgb("#27AE60");
            B_CekStatus.IsEnabled = false;
            B_CekStatus.Text = "MENYIMPAN PEMBAYARAN...";
            B_CekStatus.BackgroundColor = Color.FromArgb("#27AE60");

            // Eksekusi simpan pembayaran (persis seperti B_SimpanPembayaran_Clicked)
            bool tersimpan = await SimpanPembayaranAsync();

            if (tersimpan)
            {
                await DisplayAlertAsync("Sukses",
                    "Pembayaran QRIS berhasil dan tersimpan ke sistem.", "OK");

                // Kembali ke List-Faktur (instance baru agar status faktur ter-refresh)
                Application.Current.MainPage = new NavigationPage(new List_Faktur());
            }
            else
            {
                B_CekStatus.Text = "PEMBAYARAN BERHASIL";
            }
        });
    }

    // Simpan pembayaran ke save-receipt.php — payload identik dengan B_SimpanPembayaran_Clicked
    private async Task<bool> SimpanPembayaranAsync()
    {
        if (_receiptData == null)
            return false;

        var detailDiscount = new List<object>();
        if (_receiptData.DiskonPembayaran > 0)
        {
            detailDiscount.Add(new
            {
                accountNo = int.Parse(_receiptData.DiskonAccountNo),
                amount = _receiptData.DiskonPembayaran
            });
        }

        var detailInvoice = new List<object>
        {
            new
            {
                invoiceNo = _receiptData.InvoiceNo,
                paymentAmount = _receiptData.PaymentAmount,
                detailDiscount = detailDiscount
            }
        };

        var payload = new
        {
            bankNo = _receiptData.BankNo,
            number = _receiptData.Number ?? "",
            chequeAmount = _receiptData.ChequeAmount,
            customerNo = _receiptData.CustomerNo,
            transDate = _receiptData.TransDate,
            chequeDate = _receiptData.TransDate,
            paymentMethod = _receiptData.PaymentMethod,
            description = _receiptData.Description ?? "",
            charField1 = _orderId, // referensi order QRIS
            charField2 = _receiptData.CharField2,
            detailInvoice = detailInvoice
        };

        try
        {
            string cleanToken = Preferences.Get("TOKEN_KEY", "").Replace("Bearer ", "").Trim();
            string apiUrl = $"{App.API_HOST}penjualan/save-receipt.php";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);

            string jsonPayload = JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(apiUrl, content);
            string responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return true;

            await DisplayAlertAsync("Gagal Menyimpan", $"Sistem merespons: {responseString}", "OK");
            return false;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error Koneksi", $"Terjadi kesalahan: {ex.Message}", "OK");
            return false;
        }
    }

    private async void B_CekStatus_Clicked(object sender, EventArgs e)
    {
        if (_isExpired || _isPaid)
            return;

        await CekStatusPembayaranAsync();

        if (!_isPaid)
            await DisplayAlertAsync("Cek Status", "Pembayaran masih menunggu. Silakan selesaikan scan QRIS.", "OK");
    }

    private async void TapTutup_Tapped(object sender, TappedEventArgs e)
    {
        bool konfirmasi = await DisplayAlertAsync("Batalkan QRIS",
            "Tutup halaman pembayaran QRIS?", "Ya", "Tidak");
        if (konfirmasi)
        {
            StopCountdown();
            await Navigation.PopAsync();
        }
    }

    // Response create_qris.php
    private class QrisResponse
    {
        public string status { get; set; }
        public string qris_url { get; set; }
        public string message { get; set; }
    }

    // Response midtrans_status.php (berupa array JSON)
    private class StatusResponse
    {
        public string order_id { get; set; }
        public string gross_amount { get; set; }
        public string transaction_status { get; set; }
        public string settlement_time { get; set; }
    }

    // Data pembayaran yang dibawa dari Pembayaran-Faktur untuk disimpan saat settlement
    public class PaymentReceiptData
    {
        public string BankNo { get; set; }
        public string Number { get; set; }            // nomor bukti pembayaran
        public double ChequeAmount { get; set; }      // grand total (setelah diskon)
        public string CustomerNo { get; set; }
        public string TransDate { get; set; }         // format yyyy-MM-dd
        public string PaymentMethod { get; set; }
        public string Description { get; set; }
        public string CharField2 { get; set; }
        public string InvoiceNo { get; set; }
        public double PaymentAmount { get; set; }     // total faktur sebelum diskon
        public double DiskonPembayaran { get; set; }
        public string DiskonAccountNo { get; set; }
    }
}
