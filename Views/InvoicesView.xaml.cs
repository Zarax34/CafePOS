using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CafePOS.Models;
using CafePOS.Services;

namespace CafePOS.Views;

public partial class InvoicesView : UserControl
{
    private Order? _selectedOrderWithItems;
    private Purchase? _selectedPurchase;
    private Expense? _selectedExpense;
    private string _currentFilter = "All";

    public InvoicesView()
    {
        InitializeComponent();
        Loaded += InvoicesView_Loaded;
    }

    private void InvoicesView_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshList();
    }

    private void RefreshList()
    {
        try
        {
            var items = new List<InvoiceListItem>();

            bool showSales = _currentFilter is "All" or "Sale";
            bool showPurchases = _currentFilter is "All" or "Purchase";
            bool showExpenses = _currentFilter is "All" or "Expense";

            if (showSales)
            {
                List<Order> orders;
                if (DatePickerFilter.SelectedDate.HasValue)
                {
                    var date = DatePickerFilter.SelectedDate.Value.Date;
                    var from = date;
                    var to = date.AddDays(1).AddSeconds(-1);
                    orders = OrderService.GetOrdersByDateRange(from, to);

                    var query = SearchBox.Text.Trim();
                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        orders = orders.Where(o =>
                            (o.InvoiceNumber != null && o.InvoiceNumber.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                            (o.CustomerName != null && o.CustomerName.Contains(query, StringComparison.OrdinalIgnoreCase))
                        ).ToList();
                    }
                }
                else if (!string.IsNullOrWhiteSpace(SearchBox.Text))
                {
                    orders = OrderService.SearchOrders(SearchBox.Text.Trim());
                }
                else
                {
                    orders = OrderService.GetTodayOrders();
                }

                items.AddRange(orders.Select(o => new InvoiceListItem
                {
                    InvoiceNumber = o.InvoiceNumber,
                    CreatedAt = o.CreatedAt,
                    PersonName = o.CashierName ?? "",
                    Total = o.Total,
                    Type = InvoiceType.Sale
                }));
            }

            if (showPurchases)
            {
                List<Purchase> purchases;
                if (DatePickerFilter.SelectedDate.HasValue)
                {
                    var date = DatePickerFilter.SelectedDate.Value.Date;
                    var from = date;
                    var to = date.AddDays(1).AddSeconds(-1);
                    purchases = PurchaseService.GetPurchasesByDateRange(from, to);

                    var query = SearchBox.Text.Trim();
                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        purchases = purchases.Where(p =>
                            (p.InvoiceNumber != null && p.InvoiceNumber.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                            (p.SupplierName != null && p.SupplierName.Contains(query, StringComparison.OrdinalIgnoreCase))
                        ).ToList();
                    }
                }
                else if (!string.IsNullOrWhiteSpace(SearchBox.Text))
                {
                    purchases = PurchaseService.SearchPurchases(SearchBox.Text.Trim());
                }
                else
                {
                    purchases = PurchaseService.GetPurchasesByDateRange(
                        DateTime.Today, DateTime.Today.AddDays(1).AddSeconds(-1));
                }

                items.AddRange(purchases.Select(p => new InvoiceListItem
                {
                    InvoiceNumber = p.InvoiceNumber,
                    CreatedAt = p.CreatedAt,
                    PersonName = p.CreatorName ?? "",
                    Total = p.Total,
                    Type = InvoiceType.Purchase,
                    ExternalInvoiceNumber = p.ExternalInvoiceNumber
                }));
            }

            if (showExpenses)
            {
                List<Expense> expenses;
                if (DatePickerFilter.SelectedDate.HasValue)
                {
                    var date = DatePickerFilter.SelectedDate.Value.Date;
                    var from = date;
                    var to = date.AddDays(1).AddSeconds(-1);
                    expenses = ExpenseService.GetAllExpenses(from, to);

                    var query = SearchBox.Text.Trim();
                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        expenses = expenses.Where(e =>
                            (e.InvoiceNumber != null && e.InvoiceNumber.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                            e.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                        ).ToList();
                    }
                }
                else if (!string.IsNullOrWhiteSpace(SearchBox.Text))
                {
                    var query = SearchBox.Text.Trim();
                    expenses = ExpenseService.GetAllExpenses()
                        .Where(e =>
                            (e.InvoiceNumber != null && e.InvoiceNumber.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                            e.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                        ).ToList();
                }
                else
                {
                    var today = DateTime.Today;
                    expenses = ExpenseService.GetAllExpenses(today, today.AddDays(1).AddSeconds(-1));
                }

                items.AddRange(expenses
                    .Where(e => e.InvoiceNumber != null)
                    .Select(e => new InvoiceListItem
                {
                    InvoiceNumber = e.InvoiceNumber!,
                    CreatedAt = e.CreatedAt,
                    PersonName = e.CashierName,
                    Total = e.Amount,
                    Type = InvoiceType.Expense,
                    Description = e.Description
                }));
            }

            items = items.OrderByDescending(i => i.CreatedAt).ToList();
            InvoicesList.ItemsSource = items;
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ أثناء تحميل الفواتير:\n{ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            _currentFilter = btn.Tag as string ?? "All";
            FilterAll.Opacity = _currentFilter == "All" ? 1.0 : 0.5;
            FilterSales.Opacity = _currentFilter == "Sale" ? 1.0 : 0.5;
            FilterPurchases.Opacity = _currentFilter == "Purchase" ? 1.0 : 0.5;
            FilterExpenses.Opacity = _currentFilter == "Expense" ? 1.0 : 0.5;
            ClearDetailPanels();
            InvoicesList.SelectedItem = null;
            RefreshList();
        }
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            RefreshList();
        }
    }

    private void DatePickerFilter_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshList();
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        DatePickerFilter.SelectedDate = null;
        RefreshList();
    }

    private void ClearDetailPanels()
    {
        _selectedOrderWithItems = null;
        _selectedPurchase = null;
        _selectedExpense = null;
        EmptyStatePanel.Visibility = Visibility.Visible;
        DetailPanel.Visibility = Visibility.Collapsed;
        PurchaseDetailPanel.Visibility = Visibility.Collapsed;
        ExpenseDetailPanel.Visibility = Visibility.Collapsed;
    }

    private void InvoicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (InvoicesList.SelectedItem is InvoiceListItem selected)
        {
            try
            {
                if (selected.Type == InvoiceType.Sale)
                {
                    var fullOrder = OrderService.GetOrderByInvoice(selected.InvoiceNumber);
                    if (fullOrder != null)
                    {
                        _selectedOrderWithItems = fullOrder;
                        _selectedPurchase = null;
                        _selectedExpense = null;
                        DisplayInvoiceDetails(fullOrder);
                    }
                }
                else if (selected.Type == InvoiceType.Purchase)
                {
                    var fullPurchase = PurchaseService.GetPurchaseByInvoice(selected.InvoiceNumber);
                    if (fullPurchase != null)
                    {
                        _selectedPurchase = fullPurchase;
                        _selectedOrderWithItems = null;
                        _selectedExpense = null;
                        DisplayPurchaseDetails(fullPurchase);
                    }
                }
                else
                {
                    var fullExpense = ExpenseService.GetExpenseByInvoice(selected.InvoiceNumber);
                    if (fullExpense != null)
                    {
                        _selectedExpense = fullExpense;
                        _selectedOrderWithItems = null;
                        _selectedPurchase = null;
                        DisplayExpenseDetails(fullExpense);
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"حدث خطأ أثناء تحميل تفاصيل الفاتورة:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            ClearDetailPanels();
        }
    }

    private void DisplayInvoiceDetails(Order order)
    {
        EmptyStatePanel.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
        PurchaseDetailPanel.Visibility = Visibility.Collapsed;

        DtlCafeName.Text = SettingsService.GetSetting("cafe_name") ?? "كافيه";
        var phone = SettingsService.GetSetting("phone");
        if (string.IsNullOrWhiteSpace(phone))
        {
            DtlPhone.Visibility = Visibility.Collapsed;
        }
        else
        {
            DtlPhone.Visibility = Visibility.Visible;
            DtlPhone.Text = $"الهاتف: {phone}";
        }

        DtlInvoiceNum.Text = $"فاتورة رقم: {order.InvoiceNumber}";
        DtlOrderNum.Text = $"رقم الطلب: {order.OrderNumber}";
        DtlDate.Text = $"التاريخ: {order.CreatedAt:yyyy-MM-dd HH:mm}";
        DtlCashier.Text = $"الكاشير: {order.CashierName}";

        if (string.IsNullOrWhiteSpace(order.PaymentMethod))
        {
            DtlPaymentMethodCell.Visibility = Visibility.Collapsed;
        }
        else
        {
            DtlPaymentMethodCell.Visibility = Visibility.Visible;
            DtlPaymentMethod.Text = $"طريقة الدفع: {order.PaymentMethod}";
        }

        if (string.IsNullOrWhiteSpace(order.CustomerName))
        {
            DtlCustomerCell.Visibility = Visibility.Collapsed;
        }
        else
        {
            DtlCustomerCell.Visibility = Visibility.Visible;
            DtlCustomer.Text = $"العميل: {order.CustomerName}";
        }

        DtlItemsControl.ItemsSource = order.Items;

        DtlSubtotal.Text = $"{order.Subtotal:F2} ر.ي";
        if (order.DiscountAmount > 0)
        {
            DtlDiscountRow.Visibility = Visibility.Visible;
            DtlDiscountLabel.Text = $"خصم ({order.DiscountPercent}%)";
            DtlDiscountAmount.Text = $"-{order.DiscountAmount:F2} ر.ي";
        }
        else
        {
            DtlDiscountRow.Visibility = Visibility.Collapsed;
        }
        DtlTotal.Text = $"{order.Total:F2} ر.ي";

        DtlFooter.Text = SettingsService.GetSetting("footer") ?? "شكراً لزيارتكم";
    }

    private void DisplayPurchaseDetails(Purchase purchase)
    {
        EmptyStatePanel.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Collapsed;
        PurchaseDetailPanel.Visibility = Visibility.Visible;

        PurCafeName.Text = SettingsService.GetSetting("cafe_name") ?? "كافيه";
        var phone = SettingsService.GetSetting("phone");
        if (string.IsNullOrWhiteSpace(phone))
        {
            PurPhone.Visibility = Visibility.Collapsed;
        }
        else
        {
            PurPhone.Visibility = Visibility.Visible;
            PurPhone.Text = $"الهاتف: {phone}";
        }

        PurInvoiceNum.Text = $"فاتورة رقم: {purchase.InvoiceNumber}";
        if (string.IsNullOrWhiteSpace(purchase.ExternalInvoiceNumber))
        {
            PurExternalInvoiceNumCell.Visibility = Visibility.Collapsed;
        }
        else
        {
            PurExternalInvoiceNumCell.Visibility = Visibility.Visible;
            PurExternalInvoiceNum.Text = $"رقم فاتورة المورد: {purchase.ExternalInvoiceNumber}";
        }
        PurSupplier.Text = $"المورد: {purchase.SupplierName ?? "—"}";
        PurDate.Text = $"التاريخ: {purchase.CreatedAt:yyyy-MM-dd HH:mm}";
        PurCreator.Text = $"المستخدم: {purchase.CreatorName ?? ""}";

        if (string.IsNullOrWhiteSpace(purchase.Notes))
        {
            PurNotesCell.Visibility = Visibility.Collapsed;
        }
        else
        {
            PurNotesCell.Visibility = Visibility.Visible;
            PurNotes.Text = $"ملاحظات: {purchase.Notes}";
        }

        PurItemsControl.ItemsSource = purchase.Items;
        PurTotal.Text = $"{purchase.Total:F2} ر.ي";

        PurFooter.Text = SettingsService.GetSetting("footer") ?? "شكراً لزيارتكم";
    }

    private void DisplayExpenseDetails(Expense expense)
    {
        EmptyStatePanel.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Collapsed;
        PurchaseDetailPanel.Visibility = Visibility.Collapsed;
        ExpenseDetailPanel.Visibility = Visibility.Visible;

        ExpCafeName.Text = SettingsService.GetSetting("cafe_name") ?? "كافيه";
        var phone = SettingsService.GetSetting("phone");
        if (string.IsNullOrWhiteSpace(phone))
        {
            ExpPhone.Visibility = Visibility.Collapsed;
        }
        else
        {
            ExpPhone.Visibility = Visibility.Visible;
            ExpPhone.Text = $"الهاتف: {phone}";
        }

        ExpInvoiceNum.Text = $"فاتورة رقم: {expense.InvoiceNumber}";
        ExpDescription.Text = $"الوصف: {expense.Description}";
        ExpAmount.Text = $"المبلغ: {expense.Amount:F2} ر.ي";
        ExpDate.Text = $"التاريخ: {expense.CreatedAt:yyyy-MM-dd HH:mm}";
        ExpCashier.Text = $"الكاشير: {expense.CashierName}";
        ExpTotal.Text = $"{expense.Amount:F2} ر.ي";

        ExpFooter.Text = SettingsService.GetSetting("footer") ?? "شكراً لزيارتكم";
    }

    private void Reprint_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOrderWithItems == null) return;

        try
        {
            var success = PrintService.PrintReceipt(_selectedOrderWithItems);
            if (success)
            {
                CustomMessageBox.Show("تم إرسال أمر إعادة طباعة الفاتورة للطابعة بنجاح", "تم ✓",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                CustomMessageBox.Show("تعذر الطباعة. يرجى التحقق من اتصال الطابعة واسمها في الإعدادات.", "خطأ في الطباعة",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"خطأ أثناء محاولة الطباعة:\n{ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


}
