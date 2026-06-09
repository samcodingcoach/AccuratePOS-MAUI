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

	public Pembayaran_Faktur()
	{
		InitializeComponent();

        PickerBank.ItemDisplayBinding = new Binding("name");

        // Panggil fungsi pengambilan data saat halaman dibuka
        _ = LoadKasBankData();
    }

    private void PickerBank_SelectedIndexChanged(object sender, EventArgs e)
    {
        // Tangkap objek dari item yang dipilih pengguna
        if (PickerBank.SelectedItem is KasBankData selectedBank)
        {
            System.Diagnostics.Debug.WriteLine($"Bank Dipilih: {selectedBank.name} dengan ID: {selectedBank.id} & No: {selectedBank.no}");
            bankNo = selectedBank.no;
           
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
                        // Eksekusi perubahan UI wajib menggunakan MainThread
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            PickerBank.ItemsSource = apiResult.data;
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
}