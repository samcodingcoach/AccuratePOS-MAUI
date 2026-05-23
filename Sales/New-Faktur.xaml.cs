using System.Text;
using System.Net.Http;
using Newtonsoft.Json;
namespace MyPosAccurate2026.Sales;

public partial class New_Faktur : ContentPage
{
    public string? SelectedKonsumenValue { get; private set; }
    public record KonsumenOption(string Text, string Value);
    public New_Faktur()
	{
		InitializeComponent();


        var listKonsumen = new List<KonsumenOption>
        {
            new("Free", "MB003"),
            new("Konsumen Shopee", "C.00001"),
            new("Membership", "MB002"),
            new("Non Member", "MB001")
        };

        // 2. Bind ke Picker
        PickerKonsumen.ItemsSource = listKonsumen;
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