using System.Windows;
using System.Windows.Controls;
using CafePOS.Models;
using CafePOS.Services;

namespace CafePOS.Views;

public partial class AddUserWindow : Window
{
    public bool UserSaved { get; private set; }
    private readonly User? _editUser;

    public AddUserWindow(User? editUser = null)
    {
        InitializeComponent();
        _editUser = editUser;

        if (editUser != null)
        {
            Title = "تعديل حساب مستخدم";
            HeaderText.Text = "✏️ تعديل حساب مستخدم";
            UsernameBox.Text = editUser.Username;
            FullNameBox.Text = editUser.FullName;
            PasswordHint.Visibility = Visibility.Visible;

            foreach (ComboBoxItem item in RoleCombo.Items)
            {
                if (item.Tag?.ToString() == editUser.Role)
                {
                    RoleCombo.SelectedItem = item;
                    break;
                }
            }

            UsernameBox.Focus();
        }
        else
        {
            UsernameBox.Focus();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        UserSaved = false;
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

        var selectedItem = RoleCombo.SelectedItem as ComboBoxItem;
        var role = selectedItem?.Tag?.ToString() ?? "cashier";

        if (_editUser != null)
        {
            // Edit mode
            var success = AuthService.UpdateUser(_editUser.Id, username, fullName, role,
                string.IsNullOrWhiteSpace(password) ? null : password);

            if (success)
            {
                CustomMessageBox.Show("تم تحديث بيانات المستخدم بنجاح!", "تم ✓", MessageBoxButton.OK, MessageBoxImage.Information);
                UserSaved = true;
                DialogResult = true;
                Close();
            }
            else
            {
                CustomMessageBox.Show("اسم المستخدم هذا مسجل بالفعل. يرجى اختيار اسم آخر.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            // Add mode
            if (string.IsNullOrWhiteSpace(password))
            {
                CustomMessageBox.Show("الرجاء إدخال كلمة المرور", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (AuthService.CreateUser(username, password, role, fullName))
            {
                CustomMessageBox.Show("تم إنشاء حساب المستخدم بنجاح!", "تم ✓", MessageBoxButton.OK, MessageBoxImage.Information);
                UserSaved = true;
                DialogResult = true;
                Close();
            }
            else
            {
                CustomMessageBox.Show("اسم المستخدم هذا مسجل بالفعل في النظام. يرجى اختيار اسم مستخدم آخر.", "خطأ في التسجيل", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
