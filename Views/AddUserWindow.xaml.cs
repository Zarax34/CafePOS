using System.Windows;
using System.Windows.Controls;
using CafePOS.Services;

namespace CafePOS.Views;

public partial class AddUserWindow : Window
{
    public bool UserCreated { get; private set; }

    public AddUserWindow()
    {
        InitializeComponent();
        UsernameBox.Focus();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        UserCreated = false;
        DialogResult = false;
        Close();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var username = UsernameBox.Text.Trim();
        var fullName = FullNameBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            CustomMessageBox.Show("الرجاء إدخال اسم المستخدم (رقم الكاشير)", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            CustomMessageBox.Show("الرجاء إدخال اسم الكاشير المعروض", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            CustomMessageBox.Show("الرجاء إدخال كلمة المرور", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedItem = RoleCombo.SelectedItem as ComboBoxItem;
        var role = selectedItem?.Tag?.ToString() ?? "cashier";

        if (AuthService.CreateUser(username, password, role, fullName))
        {
            CustomMessageBox.Show("تم إنشاء حساب المستخدم بنجاح!", "تم ✓", MessageBoxButton.OK, MessageBoxImage.Information);
            UserCreated = true;
            DialogResult = true;
            Close();
        }
        else
        {
            CustomMessageBox.Show("اسم المستخدم هذا مسجل بالفعل في النظام. يرجى اختيار اسم مستخدم آخر.", "خطأ في التسجيل", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
