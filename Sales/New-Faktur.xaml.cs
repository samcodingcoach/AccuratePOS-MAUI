using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Newtonsoft.Json;
namespace MyPosAccurate2026.Sales;

public partial class New_Faktur : ContentPage
{
    public string? SelectedKonsumenValue { get; private set; }
    public record KonsumenOption(string Text, string Value);

    public ObservableCollection<ItemModel> AutoCompleteResults { get; set; } = new ObservableCollection<ItemModel>();

    // 1. Deklarasikan HttpClient tanpa langsung mengisi string URL di sini
    private readonly HttpClient _httpClient;

    public New_Faktur()
	{
		InitializeComponent();
        //cek_token();
        _httpClient = new HttpClient { BaseAddress = new Uri(App.API_HOST) };

        var listKonsumen = new List<KonsumenOption>
        {
            new("Free", "MB003"),
            new("Konsumen Shopee", "C.00001"),
            new("Membership", "MB002"),
            new("Non Member", "MB001")
        };

        // 2. Bind ke Picker
        PickerKonsumen.ItemsSource = listKonsumen;

        List_AutoComplete.ItemsSource = AutoCompleteResults;
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

    private void List_AutoComplete_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ItemModel selectedItem)
        {
            // Pindahkan teks dan sembunyikan dropdown
            SearchBar_Item.Text = selectedItem.item_no;
            Border_AutoComplete.IsVisible = false;

            System.Diagnostics.Debug.WriteLine($"Barang Dipilih: {selectedItem.name} - {selectedItem.item_no}");

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
            System.Diagnostics.Debug.WriteLine($"Value tersimpan: {SelectedKonsumenValue}");
            
        }
    }


}