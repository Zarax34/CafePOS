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

        // Role-based visibility
        if (_currentUser.IsCashier)
        {
            // Cashier: فقط نقطة البيع والمرتجعات والفواتير والمصروفات
            BtnPurchases.Visibility = Visibility.Collapsed;
            BtnReports.Visibility = Visibility.Collapsed;
            BtnProducts.Visibility = Visibility.Collapsed;
            BtnSettings.Visibility = Visibility.Collapsed;
            BtnWorkers.Visibility = Visibility.Collapsed;
            BtnWorkerWithdrawals.Visibility = Visibility.Collapsed;

            // Hide returns button if returns are disabled by admin
            if (!SettingsService.IsReturnsEnabled())
            {
                BtnReturns.Visibility = Visibility.Collapsed;
            }
        }
        else if (_currentUser.IsStoreManager)
        {
            // Store manager: يرى كل شيء عدا نقطة البيع والمرتجعات والإعدادات
            BtnPOS.Visibility = Visibility.Collapsed;
            BtnReturns.Visibility = Visibility.Collapsed;
            BtnSettings.Visibility = Visibility.Collapsed;
            ShiftClose.Visibility = Visibility.Collapsed;
        }
        // Full manager (admin): يرى كل الأزرار

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
                if (!_currentUser.IsStoreManager)
                {
                    _posView ??= new POSView();
                    _posView.Refresh();
                    ContentArea.Content = _posView;
                }
                break;

            case "Returns":
                if (!_currentUser.IsStoreManager)
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
                ContentArea.Content = new InvoicesView();
                break;

            case "Purchases":
                if (_currentUser.IsManager || _currentUser.IsStoreManager)
                {
                    var purchasesView = new PurchasesView();
                    purchasesView.Refresh();
                    ContentArea.Content = purchasesView;
                }
                break;

            case "Expenses":
                try
                {
                    ContentArea.Content = new ExpensesView();
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"خطأ في فتح شاشة المصروفات:\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}", "خطأ",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                break;

            case "Workers":
                if (_currentUser.IsManager || _currentUser.IsStoreManager)
                    ContentArea.Content = new WorkersView();
                break;

            case "WorkerWithdrawals":
                if (_currentUser.IsManager || _currentUser.IsStoreManager)
                    ContentArea.Content = new WorkerWithdrawalsView();
                break;

            case "Reports":
                if (_currentUser.IsManager || _currentUser.IsStoreManager)
                    ContentArea.Content = new ReportsView();
                break;

            case "Products":
                if (_currentUser.IsManager || _currentUser.IsStoreManager)
                    ContentArea.Content = new ProductsView();
                break;

            case "Settings":
                if (_currentUser.IsManager)
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
