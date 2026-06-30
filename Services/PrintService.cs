using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Printing;
using System.Linq;
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
    /// Prints an order receipt.
    /// </summary>
    public static bool PrintReceipt(Order order)
    {
        var printerName = SettingsService.GetSetting("printer_name");
        if (string.IsNullOrWhiteSpace(printerName))
        {
            // Try default printer name
            printerName = "POS-80";
        }

        var data = BuildReceiptData(order);
        return RawPrinterHelper.SendBytesToPrinter(printerName, data);
    }

    /// <summary>
    /// Prints a return receipt.
    /// </summary>
    public static bool PrintReturnReceipt(Return ret)
    {
        var printerName = SettingsService.GetSetting("printer_name");
        if (string.IsNullOrWhiteSpace(printerName))
            printerName = "POS-80";

        var data = BuildReturnReceiptData(ret);
        return RawPrinterHelper.SendBytesToPrinter(printerName, data);
    }

    private static byte[] BuildReceiptData(Order order)
    {
        var enc = GetArabicEncoding();
        using var ms = new MemoryStream();

        // Initialize
        ms.Write(ESC_INIT);
        ms.Write(ESC_CODEPAGE);

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
        ms.Write(ESC_FEED5);
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

        ms.Write(ESC_FEED5);
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
            var printerName = SettingsService.GetSetting("printer_name");
            if (string.IsNullOrWhiteSpace(printerName))
            {
                printerName = "POS-80";
            }

            var printServer = new LocalPrintServer();
            var printQueues = printServer.GetPrintQueues();
            var queue = printQueues.FirstOrDefault(q => q.Name.Equals(printerName, StringComparison.OrdinalIgnoreCase));

            if (queue == null)
            {
                return (false, false, $"الطابعة '{printerName}' غير معرفة في النظام");
            }

            if (queue.IsOffline)
            {
                return (true, false, $"الطابعة '{printerName}' غير متصلة");
            }

            return (true, true, $"الطابعة '{printerName}' متصلة وجاهزة");
        }
        catch (Exception ex)
        {
            return (false, false, $"خطأ في فحص الطابعة: {ex.Message}");
        }
    }
}
