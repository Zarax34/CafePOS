using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CafePOS.Services;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace CafePOS.Views;

public class RankedProductItem
{
    public int Rank { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public decimal TotalRevenue { get; set; }
}

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
        ReportDatePicker.SelectedDate = DateTime.Now;
        LoadDailyReport();
        LoadTopProducts();

        // Hide shift close button for managers (managers close shifts from the reports panel)
        // Show for cashiers (they see only their total sales)
        var currentUser = AuthService.CurrentUser;
        if (currentUser != null && currentUser.Role == "cashier")
        {
            BtnShiftClose.Visibility = Visibility.Visible;
        }
    }

    /// <summary>
    /// Gets the selected date from the DatePicker, or today if none selected.
    /// </summary>
    private DateTime GetSelectedDate()
    {
        return ReportDatePicker.SelectedDate ?? DateTime.Now;
    }

    private void SingleDayReport_Click(object sender, RoutedEventArgs e)
    {
        LoadSingleDayReport();
    }

    private void DailyReport_Click(object sender, RoutedEventArgs e)
    {
        LoadDailyReport();
    }

    private void MonthlyReport_Click(object sender, RoutedEventArgs e)
    {
        LoadMonthlyReport();
    }

    private void TopProducts_Click(object sender, RoutedEventArgs e)
    {
        ShowTopProductsPanel();
        LoadTopProducts();
    }

    private void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        var date = GetSelectedDate().Date;

        var saveDialog = new SaveFileDialog
        {
            Filter = "ملف PDF|*.pdf",
            Title = "تصدير تقرير اليوم إلى PDF",
            FileName = $"CafePOS_Daily_Report_{date:yyyyMMdd}.pdf"
        };

        if (saveDialog.ShowDialog() != true) return;

        try
        {
            var report = ReportService.GetDailySalesReport(date);
            var from = date.ToString("yyyy-MM-dd 00:00:00");
            var to = date.ToString("yyyy-MM-dd 23:59:59");
            var topItems = ReportService.GetTopProducts(date, date, 10);
            var pmtBreakdown = ReportService.GetPaymentMethodBreakdown(from, to);

            PdfReportService.ExportDailyReport(saveDialog.FileName, date, report, topItems, pmtBreakdown);

            CustomMessageBox.Show(
                $"تم تصدير تقرير اليوم بنجاح!\n\n{saveDialog.FileName}",
                "تم ✓", 
                MessageBoxButton.OK, 
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ أثناء تصدير PDF:\n{ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportReport_Click(object sender, RoutedEventArgs e)
    {
        var saveDialog = new SaveFileDialog
        {
            Filter = "ملف Excel|*.xlsx",
            Title = "تصدير التقرير",
            FileName = $"CafePOS_Report_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
        };

        if (saveDialog.ShowDialog() != true) return;

        try
        {
            var date = GetSelectedDate().Date;
            var report = ReportService.GetDailySalesReport(date);
            var from = date.ToString("yyyy-MM-dd 00:00:00");
            var to = date.ToString("yyyy-MM-dd 23:59:59");
            var topItems = ReportService.GetTopProducts(date, date, 10);
            var pmtBreakdown = ReportService.GetPaymentMethodBreakdown(from, to);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add($"تقرير يوم {date:yyyy-MM-dd}");

            // RTL
            ws.RightToLeft = true;

            // Title
            ws.Cell("A1").Value = $"تقرير مبيعات اليوم ({date:yyyy-MM-dd})";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 16;
            ws.Range("A1:D1").Merge();

            // Summary metrics
            ws.Cell("A3").Value = "البيان";
            ws.Cell("B3").Value = "القيمة";

            var headerRange = ws.Range("A3:B3");
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0x8D6E63);
            headerRange.Style.Font.FontColor = XLColor.White;

            int row = 4;
            AddExcelMetricRow(ws, ref row, "إجمالي الإيرادات", report.TotalRevenue);
            AddExcelMetricRow(ws, ref row, "إجمالي الخصومات", report.TotalDiscounts);
            AddExcelMetricRow(ws, ref row, "إجمالي المرتجعات", report.TotalReturns);
            AddExcelMetricRow(ws, ref row, "إجمالي المشتريات", report.TotalPurchases);
            AddExcelMetricRow(ws, ref row, "إجمالي المصروفات", report.TotalExpenses);
            AddExcelMetricRow(ws, ref row, "صافي التحصيل", report.NetCash, isHighlight: true);
            AddExcelMetricRow(ws, ref row, "صافي الربح", report.NetProfit, isHighlight: true, highlightColor: XLColor.FromArgb(0x2E7D32));
            ws.Cell(row, 1).Value = "عدد الفواتير";
            ws.Cell(row, 2).Value = report.OrderCount;
            row += 2;

            // Payment Method Breakdown
            if (pmtBreakdown.Count > 0)
            {
                ws.Cell(row, 1).Value = "المبيعات حسب طريقة الدفع";
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontSize = 13;
                ws.Range(row, 1, row, 3).Merge();
                row++;

                ws.Cell(row, 1).Value = "طريقة الدفع";
                ws.Cell(row, 2).Value = "عدد الفواتير";
                ws.Cell(row, 3).Value = "الإيرادات";
                var pmtHeader = ws.Range(row, 1, row, 3);
                pmtHeader.Style.Font.Bold = true;
                pmtHeader.Style.Fill.BackgroundColor = XLColor.FromArgb(0x8D6E63);
                pmtHeader.Style.Font.FontColor = XLColor.White;
                row++;

                foreach (var item in pmtBreakdown)
                {
                    ws.Cell(row, 1).Value = item.PaymentMethod;
                    ws.Cell(row, 2).Value = item.OrderCount;
                    ws.Cell(row, 3).Value = (double)item.TotalRevenue;
                    ws.Cell(row, 3).Style.NumberFormat.NumberFormatId = 4;
                    row++;
                }
                row++;
            }

            // Top Products
            if (topItems.Count > 0)
            {
                ws.Cell(row, 1).Value = "المنتجات الأكثر مبيعاً";
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontSize = 13;
                ws.Range(row, 1, row, 3).Merge();
                row++;

                ws.Cell(row, 1).Value = "المنتج";
                ws.Cell(row, 2).Value = "الكمية";
                ws.Cell(row, 3).Value = "الإيرادات";
                var topHeader = ws.Range(row, 1, row, 3);
                topHeader.Style.Font.Bold = true;
                topHeader.Style.Fill.BackgroundColor = XLColor.FromArgb(0x8D6E63);
                topHeader.Style.Font.FontColor = XLColor.White;
                row++;

                foreach (var item in topItems)
                {
                    ws.Cell(row, 1).Value = item.Name;
                    ws.Cell(row, 2).Value = item.TotalQuantity;
                    ws.Cell(row, 3).Value = (double)item.TotalRevenue;
                    ws.Cell(row, 3).Style.NumberFormat.NumberFormatId = 4;
                    row++;
                }
            }

            // Column widths
            ws.Column(1).Width = 30;
            ws.Column(2).Width = 18;
            ws.Column(3).Width = 22;

            workbook.SaveAs(saveDialog.FileName);

            CustomMessageBox.Show($"تم تصدير التقرير بنجاح!\n\n{saveDialog.FileName}", "تم ✓",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ أثناء التصدير:\n{ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void AddExcelMetricRow(IXLWorksheet ws, ref int row, string label, decimal value, bool isHighlight = false, XLColor? highlightColor = null)
    {
        var bgColor = highlightColor ?? XLColor.FromArgb(245, 245, 245);
        ws.Cell(row, 1).Value = label;
        ws.Cell(row, 2).Value = (double)value;
        ws.Cell(row, 2).Style.NumberFormat.NumberFormatId = 4;

        if (isHighlight)
        {
            var rowRange = ws.Range(row, 1, row, 2);
            rowRange.Style.Fill.BackgroundColor = bgColor;
            rowRange.Style.Font.Bold = true;
        }

        row += 1;
    }

    private void YearlyReport_Click(object sender, RoutedEventArgs e)
    {
        var year = GetSelectedDate().Year;

        var saveDialog = new SaveFileDialog
        {
            Filter = "ملف Excel|*.xlsx",
            Title = $"تصدير التقرير السنوي {year}",
            FileName = $"CafePOS_Yearly_{year}.xlsx"
        };

        if (saveDialog.ShowDialog() != true) return;

        try
        {
            var breakdown = ReportService.GetYearlyBreakdown(year);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add($"تقرير سنوي {year}");

            ws.RightToLeft = true;

            // Title
            ws.Cell("A1").Value = $"التقرير السنوي ({year})";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 16;
            ws.Range("A1:G1").Merge();

            // Header row
            ws.Cell("A3").Value = "الشهر";
            ws.Cell("B3").Value = "عدد الطلبات";
            ws.Cell("C3").Value = "إجمالي المبيعات";
            ws.Cell("D3").Value = "الخصومات";
            ws.Cell("E3").Value = "المرتجعات";
            ws.Cell("F3").Value = "المشتريات";
            ws.Cell("G3").Value = "صافي التحصيل";

            var headerRange = ws.Range("A3:G3");
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0x8D6E63);
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            decimal grandRevenue = 0, grandDiscounts = 0, grandReturns = 0, grandNet = 0;
            int grandOrders = 0;
            int row = 4;

            var monthNames = new Dictionary<string, string>
            {
                ["01"] = "يناير", ["02"] = "فبراير", ["03"] = "مارس",
                ["04"] = "أبريل", ["05"] = "مايو", ["06"] = "يونيو",
                ["07"] = "يوليو", ["08"] = "أغسطس", ["09"] = "سبتمبر",
                ["10"] = "أكتوبر", ["11"] = "نوفمبر", ["12"] = "ديسمبر"
            };

            foreach (var item in breakdown)
            {
                var monthParts = item.Month.Split('-');
                var monthName = monthParts.Length == 2 && monthNames.TryGetValue(monthParts[1], out var mn)
                    ? mn : item.Month;

                ws.Cell(row, 1).Value = monthName;
                ws.Cell(row, 2).Value = item.OrderCount;
                ws.Cell(row, 3).Value = (double)item.TotalRevenue;
                ws.Cell(row, 3).Style.NumberFormat.NumberFormatId = 4;
                ws.Cell(row, 4).Value = (double)item.TotalDiscounts;
                ws.Cell(row, 4).Style.NumberFormat.NumberFormatId = 4;
                ws.Cell(row, 5).Value = (double)item.TotalReturns;
                ws.Cell(row, 5).Style.NumberFormat.NumberFormatId = 4;
                ws.Cell(row, 6).Value = (double)item.NetCash;
                ws.Cell(row, 6).Style.NumberFormat.NumberFormatId = 4;

                grandRevenue += item.TotalRevenue;
                grandDiscounts += item.TotalDiscounts;
                grandReturns += item.TotalReturns;
                grandNet += item.NetCash;
                grandOrders += item.OrderCount;
                row++;
            }

            // Total row
            var totalRow = row;
            ws.Cell(totalRow, 1).Value = "المجموع السنوي";
            ws.Cell(totalRow, 1).Style.Font.Bold = true;
            ws.Cell(totalRow, 2).Value = grandOrders;
            ws.Cell(totalRow, 2).Style.Font.Bold = true;
            ws.Cell(totalRow, 3).Value = (double)grandRevenue;
            ws.Cell(totalRow, 3).Style.Font.Bold = true;
            ws.Cell(totalRow, 3).Style.NumberFormat.NumberFormatId = 4;
            ws.Cell(totalRow, 4).Value = (double)grandDiscounts;
            ws.Cell(totalRow, 4).Style.Font.Bold = true;
            ws.Cell(totalRow, 4).Style.NumberFormat.NumberFormatId = 4;
            ws.Cell(totalRow, 5).Value = (double)grandReturns;
            ws.Cell(totalRow, 5).Style.Font.Bold = true;
            ws.Cell(totalRow, 5).Style.NumberFormat.NumberFormatId = 4;
            ws.Cell(totalRow, 6).Value = (double)grandNet;
            ws.Cell(totalRow, 6).Style.Font.Bold = true;
            ws.Cell(totalRow, 6).Style.NumberFormat.NumberFormatId = 4;

            var totalRange = ws.Range(totalRow, 1, totalRow, 6);
            totalRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0xE8F5E9);

            // Column widths
            ws.Column(1).Width = 15;
            ws.Column(2).Width = 16;
            ws.Column(3).Width = 20;
            ws.Column(4).Width = 16;
            ws.Column(5).Width = 16;
            ws.Column(6).Width = 20;

            workbook.SaveAs(saveDialog.FileName);

            CustomMessageBox.Show($"تم تصدير التقرير السنوي بنجاح!\n\n{saveDialog.FileName}", "تم ✓",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ أثناء التصدير:\n{ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShiftClose_Click(object sender, RoutedEventArgs e)
    {
        var currentUser = AuthService.CurrentUser;
        if (currentUser == null) return;

        if (currentUser.Role == "cashier")
        {
            // Cashier: show CloseShiftWindow (displays total sales only)
            var cashierShiftWin = new CloseShiftWindow(currentUser);
            cashierShiftWin.Owner = Window.GetWindow(this);
            cashierShiftWin.ShowDialog();
        }
        else
        {
            // Manager: show the in-page shift close panel
            ShowShiftClosePanel();
        }
    }

    private void LoadSingleDayReport()
    {
        var date = GetSelectedDate();
        var report = ReportService.GetDailySalesReport(date);
        ReportTitle.Text = $"تقرير يوم واحد ({date:yyyy-MM-dd})";
        DisplayReport(report);
        LoadPaymentMethodBreakdown(date.ToString("yyyy-MM-dd 00:00:00"), date.ToString("yyyy-MM-dd 23:59:59"));
    }

    private void LoadDailyReport()
    {
        var date = GetSelectedDate();
        var report = ReportService.Get30DaySalesReport(date);
        ReportTitle.Text = $"تقرير 30 يوم ({date:yyyy-MM-dd} إلى {date.AddDays(29):yyyy-MM-dd})";
        DisplayReport(report);
        LoadPaymentMethodBreakdown(date.ToString("yyyy-MM-dd 00:00:00"), date.AddDays(29).ToString("yyyy-MM-dd 23:59:59"));
    }

    private void LoadMonthlyReport()
    {
        var date = GetSelectedDate();
        var report = ReportService.GetMonthlySalesReport(date.Year, date.Month);
        ReportTitle.Text = $"تقرير مبيعات الشهر ({date:yyyy-MM})";
        DisplayReport(report);
        var lastDay = DateTime.DaysInMonth(date.Year, date.Month);
        var from = new DateTime(date.Year, date.Month, 1).ToString("yyyy-MM-dd 00:00:00");
        var to = new DateTime(date.Year, date.Month, lastDay).ToString("yyyy-MM-dd 23:59:59");
        LoadPaymentMethodBreakdown(from, to);
    }

    private void LoadPaymentMethodBreakdown(string from, string to)
    {
        var breakdown = ReportService.GetPaymentMethodBreakdown(from, to);
        PaymentMethodList.ItemsSource = breakdown;
        PaymentMethodPanel.Visibility = breakdown.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DisplayReport(SalesReport report)
    {
        TotalRevenueText.Text = $"{report.TotalRevenue:F2} ر.ي";
        TotalDiscountsText.Text = $"-{report.TotalDiscounts:F2} ر.ي";
        TotalReturnsText.Text = $"-{report.TotalReturns:F2} ر.ي";
        TotalPurchasesText.Text = $"-{report.TotalPurchases:F2} ر.ي";
        TotalExpensesText.Text = $"-{report.TotalExpenses:F2} ر.ي";
        NetCashText.Text = $"{report.NetCash:F2} ر.ي";
        NetProfitText.Text = $"{report.NetProfit:F2} ر.ي";
        OrderCountText.Text = $"عدد الفواتير: {report.OrderCount}";
    }

    private void LoadTopProducts()
    {
        var date = GetSelectedDate().Date;
        var items = ReportService.GetTopProducts(date, date.AddDays(29).AddHours(23).AddMinutes(59).AddSeconds(59), 10);

        var ranked = items.Select((item, index) => new RankedProductItem
        {
            Rank = index + 1,
            Name = item.Name,
            TotalQuantity = item.TotalQuantity,
            TotalRevenue = item.TotalRevenue
        }).ToList();

        TopProductsList.ItemsSource = ranked;
    }

    private void ShowTopProductsPanel()
    {
        RightPanelTitle.Text = "المنتجات الأكثر مبيعاً";
        TopProductsList.Visibility = Visibility.Visible;
        ShiftClosePanel.Visibility = Visibility.Collapsed;
    }

    private void ShowShiftClosePanel()
    {
        RightPanelTitle.Text = "إغلاق الشفت";
        TopProductsList.Visibility = Visibility.Collapsed;
        ShiftClosePanel.Visibility = Visibility.Visible;
        ShiftResultPanel.Visibility = Visibility.Collapsed;
        ActualCashBox.Text = string.Empty;
    }

    private void ActualCashBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExecuteShiftClose_Click(sender, e);
        }
    }

    private void ExecuteShiftClose_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(ActualCashBox.Text.Trim(), out var actualCash))
        {
            CustomMessageBox.Show("الرجاء إدخال مبلغ صحيح", "تنبيه",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var cashierId = AuthService.CurrentUser?.Id ?? 0;

        // Open a shift if none exists, then close it
        var currentShift = ShiftService.GetCurrentShift(cashierId);
        if (currentShift == null)
        {
            currentShift = ShiftService.OpenShift(cashierId);
        }

        var closedShift = ShiftService.CloseShift(currentShift.Id, actualCash);
        if (closedShift == null)
        {
            CustomMessageBox.Show("حدث خطأ أثناء إغلاق الشفت", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Display results
        ShiftExpectedText.Text = $"المبلغ المتوقع: {closedShift.ExpectedCash:F2} ر.ي";
        ShiftActualText.Text = $"المبلغ الفعلي: {closedShift.ActualCash:F2} ر.ي";

        var diff = closedShift.Difference;
        if (diff == 0)
        {
            ShiftDiffText.Text = "✓ الخزينة متطابقة";
            ShiftDiffText.Foreground = FindResource("SuccessGreenBrush") as System.Windows.Media.Brush;
        }
        else if (diff > 0)
        {
            ShiftDiffText.Text = $"↑ زيادة: {diff:F2} ر.ي";
            ShiftDiffText.Foreground = FindResource("SuccessGreenBrush") as System.Windows.Media.Brush;
        }
        else
        {
            ShiftDiffText.Text = $"↓ نقص: {Math.Abs(diff):F2} ر.ي";
            ShiftDiffText.Foreground = FindResource("ErrorRedBrush") as System.Windows.Media.Brush;
        }

        ShiftResultPanel.Visibility = Visibility.Visible;

        CustomMessageBox.Show("تم إغلاق الشفت بنجاح!", "تم ✓",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
