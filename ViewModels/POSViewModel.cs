using System.Collections.ObjectModel;
using System.Windows;
using System.Linq;
using System.Collections.Generic;
using System;
using CafePOS.Models;
using CafePOS.Services;
using CafePOS.Views;

namespace CafePOS.ViewModels;

public class CartItem : BaseViewModel
{
    private int _quantity = 1;

    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
                OnPropertyChanged(nameof(Subtotal));
        }
    }

    public decimal Subtotal => Price * Quantity;
}

public class POSViewModel : BaseViewModel
{
    private ObservableCollection<Product> _allProducts = new();
    private ObservableCollection<Product> _filteredProducts = new();
    private ObservableCollection<CartItem> _cartItems = new();
    private ObservableCollection<Category> _categories = new();
    private int? _selectedCategoryId = 0;
    private string _customerName = string.Empty;
    private bool _isDiscountApplied;
    private bool _isDiscountEnabled;
    private decimal _discountPercent;
    private string _lastOrderMessage = string.Empty;
    private string _printerStatusText = "جاري فحص الطابعة...";
    private bool _isPrinterOnline;
    private List<PaymentMethod> _paymentMethods = new();
    private PaymentMethod? _selectedPaymentMethod;

    public string PrinterStatusText
    {
        get => _printerStatusText;
        set => SetProperty(ref _printerStatusText, value);
    }

    public bool IsPrinterOnline
    {
        get => _isPrinterOnline;
        set => SetProperty(ref _isPrinterOnline, value);
    }

    public List<PaymentMethod> PaymentMethods
    {
        get => _paymentMethods;
        set => SetProperty(ref _paymentMethods, value);
    }

    public PaymentMethod? SelectedPaymentMethod
    {
        get => _selectedPaymentMethod;
        set => SetProperty(ref _selectedPaymentMethod, value);
    }

    public ObservableCollection<Product> FilteredProducts
    {
        get => _filteredProducts;
        set => SetProperty(ref _filteredProducts, value);
    }

    public ObservableCollection<CartItem> CartItems
    {
        get => _cartItems;
        set => SetProperty(ref _cartItems, value);
    }

    public ObservableCollection<Category> Categories
    {
        get => _categories;
        set => SetProperty(ref _categories, value);
    }

    public int? SelectedCategoryId
    {
        get => _selectedCategoryId;
        set
        {
            if (SetProperty(ref _selectedCategoryId, value))
                FilterProducts();
        }
    }

    public string CustomerName
    {
        get => _customerName;
        set => SetProperty(ref _customerName, value);
    }

    public bool IsDiscountApplied
    {
        get => _isDiscountApplied;
        set
        {
            if (SetProperty(ref _isDiscountApplied, value))
                RecalculateTotals();
        }
    }

    public bool IsDiscountEnabled
    {
        get => _isDiscountEnabled;
        set => SetProperty(ref _isDiscountEnabled, value);
    }

    public decimal DiscountPercent
    {
        get => _discountPercent;
        set => SetProperty(ref _discountPercent, value);
    }

    public decimal Subtotal => CartItems.Sum(i => i.Subtotal);

    public decimal DiscountAmount => IsDiscountApplied ? Math.Round(Subtotal * DiscountPercent / 100m, 2) : 0m;

    public decimal Total => Subtotal - DiscountAmount;

    public string LastOrderMessage
    {
        get => _lastOrderMessage;
        set => SetProperty(ref _lastOrderMessage, value);
    }

    public bool HasItems => CartItems.Count > 0;

    // Commands
    public RelayCommand AddToCartCommand { get; }
    public RelayCommand RemoveFromCartCommand { get; }
    public RelayCommand IncrementCommand { get; }
    public RelayCommand DecrementCommand { get; }
    public RelayCommand SelectCategoryCommand { get; }
    public RelayCommand ApplyDiscountCommand { get; }
    public RelayCommand CompleteOrderCommand { get; }
    public RelayCommand ClearCartCommand { get; }

    public POSViewModel()
    {
        AddToCartCommand = new RelayCommand(p => AddToCart(p as Product));
        RemoveFromCartCommand = new RelayCommand(p => RemoveFromCart(p as CartItem));
        IncrementCommand = new RelayCommand(p => IncrementQuantity(p as CartItem));
        DecrementCommand = new RelayCommand(p => DecrementQuantity(p as CartItem));
        SelectCategoryCommand = new RelayCommand(p =>
        {
            SelectedCategoryId = p is int catId ? catId : (int?)null;
        });
        ApplyDiscountCommand = RelayCommand.Create(ToggleDiscount);
        CompleteOrderCommand = RelayCommand.Create(CompleteOrder, () => HasItems);
        ClearCartCommand = RelayCommand.Create(ClearCart);

        LoadData();

        // Check printer status initially
        CheckPrinterStatus();

        // Check printer status periodically every 5 seconds
        var timer = new System.Windows.Threading.DispatcherTimer();
        timer.Interval = TimeSpan.FromSeconds(5);
        timer.Tick += (s, e) => CheckPrinterStatus();
        timer.Start();
    }

    private void CheckPrinterStatus()
    {
        var (isInstalled, isOnline, statusMessage) = PrintService.GetPrinterStatus();
        PrinterStatusText = statusMessage;
        IsPrinterOnline = isInstalled && isOnline;
    }

    public void LoadData()
    {
        LoadProducts();
        LoadCategories();
        LoadDiscountSettings();
        LoadPaymentMethods();
    }

    private void LoadPaymentMethods()
    {
        PaymentMethods = PaymentMethodService.GetAll();
        SelectedPaymentMethod = PaymentMethods.FirstOrDefault();
    }

    private void LoadProducts()
    {
        _allProducts = new ObservableCollection<Product>(ProductService.GetAllProducts());
        FilterProducts();
    }

    private void LoadCategories()
    {
        var cats = ProductService.GetAllCategories();
        var allCats = new List<Category> { new Category { Id = 0, Name = "الكل", IsActive = true } };
        allCats.AddRange(cats);
        Categories = new ObservableCollection<Category>(allCats);
    }

    private void LoadDiscountSettings()
    {
        IsDiscountEnabled = SettingsService.IsDiscountEnabled();
        DiscountPercent = SettingsService.GetDiscountPercent();
    }

    private void FilterProducts()
    {
        if (SelectedCategoryId == 0 || SelectedCategoryId == null)
        {
            FilteredProducts = new ObservableCollection<Product>(_allProducts);
        }
        else
        {
            FilteredProducts = new ObservableCollection<Product>(
                _allProducts.Where(p => p.CategoryId == SelectedCategoryId.Value)
            );
        }
    }

    private void AddToCart(Product? product)
    {
        if (product == null) return;

        var existing = CartItems.FirstOrDefault(i => i.ProductId == product.Id);
        if (existing != null)
        {
            existing.Quantity++;
        }
        else
        {
            CartItems.Add(new CartItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Price = product.Price,
                Quantity = 1
            });
        }

        RecalculateTotals();
    }

    private void RemoveFromCart(CartItem? item)
    {
        if (item == null) return;
        CartItems.Remove(item);
        RecalculateTotals();
    }

    private void IncrementQuantity(CartItem? item)
    {
        if (item == null) return;
        item.Quantity++;
        RecalculateTotals();
    }

    private void DecrementQuantity(CartItem? item)
    {
        if (item == null) return;
        if (item.Quantity <= 1)
        {
            CartItems.Remove(item);
        }
        else
        {
            item.Quantity--;
        }
        RecalculateTotals();
    }

    private void ToggleDiscount()
    {
        if (!IsDiscountEnabled) return;

        if (!IsDiscountApplied)
        {
            var result = CustomMessageBox.Show(
                $"سيتم تطبيق خصم {DiscountPercent}% على الفاتورة\nقيمة الخصم: {DiscountAmount:F2} ر.ي\n\nهل تريد المتابعة؟",
                "تطبيق الخصم",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
                IsDiscountApplied = true;
        }
        else
        {
            IsDiscountApplied = false;
        }
    }

    private void CompleteOrder()
    {
        if (!HasItems)
        {
            CustomMessageBox.Show("لا توجد أصناف في الفاتورة", "تنبيه",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var order = new Order
            {
                CustomerName = string.IsNullOrWhiteSpace(CustomerName) ? null : CustomerName,
                Subtotal = Subtotal,
                DiscountPercent = IsDiscountApplied ? DiscountPercent : 0,
                DiscountAmount = DiscountAmount,
                Total = Total,
                CashierId = AuthService.CurrentUser?.Id ?? 0,
                CashierName = (AuthService.CurrentUser != null) 
                    ? (AuthService.CurrentUser.Username + (string.IsNullOrWhiteSpace(AuthService.CurrentUser.FullName) ? "" : " - " + AuthService.CurrentUser.FullName)) 
                    : string.Empty,
                PaymentMethod = SelectedPaymentMethod?.Name,
                Items = CartItems.Select(ci => new OrderItem
                {
                    ProductId = ci.ProductId,
                    ProductName = ci.ProductName,
                    Price = ci.Price,
                    Quantity = ci.Quantity,
                    Subtotal = ci.Subtotal
                }).ToList()
            };

            var savedOrder = OrderService.CreateOrder(order);

            // Print receipt (non-blocking on failure)
            var printWarning = "";
            try
            {
                var printed = PrintService.PrintReceipt(savedOrder);
                if (!printed)
                    printWarning = "\n⚠ تعذرت الطباعة: لم تجد الطابعة أو لم تستجب";
            }
            catch (Exception printEx)
            {
                printWarning = $"\n⚠ فشلت الطباعة: {printEx.Message}";
            }

            LastOrderMessage = $"✓ تم إنهاء الطلب رقم {savedOrder.OrderNumber} | الفاتورة: {savedOrder.InvoiceNumber}";

            CustomMessageBox.Show(
                $"تم إنهاء الطلب بنجاح!\n\nرقم الطلب: {savedOrder.OrderNumber}\nرقم الفاتورة: {savedOrder.InvoiceNumber}\nالإجمالي: {savedOrder.Total:F2} ر.ي{printWarning}",
                string.IsNullOrEmpty(printWarning) ? "تم بنجاح ✓" : "تم بنجاح مع ملاحظة",
                MessageBoxButton.OK,
                string.IsNullOrEmpty(printWarning) ? MessageBoxImage.Information : MessageBoxImage.Warning);

            // Clear cart for next order
            ClearCartInternal();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"حدث خطأ أثناء حفظ الطلب:\n{ex.Message}", "خطأ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearCart()
    {
        if (!HasItems) return;

        var result = CustomMessageBox.Show("هل تريد إلغاء جميع الأصناف من الفاتورة؟", "تأكيد الإلغاء",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            ClearCartInternal();
    }

    private void ClearCartInternal()
    {
        CartItems.Clear();
        CustomerName = string.Empty;
        IsDiscountApplied = false;
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(DiscountAmount));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(HasItems));
    }
}
