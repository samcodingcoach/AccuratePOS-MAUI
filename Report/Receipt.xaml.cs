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
		LblPeriode.Text = $"Per Periode {start:dd/MM/yyyy} - {end:dd/MM/yyyy}";

		// Bersihkan tabel
		ReportGrid.Children.Clear();
		ReportGrid.RowDefinitions.Clear();
		int row = 0;

		// Header tabel
		AddCell(MakeLabel("Metode Pembayaran", bold: true), row, 0);
		AddCell(MakeLabel("Jumlah", bold: true, alignEnd: true), row, 1);
		AddCell(MakeLabel("Total", bold: true, alignEnd: true), row, 2);
		row++;
		AddSeparator(ref row);

		if (receipts == null || receipts.Count == 0)
		{
			AddCell(MakeLabel("Tidak ada data penerimaan pada periode ini.", color: "#999"), row, 0, colSpan: 3);
			row++;
			LblEmptyReport.IsVisible = false;
			ReportBorder.IsVisible = true;
			return;
		}

		// Kelompokkan per tanggal (urut menaik), lalu per metode pembayaran
		var byDate = receipts
			.GroupBy(r => r.transDate)
			.OrderBy(g => ParseTransDate(g.Key));

		foreach (var dateGroup in byDate)
		{
			// Sub-header tanggal
			AddCell(MakeLabel(dateGroup.Key, bold: false, color: "#333"), row, 0, colSpan: 3);
			row++;

			foreach (var methodGroup in dateGroup.GroupBy(r => r.paymentMethodName))
			{
				double sum = methodGroup.Sum(r => r.totalPayment);
				int count = methodGroup.Count();

				AddCell(MakeLabel("   " + methodGroup.Key), row, 0);
				AddCell(MakeLabel(count.ToString(IdCulture), alignEnd: true), row, 1);
				AddCell(MakeLabel(sum.ToString("N0", IdCulture), alignEnd: true), row, 2);
				row++;
			}

			// Total per tanggal
			double dateSum = dateGroup.Sum(r => r.totalPayment);
			int dateCount = dateGroup.Count();
			AddSpacer(ref row, 6);
			AddCell(MakeLabel("Total"), row, 0);
			AddCell(MakeLabel(dateCount.ToString(IdCulture), alignEnd: true), row, 1);
			AddCell(MakeLabel(dateSum.ToString("N0", IdCulture), alignEnd: true), row, 2);
			row++;
			AddSpacer(ref row, 8);
		}

		AddSeparator(ref row);

		// Ringkasan
		AddCell(MakeLabel("Ringkasan", bold: true), row, 0, colSpan: 3);
		row++;

		AddCell(MakeLabel("Jumlah Transaksi"), row, 0, colSpan: 2);
		AddCell(MakeLabel(receipts.Count.ToString(IdCulture), alignEnd: true), row, 2);
		row++;

		foreach (var methodGroup in receipts.GroupBy(r => r.paymentMethodName))
		{
			double sum = methodGroup.Sum(r => r.totalPayment);
			AddCell(MakeLabel(methodGroup.Key), row, 0, colSpan: 2);
			AddCell(MakeLabel(sum.ToString("N0", IdCulture), alignEnd: true), row, 2);
			row++;
		}

		AddSpacer(ref row, 6);

		// Baris Total (diberi latar abu-abu)
		double grandTotal = receipts.Sum(r => r.totalPayment);
		AddRowBackground(row, "#E8E8E8");
		AddCell(MakeLabel("Total", bold: true, alignCenter: true), row, 0, colSpan: 2);
		AddCell(MakeLabel(grandTotal.ToString("N0", IdCulture), bold: true, alignEnd: true), row, 2);
		row++;

		// Baris Nama Kasir
		AddCell(MakeLabel("Nama Kasir", alignCenter: true), row, 0, colSpan: 2);
		AddCell(MakeLabel(namaKasir, bold: true, alignEnd: true), row, 2);
		row++;

		LblEmptyReport.IsVisible = false;
		ReportBorder.IsVisible = true;
	}

	// ---------- Helper builder tabel ----------

	private Label MakeLabel(string text, bool bold = false, bool alignEnd = false,
		bool alignCenter = false, string color = "#222")
	{
		return new Label
		{
			Text = text,
			FontSize = 13,
			FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
			TextColor = Color.FromArgb(color),
			VerticalTextAlignment = TextAlignment.Center,
			HorizontalTextAlignment = alignEnd ? TextAlignment.End
				: alignCenter ? TextAlignment.Center : TextAlignment.Start
		};
	}

	private void AddCell(View view, int row, int col, int colSpan = 1)
	{
		EnsureRow(row);
		Grid.SetRow(view, row);
		Grid.SetColumn(view, col);
		if (colSpan > 1) Grid.SetColumnSpan(view, colSpan);
		ReportGrid.Children.Add(view);
	}

	private void AddRowBackground(int row, string color)
	{
		EnsureRow(row);
		var bg = new BoxView { Color = Color.FromArgb(color) };
		Grid.SetRow(bg, row);
		Grid.SetColumn(bg, 0);
		Grid.SetColumnSpan(bg, 3);
		ReportGrid.Children.Add(bg); // ditambahkan lebih dulu -> berada di belakang
	}

	private void AddSeparator(ref int row)
	{
		EnsureRow(row);
		var line = new BoxView { Color = Color.FromArgb("#333"), HeightRequest = 1 };
		Grid.SetRow(line, row);
		Grid.SetColumn(line, 0);
		Grid.SetColumnSpan(line, 3);
		ReportGrid.Children.Add(line);
		row++;
	}

	private void AddSpacer(ref int row, double height)
	{
		EnsureRow(row);
		var spacer = new BoxView { Color = Colors.Transparent, HeightRequest = height };
		Grid.SetRow(spacer, row);
		Grid.SetColumn(spacer, 0);
		Grid.SetColumnSpan(spacer, 3);
		ReportGrid.Children.Add(spacer);
		row++;
	}

	private void EnsureRow(int row)
	{
		while (ReportGrid.RowDefinitions.Count <= row)
			ReportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
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
}
