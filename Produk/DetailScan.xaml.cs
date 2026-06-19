namespace MyPosAccurate2026.Produk;

public partial class DetailScan : ContentPage
{
    private string _itemNo;

    public DetailScan(string itemNo)
    {
        InitializeComponent();
        _itemNo = itemNo;

        ProductImage.Source = BuildImageSource(_itemNo);
    }

    private ImageSource BuildImageSource(string itemNo)
    {
        if (string.IsNullOrWhiteSpace(itemNo)) return "nophotoproduct150.jpg";
        string baseHost = App.API_HOST.Replace("api/", "");
        return $"{baseHost}images/{itemNo}.jpg";
    }
}
