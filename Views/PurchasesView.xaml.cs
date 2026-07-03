using System.Windows;
using System.Windows.Controls;
using CafePOS.Models;
using CafePOS.Services;
using CafePOS.ViewModels;
using System.Linq;

namespace CafePOS.Views;

public partial class PurchasesView : UserControl
{
    private List<Product> _products = new();
    private List<CartPurchaseItem> _cart = new();

    public PurchasesView()
    {
        InitializeComponent();
        LoadProducts();
    }

    public void Refresh()
    {
        LoadProducts();
    }

    private void AddNewProduct_Click(object sender, RoutedEventArgs e)
    {
        var name = NewProductName.Text.Trim();
        if (string.IsNullOrEmpty(name) || name == "اسم المنتج")
        {
            CustomMessageBox.Show("الرجاء إدخال اسم المنتج", "تنبيه",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(NewProductPrice.Text.Trim(), out var price) || price <= 0)
        {
            CustomMessageBox.Show("الرجاء إدخال سعر تكلفة صحيح", "تنبيه",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Get or create default category
        var categories = ProductService.GetAllCategories();
        var defaultCat = categories.FirstOrDefault()?.Id ?? 0;
        if (defaultCat == 0)
        {
            defaultCat = ProductService.AddCategory(new Category { Name = "عام", SortOrder = 0, IsActive = true }).Id;
        }

        var product = ProductService.AddProduct(new Product
        {
            Name = name,
            Price = price, // cost price (product won't appear in sales)
            CategoryId = defaultCat,
            IsActive = true,
            SortOrder = 0,
            IsPurchaseOnly = true
        });

        // Add directly to cart with cost price
        _cart.Add(new CartPurchaseItem
        {
            ProductId = product.Id,
            ProductName = product.Name,
            CostPrice = price,
            Quantity = 1,
            Subtotal = price
        });

        // Refresh products list
        LoadProducts();
        UpdateTotal();

        // Reset inputs
        NewProductName.Text = "اسم المنتج";
        NewProductPrice.Text = "0";
    }

    private void LoadProducts()
    {
        _products = ProductService.GetAllPurchaseProducts();
        ProductsList.ItemsSource = _products;
    }

    private void UpdateTotal()
    {
        TotalText.Text = _cart.Sum(c => c.Subtotal).ToString("F2");
        CartList.ItemsSource = null;
        CartList.ItemsSource = _cart;
    }

    private void ProductCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var border = (Border)sender;
        var product = (Product)border.DataContext;

        var existing = _cart.FirstOrDefault(c => c.ProductId == product.Id);
        if (existing != null)
        {
            existing.Quantity++;
            existing.Subtotal = existing.CostPrice * existing.Quantity;
        }
        else
        {
            var costPrice = product.IsPurchaseOnly ? product.Price : product.Price * 0.5m;
            _cart.Add(new CartPurchaseItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                CostPrice = costPrice,
                Quantity = 1,
                Subtotal = costPrice
            });
        }

        UpdateTotal();
    }

    private void Increment_Click(object sender, RoutedEventArgs e)
    {
        var item = (CartPurchaseItem)((Button)sender).DataContext;
        item.Quantity++;
        item.Subtotal = item.CostPrice * item.Quantity;
        UpdateTotal();
    }

    private void Decrement_Click(object sender, RoutedEventArgs e)
    {
        var item = (CartPurchaseItem)((Button)sender).DataContext;
        if (item.Quantity > 1)
        {
            item.Quantity--;
            item.Subtotal = item.CostPrice * item.Quantity;
            UpdateTotal();
        }
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        var item = (CartPurchaseItem)((Button)sender).DataContext;
        _cart.Remove(item);
        UpdateTotal();
    }

    private void SavePurchase_Click(object sender, RoutedEventArgs e)
    {
        if (_cart.Count == 0)
        {
            CustomMessageBox.Show("لم يتم اختيار أي منتجات", "تنبيه",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var user = AuthService.CurrentUser;
        if (user == null) return;

        var purchase = new Purchase
        {
            SupplierName = SupplierBox.Text.Trim(),
            Notes = NotesBox.Text.Trim(),
            Total = _cart.Sum(c => c.Subtotal),
            CreatedBy = user.Id,
            CreatedAt = DateTime.Now,
            Items = _cart.Select(c => new PurchaseItem
            {
                ProductId = c.ProductId,
                ProductName = c.ProductName,
                CostPrice = c.CostPrice,
                Quantity = c.Quantity,
                Subtotal = c.Subtotal
            }).ToList()
        };

        try
        {
            PurchaseService.CreatePurchase(purchase);
            CustomMessageBox.Show($"تم حفظ فاتورة المشتريات بنجاح\nرقم الفاتورة: {purchase.InvoiceNumber}\nالإجمالي: {purchase.Total:F2}", "تم ✓",
                MessageBoxButton.OK, MessageBoxImage.Information);

            // Clear cart
            _cart.Clear();
            SupplierBox.Clear();
            NotesBox.Clear();
            UpdateTotal();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ أثناء حفظ المشتريات:\n{ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

public class CartPurchaseItem : BaseViewModel
{
    private int _qty;
    private decimal _costPrice;
    private decimal _subtotal;

    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;

    public decimal CostPrice
    {
        get => _costPrice;
        set { _costPrice = value; _subtotal = value * _qty; OnPropertyChanged(); OnPropertyChanged(nameof(Subtotal)); }
    }

    public int Quantity
    {
        get => _qty;
        set { _qty = value; _subtotal = CostPrice * value; OnPropertyChanged(); OnPropertyChanged(nameof(Subtotal)); }
    }

    public decimal Subtotal
    {
        get => _subtotal;
        set { _subtotal = value; OnPropertyChanged(); }
    }
}
