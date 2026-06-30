using System.Windows;
using System.Windows.Controls;
using CafePOS.Services;

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
        var report = ReportService.GetDailySalesReport(date);
        ReportTitle.Text = $"تقرير مبيعات اليوم ({date:yyyy-MM-dd})";
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
        var items = ReportService.GetTopProducts(date, date.AddDays(1).AddSeconds(-1), 10);

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
