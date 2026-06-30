using System.Windows;

namespace CafePOS.Views;

public partial class InputDialog : Window
{
    public string ResponseText => ResponseBox.Text.Trim();

    public InputDialog(string title, string prompt)
    {
        InitializeComponent();
        TitleText.Text = title;
        PromptText.Text = prompt;
        Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        Loaded += (_, _) => ResponseBox.Focus();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
