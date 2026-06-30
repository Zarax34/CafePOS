using System.Windows;
using System.Windows.Input;
using CafePOS.Models;
using CafePOS.ViewModels;

namespace CafePOS.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow()
    {
        InitializeComponent();

        _viewModel = new LoginViewModel();
        _viewModel.LoginSucceeded += OnLoginSucceeded;
        DataContext = _viewModel;

        // Focus username on load
        Loaded += (_, _) => UsernameBox.Focus();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = PasswordBox.Password;
    }

    private void OnLoginSucceeded(User user)
    {
        var mainWindow = new MainWindow(user);
        mainWindow.Show();
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        DragMove();
    }
}
