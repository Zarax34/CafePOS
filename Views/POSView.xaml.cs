using System.Windows;
using System.Windows.Controls;
using CafePOS.ViewModels;

namespace CafePOS.Views;

public partial class POSView : UserControl
{
    private readonly POSViewModel _viewModel;

    public POSView()
    {
        InitializeComponent();
        _viewModel = new POSViewModel();
        DataContext = _viewModel;
    }

    /// <summary>
    /// Refreshes product and settings data (called when returning to POS tab).
    /// </summary>
    public void Refresh()
    {
        _viewModel.LoadData();
    }
}
