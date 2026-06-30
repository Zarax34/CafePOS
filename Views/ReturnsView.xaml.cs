using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CafePOS.Models;
using CafePOS.Services;

namespace CafePOS.Views;

/// <summary>
/// Wraps OrderItem with a selection flag for the returns UI.
/// </summary>
public class SelectableOrderItem : OrderItem
{
    public bool IsSelected { get; set; }
}

public partial class ReturnsView : UserControl
{
    private Order? _currentOrder;

    public ReturnsView()
    {
        InitializeComponent();
        LoadRecentReturns();
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            SearchInvoice();
    }

    private void SearchInvoice_Click(object sender, RoutedEventArgs e)
    {
        SearchInvoice();
    }

    private void SearchInvoice()
    {
        var invoiceNumber = SearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            CustomMessageBox.Show("الرجاء إدخال رقم الفاتورة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _currentOrder = OrderService.GetOrderByInvoice(invoiceNumber);

        if (_currentOrder == null)
        {
            CustomMessageBox.Show("لم يتم العثور على فاتورة بهذا الرقم", "غير موجود", MessageBoxButton.OK, MessageBoxImage.Warning);
            InvoiceInfoPanel.Visibility = Visibility.Collapsed;
            ItemsHeaderText.Visibility = Visibility.Collapsed;
            OriginalItemsList.ItemsSource = null;
            return;
        }

        // Show invoice info
        InvNumberText.Text = $"رقم الفاتورة: {_currentOrder.InvoiceNumber}";
        InvDateText.Text = $"التاريخ: {_currentOrder.CreatedAt:yyyy-MM-dd HH:mm}";
        InvTotalText.Text = $"الإجمالي: {_currentOrder.Total:F2} ر.ي";
        InvCustomerText.Text = string.IsNullOrWhiteSpace(_currentOrder.CustomerName)
            ? "" : $"العميل: {_currentOrder.CustomerName}";
        InvoiceInfoPanel.Visibility = Visibility.Visible;
        ItemsHeaderText.Visibility = Visibility.Visible;

        // Map items to selectable
        var selectableItems = _currentOrder.Items.Select(i => new SelectableOrderItem
        {
            Id = i.Id,
            OrderId = i.OrderId,
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            Price = i.Price,
            Quantity = i.Quantity,
            Subtotal = i.Subtotal,
            IsSelected = false
        }).ToList();

        OriginalItemsList.ItemsSource = selectableItems;
    }

    private void ProcessReturn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentOrder == null)
        {
            CustomMessageBox.Show("الرجاء البحث عن فاتورة أولاً", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var reason = ReasonBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            CustomMessageBox.Show("الرجاء كتابة سبب الإرجاع", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedItems = (OriginalItemsList.ItemsSource as IEnumerable<SelectableOrderItem>)?
            .Where(i => i.IsSelected).ToList();

        if (selectedItems == null || selectedItems.Count == 0)
        {
            CustomMessageBox.Show("الرجاء تحديد صنف واحد على الأقل للإرجاع", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var totalRefund = selectedItems.Sum(i => i.Subtotal);

        var confirm = CustomMessageBox.Show(
            $"سيتم إرجاع {selectedItems.Count} صنف/أصناف\n" +
            $"المبلغ المسترد: {totalRefund:F2} ر.ي\n\n" +
            "هل تريد المتابعة وصرف المبلغ نقداً للزبون؟",
            "تأكيد المرتجع",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var ret = new Return
            {
                OrderId = _currentOrder.Id,
                InvoiceNumber = _currentOrder.InvoiceNumber,
                Reason = reason,
                TotalRefund = totalRefund,
                CashierId = AuthService.CurrentUser?.Id ?? 0,
                Items = selectedItems.Select(i => new ReturnItem
                {
                    OrderItemId = i.Id,
                    ProductName = i.ProductName,
                    Price = i.Price,
                    Quantity = i.Quantity,
                    Subtotal = i.Subtotal
                }).ToList()
            };

            ReturnService.ProcessReturn(ret);

            // Print return receipt (non-blocking on failure)
            try
            {
                PrintService.PrintReturnReceipt(ret);
            }
            catch
            {
                // Print failure shouldn't block return success feedback
            }

            CustomMessageBox.Show(
                $"تم تسجيل المرتجع بنجاح!\n\n" +
                $"المبلغ المسترد: {totalRefund:F2} ر.ي\n" +
                "يرجى صرف المبلغ نقداً للزبون الآن.",
                "تم بنجاح ✓",
                MessageBoxButton.OK, MessageBoxImage.Information);

            // Reset form
            _currentOrder = null;
            SearchBox.Text = string.Empty;
            ReasonBox.Text = string.Empty;
            InvoiceInfoPanel.Visibility = Visibility.Collapsed;
            ItemsHeaderText.Visibility = Visibility.Collapsed;
            OriginalItemsList.ItemsSource = null;
            LoadRecentReturns();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadRecentReturns()
    {
        RecentReturnsList.ItemsSource = ReturnService.GetTodayReturns();
    }
}
