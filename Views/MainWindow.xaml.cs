using System.Windows;
using CafePOS.Models;
using CafePOS.Services;

namespace CafePOS.Views;

public partial class MainWindow : Window
{
    private readonly User _currentUser;
    private POSView? _posView;

    public MainWindow(User user)
    {
        InitializeComponent();

        _currentUser = user;
        SetupUI();

        if (_currentUser.IsCashier)
            NavigateTo("POS");
        else if (_currentUser.IsStoreManager)
            NavigateTo("Products");
        else
            NavigateTo("POS");
    }

    private void SetupUI()
    {
        // User greeting
        UserGreeting.Text = $"مرحباً، {(!string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.FullName : _currentUser.Username)}";

        // Role badge
        if (_currentUser.IsManager)
            RoleBadge.Text = "مدير";
        else if (_currentUser.IsStoreManager)
            RoleBadge.Text = "مدير محل";
        else
            RoleBadge.Text = "كاشير";

        // Permission-based visibility (falls back to role defaults when no
        // explicit permissions are configured for the user)
        BtnPOS.Visibility = _currentUser.HasPermission("pos_access") ? Visibility.Visible : Visibility.Collapsed;
        BtnReturns.Visibility = _currentUser.HasPermission("returns_manage") ? Visibility.Visible : Visibility.Collapsed;
        BtnInvoices.Visibility = _currentUser.HasPermission("invoices_view") ? Visibility.Visible : Visibility.Collapsed;
        BtnPurchases.Visibility = _currentUser.HasPermission("purchases_view") ? Visibility.Visible : Visibility.Collapsed;
        BtnExpenses.Visibility = _currentUser.HasPermission("expenses_view") ? Visibility.Visible : Visibility.Collapsed;
        BtnReports.Visibility = _currentUser.HasPermission("reports_view") ? Visibility.Visible : Visibility.Collapsed;
        BtnProducts.Visibility = _currentUser.HasPermission("products_view") ? Visibility.Visible : Visibility.Collapsed;
        BtnWorkers.Visibility = _currentUser.HasPermission("workers_view") ? Visibility.Visible : Visibility.Collapsed;
        BtnWorkerWithdrawals.Visibility = _currentUser.HasPermission("workers_withdrawals") ? Visibility.Visible : Visibility.Collapsed;
        BtnSettings.Visibility = _currentUser.HasPermission("settings_access") ? Visibility.Visible : Visibility.Collapsed;
        ShiftClose.Visibility = _currentUser.HasPermission("shift_close") ? Visibility.Visible : Visibility.Collapsed;

        // Hide returns button if returns are disabled by admin
        if (_currentUser.IsCashier && !SettingsService.IsReturnsEnabled())
        {
            BtnReturns.Visibility = Visibility.Collapsed;
        }

        // Open shift automatically on startup if none exists (not needed for store managers)
        if (!_currentUser.IsStoreManager)
        {
            try
            {
                var currentShift = ShiftService.GetCurrentShift(_currentUser.Id);
                if (currentShift == null)
                {
                    ShiftService.OpenShift(_currentUser.Id);
                }
            }
            catch
            {
                // Fail silently
            }
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            BtnState.Content = "🗖";
        }
        else
        {
            WindowState = WindowState.Maximized;
            BtnState.Content = "❐";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        var result = CustomMessageBox.Show("هل تريد إغلاق النظام بالكامل؟", "تأكيد الخروج",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            Application.Current.Shutdown();
        }
    }

    private void ShiftClose_Click(object sender, RoutedEventArgs e)
    {
        var closeShiftWin = new CloseShiftWindow(_currentUser);
        closeShiftWin.Owner = this;
        if (closeShiftWin.ShowDialog() == true)
        {
            AuthService.Logout();
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            Close();
        }
    }

    private void NavigateTo(string page)
    {
        switch (page)
        {
            case "POS":
                if (_currentUser.HasPermission("pos_access"))
                {
                    _posView ??= new POSView();
                    _posView.Refresh();
                    ContentArea.Content = _posView;
                }
                break;

            case "Returns":
                if (_currentUser.HasPermission("returns_manage"))
                {
                    if (_currentUser.IsCashier && !SettingsService.IsReturnsEnabled())
                    {
                        CustomMessageBox.Show("تم تعطيل المرتجعات للكاشير من قبل مدير النظام", "غير مسموح",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    ContentArea.Content = new ReturnsView();
                }
                break;

            case "Invoices":
                if (_currentUser.HasPermission("invoices_view"))
                    ContentArea.Content = new InvoicesView();
                break;

            case "Purchases":
                if (_currentUser.HasPermission("purchases_view"))
                {
                    var purchasesView = new PurchasesView();
                    purchasesView.Refresh();
                    ContentArea.Content = purchasesView;
                }
                break;

            case "Expenses":
                if (_currentUser.HasPermission("expenses_view"))
                {
                    try
                    {
                        ContentArea.Content = new ExpensesView();
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.Show($"خطأ في فتح شاشة المصروفات:\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}", "خطأ",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                break;

            case "Workers":
                if (_currentUser.HasPermission("workers_view"))
                    ContentArea.Content = new WorkersView();
                break;

            case "WorkerWithdrawals":
                if (_currentUser.HasPermission("workers_withdrawals"))
                    ContentArea.Content = new WorkerWithdrawalsView();
                break;

            case "Reports":
                if (_currentUser.HasPermission("reports_view"))
                    ContentArea.Content = new ReportsView();
                break;

            case "Products":
                if (_currentUser.HasPermission("products_view"))
                    ContentArea.Content = new ProductsView();
                break;

            case "Settings":
                if (_currentUser.HasPermission("settings_access"))
                    ContentArea.Content = new SettingsView();
                break;
        }
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string page)
        {
            NavigateTo(page);
        }
    }

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        MorePopup.IsOpen = !MorePopup.IsOpen;
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        var result = CustomMessageBox.Show("هل تريد تسجيل الخروج؟", "تأكيد تسجيل الخروج",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            AuthService.Logout();
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            Close();
        }
    }
}
