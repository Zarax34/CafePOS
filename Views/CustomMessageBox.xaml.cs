using System.Windows;

namespace CafePOS.Views;

public partial class CustomMessageBox : Window
{
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    public CustomMessageBox(string message, string title, MessageBoxButton button, MessageBoxImage image)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;

        // Setup Icon and Background
        string emoji = "ℹ";
        string brushKey = "AccentCopperBrush";
        switch (image)
        {
            case MessageBoxImage.Information:
                emoji = "ℹ";
                brushKey = "SuccessGreenBrush";
                break;
            case MessageBoxImage.Warning:
                emoji = "⚠";
                brushKey = "AccentCopperBrush";
                break;
            case MessageBoxImage.Error:
                emoji = "❌";
                brushKey = "ErrorRedBrush";
                break;
            case MessageBoxImage.Question:
                emoji = "❓";
                brushKey = "PrimaryBrownBrush";
                break;
        }
        IconText.Text = emoji;
        IconBorder.Background = FindResource(brushKey) as System.Windows.Media.Brush;

        // Setup Buttons
        if (button == MessageBoxButton.OK)
        {
            BtnOk.Visibility = Visibility.Visible;
            BtnYes.Visibility = Visibility.Collapsed;
            BtnNo.Visibility = Visibility.Collapsed;
        }
        else if (button == MessageBoxButton.YesNo)
        {
            BtnOk.Visibility = Visibility.Collapsed;
            BtnYes.Visibility = Visibility.Visible;
            BtnNo.Visibility = Visibility.Visible;
        }
    }

    public static MessageBoxResult Show(string message, string title = "تنبيه", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information)
    {
        var msgBox = new CustomMessageBox(message, title, button, image);
        
        // Find current active window to set as owner
        var activeWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (activeWindow != null)
        {
            msgBox.Owner = activeWindow;
        }
        
        msgBox.ShowDialog();
        return msgBox.Result;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.OK;
        DialogResult = true;
        Close();
    }

    private void BtnYes_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Yes;
        DialogResult = true;
        Close();
    }

    private void BtnNo_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.No;
        DialogResult = false;
        Close();
    }
}
