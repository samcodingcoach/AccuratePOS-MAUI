# Generate PDF di .NET MAUI

**User:** Anonymous  
**Created:** 6/17/2026 11:47:48  
**Updated:** 6/17/2026 12:00:24  
**Exported:** 6/17/2026 12:05:56  
**Link:** [https://chatgpt.com/c/6a3218bc-9448-83ec-a599-d43e62cfc4d4](https://chatgpt.com/c/6a3218bc-9448-83ec-a599-d43e62cfc4d4)  

## Prompt:
6/17/2026, 11:47:45 AM

saya membuat aplikasi di net maui 10. bagaimana menggenerate pdf

## Response:
6/17/2026, 11:47:48 AM

Di .NET MAUI 10, cara paling umum untuk membuat PDF adalah menggunakan library seperti **QuestPDF** atau **PDFSharp**. Untuk aplikasi bisnis (invoice, struk, laporan), saya lebih menyarankan **QuestPDF** karena sintaksnya modern dan mudah.

## 1. Install QuestPDF

Di terminal project:

```bash
dotnet add package QuestPDF
```

Atau melalui NuGet Package Manager:

```bash
Install-Package QuestPDF
```

---

## 2. Contoh Generate PDF Sederhana

Buat class:

```csharp
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

public static class PdfGenerator
{
    public static byte[] GenerateInvoice()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);

                page.Header()
                    .Text("INVOICE")
                    .FontSize(24)
                    .Bold();

                page.Content()
                    .Column(col =>
                    {
                        col.Item().Text("Nama : Samsu Bahri");
                        col.Item().Text("Tanggal : " + DateTime.Now.ToString("dd/MM/yyyy"));
                        col.Item().Text("Total : Rp 150.000");
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Halaman ");
                        x.CurrentPageNumber();
                    });
            });
        }).GeneratePdf();
    }
}
```

---

## 3. Simpan PDF ke Storage

Misalnya saat tombol diklik:

```csharp
private async void BtnPdf_Clicked(object sender, EventArgs e)
{
    var pdfBytes = PdfGenerator.GenerateInvoice();

    string filePath = Path.Combine(
        FileSystem.AppDataDirectory,
        "invoice.pdf");

    File.WriteAllBytes(filePath, pdfBytes);

    await DisplayAlert(
        "Sukses",
        $"PDF tersimpan:\n{filePath}",
        "OK");
}
```

---

## 4. Membuka PDF Setelah Dibuat

```csharp
await Launcher.OpenAsync(new OpenFileRequest
{
    File = new ReadOnlyFile(filePath)
});
```

---

## 5. Membuat Tabel Invoice

Contoh:

```csharp
page.Content().Table(table =>
{
    table.ColumnsDefinition(columns =>
    {
        columns.RelativeColumn(3);
        columns.RelativeColumn();
        columns.RelativeColumn();
    });

    table.Header(header =>
    {
        header.Cell().Text("Barang").Bold();
        header.Cell().Text("Qty").Bold();
        header.Cell().Text("Harga").Bold();
    });

    table.Cell().Text("Beras 5 Kg");
    table.Cell().Text("2");
    table.Cell().Text("150.000");

    table.Cell().Text("Minyak Goreng");
    table.Cell().Text("1");
    table.Cell().Text("25.000");
});
```

---

## 6. Menambahkan Logo dari Resources

Misalnya file logo disimpan di:

```
Resources\Images\logo.png
```

```csharp
var logoPath = Path.Combine(FileSystem.AppDataDirectory, "logo.png");
```

Atau load sebagai stream:

```csharp
using var stream = await FileSystem.OpenAppPackageFileAsync("logo.png");

page.Header()
    .Image(stream);
```

---

## Cocok untuk kasus Anda

Karena sebelumnya Anda pernah membuat aplikasi **kasir, invoice, QR attendance sekolah, dan WhatsApp invoice**, biasanya PDF yang dibutuhkan berupa:

- Invoice penjualan
- Struk transaksi
- Laporan harian
- Laporan stok
- Bukti pembayaran

QuestPDF sangat cocok untuk kebutuhan tersebut dan bisa menghasilkan PDF yang rapi tanpa bergantung pada Android atau Windows tertentu.

Jika Anda ingin, saya bisa membuatkan **template invoice profesional ukuran A4 lengkap dengan logo toko, tabel detail barang, subtotal, diskon, dan QR Code** yang siap dipakai di .NET MAUI 10.



---
Powered by [ChatGPT Exporter](https://www.chatgptexporter.com)