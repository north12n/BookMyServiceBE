using BookMyService.Models;
using BookMyServiceBE.Repository.IRepository;
using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BookMyServiceBE.Repository
{
    public class ReceiptPdfService : IReceiptPdfService
    {
        public byte[] CreateBookingReceiptPdf(Booking b)
        {
            var th = new CultureInfo("th-TH");
            var issueAt = DateTime.UtcNow;
            var amount = b.FinalPrice ?? b.EstimatedPrice;

            // ชำระแล้วถ้าสถานะอย่างน้อย Paid/Assigned/InProgress/Completed
            var paidStatuses = new[]
            {
                BookingStatus.Paid,
                BookingStatus.Assigned,
                BookingStatus.InProgress,
                BookingStatus.Completed
            };
            var isPaid = paidStatuses.Contains(b.Status);

            var customerName = b.Customer?.FullName ?? $"UID:{b.UserId}";
            var providerName = b.ProviderService?.Provider?.FullName ?? "-";
            var serviceTitle = b.ProviderService?.Title ?? "-";
            var categoryName = b.ProviderService?.ServiceCategory?.Name ?? "-";

            var scheduledText = b.RequestedStartAt.ToString("dd MMM yyyy HH:mm", th) + " น.";

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(12));
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Text(text =>
                        {
                            text.Span("ใบเสร็จรับเงิน / Receipt").SemiBold().FontSize(18);
                            text.Line($"\nรหัสการจอง: {b.BookingCode}");
                            text.Line($"วันที่ออกใบเสร็จ: {issueAt.ToString("dd MMM yyyy HH:mm", th)} น.");
                        });
                        row.ConstantItem(120).AlignRight().Image(Placeholders.Image(120, 40));
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(8);

                        col.Item().BorderBottom(1).PaddingBottom(6);

                        col.Item().Text("ข้อมูลผู้เกี่ยวข้อง").SemiBold();
                        col.Item().Grid(grid =>
                        {
                            grid.Columns(2);
                            grid.Spacing(6);
                            grid.Item().Text($"ลูกค้า: {customerName}");
                            grid.Item().Text($"ผู้ให้บริการ: {providerName}");
                            grid.Item().Text($"หมวดบริการ: {categoryName}");
                            grid.Item().Text($"นัดหมาย: {scheduledText}");
                        });

                        col.Item().PaddingTop(10).Text("รายละเอียดรายการ").SemiBold();
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3); // รายการ
                                c.RelativeColumn(1); // ราคา
                            });

                            t.Header(h =>
                            {
                                h.Cell().Element(CellHeader).Text("รายการ");
                                h.Cell().Element(CellHeader).AlignRight().Text("เป็นเงิน (บาท)");
                            });

                            t.Cell().Element(CellBody).Text(serviceTitle);
                            t.Cell().Element(CellBody).AlignRight().Text($"{amount:N2}");

                            t.Footer(f =>
                            {
                                f.Cell().Element(CellFooter).Text("รวมทั้งสิ้น").SemiBold();
                                f.Cell().Element(CellFooter).AlignRight().Text($"{amount:N2}").SemiBold();
                            });

                            static IContainer CellHeader(IContainer x) =>
                                x.DefaultTextStyle(s => s.SemiBold()).PaddingVertical(6).BorderBottom(1);

                            static IContainer CellBody(IContainer x) => x.PaddingVertical(6);

                            static IContainer CellFooter(IContainer x) =>
                                x.BorderTop(1).PaddingTop(6);
                        });

                        col.Item().PaddingTop(10).Row(r =>
                        {
                            r.RelativeItem().Text(txt =>
                            {
                                txt.Span("สถานะชำระเงิน: ").SemiBold();
                                txt.Span(isPaid ? "ชำระแล้ว" : "ค้างชำระ")
                                   .SemiBold()
                                   .FontColor(isPaid ? Colors.Green.Darken2 : Colors.Red.Medium);
                            });
                        });

                        col.Item().PaddingTop(30).Text("ขอบคุณที่ใช้บริการ").AlignCenter();
                    });

                    page.Footer().AlignRight().Text(x =>
                    {
                        x.Span("หน้า ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });

            return pdf.GeneratePdf();
        }
    }
}
