using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CafePOS.Models;
using CafePOS.Services;

namespace CafePOS.Views;

public partial class ExpensesView : UserControl
{
    public ExpensesView()
    {
        InitializeComponent();
        Loaded += ExpensesView_Loaded;
    }

    private void ExpensesView_Loaded(object sender, RoutedEventArgs e)
    {
        LoadWorkers();
        LoadExpenses();
        AmountBox.Text = "0";
    }

    private void LoadWorkers()
    {
        try
        {
            var workers = WorkerService.GetAll();
            workers.Insert(0, new Worker { Id = 0, Name = "-- اختر العامل --" });
            WorkerCombo.ItemsSource = workers;
            WorkerCombo.SelectedIndex = 0;
        }
        catch { }
    }

    private void ExpenseTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || WorkerCombo == null || DescriptionBox == null) return;
        var selectedType = (ExpenseTypeCombo.SelectedItem as ComboBoxItem)?.Content as string;
        WorkerCombo.Visibility = selectedType == "سحبيات العمال" ? Visibility.Visible : Visibility.Collapsed;
        DescriptionBox.IsEnabled = selectedType != "سحبيات العمال";
        if (selectedType == "سحبيات العمال")
        {
            DescriptionBox.Text = "سحبيات العمال";
        }
    }

    private void LoadExpenses()
    {
        try
        {
            var expenses = ExpenseService.GetAllExpenses();
            ExpensesGrid.ItemsSource = expenses;

            ExpensesCountText.Text = $"عدد المصروفات: {expenses.Count}";
            TotalExpensesText.Text = $"الإجمالي: {expenses.Sum(x => x.Amount):F2} ر.ي";
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ أثناء تحميل المصروفات:\n{ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddExpense_Click(object sender, RoutedEventArgs e)
    {
        var selectedType = (ExpenseTypeCombo.SelectedItem as ComboBoxItem)?.Content as string ?? "سداد";

        if (selectedType != "سحبيات العمال")
        {
            var description = DescriptionBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                CustomMessageBox.Show("الرجاء إدخال وصف المصروف", "تنبيه",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (!decimal.TryParse(AmountBox.Text.Trim(), out var amount) || amount <= 0)
        {
            CustomMessageBox.Show("الرجاء إدخال مبلغ صحيح أكبر من صفر", "تنبيه",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var user = AuthService.CurrentUser;
        if (user == null) return;

        var currentShift = ShiftService.GetCurrentShift(user.Id);
        if (currentShift == null)
        {
            currentShift = ShiftService.OpenShift(user.Id);
        }

        int? workerId = null;
        if (selectedType == "سحبيات العمال")
        {
            var selectedWorker = WorkerCombo.SelectedItem as Worker;
            if (selectedWorker == null || selectedWorker.Id == 0)
            {
                CustomMessageBox.Show("الرجاء اختيار العامل", "تنبيه",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            workerId = selectedWorker.Id;
        }

        try
        {
            var expense = new Expense
            {
                Description = selectedType == "سحبيات العمال" ? "سحبيات العمال" : DescriptionBox.Text.Trim(),
                Amount = amount,
                CashierId = user.Id,
                ShiftId = currentShift.Id,
                CreatedAt = DateTime.Now,
                ExpenseType = selectedType,
                WorkerId = workerId
            };

            ExpenseService.AddExpense(expense);
            LoadExpenses();

            DescriptionBox.Clear();
            AmountBox.Text = "0";
            ExpenseTypeCombo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ أثناء إضافة المصروف:\n{ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteExpense_Click(object sender, RoutedEventArgs e)
    {
        var expense = (Expense)((Button)sender).DataContext;

        if (expense.CreatedAt.AddMinutes(15) < DateTime.Now)
        {
            CustomMessageBox.Show("لا يمكن حذف المصروف بعد مرور أكثر من 15 دقيقة على إضافته",
                "غير مسموح", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = CustomMessageBox.Show($"هل أنت متأكد من حذف المصروف:\n{expense.Description} - {expense.Amount:F2} ر.ي؟",
            "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            ExpenseService.DeleteExpense(expense.Id);
            LoadExpenses();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ أثناء حذف المصروف:\n{ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
