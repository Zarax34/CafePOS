using System.Windows;
using CafePOS.Services;

namespace CafePOS.Views;

public partial class LicenseWindow : Window
{
    public bool Authorized { get; private set; }

    public LicenseWindow()
    {
        InitializeComponent();
        LoadInfo();
    }

    private void LoadInfo()
    {
        // Show hardware ID
        HardwareIdText.Text = LicenseService.GetHardwareId();

        // Show trial info
        var remaining = LicenseService.GetTrialDaysRemaining();
        if (remaining > 0)
        {
            TrialText.Text = $"فترة تجريبية: متبقي {remaining} يوم";
            TrialButton.Visibility = Visibility.Visible;
        }
        else
        {
            TrialText.Text = "⚠ انتهت الفترة التجريبية — يرجى تفعيل الترخيص";
            TrialText.Foreground = FindResource("ErrorRedBrush") as System.Windows.Media.SolidColorBrush;
            TrialButton.Visibility = Visibility.Collapsed;
        }
    }

    private void CopyHwId_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(HardwareIdText.Text);
        MessageBox.Show("تم نسخ رقم الجهاز!", "تم",
            MessageBoxButton.OK, MessageBoxImage.Information,
            MessageBoxResult.OK, MessageBoxOptions.RtlReading);
    }

    private void Activate_Click(object sender, RoutedEventArgs e)
    {
        var key = LicenseKeyBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            ShowError("الرجاء إدخال مفتاح الترخيص");
            return;
        }

        if (LicenseService.ActivateLicense(key))
        {
            MessageBox.Show("تم تفعيل الترخيص بنجاح! 🎉\nشكراً لاختياركم نظام إدارة الكافيه الذكي.",
                "تم التفعيل ✓",
                MessageBoxButton.OK, MessageBoxImage.Information,
                MessageBoxResult.OK, MessageBoxOptions.RtlReading);

            Authorized = true;
            DialogResult = true;
            Close();
        }
        else
        {
            ShowError("مفتاح الترخيص غير صالح. تأكد من إدخاله بشكل صحيح.");
        }
    }

    private void ContinueTrial_Click(object sender, RoutedEventArgs e)
    {
        Authorized = true;
        DialogResult = true;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Authorized = false;
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
