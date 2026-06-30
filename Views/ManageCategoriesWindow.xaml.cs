using System.Windows;
using System.Windows.Controls;
using CafePOS.Models;
using CafePOS.Services;
using CafePOS.Data;

namespace CafePOS.Views;

public partial class ManageCategoriesWindow : Window
{
    private Category? _editingCategory;

    public ManageCategoriesWindow()
    {
        InitializeComponent();
        LoadCategories();
    }

    private void LoadCategories()
    {
        // Load all categories including inactive ones for management
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        var categories = new List<Category>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, Name, SortOrder, IsActive FROM categories ORDER BY SortOrder, Name;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                categories.Add(new Category
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    SortOrder = reader.GetInt32(2),
                    IsActive = reader.GetInt32(3) == 1
                });
            }
        }
        CategoriesList.ItemsSource = categories;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = CategoryNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            CustomMessageBox.Show("الرجاء إدخال اسم التصنيف", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_editingCategory != null)
            {
                _editingCategory.Name = name;
                ProductService.UpdateCategory(_editingCategory);
                CustomMessageBox.Show("تم تحديث التصنيف بنجاح", "تم ✓", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                ProductService.AddCategory(new Category { Name = name, IsActive = true });
                CustomMessageBox.Show("تم إضافة التصنيف بنجاح", "تم ✓", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            ClearForm();
            LoadCategories();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        ClearForm();
    }

    private void CategoriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoriesList.SelectedItem is Category category)
        {
            _editingCategory = category;
            FormTitle.Text = "تعديل التصنيف";
            CategoryNameBox.Text = category.Name;
            CancelBtn.Visibility = Visibility.Visible;
            SaveBtn.Content = "تحديث";
        }
    }

    private void ToggleActive_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Category category)
        {
            category.IsActive = !category.IsActive;
            ProductService.UpdateCategory(category);
            LoadCategories();
        }
    }

    private void ClearForm()
    {
        _editingCategory = null;
        FormTitle.Text = "إضافة تصنيف جديد";
        CategoryNameBox.Text = string.Empty;
        CancelBtn.Visibility = Visibility.Collapsed;
        SaveBtn.Content = "حفظ";
        CategoriesList.SelectedItem = null;
    }
}
