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

            InvoicesList.ItemsSource = orders;
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ أثناء تحميل الفواتير:\n{ex.Message}", "خطأ", 
                MessageBoxButton.OK, MessageBoxImage.Error);
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

    private void InvoicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (InvoicesList.SelectedItem is Order selectedBrief)
        {
            try
            {
                // Fetch full order with items
                var fullOrder = OrderService.GetOrderByInvoice(selectedBrief.InvoiceNumber);
                if (fullOrder != null)
                {
                    _selectedOrderWithItems = fullOrder;
                    DisplayInvoiceDetails(fullOrder);
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
            _selectedOrderWithItems = null;
            EmptyStatePanel.Visibility = Visibility.Visible;
            DetailPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void DisplayInvoiceDetails(Order order)
    {
        EmptyStatePanel.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;

        // Header info from settings
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

        // Invoice details
        DtlInvoiceNum.Text = $"فاتورة رقم: {order.InvoiceNumber}";
        DtlOrderNum.Text = $"رقم الطلب: {order.OrderNumber}";
        DtlDate.Text = $"التاريخ: {order.CreatedAt:yyyy-MM-dd HH:mm}";
        DtlCashier.Text = $"الكاشير: {order.CashierName}";

        if (string.IsNullOrWhiteSpace(order.CustomerName))
        {
            DtlCustomer.Visibility = Visibility.Collapsed;
        }
        else
        {
            DtlCustomer.Visibility = Visibility.Visible;
            DtlCustomer.Text = $"العميل: {order.CustomerName}";
        }

        // Items list
        DtlItemsControl.ItemsSource = order.Items;

        // Totals
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

        // Footer
        DtlFooter.Text = SettingsService.GetSetting("footer") ?? "شكراً لزيارتكم";
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
