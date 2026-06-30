using System.IO;
using System.Windows;
using System.Windows.Controls;
using CafePOS.Models;
using CafePOS.Services;
using Microsoft.Win32;

using System.Windows.Media.Imaging;

namespace CafePOS.Views;

public partial class SettingsView : UserControl
{
    private string? _logoPath;

    public SettingsView()
    {
        InitializeComponent();
        LoadSettings();
        LoadUsers();
    }

    private static BitmapImage? LoadImageSafe(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var data = File.ReadAllBytes(path);
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = new MemoryStream(data);
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch { return null; }
    }

    private void LoadSettings()
    {
        var settings = SettingsService.GetAllSettings();

        CafeNameBox.Text = settings.GetValueOrDefault("cafe_name", "");
        PhoneBox.Text = settings.GetValueOrDefault("phone", "");
        FooterBox.Text = settings.GetValueOrDefault("footer", "");
        _logoPath = settings.GetValueOrDefault("logo_path", "");
        PrinterNameBox.Text = settings.GetValueOrDefault("printer_name", "");
        DiscountEnabledCheck.IsChecked = settings.GetValueOrDefault("discount_enabled", "0") == "1";
        DiscountPercentBox.Text = settings.GetValueOrDefault("discount_percent", "10");

        // Load logo preview
        LogoPreviewImg.Source = LoadImageSafe(_logoPath ?? string.Empty);
        UpdateReceiptPreview();
    }

    private void LoadUsers()
    {
        UsersList.ItemsSource = AuthService.GetAllUsers();
    }

    // ======================== Receipt Preview ========================

    private void ReceiptPreview_Changed(object sender, TextChangedEventArgs e)
    {
        UpdateReceiptPreview();
    }

    private void UpdateReceiptPreview()
    {
        if (PreviewCafeName == null) return;

        PreviewCafeName.Text = string.IsNullOrWhiteSpace(CafeNameBox.Text) ? "كافيه" : CafeNameBox.Text;
        PreviewPhone.Text = PhoneBox.Text;
        PreviewPhone.Visibility = string.IsNullOrWhiteSpace(PhoneBox.Text)
            ? Visibility.Collapsed : Visibility.Visible;
        PreviewFooter.Text = string.IsNullOrWhiteSpace(FooterBox.Text) ? "شكراً لزيارتكم" : FooterBox.Text;

        PreviewLogoImg.Source = LoadImageSafe(_logoPath ?? string.Empty);
    }

    // ======================== Logo ========================

    private void SelectLogo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "صور PNG|*.png|جميع الصور|*.png;*.jpg;*.jpeg;*.bmp",
            Title = "اختيار شعار الكافيه"
        };

        if (dialog.ShowDialog() == true)
        {
            var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            var destPath = Path.Combine(dataDir, "logo.png");

            try
            {
                Directory.CreateDirectory(dataDir);
                File.Copy(dialog.FileName, destPath, true);
                _logoPath = destPath;
                SettingsService.SetSetting("logo_path", _logoPath);

                var img = LoadImageSafe(_logoPath);
                LogoPreviewImg.Source = img;
                PreviewLogoImg.Source = img;

                if (img == null)
                    CustomMessageBox.Show("تم نسخ الملف لكن تعذر عرضه. تأكد من أن الملف بصيغة مدعومة (PNG/JPG).", "تنبيه",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"خطأ في نسخ الشعار: {ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ======================== User Management ========================

    private void AddUser_Click(object sender, RoutedEventArgs e)
    {
        var addUserWin = new AddUserWindow();
        addUserWin.Owner = Window.GetWindow(this);
        if (addUserWin.ShowDialog() == true)
        {
            LoadUsers();
        }
    }

    private void EditUser_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not User user) return;

        var editWin = new AddUserWindow(user);
        editWin.Owner = Window.GetWindow(this);
        if (editWin.ShowDialog() == true)
        {
            LoadUsers();
        }
    }

    private void ToggleUserActive_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not User user) return;

        if (user.IsActive)
        {
            // Deactivate
            var confirm = CustomMessageBox.Show(
                $"هل تريد تعطيل حساب المستخدم '{user.Username}'?\n\nلن يتمكن من تسجيل الدخول بعد التعطيل.",
                "تأكيد تعطيل الحساب",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var success = AuthService.DeleteUser(user.Id);
                if (success)
                {
                    LoadUsers();
                }
            }
            catch (InvalidOperationException ex)
            {
                CustomMessageBox.Show(ex.Message, "غير مسموح",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            // Reactivate
            var confirm = CustomMessageBox.Show(
                $"هل تريد إعادة تفعيل حساب المستخدم '{user.Username}'?",
                "تأكيد التفعيل",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            var success = AuthService.ReactivateUser(user.Id);
            if (success)
            {
                CustomMessageBox.Show($"تم إعادة تفعيل حساب '{user.Username}' بنجاح", "تم ✓",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                LoadUsers();
            }
        }
    }

    private void DeleteUser_Click(object sender, RoutedEventArgs e)
    {
        if (UsersList.SelectedItem is not User selectedUser)
        {
            CustomMessageBox.Show("الرجاء تحديد مستخدم من القائمة أولاً", "تنبيه",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = CustomMessageBox.Show(
            $"هل تريد تعطيل حساب المستخدم '{selectedUser.Username}'?\n\nلن يتمكن من تسجيل الدخول بعد التعطيل.",
            "تأكيد حذف المستخدم",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var success = AuthService.DeleteUser(selectedUser.Id);
            if (success)
            {
                CustomMessageBox.Show($"تم تعطيل حساب '{selectedUser.Username}' بنجاح", "تم ✓",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                LoadUsers();
            }
            else
            {
                CustomMessageBox.Show("لم يتم العثور على المستخدم", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (InvalidOperationException ex)
        {
            CustomMessageBox.Show(ex.Message, "غير مسموح",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ======================== Backup & Sync ========================

    private void ExportBackup_Click(object sender, RoutedEventArgs e)
    {
        var saveDialog = new SaveFileDialog
        {
            Filter = "ملف نسخة احتياطية|*.json",
            Title = "تصدير نسخة احتياطية",
            FileName = $"CafePOS_Backup_{DateTime.Now:yyyyMMdd_HHmm}.json"
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                BackupService.ExportBackup(saveDialog.FileName);
                CustomMessageBox.Show($"تم تصدير النسخة الاحتياطية بنجاح!\n\n{saveDialog.FileName}", "تم ✓",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"حدث خطأ أثناء التصدير:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ImportBackup_Click(object sender, RoutedEventArgs e)
    {
        var openDialog = new OpenFileDialog
        {
            Filter = "ملف نسخة احتياطية|*.json",
            Title = "استيراد ومزامنة"
        };

        if (openDialog.ShowDialog() == true)
        {
            var confirm = CustomMessageBox.Show(
                "سيتم استيراد البيانات ومزامنتها بذكاء:\n\n" +
                "• الفواتير الموجودة (بنفس الرقم) لن تتكرر\n" +
                "• التصنيفات والمنتجات الجديدة ستُضاف\n" +
                "• المستخدمون الجدد سيُضافون\n\n" +
                "هل تريد المتابعة؟",
                "تأكيد الاستيراد",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var result = BackupService.ImportBackupAndSync(openDialog.FileName);
                CustomMessageBox.Show(
                    $"تمت المزامنة بنجاح!\n\n{result}",
                    "تم ✓",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // Reload data
                LoadSettings();
                LoadUsers();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"حدث خطأ أثناء الاستيراد:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ======================== Save ========================

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SettingsService.SetSetting("cafe_name", CafeNameBox.Text.Trim());
            SettingsService.SetSetting("phone", PhoneBox.Text.Trim());
            SettingsService.SetSetting("footer", FooterBox.Text.Trim());
            SettingsService.SetSetting("printer_name", PrinterNameBox.Text.Trim());
            SettingsService.SetSetting("discount_enabled", DiscountEnabledCheck.IsChecked == true ? "1" : "0");

            if (decimal.TryParse(DiscountPercentBox.Text.Trim(), out var percent) && percent >= 0 && percent <= 100)
            {
                SettingsService.SetSetting("discount_percent", percent.ToString());
            }

            CustomMessageBox.Show("تم حفظ الإعدادات بنجاح!", "تم ✓",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
