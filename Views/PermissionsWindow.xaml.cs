using System.Windows;
using System.Windows.Controls;
using CafePOS.Models;
using CafePOS.Services;

namespace CafePOS.Views;

public partial class PermissionsWindow : Window
{
    private readonly int _userId;
    private readonly List<CheckBox> _checkboxes = new();

    public PermissionsWindow(User user)
    {
        InitializeComponent();
        _userId = user.Id;

        HeaderText.Text = $"🔐 صلاحيات المستخدم: {user.Username}";
        UserInfoText.Text = $"{(string.IsNullOrWhiteSpace(user.FullName) ? "" : user.FullName + " • ")}{GetRoleName(user.Role)}";

        var current = AuthService.GetPermissions(user.Id);
        var all = AuthService.GetAllPermissions();

        foreach (var group in all.GroupBy(p => p.Category))
        {
            PermissionsPanel.Children.Add(new TextBlock
            {
                Text = group.Key,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = (System.Windows.Media.Brush)FindResource("PrimaryBrownBrush"),
                FontFamily = (System.Windows.Media.FontFamily)FindResource("AppFont"),
                Margin = new Thickness(5, 8, 5, 6)
            });

            var wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            foreach (var perm in group)
            {
                var initial = current.TryGetValue(perm.Key, out var v) ? v : user.HasPermission(perm.Key);
                var cb = new CheckBox
                {
                    Content = perm.NameArabic,
                    IsChecked = initial,
                    Tag = perm.Key,
                    FontSize = 13,
                    Foreground = (System.Windows.Media.Brush)FindResource("PrimaryBrownBrush"),
                    FontFamily = (System.Windows.Media.FontFamily)FindResource("AppFont"),
                    Margin = new Thickness(4, 4, 10, 4),
                    MinWidth = 165
                };
                _checkboxes.Add(cb);
                wrap.Children.Add(cb);
            }
            PermissionsPanel.Children.Add(wrap);
        }
    }

    private static string GetRoleName(string role) => role switch
    {
        "manager" => "مدير",
        "storemanager" => "مدير محل",
        _ => "كاشير"
    };

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var result = new Dictionary<string, bool>();
        foreach (var cb in _checkboxes)
        {
            if (cb.Tag is string key)
                result[key] = cb.IsChecked == true;
        }

        try
        {
            AuthService.SavePermissions(_userId, result);
            CustomMessageBox.Show("تم حفظ صلاحيات المستخدم بنجاح", "تم ✓",
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ أثناء حفظ الصلاحيات:\n{ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
