using System;
using System.Windows;
using System.Windows.Controls;
using CafePOS.Models;
using CafePOS.Services;

namespace CafePOS.Views;

public partial class WorkersView : UserControl
{
    public WorkersView()
    {
        InitializeComponent();
        Loaded += WorkersView_Loaded;
    }

    private void WorkersView_Loaded(object sender, RoutedEventArgs e)
    {
        LoadWorkers();
    }

    private void LoadWorkers()
    {
        try
        {
            var workers = WorkerService.GetAll();
            WorkersGrid.ItemsSource = workers;
            WorkersCountText.Text = $"عدد العمال: {workers.Count}";
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ أثناء تحميل العمال:\n{ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddWorker_Click(object sender, RoutedEventArgs e)
    {
        var name = WorkerNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            CustomMessageBox.Show("الرجاء إدخال اسم العامل", "تنبيه",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            WorkerService.Add(name, WorkerPhoneBox.Text.Trim());
            LoadWorkers();
            WorkerNameBox.Clear();
            WorkerPhoneBox.Clear();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ أثناء إضافة العامل:\n{ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteWorker_Click(object sender, RoutedEventArgs e)
    {
        var worker = (Worker)((Button)sender).DataContext;

        var result = CustomMessageBox.Show($"هل أنت متأكد من حذف العامل:\n{worker.Name}؟",
            "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            WorkerService.Delete(worker.Id);
            LoadWorkers();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ أثناء حذف العامل:\n{ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
