using System.Collections.Generic;
using System.IO;
using System.Printing;
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
        LoadLicenseInfo();
        LoadSettings();
        LoadUsers();
        LoadPaymentMethods();
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

    private void LoadLicenseInfo()
    {
        try
        {
            var isLicensed = LicenseService.IsLicensed();
            if (isLicensed)
            {
                LicenseStatusText.Text = "✅ نشط";
                LicenseStatusText.Foreground = FindResource("SuccessGreenBrush") as System.Windows.Media.SolidColorBrush;
                LicenseKeyBox.IsEnabled = false;
            }
            else
            {
                var remaining = LicenseService.GetTrialDaysRemaining();
                if (remaining > 0)
                {
                    LicenseStatusText.Text = "⚠ تجريبي";
                    LicenseStatusText.Foreground = FindResource("AccentCopperBrush") as System.Windows.Media.SolidColorBrush;
                    LicenseTrialText.Text = $"متبقي {remaining} يوم";
                    LicenseTrialText.Visibility = Visibility.Visible;
                }
                else
                {
                    LicenseStatusText.Text = "✕ غير نشط";
                    LicenseStatusText.Foreground = FindResource("ErrorRedBrush") as System.Windows.Media.SolidColorBrush;
                    LicenseTrialText.Text = "انتهت الفترة التجريبية";
                    LicenseTrialText.Visibility = Visibility.Visible;
                }
                LicenseKeyBox.IsEnabled = true;
            }
            HardwareIdText.Text = LicenseService.GetHardwareId();
        }
        catch { }
    }

    private void CopyHwId_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(HardwareIdText.Text);
        CustomMessageBox.Show("تم نسخ رقم الجهاز!", "تم",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ActivateLicense_Click(object sender, RoutedEventArgs e)
    {
        var key = LicenseKeyBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            CustomMessageBox.Show("الرجاء إدخال مفتاح التفعيل", "تنبيه",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (LicenseService.ActivateLicense(key))
        {
            CustomMessageBox.Show("تم تفعيل الترخيص بنجاح! 🎉", "تم التفعيل ✓",
                MessageBoxButton.OK, MessageBoxImage.Information);
            LoadLicenseInfo();
            LicenseKeyBox.Clear();
        }
        else
        {
            CustomMessageBox.Show("مفتاح التفعيل غير صالح", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadSettings()
    {
        var settings = SettingsService.GetAllSettings();

        CafeNameBox.Text = settings.GetValueOrDefault("cafe_name", "");
        PhoneBox.Text = settings.GetValueOrDefault("phone", "");
        FooterBox.Text = settings.GetValueOrDefault("footer", "");
        _logoPath = settings.GetValueOrDefault("logo_path", "");
        var savedPrinter = settings.GetValueOrDefault("printer_name", "");
        RefreshPrinterList(savedPrinter);
        DiscountEnabledCheck.IsChecked = settings.GetValueOrDefault("discount_enabled", "0") == "1";
        DiscountPercentBox.Text = settings.GetValueOrDefault("discount_percent", "10");
        ReturnsEnabledCheck.IsChecked = settings.GetValueOrDefault("returns_enabled", "1") != "0";
        RasterPrintCheck.IsChecked = settings.GetValueOrDefault("raster_print", "1") == "1";
        CompactReceiptCheck.IsChecked = settings.GetValueOrDefault("compact_receipt", "0") == "1";
        InvertColorsCheck.IsChecked = settings.GetValueOrDefault("invert_receipt_colors", "0") == "1";

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
            var dataDir = CafePOS.Helpers.AppPaths.DataDirectory;
            var destPath = CafePOS.Helpers.AppPaths.LogoPath;

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

    private void RefreshPrinterList(string? selectName = null)
    {
        try
        {
            var server = new LocalPrintServer();
            var queues = server.GetPrintQueues();
            var printerNames = queues.Select(q => q.FullName).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            PrinterCombo.ItemsSource = printerNames;

            if (!string.IsNullOrWhiteSpace(selectName) && printerNames.Contains(selectName))
                PrinterCombo.SelectedItem = selectName;
            else if (printerNames.Count > 0)
                PrinterCombo.SelectedIndex = 0;

            PrinterStatusText.Text = printerNames.Count > 0
                ? $"✅ تم العثور على {printerNames.Count} طابعة"
                : "⚠️ لم يتم العثور على أي طابعة";
        }
        catch
        {
            PrinterStatusText.Text = "❌ فشل الاتصال بخدمة الطابعات";
        }
    }

    private void DetectPrinters_Click(object sender, RoutedEventArgs e)
    {
        RefreshPrinterList(PrinterCombo.SelectedItem?.ToString());
    }

    private void TestPrint_Click(object sender, RoutedEventArgs e)
    {
        var printerName = PrinterCombo.Text.Trim();
        SettingsService.SetSetting("printer_name", printerName);

        try
        {
            var success = PrintService.TestPrinter(printerName);

            if (success)
                CustomMessageBox.Show("تم إرسال أمر الطباعة بنجاح!\n\nإذا لم تظهر رسالة الاختبار مطبوعة، فتأكد من:\n• توصيل الطابعة بالكهرباء\n• وجود ورق حراري\n• اسم الطابعة صحيح", "طباعة",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            else
                CustomMessageBox.Show("لم تستجب الطابعة. تأكد من توصيلها وأن الاسم صحيح.", "فشلت الطباعة",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"فشلت الطباعة:\n{ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ======================== Payment Methods ========================

    private void LoadPaymentMethods()
    {
        PaymentMethodsList.ItemsSource = PaymentMethodService.GetAll();
    }

    private void AddPaymentMethod_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog("إضافة طريقة دفع", "الرجاء إدخال اسم طريقة الدفع الجديدة:");
        if (dialog.ShowDialog() != true) return;
        var name = dialog.ResponseText;
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            PaymentMethodService.Add(name.Trim());
            LoadPaymentMethods();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeletePaymentMethod_Click(object sender, RoutedEventArgs e)
    {
        if (PaymentMethodsList.SelectedItem is not PaymentMethod selected)
        {
            CustomMessageBox.Show("الرجاء تحديد طريقة دفع من القائمة أولاً", "تنبيه",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = CustomMessageBox.Show(
            $"هل تريد حذف طريقة الدفع '{selected.Name}'?",
            "تأكيد الحذف",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            PaymentMethodService.Delete(selected.Id);
            LoadPaymentMethods();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
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
            SettingsService.SetSetting("printer_name", PrinterCombo.Text.Trim());
            SettingsService.SetSetting("discount_enabled", DiscountEnabledCheck.IsChecked == true ? "1" : "0");
            SettingsService.SetSetting("returns_enabled", ReturnsEnabledCheck.IsChecked == true ? "1" : "0");
            SettingsService.SetSetting("raster_print", RasterPrintCheck.IsChecked == true ? "1" : "0");
            SettingsService.SetSetting("compact_receipt", CompactReceiptCheck.IsChecked == true ? "1" : "0");
            SettingsService.SetSetting("invert_receipt_colors", InvertColorsCheck.IsChecked == true ? "1" : "0");

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
