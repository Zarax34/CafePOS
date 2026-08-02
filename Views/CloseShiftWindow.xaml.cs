using System.Windows;
using System.Windows.Input;
using CafePOS.Models;
using CafePOS.Services;
using CafePOS.Data;

namespace CafePOS.Views;

public partial class CloseShiftWindow : Window
{
    private readonly User _cashier;
    private Shift? _currentShift;
    private decimal _totalSales;

    public bool ShiftClosed { get; private set; }

    public CloseShiftWindow(User cashier)
    {
        InitializeComponent();
        _cashier = cashier;
        LoadShiftInfo();
    }

    private void LoadShiftInfo()
    {
        _currentShift = ShiftService.GetCurrentShift(_cashier.Id);
        if (_currentShift == null)
        {
            // Open a shift on demand if none exists
            _currentShift = ShiftService.OpenShift(_cashier.Id);
        }

        CashierText.Text = _cashier.Username;
        StartTimeText.Text = _currentShift.StartTime.ToString("yyyy-MM-dd HH:mm:ss");

        // Calculate expected sales for cashier to view (total orders - total returns)
        _totalSales = CalculateTotalSales(_currentShift.StartTime);
        TotalSalesText.Text = $"{_totalSales:F2} ر.ي";
        PettyCashText.Text = $"{_totalSales:F2} ر.ي";
    }

    private void DepositBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (PettyCashText == null) return;
        if (decimal.TryParse(DepositBox.Text.Trim(), out var deposit) && deposit >= 0)
        {
            PettyCashText.Text = $"{_totalSales - deposit:F2} ر.ي";
        }
        else
        {
            PettyCashText.Text = $"{_totalSales:F2} ر.ي";
        }
    }

    private decimal CalculateTotalSales(DateTime start)
    {
        decimal sales = 0;
        decimal returns = 0;
        decimal expenses = 0;

        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT COALESCE(SUM(Total), 0) FROM orders
                WHERE CashierId = @cashier AND CreatedAt >= @start;";
            cmd.Parameters.AddWithValue("@cashier", _cashier.Id);
            cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd HH:mm:ss"));
            sales = (decimal)Convert.ToDouble(cmd.ExecuteScalar());
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT COALESCE(SUM(TotalRefund), 0) FROM returns
                WHERE CashierId = @cashier AND CreatedAt >= @start;";
            cmd.Parameters.AddWithValue("@cashier", _cashier.Id);
            cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd HH:mm:ss"));
            returns = (decimal)Convert.ToDouble(cmd.ExecuteScalar());
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT COALESCE(SUM(Amount), 0) FROM expenses
                WHERE CashierId = @cashier AND CreatedAt >= @start;";
            cmd.Parameters.AddWithValue("@cashier", _cashier.Id);
            cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd HH:mm:ss"));
            expenses = (decimal)Convert.ToDouble(cmd.ExecuteScalar());
        }

        return sales - returns - expenses;
    }

    private void ActualCashBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Confirm_Click(sender, e);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        ShiftClosed = false;
        DialogResult = false;
        Close();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(ActualCashBox.Text.Trim(), out var actualCash) || actualCash < 0)
        {
            CustomMessageBox.Show("الرجاء إدخال مبلغ صحيح وموجب", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal deposit = 0;
        if (!string.IsNullOrWhiteSpace(DepositBox.Text.Trim()))
        {
            if (!decimal.TryParse(DepositBox.Text.Trim(), out deposit) || deposit < 0)
            {
                CustomMessageBox.Show("الرجاء إدخال مبلغ إيداع صحيح وموجب", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (deposit > actualCash)
        {
            CustomMessageBox.Show("المبلغ المودع لا يمكن أن يتجاوز المبلغ الفعلي الموجود في الخزينة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_currentShift == null) return;

        var closedShift = ShiftService.CloseShift(_currentShift.Id, actualCash, deposit);
        if (closedShift != null)
        {
            CustomMessageBox.Show("تم إغلاق ورديتك بنجاح! سيتم الآن تسجيل الخروج التلقائي.", "تم الإغلاق ✓", MessageBoxButton.OK, MessageBoxImage.Information);
            ShiftClosed = true;
            DialogResult = true;
            Close();
        }
        else
        {
            CustomMessageBox.Show("فشل في إغلاق الشفت. يرجى المحاولة مرة أخرى.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
