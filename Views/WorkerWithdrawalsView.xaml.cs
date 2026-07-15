using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CafePOS.Services;

namespace CafePOS.Views;

public partial class WorkerWithdrawalsView : UserControl
{
    public WorkerWithdrawalsView()
    {
        InitializeComponent();
        Loaded += WorkerWithdrawalsView_Loaded;
    }

    private void WorkerWithdrawalsView_Loaded(object sender, RoutedEventArgs e)
    {
        FromDate.SelectedDate = DateTime.Now.Date;
        ToDate.SelectedDate = DateTime.Now.Date.AddDays(30);
        LoadSummary();
    }

    private DateTime? GetFromDate() => FromDate.SelectedDate;
    private DateTime? GetToDate() => ToDate.SelectedDate?.AddDays(1).AddSeconds(-1);

    private void LoadSummary()
    {
        try
        {
            var summaries = ExpenseService.GetWorkerWithdrawalSummary(GetFromDate(), GetToDate());
            WorkerSummaryList.ItemsSource = summaries;
            TotalWithdrawalsText.Text = $"إجمالي السحبيات: {summaries.Sum(s => s.TotalAmount):F2} ر.ي";
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ أثناء تحميل الملخص:\n{ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadSummary();
        WithdrawalsGrid.ItemsSource = null;
        DetailTitle.Text = "تفاصيل السحبيات";
        WorkerTotalText.Text = "اختر عاملاً لعرض تفاصيل السحبيات";
    }

    private void WorkerSummaryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WorkerSummaryList.SelectedItem is WorkerWithdrawalSummary summary)
        {
            LoadWorkerDetails(summary.WorkerId, summary.WorkerName);
        }
    }

    private void LoadWorkerDetails(int workerId, string workerName)
    {
        try
        {
            var withdrawals = ExpenseService.GetWorkerWithdrawals(workerId, GetFromDate(), GetToDate());
            WithdrawalsGrid.ItemsSource = withdrawals;
            DetailTitle.Text = $"سحبيات: {workerName}";
            WorkerTotalText.Text = $"الإجمالي: {withdrawals.Sum(w => w.Amount):F2} ر.ي | عدد العمليات: {withdrawals.Count}";
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ أثناء تحميل التفاصيل:\n{ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
