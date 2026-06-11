using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace MyPosAccurate2026.Sales;

public partial class QRIS : ContentPage
{
    private IDispatcherTimer _countdownTimer;
    private TimeSpan _sisaWaktu = TimeSpan.FromMinutes(10);
    private bool _isExpired = false;

    private string _orderId = "POS26-0001";
    private double _grossAmount = 10000;

    public QRIS()
    {
        InitializeComponent();
    }

    // Konstruktor terima data transaksi dari halaman pembayaran
    public QRIS(string orderId, double grossAmount) : this()
    {
        SetDataTransaksi(orderId, grossAmount);
    }

    public void SetDataTransaksi(string orderId, double grossAmount)
    {
        _orderId = orderId;
        _grossAmount = grossAmount;

        LabelOrderId.Text = orderId;

        var ci = new CultureInfo("id-ID");
        LabelTotalPembayaran.Text = $"Rp {grossAmount.ToString("N0", ci)}";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StartCountdown();
        await CreateQrisAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopCountdown();
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

    private async void B_CekStatus_Clicked(object sender, EventArgs e)
    {
        if (_isExpired)
            return;

        // TODO: panggil endpoint cek status pembayaran QRIS ke backend
        await DisplayAlert("Cek Status", "Pembayaran masih menunggu. Silakan selesaikan scan QRIS.", "OK");
    }

    private async void TapTutup_Tapped(object sender, TappedEventArgs e)
    {
        bool konfirmasi = await DisplayAlert("Batalkan QRIS",
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
}
