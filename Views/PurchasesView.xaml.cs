using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.IO;
using Microsoft.Win32;
using CafePOS.Models;
using CafePOS.Services;
using CafePOS.ViewModels;
using System.Linq;

namespace CafePOS.Views;

public partial class PurchasesView : UserControl
{
    private List<Product> _products = new();
    private List<CartPurchaseItem> _cart = new();
    private List<string> _selectedFiles = new();
    private List<string> _selectedFileNames = new();

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

    private void AttachFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = "اختيار سند الشراء",
            Filter = "الملفات المدعومة (*.jpg;*.jpeg;*.png;*.pdf;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.pdf;*.bmp;*.gif|جميع الملفات (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            _selectedFiles = dialog.FileNames.ToList();
            _selectedFileNames = dialog.SafeFileNames.ToList();
            AttachedFilesList.ItemsSource = _selectedFileNames;
        }
    }

    private void OpenAttachment_Click(object sender, RoutedEventArgs e)
    {
        var hyperlink = (Hyperlink)sender;
        var fileName = (string)hyperlink.DataContext;
        var paths = SavedAttachmentsList.Tag as string;
        if (string.IsNullOrEmpty(paths)) return;

        var allPaths = paths.Split('|');
        var allNames = ((System.Collections.IList)SavedAttachmentsList.ItemsSource).Cast<string>().ToList();

        for (int i = 0; i < allNames.Count; i++)
        {
            if (allNames[i] == fileName && i < allPaths.Length && File.Exists(allPaths[i]))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = allPaths[i],
                        UseShellExecute = true
                    });
                }
                catch { }
                break;
            }
        }
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
            ExternalInvoiceNumber = ExternalInvoiceBox.Text.Trim(),
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
            // Copy files to Attachments folder before saving
            if (_selectedFiles.Count > 0)
            {
                var attachDir = CafePOS.Helpers.AppPaths.GetPath("Attachments");
                Directory.CreateDirectory(attachDir);

                var savedPaths = new List<string>();
                var savedNames = new List<string>();

                foreach (var filePath in _selectedFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var ext = Path.GetExtension(filePath);
                    var uniqueName = $"{fileName}_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
                    var destPath = Path.Combine(attachDir, uniqueName);

                    File.Copy(filePath, destPath, overwrite: true);
                    savedPaths.Add(destPath);
                    savedNames.Add(Path.GetFileName(filePath));
                }

                purchase.AttachmentPath = string.Join("|", savedPaths);
                purchase.AttachmentFileName = string.Join("|", savedNames);
            }

            PurchaseService.CreatePurchase(purchase);

            CustomMessageBox.Show($"تم حفظ فاتورة المشتريات بنجاح\nرقم الفاتورة: {purchase.InvoiceNumber}\nالإجمالي: {purchase.Total:F2}", "تم ✓",
                MessageBoxButton.OK, MessageBoxImage.Information);

            // Show saved attachments
            if (!string.IsNullOrEmpty(purchase.AttachmentFileName))
            {
                var names = purchase.AttachmentFileName.Split('|').ToList();
                SavedAttachmentsList.ItemsSource = names;
                SavedAttachmentsList.Tag = purchase.AttachmentPath;
                SavedAttachmentsList.Visibility = Visibility.Visible;
            }

            // Clear cart
            _cart.Clear();
            SupplierBox.Clear();
            ExternalInvoiceBox.Clear();
            NotesBox.Clear();
            _selectedFiles.Clear();
            _selectedFileNames.Clear();
            AttachedFilesList.ItemsSource = null;
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
