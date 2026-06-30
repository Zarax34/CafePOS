using System.IO;
using System.Windows;
using System.Windows.Controls;
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
            var from = date.ToString("yyyy-MM-dd 00:00:00");
            var to = date.AddDays(29).ToString("yyyy-MM-dd 23:59:59");

            var breakdown = ReportService.GetDailyBreakdown(from, to);
            var report = ReportService.Get30DaySalesReport(date);
            var topItems = ReportService.GetTopProducts(date, date.AddDays(29).AddHours(23).AddMinutes(59).AddSeconds(59), 10);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("تقرير 30 يوم");

            // RTL
            ws.RightToLeft = true;

            // Title
            ws.Cell("A1").Value = ReportTitle.Text;
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 16;
            ws.Range("A1:C1").Merge();

            // Header row
            ws.Cell("A3").Value = "التاريخ";
            ws.Cell("B3").Value = "عدد الطلبات";
            ws.Cell("C3").Value = "إجمالي المبيعات";

            var headerRange = ws.Range("A3:C3");
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0x8D6E63);
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            decimal grandTotal = 0;
            int grandOrders = 0;
            int row = 4;

            for (int i = 0; i < 30; i++)
            {
                var dayStr = date.AddDays(i).ToString("yyyy-MM-dd");
                var match = breakdown.FirstOrDefault(d => d.Date == dayStr);

                ws.Cell(row, 1).Value = dayStr;
                ws.Cell(row, 2).Value = match?.OrderCount ?? 0;
                ws.Cell(row, 3).Value = (double)(match?.TotalRevenue ?? 0);
                ws.Cell(row, 3).Style.NumberFormat.NumberFormatId = 4;

                grandTotal += match?.TotalRevenue ?? 0;
                grandOrders += match?.OrderCount ?? 0;
                row++;
            }

            // Total row
            var totalRow = row;
            ws.Cell(totalRow, 1).Value = "المجموع الكلي";
            ws.Cell(totalRow, 1).Style.Font.Bold = true;
            ws.Cell(totalRow, 2).Value = grandOrders;
            ws.Cell(totalRow, 2).Style.Font.Bold = true;
            ws.Cell(totalRow, 3).Value = (double)grandTotal;
            ws.Cell(totalRow, 3).Style.Font.Bold = true;
            ws.Cell(totalRow, 3).Style.NumberFormat.NumberFormatId = 4;

            var totalRange = ws.Range(totalRow, 1, totalRow, 3);
            totalRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0xE8F5E9);
            totalRange.Style.Font.Bold = true;

            // Summary section
            var summaryRow = totalRow + 2;
            ws.Cell(summaryRow, 1).Value = "ملخص التقارير";
            ws.Cell(summaryRow, 1).Style.Font.Bold = true;
            ws.Cell(summaryRow, 1).Style.Font.FontSize = 14;
            ws.Range(summaryRow, 1, summaryRow, 3).Merge();

            ws.Cell(summaryRow + 1, 1).Value = "إجمالي الإيرادات";
            ws.Cell(summaryRow + 1, 2).Value = (double)report.TotalRevenue;
            ws.Cell(summaryRow + 1, 2).Style.NumberFormat.NumberFormatId = 4;

            ws.Cell(summaryRow + 2, 1).Value = "إجمالي الخصومات";
            ws.Cell(summaryRow + 2, 2).Value = (double)report.TotalDiscounts;
            ws.Cell(summaryRow + 2, 2).Style.NumberFormat.NumberFormatId = 4;

            ws.Cell(summaryRow + 3, 1).Value = "إجمالي المرتجعات";
            ws.Cell(summaryRow + 3, 2).Value = (double)report.TotalReturns;
            ws.Cell(summaryRow + 3, 2).Style.NumberFormat.NumberFormatId = 4;

            ws.Cell(summaryRow + 4, 1).Value = "صافي التحصيل";
            ws.Cell(summaryRow + 4, 2).Value = (double)report.NetCash;
            ws.Cell(summaryRow + 4, 2).Style.NumberFormat.NumberFormatId = 4;

            ws.Cell(summaryRow + 5, 1).Value = "عدد الفواتير";
            ws.Cell(summaryRow + 5, 2).Value = report.OrderCount;

            // Top products section
            var topRow = summaryRow + 7;
            ws.Cell(topRow, 1).Value = "المنتجات الأكثر مبيعاً";
            ws.Cell(topRow, 1).Style.Font.Bold = true;
            ws.Cell(topRow, 1).Style.Font.FontSize = 14;
            ws.Range(topRow, 1, topRow, 3).Merge();

            ws.Cell(topRow + 1, 1).Value = "المنتج";
            ws.Cell(topRow + 1, 2).Value = "الكمية";
            ws.Cell(topRow + 1, 3).Value = "الإيرادات";

            var topHeader = ws.Range(topRow + 1, 1, topRow + 1, 3);
            topHeader.Style.Font.Bold = true;
            topHeader.Style.Fill.BackgroundColor = XLColor.FromArgb(0x8D6E63);
            topHeader.Style.Font.FontColor = XLColor.White;

            for (int i = 0; i < topItems.Count; i++)
            {
                ws.Cell(topRow + 2 + i, 1).Value = topItems[i].Name;
                ws.Cell(topRow + 2 + i, 2).Value = topItems[i].TotalQuantity;
                ws.Cell(topRow + 2 + i, 3).Value = (double)topItems[i].TotalRevenue;
                ws.Cell(topRow + 2 + i, 3).Style.NumberFormat.NumberFormatId = 4;
            }

            // Column widths
            ws.Column(1).Width = 25;
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

    private void LoadDailyReport()
    {
        var date = GetSelectedDate();
        var report = ReportService.Get30DaySalesReport(date);
        ReportTitle.Text = $"تقرير 30 يوم ({date:yyyy-MM-dd} إلى {date.AddDays(29):yyyy-MM-dd})";
        DisplayReport(report);
    }

    private void LoadMonthlyReport()
    {
        var date = GetSelectedDate();
        var report = ReportService.GetMonthlySalesReport(date.Year, date.Month);
        ReportTitle.Text = $"تقرير مبيعات الشهر ({date:yyyy-MM})";
        DisplayReport(report);
    }

    private void DisplayReport(SalesReport report)
    {
        TotalRevenueText.Text = $"{report.TotalRevenue:F2} ر.ي";
        TotalDiscountsText.Text = $"-{report.TotalDiscounts:F2} ر.ي";
        TotalReturnsText.Text = $"-{report.TotalReturns:F2} ر.ي";
        NetCashText.Text = $"{report.NetCash:F2} ر.ي";
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
