using System.IO;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace CafePOS.Services;

/// <summary>
/// Exports cashier daily reports to PDF using PDFsharp.
/// </summary>
public static class PdfReportService
{
    private const string FONT_FAMILY = "Segoe UI";
    private const string TITLE_FONT = "Segoe UI Semibold";
    private const double PAGE_WIDTH = 595;   // A4 width in points (portrait)
    private const double PAGE_HEIGHT = 842;  // A4 height in points
    private const double MARGIN = 40;
    private const double LINE_HEIGHT = 20;
    private static readonly XColor PRIMARY_COLOR = XColor.FromArgb(255, 78, 52, 46);      // #4E342E
    private static readonly XColor ACCENT_COLOR = XColor.FromArgb(255, 166, 123, 91);     // #A67B5B
    private static readonly XColor TEXT_COLOR = XColor.FromArgb(255, 51, 51, 51);          // #333
    private static readonly XColor LIGHT_GRAY_BG = XColor.FromArgb(255, 245, 245, 245);   // #F5F5F5
    private static readonly XColor SEPARATOR_COLOR = XColor.FromArgb(255, 223, 223, 223); // #DFDFDF

    static PdfReportService()
    {
        GlobalFontSettings.FontResolver = new SimpleFontResolver();
    }

    /// <summary>
    /// Exports a daily cashier report as a PDF file.
    /// </summary>
    public static void ExportDailyReport(string savePath, DateTime date, SalesReport report,
        List<TopProductItem>? topProducts = null,
        List<PaymentMethodBreakdownItem>? paymentMethods = null)
    {
        if (GlobalFontSettings.FontResolver == null)
            GlobalFontSettings.FontResolver = new SimpleFontResolver();

        using var doc = new PdfDocument();
        doc.Info.Title = $"تقرير اليوم - {date:yyyy-MM-dd}";
        doc.Info.Author = "CafePOS";
        doc.Info.Creator = "CafePOS System";
        doc.Info.Subject = "تقرير مبيعات اليوم";

        using var gfx = AddPage(doc);
        double y = MARGIN;

        // Header
        y = AddHeader(gfx, "تقرير مبيعات اليوم", y, date);

        // Summary metrics table
        double sectionY = y + 10;
        sectionY = AddMetricRow(gfx, "إجمالي الإيرادات", $"{report.TotalRevenue:F2} ر.ي", sectionY, XColor.FromArgb(255, 46, 125, 50));
        sectionY = AddMetricRow(gfx, "إجمالي الخصومات", $"−{report.TotalDiscounts:F2} ر.ي", sectionY, XColor.FromArgb(255, 230, 81, 0));
        sectionY = AddMetricRow(gfx, "إجمالي المرتجعات", $"−{report.TotalReturns:F2} ر.ي", sectionY, XColor.FromArgb(255, 198, 40, 40));
        sectionY = AddMetricRow(gfx, "إجمالي المشتريات", $"−{report.TotalPurchases:F2} ر.ي", sectionY, XColor.FromArgb(255, 21, 101, 192));
        sectionY = AddMetricRow(gfx, "إجمالي المصروفات", $"−{report.TotalExpenses:F2} ر.ي", sectionY, XColor.FromArgb(255, 245, 127, 23));
        sectionY = AddMetricRow(gfx, "إجمالي المودع (البنك/الإدارة)", $"{report.TotalDeposit:F2} ر.ي", sectionY, XColor.FromArgb(255, 21, 101, 192));
        sectionY = AddMetricRow(gfx, "نثريات المحل", $"{report.TotalPettyCash:F2} ر.ي", sectionY, XColor.FromArgb(255, 46, 125, 50));

        // Highlighted summary
        DrawSeparator(gfx, ref y);
        y += 15;
        y = AddHighlightedMetric(gfx, "صافي التحصيل", $"{report.NetCash:F2} ر.ي", y);
        y = AddHighlightedMetric(gfx, "صافي الربح (بعد خصم المشتريات)", $"{report.NetProfit:F2} ر.ي", y, isGreen: true);
        y += 5;
        AddSimpleText(gfx, $"عدد الفواتير: {report.OrderCount}", y);
        y += LINE_HEIGHT + 10;

        // Payment Method Breakdown
        if (paymentMethods is { Count: > 0 })
        {
            DrawSeparator(gfx, ref y);
            y += 10;
            AddSectionTitle(gfx, "المبيعات حسب طريقة الدفع", y);
            y += 25;

            var headerY = y;
            DrawTableRow(gfx, "طريقة الدفع", "عدد الفواتير", "الإيرادات", headerY, isHeader: true);
            y += LINE_HEIGHT;

            foreach (var item in paymentMethods)
            {
                DrawTableRow(gfx, item.PaymentMethod, item.OrderCount.ToString(), $"{item.TotalRevenue:F2}", y);
                y += LINE_HEIGHT;
                EnsurePageSpace(doc, gfx, ref y);
            }
            y += 10;
        }

        // Top Products
        if (topProducts is { Count: > 0 })
        {
            DrawSeparator(gfx, ref y);
            y += 10;
            AddSectionTitle(gfx, "المنتجات الأكثر مبيعاً", y);
            y += 25;

            var headerY = y;
            DrawTableRow(gfx, "المنتج", "الكمية", "الإيرادات", headerY, isHeader: true);
            y += LINE_HEIGHT;

            foreach (var item in topProducts)
            {
                DrawTableRow(gfx, item.Name, item.TotalQuantity.ToString(), $"{item.TotalRevenue:F2}", y);
                y += LINE_HEIGHT;
                EnsurePageSpace(doc, gfx, ref y);
            }
            y += 10;
        }

        // Footer
        DrawFooter(gfx, date);

        doc.Save(savePath);

        // Open the PDF with the default viewer
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(savePath) { UseShellExecute = true }); }
        catch { }
    }

    private static XGraphics AddPage(PdfDocument doc)
    {
        var page = doc.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        return XGraphics.FromPdfPage(page);
    }

    private static double AddHeader(XGraphics gfx, string title, double startY, DateTime date)
    {
        double y = startY;

        // Title (RTL-aligned)
        var titleFont = new XFont(TITLE_FONT, 22, XFontStyleEx.Bold);
        var titleSize = gfx.MeasureString(title, titleFont);
        gfx.DrawString(title, titleFont, new XSolidBrush(PRIMARY_COLOR),
            new XPoint(PAGE_WIDTH - MARGIN - titleSize.Width, y + 20));
        y += 45;

        // Date and time
        var dateFont = new XFont(FONT_FAMILY, 11, XFontStyleEx.Regular);
        var dateText = $"التاريخ: {date:yyyy-MM-dd}";
        var dateSize = gfx.MeasureString(dateText, dateFont);
        gfx.DrawString(dateText, dateFont, new XSolidBrush(TEXT_COLOR),
            new XPoint(PAGE_WIDTH - MARGIN - dateSize.Width, y));

        var timeFont = new XFont(FONT_FAMILY, 10, XFontStyleEx.Regular);
        var timeText = $"أُصدر: {DateTime.Now:HH:mm}";
        gfx.DrawString(timeText, timeFont, new XSolidBrush(XColor.FromArgb(255, 153, 153, 153)),
            new XPoint(MARGIN, y));

        return y + LINE_HEIGHT + 10;
    }

    private static double AddMetricRow(XGraphics gfx, string label, string value, double y, XColor color)
    {
        // Background
        gfx.DrawRectangle(new XSolidBrush(LIGHT_GRAY_BG),
            new XRect(MARGIN, y, PAGE_WIDTH - MARGIN * 2, LINE_HEIGHT + 6));

        // Label (right-aligned for RTL)
        var labelFont = new XFont(FONT_FAMILY, 12, XFontStyleEx.Regular);
        var labelSize = gfx.MeasureString(label, labelFont);
        gfx.DrawString(label, labelFont, new XSolidBrush(color),
            new XPoint(PAGE_WIDTH - MARGIN - 10 - labelSize.Width, y + LINE_HEIGHT - 2));

        // Value (left-aligned, monospace-like)
        var valueFont = new XFont(FONT_FAMILY, 13, XFontStyleEx.Bold);
        gfx.DrawString(value, valueFont, new XSolidBrush(color),
            new XPoint(MARGIN + 10, y + LINE_HEIGHT - 2));

        return y + LINE_HEIGHT + 8;
    }

    private static double AddHighlightedMetric(XGraphics gfx, string label, string value, double y, bool isGreen = false)
    {
        var color = isGreen ? XColor.FromArgb(255, 46, 125, 50) : PRIMARY_COLOR;

        // Highlighted background
        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(30, color.R, color.G, color.B)),
            new XRect(MARGIN, y, PAGE_WIDTH - MARGIN * 2, LINE_HEIGHT + 14));

        // Label
        var labelFont = new XFont(FONT_FAMILY, 13, XFontStyleEx.Bold);
        var labelSize = gfx.MeasureString(label, labelFont);
        gfx.DrawString(label, labelFont, new XSolidBrush(color),
            new XPoint(PAGE_WIDTH - MARGIN - 10 - labelSize.Width, y + LINE_HEIGHT + 8));

        // Value (larger)
        var valueFont = new XFont(FONT_FAMILY, 18, XFontStyleEx.Bold);
        gfx.DrawString(value, valueFont, new XSolidBrush(color),
            new XPoint(MARGIN + 10, y + LINE_HEIGHT + 11));

        return y + LINE_HEIGHT + 16;
    }

    private static void DrawSeparator(XGraphics gfx, ref double y)
    {
        gfx.DrawLine(new XPen(SEPARATOR_COLOR, 1), MARGIN, y, PAGE_WIDTH - MARGIN, y);
        y += 8;
    }

    private static void AddSectionTitle(XGraphics gfx, string title, double y)
    {
        var font = new XFont(TITLE_FONT, 15, XFontStyleEx.Bold);
        var size = gfx.MeasureString(title, font);
        gfx.DrawString(title, font, new XSolidBrush(PRIMARY_COLOR),
            new XPoint(PAGE_WIDTH - MARGIN - size.Width, y));
    }

    private static void DrawTableRow(XGraphics gfx, string col1, string col2, string col3, double y, bool isHeader = false)
    {
        var fontSize = isHeader ? 12 : 11;
        var bold = isHeader ? XFontStyleEx.Bold : XFontStyleEx.Regular;

        if (isHeader)
        {
            gfx.DrawRectangle(new XSolidBrush(ACCENT_COLOR),
                new XRect(MARGIN, y - 3, PAGE_WIDTH - MARGIN * 2, LINE_HEIGHT + 4));
        }

        var font = new XFont(FONT_FAMILY, fontSize, bold);

        // Column positions for RTL (col1 = rightmost)
        var xCol1 = PAGE_WIDTH - MARGIN - 10 - gfx.MeasureString(col1, font).Width;
        var xCol2 = PAGE_WIDTH - MARGIN - 10 - gfx.MeasureString(col2, font).Width * 3;  // rough center
        var xCol3 = MARGIN + 10;

        var textColor = isHeader ? XColor.FromArgb(255, 255, 255, 255) : TEXT_COLOR;

        gfx.DrawString(col1, font, new XSolidBrush(textColor), new XPoint(xCol1, y));
        gfx.DrawString(col2, font, new XSolidBrush(textColor), new XPoint(Math.Max(xCol1 - 80, 150), y));
        gfx.DrawString(col3, font, new XSolidBrush(textColor), new XPoint(xCol3, y));
    }

    private static void AddSimpleText(XGraphics gfx, string text, double y)
    {
        var font = new XFont(FONT_FAMILY, 11, XFontStyleEx.Regular);
        var size = gfx.MeasureString(text, font);
        gfx.DrawString(text, font, new XSolidBrush(XColor.FromArgb(255, 136, 136, 136)),
            new XPoint(PAGE_WIDTH - MARGIN - size.Width, y));
    }

    private static void EnsurePageSpace(PdfDocument doc, XGraphics gfx, ref double y)
    {
        if (y > PAGE_HEIGHT - MARGIN - 40)
        {
            gfx.Dispose();
            var newGfx = AddPage(doc);
            gfx = newGfx;
            y = MARGIN + 20;
        }
    }

    private static void DrawFooter(XGraphics gfx, DateTime date)
    {
        var font = new XFont(FONT_FAMILY, 8, XFontStyleEx.Regular);
        var text = $"صفحة 1 — تم الإنشاء بواسطة CafePOS في {date:yyyy-MM-dd HH:mm}";
        var size = gfx.MeasureString(text, font);
        gfx.DrawString(text, font, new XSolidBrush(XColor.FromArgb(255, 153, 153, 153)),
            new XPoint(PAGE_WIDTH / 2 - size.Width / 2, PAGE_HEIGHT - 30));
    }
}

/// <summary>
/// Simple font resolver for PDFsharp that uses Segoe UI (has Arabic support).
/// </summary>
public class SimpleFontResolver : IFontResolver
{
    public byte[]? GetFont(string faceName)
    {
        var cleanName = faceName
            .Replace(" Segoe UI", "").Replace("Segoe UI#", "").Replace("#b", "")
            .Replace("-italic", "").Replace("-bold", "");

        var fontPath = faceName.Contains("bold", StringComparison.OrdinalIgnoreCase)
            || faceName.Contains("b#", StringComparison.OrdinalIgnoreCase)
            ? ResolveFontPath("segoeuib.ttf") : ResolveFontPath("segoeui.ttf");

        if (fontPath != null && File.Exists(fontPath))
            return File.ReadAllBytes(fontPath);

        // Fallback to Arial (common on Windows)
        fontPath = ResolveFontPath("arial.ttf");
        return fontPath != null && File.Exists(fontPath) ? File.ReadAllBytes(fontPath) : null;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var suffix = isBold ? "b#" : "";
        return new FontResolverInfo($"{familyName}{suffix}");
    }

    private static string? ResolveFontPath(string fileName)
    {
        var fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var fullPath = Path.Combine(fontsDir, fileName);
        if (File.Exists(fullPath)) return fullPath;

        // Linux fonts
        var userFonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "microsoft", "windows", "fonts");
        fullPath = Path.Combine(userFonts, fileName);
        return File.Exists(fullPath) ? fullPath : null;
    }
}
