using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace MyPosAccurate2026.Receipt;

public partial class List_Receipt : ContentPage
{
	string customerNo = string.Empty;
	string bankNo = string.Empty;

	ObservableCollection<KonsumenItem> _konsumenList = new ObservableCollection<KonsumenItem>();
	ObservableCollection<KasBankItem> _caraBayarList = new ObservableCollection<KasBankItem>();
	bool _filterLoaded = false;

	public List_Receipt()
	{
		InitializeComponent();
		CV_Konsumen.ItemsSource = _konsumenList;
		CV_CaraBayar.ItemsSource = _caraBayarList;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		if (!_filterLoaded)
		{
			_filterLoaded = true;
			await LoadKonsumen();
			await LoadCaraBayar();
		}
	}

	private async Task LoadKonsumen()
	{
		string cleanToken = Preferences.Get("TOKEN_KEY", "").Replace("Bearer ", "").Trim();

		if (string.IsNullOrEmpty(cleanToken))
		{
			await DisplayAlertAsync("Sesi Habis", "Anda harus login kembali.", "OK");
			return;
		}

		try
		{
			using (var client = new HttpClient())
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);

				string apiUrl = $"{App.API_HOST}pelanggan/list.php";
				var response = await client.GetAsync(apiUrl);
				var responseContent = await response.Content.ReadAsStringAsync();

				if (responseContent.StartsWith("<"))
				{
					await DisplayAlertAsync("Error Server", "Gagal membaca format data dari server.", "OK");
					return;
				}

				// Endpoint bisa mengembalikan array langsung atau dibungkus { status, data }
				List<KonsumenItem> items;
				if (responseContent.TrimStart().StartsWith("["))
				{
					items = JsonConvert.DeserializeObject<List<KonsumenItem>>(responseContent);
				}
				else
				{
					var wrapped = JsonConvert.DeserializeObject<KonsumenListResponse>(responseContent);
					items = wrapped?.data;
				}

				MainThread.BeginInvokeOnMainThread(() =>
				{
					_konsumenList.Clear();

					// Opsi "Semua" = tanpa filter konsumen, terpilih secara default
					_konsumenList.Add(new KonsumenItem { name = "Semua", customerNo = string.Empty, IsSelected = true });
					customerNo = string.Empty;

					if (items != null)
					{
						foreach (var item in items)
							_konsumenList.Add(item);
					}
				});
			}
		}
		catch (Exception ex)
		{
			await DisplayAlertAsync("Error", $"Gagal memuat data konsumen: {ex.Message}", "OK");
		}
	}

	private void OnKonsumenTapped(object sender, TappedEventArgs e)
	{
		if (sender is Border border && border.BindingContext is KonsumenItem item)
		{
			foreach (var k in _konsumenList)
				k.IsSelected = false;

			item.IsSelected = true;
			customerNo = item.customerNo;
		}
	}

	private async Task LoadCaraBayar()
	{
		string cleanToken = Preferences.Get("TOKEN_KEY", "").Replace("Bearer ", "").Trim();

		if (string.IsNullOrEmpty(cleanToken))
		{
			await DisplayAlertAsync("Sesi Habis", "Anda harus login kembali.", "OK");
			return;
		}

		try
		{
			using (var client = new HttpClient())
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);

				string apiUrl = $"{App.API_HOST}coa/list-kasbank.php";
				var response = await client.GetAsync(apiUrl);
				var responseContent = await response.Content.ReadAsStringAsync();

				if (responseContent.StartsWith("<"))
				{
					await DisplayAlertAsync("Error Server", "Gagal membaca format data dari server.", "OK");
					return;
				}

				var apiResult = JsonConvert.DeserializeObject<KasBankListResponse>(responseContent);

				// Sembunyikan item dengan value "Kas Kecil" (sama seperti LoadKasBankData)
				var items = apiResult?.data?
					.Where(b => b.name != null && b.name.IndexOf("Kas Kecil", StringComparison.OrdinalIgnoreCase) < 0)
					.ToList();

				MainThread.BeginInvokeOnMainThread(() =>
				{
					_caraBayarList.Clear();

					// Opsi "Semua" = tanpa filter cara bayar, terpilih secara default
					_caraBayarList.Add(new KasBankItem { name = "Semua", no = string.Empty, IsSelected = true });
					bankNo = string.Empty;

					if (items != null)
					{
						foreach (var item in items)
							_caraBayarList.Add(item);
					}
				});
			}
		}
		catch (Exception ex)
		{
			await DisplayAlertAsync("Error", $"Gagal memuat data cara bayar: {ex.Message}", "OK");
		}
	}

	private void OnCaraBayarTapped(object sender, TappedEventArgs e)
	{
		if (sender is Border border && border.BindingContext is KasBankItem item)
		{
			foreach (var b in _caraBayarList)
				b.IsSelected = false;

			item.IsSelected = true;
			bankNo = item.no;
		}
	}

	public class KonsumenListResponse
	{
		public string status { get; set; }
		public List<KonsumenItem> data { get; set; }
	}

	public class KasBankListResponse
	{
		public List<KasBankItem> data { get; set; }
	}

	public class KasBankItem : INotifyPropertyChanged
	{
		public string no { get; set; }
		public string name { get; set; }
		public int id { get; set; }

		bool _isSelected;
		public bool IsSelected
		{
			get => _isSelected;
			set
			{
				if (_isSelected != value)
				{
					_isSelected = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
	}

	public class KonsumenItem : INotifyPropertyChanged
	{
		public string name { get; set; }
		public int id { get; set; }
		public string customerNo { get; set; }

		bool _isSelected;
		public bool IsSelected
		{
			get => _isSelected;
			set
			{
				if (_isSelected != value)
				{
					_isSelected = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
	}
}
