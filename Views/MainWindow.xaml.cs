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
        NavigateTo("POS");
    }

    private void SetupUI()
    {
        // User greeting
        UserGreeting.Text = $"مرحباً، {(!string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.FullName : _currentUser.Username)}";
        RoleBadge.Text = _currentUser.IsManager ? "مدير" : "كاشير";

        // Role-based visibility: hide manager-only buttons for cashier
        if (!_currentUser.IsManager)
        {
            BtnReports.Visibility = Visibility.Collapsed;
            BtnProducts.Visibility = Visibility.Collapsed;
            BtnSettings.Visibility = Visibility.Collapsed;
        }

        // Open shift automatically for the cashier/manager on startup if none exists
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
            // Fail silently or handle
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
                _posView ??= new POSView();
                _posView.Refresh();
                ContentArea.Content = _posView;
                break;

            case "Returns":
                ContentArea.Content = new ReturnsView();
                break;

            case "Invoices":
                ContentArea.Content = new InvoicesView();
                break;

            case "Reports":
                if (_currentUser.IsManager)
                    ContentArea.Content = new ReportsView();
                break;

            case "Products":
                if (_currentUser.IsManager)
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
