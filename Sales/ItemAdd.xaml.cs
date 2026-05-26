using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Newtonsoft.Json;
namespace MyPosAccurate2026.Sales;

public partial class ItemAdd : ContentPage
{
    public double ItemBalance { get; set; }
    public string CustomerCode { get; set; }
    public string SelectedSalesNumber { get; private set; }
    public ItemAdd()
	{
		InitializeComponent();
        cek_token();
        LoadSalesData();
	}


    public ItemAdd(string itemNo, string name, double balance, string konsumenValue)
    {
        InitializeComponent();
        CustomerCode = konsumenValue;
        ItemBalance = balance;
        FormNoItem.Text = itemNo;
        FormNamaBarang.Text = name;
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