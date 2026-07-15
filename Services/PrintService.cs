using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Printing;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using CafePOS.Models;
using CafePOS.Services;

namespace CafePOS.Services;

/// <summary>
/// Raw printer helper — sends raw bytes directly to a Windows printer
/// using Win32 API (winspool.drv). Required for ESC/POS thermal printers.
/// </summary>
public static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string? pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string? pDataType;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out nint hPrinter, nint pd);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
    private static extern bool ClosePrinter(nint hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool StartDocPrinter(nint hPrinter, int level, [In] DOCINFOA di);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
    private static extern bool EndDocPrinter(nint hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
    private static extern bool StartPagePrinter(nint hPrinter);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
    private static extern bool EndPagePrinter(nint hPrinter);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
    private static extern bool WritePrinter(nint hPrinter, nint pBytes, int dwCount, out int dwWritten);

    /// <summary>
    /// Sends raw bytes to a named printer.
    /// </summary>
    public static bool SendBytesToPrinter(string printerName, byte[] data)
    {
        var di = new DOCINFOA { pDocName = "CafePOS Receipt", pDataType = "RAW" };
        bool success = false;

        if (OpenPrinter(printerName.Normalize(), out var hPrinter, nint.Zero))
        {
            if (StartDocPrinter(hPrinter, 1, di))
            {
                if (StartPagePrinter(hPrinter))
                {
                    var pUnmanagedBytes = Marshal.AllocCoTaskMem(data.Length);
                    Marshal.Copy(data, 0, pUnmanagedBytes, data.Length);
                    success = WritePrinter(hPrinter, pUnmanagedBytes, data.Length, out _);
                    Marshal.FreeCoTaskMem(pUnmanagedBytes);
                    EndPagePrinter(hPrinter);
                }
                EndDocPrinter(hPrinter);
            }
            ClosePrinter(hPrinter);
        }

        return success;
    }
}

/// <summary>
/// Builds and prints ESC/POS receipts for 80mm thermal printers.
/// Supports Arabic text (codepage 22 — Windows-1256).
/// </summary>
public static class PrintService
{
    // ESC/POS command constants
    private static readonly byte[] ESC_INIT = [0x1B, 0x40];                  // Initialize printer
    private static readonly byte[] ESC_CENTER = [0x1B, 0x61, 0x01];          // Center align
    private static readonly byte[] ESC_LEFT = [0x1B, 0x61, 0x00];            // Left align
    private static readonly byte[] ESC_RIGHT = [0x1B, 0x61, 0x02];           // Right align
    private static readonly byte[] ESC_BOLD_ON = [0x1B, 0x45, 0x01];         // Bold on
    private static readonly byte[] ESC_BOLD_OFF = [0x1B, 0x45, 0x00];        // Bold off
    private static readonly byte[] ESC_DOUBLE_ON = [0x1D, 0x21, 0x11];       // Double width+height
    private static readonly byte[] ESC_DOUBLE_OFF = [0x1D, 0x21, 0x00];      // Normal size
    private static readonly byte[] ESC_WIDE_ON = [0x1D, 0x21, 0x10];         // Double width only
    private static readonly byte[] ESC_WIDE_OFF = [0x1D, 0x21, 0x00];        // Normal
    private static readonly byte[] ESC_CUT = [0x1D, 0x56, 0x41, 0x03];       // Partial cut
    private static readonly byte[] ESC_FEED2 = [0x1B, 0x64, 0x02];           // Feed 2 lines
    private static readonly byte[] ESC_FEED3 = [0x1B, 0x64, 0x03];           // Feed 3 lines
    private static readonly byte[] ESC_FEED5 = [0x1B, 0x64, 0x05];           // Feed 5 lines
    private static readonly byte[] ESC_CODEPAGE = [0x1B, 0x74, 0x16];        // Codepage 22 = Windows-1256 (Arabic)
    private static readonly byte[] LF = [(byte)'\n'];

    private static Encoding GetArabicEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1256); // Windows-1256 (Arabic)
    }

    /// <summary>
    /// Prints the cafe logo from Data/logo.png as a raster bit image (GS v 0).
    /// Converts the image to 1bpp monochrome and scales it for thermal receipt width.
    /// </summary>
    private static void PrintLogo(MemoryStream ms)
    {
        var logoPath = CafePOS.Helpers.AppPaths.LogoPath;
        if (!File.Exists(logoPath)) return;

        try
        {
            using var bmp = new Bitmap(logoPath);

            const int targetWidth = 384;
            const int maxHeight = 120;

            double scale = Math.Min((double)targetWidth / bmp.Width, (double)maxHeight / bmp.Height);
            int w = Math.Max(1, (int)(bmp.Width * scale));
            int h = Math.Max(1, (int)(bmp.Height * scale));
            w = (w + 7) & ~7; // must be multiple of 8 for GS v 0

            using var resized = new Bitmap(w, h);
            using (var g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(bmp, 0, 0, w, h);
            }

            int widthBytes = w / 8;
            byte[] imageData = new byte[widthBytes * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var px = resized.GetPixel(x, y);
                    int gray = (px.R * 77 + px.G * 150 + px.B * 29) >> 8;

                    if (gray < 128)
                    {
                        int bi = y * widthBytes + (x >> 3);
                        imageData[bi] |= (byte)(0x80 >> (x & 7));
                    }
                }
            }

            // GS v 0 — Print raster bit image
            ms.Write([
                0x1D, 0x76, 0x30, 48,
                (byte)(widthBytes & 0xFF),
                (byte)((widthBytes >> 8) & 0xFF),
                (byte)(h & 0xFF),
                (byte)((h >> 8) & 0xFF)
            ]);
            ms.Write(imageData);
            ms.Write([0x0A]);
        }
        catch
        {
            // Silently skip if logo can't be loaded
        }
    }

    private static string ResolvePrinterName(string? configuredName = null)
    {
        if (string.IsNullOrWhiteSpace(configuredName))
            configuredName = SettingsService.GetSetting("printer_name");

        if (!string.IsNullOrWhiteSpace(configuredName))
        {
            try
            {
                var server = new LocalPrintServer();
                var match = server.GetPrintQueues()
                    .FirstOrDefault(q =>
                        q.FullName.Equals(configuredName, StringComparison.OrdinalIgnoreCase) ||
                        q.Name.Equals(configuredName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    return match.FullName;
            }
            catch { }
        }

        // Fallback: use any available printer
        try
        {
            var server = new LocalPrintServer();
            var anyPrinter = server.GetPrintQueues().FirstOrDefault(q => !q.IsOffline);
            if (anyPrinter != null)
                return anyPrinter.FullName;
        }
        catch { }

        return configuredName ?? "POS-80";
    }

    /// <summary>
    /// Prints an order receipt with a separate mini receipt attached.
    /// Tries raster (image) mode first for Arabic RTL; falls back to text mode on failure.
    /// </summary>
    public static bool PrintReceipt(Order order)
    {
        var printerName = ResolvePrinterName();
        if (string.IsNullOrWhiteSpace(printerName))
            printerName = "POS-80";

        bool useRaster = SettingsService.GetSetting("raster_print") == "1" || string.IsNullOrWhiteSpace(SettingsService.GetSetting("raster_print"));
        bool compact = SettingsService.GetSetting("compact_receipt") == "1";

        // Verify printer is reachable; if not, try to find any available printer
        bool printerReachable = false;
        try { printerReachable = RawPrinterHelper.SendBytesToPrinter(printerName, []); }
        catch { }

        if (!printerReachable)
        {
            try
            {
                var fallback = new LocalPrintServer().GetPrintQueues()
                    .FirstOrDefault(q => !q.IsOffline && !string.IsNullOrWhiteSpace(q.FullName));
                if (fallback != null)
                    printerName = fallback.FullName;
            }
            catch { }
        }

        bool TrySend(byte[] data)
        {
            if (data.Length == 0) return false;
            try { return RawPrinterHelper.SendBytesToPrinter(printerName, data); }
            catch { return false; }
        }

        bool success = false;

        if (useRaster)
        {
            try
            {
                var mainData = BuildReceiptRasterData(order);
                success = TrySend(mainData);

                if (success && !compact)
                {
                    var miniData = BuildMiniReceiptRasterData(order);
                    TrySend(miniData);
                }
            }
            catch
            {
                success = false;
            }
        }

        if (!success)
        {
            var mainData = BuildReceiptData(order);
            success = TrySend(mainData);

            if (!compact)
            {
                var miniData = BuildMiniReceiptData(order);
                TrySend(miniData);
            }
        }

        return success;
    }

    /// <summary>
    /// Prints a simple test page to the specified printer using the raster pipeline.
    /// </summary>
    public static bool TestPrinter(string printerName)
    {
        var lines = new List<(string Text, int FontSize, bool Bold, StringFormat Align, bool HasBorder)>
        {
            ("** اختبار الطباعة **", HEADER_FONT_SIZE, true, CENTER_FORMAT, false),
            ("", FONT_SIZE, false, CENTER_FORMAT, false),
            ("إذا رأيت هذه الرسالة", FONT_SIZE, false, CENTER_FORMAT, false),
            ("فالطابعة تعمل بشكل صحيح ✓", FONT_SIZE, true, CENTER_FORMAT, false),
            ("", FONT_SIZE, false, CENTER_FORMAT, false),
            (new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false),
            (DateTime.Now.ToString("yyyy-MM-dd HH:mm"), FONT_SIZE, false, CENTER_FORMAT, false),
        };

        try
        {
            var data = RasterRender(lines);
            return RawPrinterHelper.SendBytesToPrinter(printerName, data);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Prints a return receipt. Uses raster mode for correct Arabic rendering.
    /// </summary>
    public static bool PrintReturnReceipt(Return ret)
    {
        var printerName = ResolvePrinterName();
        if (string.IsNullOrWhiteSpace(printerName))
            printerName = "POS-80";

        bool useRaster = SettingsService.GetSetting("raster_print") == "1" || string.IsNullOrWhiteSpace(SettingsService.GetSetting("raster_print"));

        if (useRaster)
        {
            var data = BuildReturnReceiptRasterData(ret);
            return RawPrinterHelper.SendBytesToPrinter(printerName, data);
        }

        var dataText = BuildReturnReceiptData(ret);
        return RawPrinterHelper.SendBytesToPrinter(printerName, dataText);
    }

    /// <summary>
    /// Prints a purchase invoice. Uses raster mode for correct Arabic rendering.
    /// </summary>
    public static bool PrintPurchaseReceipt(Purchase purchase)
    {
        var printerName = ResolvePrinterName();
        if (string.IsNullOrWhiteSpace(printerName))
            printerName = "POS-80";

        bool useRaster = SettingsService.GetSetting("raster_print") == "1" || string.IsNullOrWhiteSpace(SettingsService.GetSetting("raster_print"));

        if (useRaster)
        {
            var data = BuildPurchaseReceiptRasterData(purchase);
            return RawPrinterHelper.SendBytesToPrinter(printerName, data);
        }

        var dataText = BuildPurchaseReceiptData(purchase);
        return RawPrinterHelper.SendBytesToPrinter(printerName, dataText);
    }

    private static byte[] BuildReceiptData(Order order)
    {
        var enc = GetArabicEncoding();
        using var ms = new MemoryStream();

        // Initialize
        ms.Write(ESC_INIT);
        ms.Write(ESC_CODEPAGE);

        // Logo (if configured)
        PrintLogo(ms);

        // --- Header ---
        ms.Write(ESC_CENTER);

        // Cafe name (big)
        var cafeName = SettingsService.GetSetting("cafe_name");
        if (!string.IsNullOrWhiteSpace(cafeName))
        {
            ms.Write(ESC_DOUBLE_ON);
            ms.Write(enc.GetBytes(cafeName));
            ms.Write(LF);
            ms.Write(ESC_DOUBLE_OFF);
        }

        // Phone
        var phone = SettingsService.GetSetting("phone");
        if (!string.IsNullOrWhiteSpace(phone))
        {
            ms.Write(enc.GetBytes(phone));
            ms.Write(LF);
        }

        WriteDashes(ms, enc);

        // --- Invoice Info ---
        ms.Write(ESC_RIGHT);

        ms.Write(ESC_BOLD_ON);
        ms.Write(enc.GetBytes($"فاتورة رقم: {order.InvoiceNumber}"));
        ms.Write(LF);
        ms.Write(ESC_BOLD_OFF);

        ms.Write(ESC_WIDE_ON);
        ms.Write(enc.GetBytes($"رقم الطلب: {order.OrderNumber}"));
        ms.Write(LF);
        ms.Write(ESC_WIDE_OFF);

        ms.Write(enc.GetBytes($"التاريخ: {order.CreatedAt:yyyy-MM-dd HH:mm}"));
        ms.Write(LF);

        if (!string.IsNullOrWhiteSpace(order.CustomerName))
        {
            ms.Write(enc.GetBytes($"العميل: {order.CustomerName}"));
            ms.Write(LF);
        }

        ms.Write(enc.GetBytes($"الكاشير: {order.CashierName ?? AuthService.CurrentUser?.Username ?? ""}"));
        ms.Write(LF);

        if (!string.IsNullOrWhiteSpace(order.PaymentMethod))
        {
            ms.Write(enc.GetBytes($"طريقة الدفع: {order.PaymentMethod}"));
            ms.Write(LF);
        }

        WriteDashes(ms, enc);

        // --- Items Header ---
        ms.Write(ESC_BOLD_ON);
        var header = FormatLine("الصنف", "الكمية", "السعر", "المجموع");
        ms.Write(enc.GetBytes(header));
        ms.Write(LF);
        ms.Write(ESC_BOLD_OFF);

        WriteDashes(ms, enc);

        // --- Items ---
        foreach (var item in order.Items)
        {
            var line = FormatLine(
                item.ProductName,
                item.Quantity.ToString(),
                item.Price.ToString("F2"),
                item.Subtotal.ToString("F2")
            );
            ms.Write(enc.GetBytes(line));
            ms.Write(LF);
        }

        WriteDashes(ms, enc);

        // --- Totals ---
        ms.Write(ESC_RIGHT);

        ms.Write(enc.GetBytes(FormatTotalLine("المجموع الفرعي:", order.Subtotal.ToString("F2"))));
        ms.Write(LF);

        if (order.DiscountPercent > 0)
        {
            ms.Write(enc.GetBytes(FormatTotalLine($"خصم ({order.DiscountPercent}%):", $"-{order.DiscountAmount:F2}")));
            ms.Write(LF);
        }

        WriteDashes(ms, enc);

        ms.Write(ESC_DOUBLE_ON);
        ms.Write(enc.GetBytes(FormatTotalLine("الإجمالي:", order.Total.ToString("F2"))));
        ms.Write(LF);
        ms.Write(ESC_DOUBLE_OFF);

        WriteDashes(ms, enc);

        // --- Footer ---
        ms.Write(ESC_CENTER);
        var footer = SettingsService.GetSetting("footer");
        if (!string.IsNullOrWhiteSpace(footer))
        {
            ms.Write(enc.GetBytes(footer));
            ms.Write(LF);
        }

        // Feed and cut
        ms.Write(ESC_FEED2);
        ms.Write(ESC_CUT);

        return ms.ToArray();
    }

    private static byte[] BuildMiniReceiptData(Order order)
    {
        var enc = GetArabicEncoding();
        using var ms = new MemoryStream();

        ms.Write(ESC_INIT);
        ms.Write(ESC_CODEPAGE);

        // Feed a bit to push paper out
        ms.Write(ESC_FEED3);

        // Header
        ms.Write(ESC_CENTER);
        ms.Write(ESC_DOUBLE_ON);
        ms.Write(enc.GetBytes("** فاتورة مصغرة **"));
        ms.Write(LF);
        ms.Write(ESC_DOUBLE_OFF);

        ms.Write(enc.GetBytes(new string('-', LINE_WIDTH)));
        ms.Write(LF);

        ms.Write(ESC_RIGHT);

        ms.Write(enc.GetBytes($"رقم الطلب: {order.OrderNumber}"));
        ms.Write(LF);
        ms.Write(enc.GetBytes($"فاتورة رقم: {order.InvoiceNumber}"));
        ms.Write(LF);
        ms.Write(enc.GetBytes($"التاريخ: {order.CreatedAt:yyyy-MM-dd HH:mm}"));
        ms.Write(LF);

        if (!string.IsNullOrWhiteSpace(order.CustomerName))
        {
            ms.Write(ESC_BOLD_ON);
            ms.Write(enc.GetBytes($"العميل: {order.CustomerName}"));
            ms.Write(LF);
            ms.Write(ESC_BOLD_OFF);
        }

        if (!string.IsNullOrWhiteSpace(order.PaymentMethod))
        {
            ms.Write(enc.GetBytes($"طريقة الدفع: {order.PaymentMethod}"));
            ms.Write(LF);
        }

        ms.Write(enc.GetBytes(new string('-', LINE_WIDTH)));
        ms.Write(LF);

        // Items (compact)
        foreach (var item in order.Items)
        {
            ms.Write(enc.GetBytes($"{Truncate(item.ProductName, 20)}  x{item.Quantity}  {item.Subtotal:F2}"));
            ms.Write(LF);
        }

        ms.Write(enc.GetBytes(new string('-', LINE_WIDTH)));
        ms.Write(LF);

        // Total
        ms.Write(ESC_BOLD_ON);
        ms.Write(enc.GetBytes(FormatTotalLine("الإجمالي:", order.Total.ToString("F2"))));
        ms.Write(LF);
        ms.Write(ESC_BOLD_OFF);

        // Cafe name in footer
        ms.Write(ESC_CENTER);
        var cafeName = SettingsService.GetSetting("cafe_name");
        if (!string.IsNullOrWhiteSpace(cafeName))
        {
            ms.Write(enc.GetBytes(cafeName));
            ms.Write(LF);
        }

        ms.Write(enc.GetBytes(new string('=', LINE_WIDTH)));
        ms.Write(LF);

        ms.Write(ESC_FEED2);
        ms.Write(ESC_CUT);

        return ms.ToArray();
    }

    private static byte[] BuildReturnReceiptData(Return ret)
    {
        var enc = GetArabicEncoding();
        using var ms = new MemoryStream();

        ms.Write(ESC_INIT);
        ms.Write(ESC_CODEPAGE);

        // Header
        ms.Write(ESC_CENTER);
        ms.Write(ESC_DOUBLE_ON);
        ms.Write(enc.GetBytes("** إيصال مرتجع **"));
        ms.Write(LF);
        ms.Write(ESC_DOUBLE_OFF);

        WriteDashes(ms, enc);

        ms.Write(ESC_RIGHT);
        ms.Write(enc.GetBytes($"فاتورة أصلية: {ret.InvoiceNumber}"));
        ms.Write(LF);
        ms.Write(enc.GetBytes($"السبب: {ret.Reason}"));
        ms.Write(LF);
        ms.Write(enc.GetBytes($"التاريخ: {ret.CreatedAt:yyyy-MM-dd HH:mm}"));
        ms.Write(LF);

        WriteDashes(ms, enc);

        // Items
        foreach (var item in ret.Items)
        {
            ms.Write(enc.GetBytes($"{item.ProductName} x{item.Quantity} = {item.Subtotal:F2}"));
            ms.Write(LF);
        }

        WriteDashes(ms, enc);

        ms.Write(ESC_DOUBLE_ON);
        ms.Write(enc.GetBytes(FormatTotalLine("المبلغ المسترد:", ret.TotalRefund.ToString("F2"))));
        ms.Write(LF);
        ms.Write(ESC_DOUBLE_OFF);

        ms.Write(ESC_FEED2);
        ms.Write(ESC_CUT);

        return ms.ToArray();
    }

    private static byte[] BuildPurchaseReceiptData(Purchase purchase)
    {
        var enc = GetArabicEncoding();
        using var ms = new MemoryStream();

        ms.Write(ESC_INIT);
        ms.Write(ESC_CODEPAGE);

        // Header
        ms.Write(ESC_CENTER);

        var cafeName = SettingsService.GetSetting("cafe_name");
        if (!string.IsNullOrWhiteSpace(cafeName))
        {
            ms.Write(ESC_DOUBLE_ON);
            ms.Write(enc.GetBytes(cafeName));
            ms.Write(LF);
            ms.Write(ESC_DOUBLE_OFF);
        }

        var phone = SettingsService.GetSetting("phone");
        if (!string.IsNullOrWhiteSpace(phone))
        {
            ms.Write(enc.GetBytes(phone));
            ms.Write(LF);
        }

        WriteDashes(ms, enc);

        ms.Write(ESC_BOLD_ON);
        ms.Write(enc.GetBytes("** فاتورة مشتريات **"));
        ms.Write(LF);
        ms.Write(ESC_BOLD_OFF);

        WriteDashes(ms, enc);

        // Invoice Info
        ms.Write(ESC_RIGHT);

        ms.Write(ESC_BOLD_ON);
        ms.Write(enc.GetBytes($"فاتورة رقم: {purchase.InvoiceNumber}"));
        ms.Write(LF);
        ms.Write(ESC_BOLD_OFF);

        if (!string.IsNullOrWhiteSpace(purchase.SupplierName))
        {
            ms.Write(enc.GetBytes($"المورد: {purchase.SupplierName}"));
            ms.Write(LF);
        }

        ms.Write(enc.GetBytes($"التاريخ: {purchase.CreatedAt:yyyy-MM-dd HH:mm}"));
        ms.Write(LF);

        ms.Write(enc.GetBytes($"المستخدم: {purchase.CreatorName ?? ""}"));
        ms.Write(LF);

        if (!string.IsNullOrWhiteSpace(purchase.Notes))
        {
            ms.Write(enc.GetBytes($"ملاحظات: {purchase.Notes}"));
            ms.Write(LF);
        }

        WriteDashes(ms, enc);

        // Items Header
        ms.Write(ESC_BOLD_ON);
        var header = FormatLine("الصنف", "الكمية", "التكلفة", "المجموع");
        ms.Write(enc.GetBytes(header));
        ms.Write(LF);
        ms.Write(ESC_BOLD_OFF);

        WriteDashes(ms, enc);

        // Items
        foreach (var item in purchase.Items)
        {
            var line = FormatLine(
                item.ProductName,
                item.Quantity.ToString(),
                item.CostPrice.ToString("F2"),
                item.Subtotal.ToString("F2")
            );
            ms.Write(enc.GetBytes(line));
            ms.Write(LF);
        }

        WriteDashes(ms, enc);

        // Total
        ms.Write(ESC_RIGHT);
        ms.Write(ESC_DOUBLE_ON);
        ms.Write(enc.GetBytes(FormatTotalLine("الإجمالي:", purchase.Total.ToString("F2"))));
        ms.Write(LF);
        ms.Write(ESC_DOUBLE_OFF);

        WriteDashes(ms, enc);

        // Footer
        ms.Write(ESC_CENTER);
        var footer = SettingsService.GetSetting("footer");
        if (!string.IsNullOrWhiteSpace(footer))
        {
            ms.Write(enc.GetBytes(footer));
            ms.Write(LF);
        }

        ms.Write(ESC_FEED2);
        ms.Write(ESC_CUT);

        return ms.ToArray();
    }

    // ============================================================
    // Raster Receipt Rendering (for correct Arabic RTL printing)
    // Renders the receipt as a monochrome bitmap sent via GS v 0.
    // ============================================================

    private const int RASTER_WIDTH = 576;
    private const int FONT_SIZE = 14;
    private const int LINE_HEIGHT = 26;
    private const int HEADER_LINE_HEIGHT = 40;
    private const int HEADER_FONT_SIZE = 22;
    private static readonly FontFamily RASTER_FONT = new("Segoe UI");

    private static int GetLineHeight(int fontSize) => fontSize >= 16 ? HEADER_LINE_HEIGHT : LINE_HEIGHT;
    private static readonly StringFormat RTL_FORMAT = new()
    {
        FormatFlags = StringFormatFlags.DirectionRightToLeft,
        Alignment = StringAlignment.Near,
        LineAlignment = StringAlignment.Center
    };
    private static readonly StringFormat CENTER_FORMAT = new()
    {
        FormatFlags = StringFormatFlags.DirectionRightToLeft,
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center
    };
    private static readonly StringFormat LEFT_FORMAT = new()
    {
        Alignment = StringAlignment.Near,
        LineAlignment = StringAlignment.Center
    };
    private static readonly StringFormat RIGHT_FORMAT = new()
    {
        FormatFlags = StringFormatFlags.DirectionRightToLeft,
        Alignment = StringAlignment.Far,
        LineAlignment = StringAlignment.Center
    };

    private static byte[] BuildReceiptRasterData(Order order)
    {
        var lines = new List<(string Text, int FontSize, bool Bold, StringFormat Align, bool HasBorder)>();
        var cafeName = SettingsService.GetSetting("cafe_name");
        if (!string.IsNullOrWhiteSpace(cafeName))
            lines.Add((cafeName, HEADER_FONT_SIZE, true, CENTER_FORMAT, false));

        var phone = SettingsService.GetSetting("phone");
        if (!string.IsNullOrWhiteSpace(phone))
            lines.Add((phone, FONT_SIZE, false, CENTER_FORMAT, false));

        lines.Add((new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));

        lines.Add(($"فاتورة رقم: {order.InvoiceNumber}", FONT_SIZE, true, RTL_FORMAT, false));
        lines.Add(($"رقم الطلب: {order.OrderNumber}", FONT_SIZE, false, RTL_FORMAT, false));
        lines.Add(($"التاريخ: {order.CreatedAt:yyyy-MM-dd HH:mm}", FONT_SIZE, false, RTL_FORMAT, false));

        if (!string.IsNullOrWhiteSpace(order.CustomerName))
            lines.Add(($"العميل: {order.CustomerName}", FONT_SIZE, false, RTL_FORMAT, false));

        lines.Add(($"الكاشير: {order.CashierName ?? AuthService.CurrentUser?.Username ?? ""}", FONT_SIZE, false, RTL_FORMAT, false));

        if (!string.IsNullOrWhiteSpace(order.PaymentMethod))
            lines.Add(($"طريقة الدفع: {order.PaymentMethod}", FONT_SIZE, false, RTL_FORMAT, false));

        lines.Add((new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));

        // Column header
        var header = FormatLine("الصنف", "الكمية", "السعر", "المجموع");
        lines.Add((header, FONT_SIZE, true, LEFT_FORMAT, false));
        lines.Add((new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));

        foreach (var item in order.Items)
        {
            var line = FormatLine(item.ProductName, item.Quantity.ToString(), item.Price.ToString("F2"), item.Subtotal.ToString("F2"));
            lines.Add((line, FONT_SIZE, true, LEFT_FORMAT, true));
        }

        lines.Add((new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));
        lines.Add((FormatTotalLine("المجموع الفرعي:", order.Subtotal.ToString("F2")), FONT_SIZE, false, RTL_FORMAT, false));

        if (order.DiscountPercent > 0)
            lines.Add((FormatTotalLine($"خصم ({order.DiscountPercent}%):", $"-{order.DiscountAmount:F2}"), FONT_SIZE, false, RTL_FORMAT, false));

        lines.Add((new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));
        lines.Add((FormatTotalLine("الإجمالي:", order.Total.ToString("F2")), HEADER_FONT_SIZE, true, RTL_FORMAT, false));
        lines.Add((new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));

        var footer = SettingsService.GetSetting("footer");
        if (!string.IsNullOrWhiteSpace(footer))
            lines.Add((footer, FONT_SIZE, false, CENTER_FORMAT, false));

        var invert = SettingsService.GetSetting("invert_receipt_colors") == "1";
        return RasterRender(lines, invert);
    }

    private static byte[] BuildMiniReceiptRasterData(Order order)
    {
        var lines = new List<(string Text, int FontSize, bool Bold, StringFormat Align, bool HasBorder)>();
        lines.Add(("** فاتورة مصغرة **", HEADER_FONT_SIZE, true, CENTER_FORMAT, false));
        lines.Add((new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));
        lines.Add(($"رقم الطلب: {order.OrderNumber}", FONT_SIZE, false, RTL_FORMAT, true));
        lines.Add(($"فاتورة رقم: {order.InvoiceNumber}", FONT_SIZE, false, RTL_FORMAT, true));
        lines.Add(($"التاريخ: {order.CreatedAt:yyyy-MM-dd HH:mm}", FONT_SIZE, false, RTL_FORMAT, true));

        if (!string.IsNullOrWhiteSpace(order.CustomerName))
            lines.Add(($"العميل: {order.CustomerName}", FONT_SIZE, true, RTL_FORMAT, true));

        if (!string.IsNullOrWhiteSpace(order.PaymentMethod))
            lines.Add(($"طريقة الدفع: {order.PaymentMethod}", FONT_SIZE, false, RTL_FORMAT, true));

        lines.Add((new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));

        foreach (var item in order.Items)
        {
            lines.Add(($"{Truncate(item.ProductName, 20)}  x{item.Quantity}  {item.Subtotal:F2}", FONT_SIZE, false, LEFT_FORMAT, false));
        }

        lines.Add((new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));
        lines.Add((FormatTotalLine("الإجمالي:", order.Total.ToString("F2")), FONT_SIZE, true, RTL_FORMAT, false));

        var cafeName = SettingsService.GetSetting("cafe_name");
        if (!string.IsNullOrWhiteSpace(cafeName))
            lines.Add((cafeName, FONT_SIZE, false, CENTER_FORMAT, false));

        lines.Add((new string('=', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));

        var invert = SettingsService.GetSetting("invert_receipt_colors") == "1";
        return RasterRender(lines, invert);
    }

    private static byte[] BuildReturnReceiptRasterData(Return ret)
    {
        var lines = new List<(string Text, int FontSize, bool Bold, StringFormat Align, bool HasBorder)>();
        lines.Add(("** إيصال مرتجع **", HEADER_FONT_SIZE, true, CENTER_FORMAT, false));
        lines.Add((new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));
        lines.Add(($"فاتورة أصلية: {ret.InvoiceNumber}", FONT_SIZE, false, RTL_FORMAT, true));
        lines.Add(($"السبب: {ret.Reason}", FONT_SIZE, false, RTL_FORMAT, true));
        lines.Add(($"التاريخ: {ret.CreatedAt:yyyy-MM-dd HH:mm}", FONT_SIZE, false, RTL_FORMAT, true));
        lines.Add((new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));

        foreach (var item in ret.Items)
        {
            lines.Add(($"{item.ProductName} x{item.Quantity} = {item.Subtotal:F2}", FONT_SIZE, false, LEFT_FORMAT, false));
        }

        lines.Add((new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));
        lines.Add((FormatTotalLine("المبلغ المسترد:", ret.TotalRefund.ToString("F2")), HEADER_FONT_SIZE, true, RTL_FORMAT, false));

        var invert = SettingsService.GetSetting("invert_receipt_colors") == "1";
        return RasterRender(lines, invert);
    }

    private static byte[] BuildPurchaseReceiptRasterData(Purchase purchase)
    {
        var lines = new List<(string Text, int FontSize, bool Bold, StringFormat Align, bool HasBorder)>();
        var cafeName = SettingsService.GetSetting("cafe_name");
        if (!string.IsNullOrWhiteSpace(cafeName))
            lines.Add((cafeName, HEADER_FONT_SIZE, true, CENTER_FORMAT, false));

        var phone = SettingsService.GetSetting("phone");
        if (!string.IsNullOrWhiteSpace(phone))
            lines.Add((phone, FONT_SIZE, false, CENTER_FORMAT, false));

        lines.Add((new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));

        lines.Add(("** فاتورة مشتريات **", FONT_SIZE, true, CENTER_FORMAT, false));
        lines.Add((new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));

        lines.Add(($"فاتورة رقم: {purchase.InvoiceNumber}", FONT_SIZE, true, RTL_FORMAT, true));

        if (!string.IsNullOrWhiteSpace(purchase.SupplierName))
            lines.Add(($"المورد: {purchase.SupplierName}", FONT_SIZE, false, RTL_FORMAT, true));

        lines.Add(($"التاريخ: {purchase.CreatedAt:yyyy-MM-dd HH:mm}", FONT_SIZE, false, RTL_FORMAT, true));
        lines.Add(($"المستخدم: {purchase.CreatorName ?? ""}", FONT_SIZE, false, RTL_FORMAT, true));

        if (!string.IsNullOrWhiteSpace(purchase.Notes))
            lines.Add(($"ملاحظات: {purchase.Notes}", FONT_SIZE, false, RTL_FORMAT, true));

        lines.Add((new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));

        // Column header
        var header = FormatLine("الصنف", "الكمية", "التكلفة", "المجموع");
        lines.Add((header, FONT_SIZE, true, LEFT_FORMAT, false));
        lines.Add((new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));

        foreach (var item in purchase.Items)
        {
            var line = FormatLine(item.ProductName, item.Quantity.ToString(), item.CostPrice.ToString("F2"), item.Subtotal.ToString("F2"));
            lines.Add((line, FONT_SIZE, false, LEFT_FORMAT, false));
        }

        lines.Add((new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));
        lines.Add((FormatTotalLine("الإجمالي:", purchase.Total.ToString("F2")), HEADER_FONT_SIZE, true, RTL_FORMAT, false));
        lines.Add((new string('-', LINE_WIDTH), FONT_SIZE, false, CENTER_FORMAT, false));

        var footer = SettingsService.GetSetting("footer");
        if (!string.IsNullOrWhiteSpace(footer))
            lines.Add((footer, FONT_SIZE, false, CENTER_FORMAT, false));

        var invert = SettingsService.GetSetting("invert_receipt_colors") == "1";
        return RasterRender(lines, invert);
    }

    private static byte[] RasterRender(List<(string Text, int FontSize, bool Bold, StringFormat Align, bool HasBorder)> lines, bool invert = false)
    {
        const int topPadding = 0;
        int totalHeight = topPadding;
        foreach (var (_, fontSize, _, _, _) in lines)
            totalHeight += GetLineHeight(fontSize);

        using var bmp = new Bitmap(RASTER_WIDTH, totalHeight);
        using var g = Graphics.FromImage(bmp);
        g.Clear(invert ? Color.White : Color.Black);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

        var textBrush = invert ? Brushes.Black : Brushes.White;
        var borderPenColor = invert ? Color.Black : Color.White;

        int y = topPadding;
        foreach (var (text, fontSize, bold, format, hasBorder) in lines)
        {
            int lh = GetLineHeight(fontSize);
            if (hasBorder)
            {
                using var pen = new Pen(borderPenColor, 1);
                g.DrawRectangle(pen, 0, y + 1, RASTER_WIDTH - 1, lh - 3);
            }
            using var font = new Font(RASTER_FONT, fontSize, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
            g.DrawString(text, font, textBrush, new RectangleF(0, y, RASTER_WIDTH, lh), format);
            y += lh;
        }

        // Convert to 1bpp monochrome for thermal printer
        int w = RASTER_WIDTH;
        int h = totalHeight;
        int widthBytes = (w + 7) / 8;
        byte[] imageData = new byte[widthBytes * h];

        for (int yy = 0; yy < h; yy++)
        {
            for (int xx = 0; xx < w; xx++)
            {
                var px = bmp.GetPixel(xx, yy);
                int gray = (px.R * 77 + px.G * 150 + px.B * 29) >> 8;
                if (gray < 128)
                {
                    int bi = yy * widthBytes + (xx >> 3);
                    imageData[bi] |= (byte)(0x80 >> (xx & 7));
                }
            }
        }

        using var ms = new MemoryStream();
        ms.Write(ESC_INIT);

        // GS v 0 — Print raster bit image (m=0 normal mode for broad compatibility)
        ms.Write([
            0x1D, 0x76, 0x30, 0,
            (byte)(widthBytes & 0xFF),
            (byte)((widthBytes >> 8) & 0xFF),
            (byte)(h & 0xFF),
            (byte)((h >> 8) & 0xFF)
        ]);
        ms.Write(imageData);
        ms.Write(LF);

        // Cut immediately (no extra bottom margin)
        ms.Write(ESC_CUT);

        return ms.ToArray();
    }

    // --- Formatting Helpers (42 chars width for 80mm) ---

    private const int LINE_WIDTH = 42;

    private static void WriteDashes(MemoryStream ms, Encoding enc)
    {
        ms.Write(enc.GetBytes(new string('-', LINE_WIDTH)));
        ms.Write(LF);
    }

    /// <summary>
    /// Formats a 4-column line for item rows.
    /// </summary>
    private static string FormatLine(string col1, string col2, string col3, string col4)
    {
        // Columns: Name(18) | Qty(4) | Price(9) | Total(9)
        return $"{Truncate(col1, 18),-18}{col2,4}{col3,9}{col4,9}";
    }

    /// <summary>
    /// Formats a label:value total line, right-aligned.
    /// </summary>
    private static string FormatTotalLine(string label, string value)
    {
        var spaces = LINE_WIDTH - label.Length - value.Length;
        if (spaces < 1) spaces = 1;
        return label + new string(' ', spaces) + value;
    }

    private static string Truncate(string s, int maxLen)
    {
        return s.Length <= maxLen ? s : s[..(maxLen - 2)] + "..";
    }

    /// <summary>
    /// Checks the status of the configured thermal printer and returns its details.
    /// </summary>
    public static (bool IsInstalled, bool IsOnline, string StatusMessage) GetPrinterStatus()
    {
        try
        {
            var resolved = ResolvePrinterName();
            if (string.IsNullOrWhiteSpace(resolved))
            {
                return (false, false, "لم يتم العثور على أي طابعة في النظام");
            }

            var printServer = new LocalPrintServer();
            var printQueues = printServer.GetPrintQueues();
            var queue = printQueues.FirstOrDefault(q =>
                q.FullName.Equals(resolved, StringComparison.OrdinalIgnoreCase) ||
                q.Name.Equals(resolved, StringComparison.OrdinalIgnoreCase));

            if (queue == null)
            {
                return (false, false, $"الطابعة غير معرفة في النظام");
            }

            if (queue.IsOffline)
            {
                return (true, false, $"الطابعة '{queue.FullName}' غير متصلة");
            }

            return (true, true, $"الطابعة '{queue.FullName}' متصلة وجاهزة");
        }
        catch (Exception ex)
        {
            return (false, false, $"خطأ في فحص الطابعة: {ex.Message}");
        }
    }
}
