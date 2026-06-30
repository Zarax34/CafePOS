using System.Windows;
using System.Windows.Controls;
using CafePOS.Models;
using CafePOS.Services;

namespace CafePOS.Views;

public partial class ProductsView : UserControl
{
    private int? _editingProductId;

    public ProductsView()
    {
        InitializeComponent();
        LoadProducts();
        LoadCategories();
    }

    private void LoadProducts()
    {
        ProductsList.ItemsSource = ProductService.GetAllProductsAdmin();
    }

    private void LoadCategories()
    {
        var categories = ProductService.GetAllCategories();
        CategoryCombo.ItemsSource = categories;
        if (categories.Count > 0)
            CategoryCombo.SelectedIndex = 0;
    }

    private void NewProduct_Click(object sender, RoutedEventArgs e)
    {
        ClearFormInternal();
    }

    private void NewCategory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog("إضافة تصنيف جديد", "اسم التصنيف:");
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
        {
            ProductService.AddCategory(new Category { Name = dialog.ResponseText });
            LoadCategories();
            LoadProducts();
        }
    }

    private void ManageCategories_Click(object sender, RoutedEventArgs e)
    {
        var win = new ManageCategoriesWindow();
        win.Owner = Window.GetWindow(this);
        win.ShowDialog();
        // Refresh after closing
        LoadCategories();
        LoadProducts();
    }

    private void ProductsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProductsList.SelectedItem is Product product)
        {
            _editingProductId = product.Id;
            FormTitle.Text = "تعديل المنتج";
            ProductNameBox.Text = product.Name;
            ProductPriceBox.Text = product.Price.ToString("F2");
            CategoryCombo.SelectedValue = product.CategoryId;
            SaveProductBtn.Content = "تحديث المنتج";
        }
    }

    private void SaveProduct_Click(object sender, RoutedEventArgs e)
    {
        var name = ProductNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            CustomMessageBox.Show("الرجاء إدخال اسم المنتج", "تنبيه",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(ProductPriceBox.Text.Trim(), out var price) || price <= 0)
        {
            CustomMessageBox.Show("الرجاء إدخال سعر صحيح", "تنبيه",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (CategoryCombo.SelectedValue is not int categoryId)
        {
            CustomMessageBox.Show("الرجاء اختيار التصنيف", "تنبيه",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_editingProductId.HasValue)
            {
                ProductService.UpdateProduct(new Product
                {
                    Id = _editingProductId.Value,
                    Name = name,
                    Price = price,
                    CategoryId = categoryId,
                    IsActive = true
                });

                CustomMessageBox.Show("تم تحديث المنتج بنجاح", "تم ✓",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                ProductService.AddProduct(new Product
                {
                    Name = name,
                    Price = price,
                    CategoryId = categoryId,
                    IsActive = true
                });

                CustomMessageBox.Show("تم إضافة المنتج بنجاح", "تم ✓",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            ClearFormInternal();
            LoadProducts();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ToggleActive_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int productId)
        {
            ProductService.ToggleProductActive(productId);
            LoadProducts();
        }
    }

    private void DeleteProduct_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int productId)
        {
            var product = ProductsList.Items.OfType<Product>().FirstOrDefault(p => p.Id == productId);
            if (product == null) return;

            var result = CustomMessageBox.Show(
                $"هل أنت متأكد من حذف المنتج '{product.Name}'؟",
                "تأكيد الحذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    ProductService.DeleteProduct(productId);
                    LoadProducts();
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"حدث خطأ أثناء الحذف: {ex.Message}", "خطأ",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void EditProduct_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Product product)
        {
            ProductsList.SelectedItem = product;
        }
    }

    private void ClearForm_Click(object sender, RoutedEventArgs e)
    {
        ClearFormInternal();
    }

    private void ClearFormInternal()
    {
        _editingProductId = null;
        FormTitle.Text = "إضافة منتج جديد";
        ProductNameBox.Text = string.Empty;
        ProductPriceBox.Text = string.Empty;
        if (CategoryCombo.Items.Count > 0)
            CategoryCombo.SelectedIndex = 0;
        SaveProductBtn.Content = "حفظ المنتج";
        ProductsList.SelectedItem = null;
    }
}
