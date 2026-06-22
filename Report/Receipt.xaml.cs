using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace MyPosAccurate2026.Report;

public partial class Receipt : ContentPage
{
	public Receipt()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await LoadKasirData();
	}

	private async Task LoadKasirData()
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

			// 2. Susun URL API untuk mengambil daftar kasir lokal
			string apiUrl = $"{App.API_HOST}kasir/list-lokal.php";

			using (var client = new HttpClient())
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);

				// 3. Tarik data dari server
				var response = await client.GetAsync(apiUrl);

				if (response.IsSuccessStatusCode)
				{
					string responseContent = await response.Content.ReadAsStringAsync();

					// Jaga-jaga jika PHP mengembalikan HTML error, bukan JSON
					if (responseContent.StartsWith("<"))
					{
						System.Diagnostics.Debug.WriteLine("Respon server bukan JSON: " + responseContent);
						return;
					}

					// 4. Konversi JSON ke object C#
					var apiResult = JsonConvert.DeserializeObject<KasirResponse>(responseContent);

					if (apiResult != null && apiResult.data != null)
					{
						// 5. Masukkan data ke dalam Picker di Main Thread
						MainThread.BeginInvokeOnMainThread(() =>
						{
							PickerNamaKasir.ItemsSource = apiResult.data;
						});
					}
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("Gagal memuat data kasir: " + ex.Message);
		}
	}

	public class KasirResponse
	{
		public string status { get; set; }
		public string message { get; set; }
		public List<KasirData> data { get; set; }
	}

	public class KasirData
	{
		public int id_users { get; set; }
		public string username { get; set; }

		// Properti tampilan untuk Picker (ItemDisplayBinding), mis. "1 - Administrator"
		public string DisplayName => $"{id_users} - {username}";
	}
}
