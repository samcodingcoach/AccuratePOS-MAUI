using System.Globalization;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace MyPosAccurate2026.Report;

public partial class Receipt : ContentPage
{
	// Culture untuk format angka ribuan (mis. 150.000)
	private static readonly CultureInfo IdCulture = new CultureInfo("id-ID");

	public Receipt()
	{
		InitializeComponent();

		// Default rentang: tanggal 1 awal bulan sekarang s/d hari ini
		var today = DateTime.Today;
		DP_startdate.Date = new DateTime(today.Year, today.Month, 1);
		DP_enddate.Date = today;
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

	private async void BtnGenerate_Clicked(object sender, EventArgs e)
	{
		DateTime start = DP_startdate.Date.GetValueOrDefault(DateTime.Today);
		DateTime end = DP_enddate.Date.GetValueOrDefault(DateTime.Today);

		// Validasi rentang tanggal
		if (end < start)
		{
			await DisplayAlertAsync("Peringatan", "Tanggal akhir tidak boleh lebih awal dari tanggal mulai.", "OK");
			return;
		}

		BtnGenerate.IsEnabled = false;
		BtnGenerate.Text = "MEMUAT...";

		try
		{
			var receipts = await LoadReceiptData(start, end);
			if (receipts == null)
			{
				await DisplayAlertAsync("Gagal", "Tidak dapat memuat data penerimaan dari server.", "OK");
				return;
			}

			// Filter berdasarkan kasir yang dipilih (charField2 = "1 - Administrator")
			string namaKasir = "Semua Kasir";
			if (PickerNamaKasir.SelectedItem is KasirData kasir)
			{
				namaKasir = kasir.username;
				receipts = receipts.Where(r => r.charField2 == kasir.DisplayName).ToList();
			}

			RenderReport(receipts, start, end, namaKasir);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("Gagal generate report: " + ex.Message);
			await DisplayAlertAsync("Error", "Terjadi kesalahan saat menyusun laporan.", "OK");
		}
		finally
		{
			BtnGenerate.IsEnabled = true;
			BtnGenerate.Text = "GENERATE REPORT";
		}
	}

	// Tarik seluruh penerimaan pada rentang tanggal, manual paging (limit=100 per halaman)
	private async Task<List<ReceiptData>> LoadReceiptData(DateTime start, DateTime end)
	{
		string cleanToken = Preferences.Get("TOKEN_KEY", "").Replace("Bearer ", "").Trim();
		if (string.IsNullOrEmpty(cleanToken))
		{
			System.Diagnostics.Debug.WriteLine("Token tidak ditemukan.");
			return null;
		}

		var all = new List<ReceiptData>();
		string startParam = start.ToString("yyyy-MM-dd");
		string endParam = end.ToString("yyyy-MM-dd");
		const int limit = 100;
		int page = 1;
		bool hasMore = true;

		using (var client = new HttpClient())
		{
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);

			while (hasMore)
			{
				string apiUrl = $"{App.API_HOST}penerimaan-jual/list-receipt.php?start_date={startParam}&end_date={endParam}&limit={limit}&page={page}";

				var response = await client.GetAsync(apiUrl);
				if (!response.IsSuccessStatusCode)
					return all.Count > 0 ? all : null;

				string responseContent = await response.Content.ReadAsStringAsync();
				if (responseContent.StartsWith("<"))
				{
					System.Diagnostics.Debug.WriteLine("Respon server bukan JSON: " + responseContent);
					return all.Count > 0 ? all : null;
				}

				var apiResult = JsonConvert.DeserializeObject<ReceiptResponse>(responseContent);
				if (apiResult == null || apiResult.data == null || apiResult.data.Count == 0)
					break;

				all.AddRange(apiResult.data);

				// Lanjut paging hanya jika halaman ini penuh
				hasMore = apiResult.data.Count == limit;
				page++;
			}
		}

		return all;
	}

	private void RenderReport(List<ReceiptData> receipts, DateTime start, DateTime end, string namaKasir)
	{
		string periode = $"Per Periode {start:dd/MM/yyyy} - {end:dd/MM/yyyy}";

		if (receipts == null || receipts.Count == 0)
		{
			ReportCollection.ItemsSource = null;
			ReportBorder.IsVisible = false;
			LblEmptyReport.Text = "Tidak ada data penerimaan pada periode ini.";
			LblEmptyReport.IsVisible = true;
			return;
		}

		// Kelompokkan per tanggal (urut menaik), lalu per metode pembayaran
		var groups = receipts
			.GroupBy(r => r.transDate)
			.OrderBy(g => ParseTransDate(g.Key))
			.Select(dateGroup =>
			{
				var dg = new DateGroup
				{
					DateLabel = dateGroup.Key,
					TotalCount = dateGroup.Count(),
					TotalAmount = dateGroup.Sum(r => r.totalPayment)
				};
				dg.AddRange(dateGroup
					.GroupBy(r => r.paymentMethodName)
					.Select(m => new MethodRow
					{
						MethodName = m.Key,
						Count = m.Count(),
						Amount = m.Sum(r => r.totalPayment)
					}));
				return dg;
			})
			.ToList();

		// Ringkasan keseluruhan
		var summary = new ReportSummary
		{
			Periode = periode,
			TransactionCount = receipts.Count,
			GrandTotalAmount = receipts.Sum(r => r.totalPayment),
			NamaKasir = namaKasir,
			Methods = receipts
				.GroupBy(r => r.paymentMethodName)
				.Select(m => new MethodRow
				{
					MethodName = m.Key,
					Amount = m.Sum(r => r.totalPayment)
				})
				.ToList()
		};

		// Header & Footer membaca BindingContext; item grup membaca konteksnya sendiri
		ReportCollection.BindingContext = summary;
		ReportCollection.ItemsSource = groups;

		LblEmptyReport.IsVisible = false;
		ReportBorder.IsVisible = true;
	}

	private static DateTime ParseTransDate(string transDate)
	{
		return DateTime.TryParseExact(transDate, "dd/MM/yyyy", CultureInfo.InvariantCulture,
			DateTimeStyles.None, out var dt) ? dt : DateTime.MinValue;
	}

	// ---------- DTO ----------

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

	public class ReceiptResponse
	{
		public string status { get; set; }
		public string message { get; set; }
		public List<ReceiptData> data { get; set; }
	}

	public class ReceiptData
	{
		public string number { get; set; }
		public double totalPayment { get; set; }
		public string charField2 { get; set; }      // kasir, mis. "1 - Administrator"
		public string transDate { get; set; }       // "21/06/2026"
		public string paymentMethodName { get; set; }
		public ReceiptCustomer customer { get; set; }
	}

	public class ReceiptCustomer
	{
		public string name { get; set; }
		public string customerNo { get; set; }
	}

	// ---------- View-model untuk CollectionView ----------

	// Grup per tanggal; harus berupa koleksi agar bisa dipakai IsGrouped CollectionView.
	public class DateGroup : List<MethodRow>
	{
		public string DateLabel { get; set; }   // mis. "21/06/2026"
		public int TotalCount { get; set; }
		public double TotalAmount { get; set; }

		// Dipakai GroupFooterTemplate
		public string DisplayCount => TotalCount.ToString("N0", IdCulture);
		public string DisplayTotal => TotalAmount.ToString("N0", IdCulture);
	}

	// Baris satu metode pembayaran (dipakai item grup & ringkasan).
	public class MethodRow
	{
		public string MethodName { get; set; }
		public int Count { get; set; }
		public double Amount { get; set; }

		public string DisplayMethod => "   " + MethodName;          // sedikit indent di tabel
		public string DisplayCount => Count.ToString("N0", IdCulture);
		public string DisplayAmount => Amount.ToString("N0", IdCulture);
	}

	// Konteks Header/Footer CollectionView.
	public class ReportSummary
	{
		public string Periode { get; set; }
		public int TransactionCount { get; set; }
		public double GrandTotalAmount { get; set; }
		public string NamaKasir { get; set; }
		public List<MethodRow> Methods { get; set; }

		public string JumlahTransaksi => TransactionCount.ToString("N0", IdCulture);
		public string GrandTotal => GrandTotalAmount.ToString("N0", IdCulture);
	}
}
