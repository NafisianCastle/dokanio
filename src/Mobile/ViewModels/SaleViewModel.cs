using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using Shared.Core.DTOs;
using Mobile.Services;
using AppPermissions = Mobile.Services.Permissions;

namespace Mobile.ViewModels;

public partial class SaleViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IEnhancedSalesService _enhancedSalesService;
    private readonly IProductService _productService;
    private readonly IPrinterService _printerService;
    private readonly IReceiptService _receiptService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserContextService _userContextService;
    private readonly IBusinessManagementService _businessManagementService;

    // Event for haptic feedback
    public event EventHandler<HapticFeedbackEventArgs>? HapticFeedbackRequested;

    public SaleViewModel(
        IEnhancedSalesService enhancedSalesService,
        IProductService productService,
        IPrinterService printerService,
        IReceiptService receiptService,
        ICurrentUserService currentUserService,
        IUserContextService userContextService,
        IBusinessManagementService businessManagementService)
    {
        _enhancedSalesService = enhancedSalesService;
        _productService = productService;
        _printerService = printerService;
        _receiptService = receiptService;
        _currentUserService = currentUserService;
        _userContextService = userContextService;
        _businessManagementService = businessManagementService;
        
        Title = "New Sale";
        SaleItems = new ObservableCollection<SaleItemViewModel>();
        PaymentMethods = Enum.GetValues<PaymentMethod>().ToList();
        SelectedPaymentMethod = PaymentMethod.Cash;
    }

    [ObservableProperty]
    private ObservableCollection<SaleItemViewModel> saleItems;

    [ObservableProperty]
    private decimal subtotal;

    [ObservableProperty]
    private decimal discountAmount;

    [ObservableProperty]
    private decimal taxAmount;

    [ObservableProperty]
    private decimal totalAmount;

    [ObservableProperty]
    private List<PaymentMethod> paymentMethods;

    [ObservableProperty]
    private PaymentMethod selectedPaymentMethod;

    [ObservableProperty]
    private string invoiceNumber = string.Empty;

    [ObservableProperty]
    private bool canCompleteSale;

    [ObservableProperty]
    private string businessName = string.Empty;

    [ObservableProperty]
    private string shopName = string.Empty;

    [ObservableProperty]
    private BusinessType businessType;

    [ObservableProperty]
    private bool showBusinessTypeFeatures;

    [ObservableProperty]
    private bool enableExpiryValidation;

    [ObservableProperty]
    private bool enableWeightBasedPricing;

    private Sale? _currentSale;
    private ShopConfiguration? _shopConfiguration;

    [RelayCommand]
    private async Task AddProduct(Product product)
    {
        if (product == null) return;

        try
        {
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
            
            // Validate product for sale based on business type
            var validationResult = await _enhancedSalesService.ValidateProductForSaleAsync(
                product.Id, 
                _userContextService.CurrentShop?.Id ?? Guid.Empty);

            if (!validationResult.IsValid)
            {
                var errorMessage = validationResult.Errors.FirstOrDefault() ?? "Product validation failed";
                SetError(errorMessage);
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
                return;
            }

            // Check if product already exists in sale
            var existingItem = SaleItems.FirstOrDefault(si => si.ProductId == product.Id);
            
            if (existingItem != null)
            {
                existingItem.Quantity++;
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
            }
            else
            {
                var saleItemViewModel = new SaleItemViewModel
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.UnitPrice,
                    Quantity = 1,
                    BatchNumber = product.BatchNumber,
                    ExpiryDate = product.ExpiryDate,
                    Weight = product.IsWeightBased ? 1m : null,
                    IsWeightBased = product.IsWeightBased
                };
                
                SaleItems.Add(saleItemViewModel);
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
            }

            await CalculateTotal();
        }
        catch (Exception ex)
        {
            SetError($"Failed to add product: {ex.Message}");
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
        }
    }

    [RelayCommand]
    private async Task RemoveItem(SaleItemViewModel item)
    {
        if (item == null) return;

        TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
        SaleItems.Remove(item);
        await CalculateTotal();
    }

    [RelayCommand]
    private async Task UpdateQuantity(SaleItemViewModel item)
    {
        if (item == null || item.Quantity <= 0)
        {
            await RemoveItem(item);
            return;
        }

        await CalculateTotal();
    }

    [RelayCommand]
    private async Task CompleteSale()
    {
        if (IsBusy || !CanCompleteSale) return;

        try
        {
            IsBusy = true;
            ClearError();
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);

            var user = _currentUserService.CurrentUser;
            var shop = _userContextService.CurrentShop;
            
            if (user == null || shop == null)
            {
                SetError("User or shop context not available");
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
                return;
            }

            // Create sale using enhanced sales service
            _currentSale = await _enhancedSalesService.CreateSaleWithValidationAsync(shop.Id, user.Id, InvoiceNumber);

            // Add items to sale
            foreach (var item in SaleItems)
            {
                await _enhancedSalesService.AddItemToSaleAsync(
                    _currentSale.Id,
                    item.ProductId,
                    item.Quantity,
                    item.UnitPrice,
                    item.BatchNumber);
            }

            // Calculate with business rules
            var calculationResult = await _enhancedSalesService.CalculateWithBusinessRulesAsync(_currentSale);
            
            if (calculationResult == null)
            {
                SetError("Sale calculation failed");
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
                return;
            }

            // Complete sale
            await _enhancedSalesService.CompleteSaleAsync(_currentSale.Id, SelectedPaymentMethod);

            // Print receipt
            await PrintReceipt();

            // Clear sale
            await ClearSale();

            // Success feedback
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
            await Shell.Current.DisplayAlert("Success", "Sale completed successfully!", "OK");
        }
        catch (Exception ex)
        {
            SetError($"Failed to complete sale: {ex.Message}");
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearSale()
    {
        TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
        SaleItems.Clear();
        Subtotal = 0;
        DiscountAmount = 0;
        TaxAmount = 0;
        TotalAmount = 0;
        InvoiceNumber = GenerateInvoiceNumber();
        _currentSale = null;
        CanCompleteSale = false;
    }

    [RelayCommand]
    private async Task ScanBarcode()
    {
        TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
        await Shell.Current.GoToAsync("//scanner");
    }

    [RelayCommand]
    private async Task IncreaseQuantity(SaleItemViewModel item)
    {
        if (item == null) return;

        TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
        item.Quantity++;
        await CalculateTotal();
    }

    [RelayCommand]
    private async Task DecreaseQuantity(SaleItemViewModel item)
    {
        if (item == null) return;

        TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
        
        if (item.Quantity > 1)
        {
            item.Quantity--;
            await CalculateTotal();
        }
        else
        {
            await RemoveItem(item);
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
        await Initialize();
    }

    private void TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType feedbackType)
    {
        HapticFeedbackRequested?.Invoke(this, new HapticFeedbackEventArgs(feedbackType));
    }

    [RelayCommand]
    private async Task ApplyDiscount(SaleItemViewModel item)
    {
        if (item == null) return;

        TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);

        // Check if user has permission to apply discounts
        var hasPermission = await _userContextService.HasPermissionAsync(AppPermissions.ApplyDiscounts);
        if (!hasPermission)
        {
            SetError("You don't have permission to apply discounts");
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
            return;
        }

        // Show discount dialog (simplified for this implementation)
        var discountPercentage = await Shell.Current.DisplayPromptAsync(
            "Apply Discount", 
            "Enter discount percentage (0-100):", 
            "OK", 
            "Cancel", 
            "0", 
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(discountPercentage))
            return;

        if (decimal.TryParse(
                discountPercentage,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var discount) && discount >= 0 && discount <= 100)
        {
            item.DiscountPercentage = discount;
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
            await CalculateTotal();
        }
        else
        {
            SetError("Invalid discount percentage");
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
        }
    }

    private async Task PrintReceipt()
    {
        if (_currentSale == null) return;

        try
        {
            var isConnected = await _printerService.IsConnectedAsync();
            if (!isConnected)
            {
                await Shell.Current.DisplayAlert("Warning", "Printer not connected. Receipt not printed.", "OK");
                return;
            }

            var result = await _printerService.PrintReceiptAsync(_currentSale);
            if (!result.Success)
            {
                await Shell.Current.DisplayAlert("Warning", $"Failed to print receipt: {result.Message}", "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Warning", $"Print error: {ex.Message}", "OK");
        }
    }

    private async Task CalculateTotal()
    {
        try
        {
            Subtotal = SaleItems.Sum(item => item.LineTotal);
            
            // Apply business-specific calculations if we have a current sale
            if (_currentSale != null)
            {
                var calculationResult = await _enhancedSalesService.CalculateWithBusinessRulesAsync(_currentSale);
                if (calculationResult != null)
                {
                    DiscountAmount = calculationResult.DiscountAmount;
                    TaxAmount = calculationResult.TaxAmount;
                    TotalAmount = calculationResult.FinalTotal;
                }
                else
                {
                    // Fallback to simple calculation
                    CalculateSimpleTotal();
                }
            }
            else
            {
                CalculateSimpleTotal();
            }

            CanCompleteSale = SaleItems.Any() && TotalAmount > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Calculation error: {ex.Message}");
            CalculateSimpleTotal();
        }
    }

    private void CalculateSimpleTotal()
    {
        Subtotal = SaleItems.Sum(item => item.LineTotal);
        DiscountAmount = SaleItems.Sum(item => item.DiscountAmount);
        
        // Apply default tax rate from shop configuration
        var taxRate = _shopConfiguration?.TaxRate ?? 0.0m;
        TaxAmount = (Subtotal - DiscountAmount) * taxRate;
        
        TotalAmount = Subtotal - DiscountAmount + TaxAmount;
        CanCompleteSale = SaleItems.Any() && TotalAmount > 0;
    }

    private string GenerateInvoiceNumber()
    {
        var shopName = _userContextService.CurrentShop?.Name?.Trim();
        var shopPrefix = !string.IsNullOrEmpty(shopName)
            ? shopName.Substring(0, Math.Min(3, shopName.Length)).ToUpperInvariant()
            : "POS";
        return $"{shopPrefix}-{DateTime.Now:yyyyMMdd}-{DateTime.Now.Ticks % 10000:D4}";
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey("SelectedProduct") && query["SelectedProduct"] is Product product)
        {
            _ = Task.Run(async () => await AddProduct(product));
        }
        
        if (query.ContainsKey("ScannedBarcode") && query["ScannedBarcode"] is string barcode)
        {
            _ = Task.Run(async () => await AddProductByBarcode(barcode));
        }
    }

    private async Task AddProductByBarcode(string barcode)
    {
        try
        {
            var product = await _productService.GetProductByBarcodeAsync(barcode);
            if (product != null)
            {
                await AddProduct(product);
            }
            else
            {
                SetError($"Product not found for barcode: {barcode}");
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to find product: {ex.Message}");
        }
    }

    public async Task Initialize()
    {
        try
        {
            // Load business and shop context
            var business = _userContextService.CurrentBusiness;
            var shop = _userContextService.CurrentShop;

            if (business != null)
            {
                BusinessName = business.Name;
                BusinessType = business.Type;
                ShowBusinessTypeFeatures = true;
                
                // Enable business type-specific features
                EnableExpiryValidation = BusinessType == BusinessType.Pharmacy;
                EnableWeightBasedPricing = BusinessType == BusinessType.Grocery || BusinessType == BusinessType.SuperShop;
            }

            if (shop != null)
            {
                ShopName = shop.Name;
                
                // Load shop configuration
                _shopConfiguration = await _businessManagementService.GetShopConfigurationAsync(shop.Id);
            }

            InvoiceNumber = GenerateInvoiceNumber();
            await ClearSale();
        }
        catch (Exception ex)
        {
            SetError($"Failed to initialize sale: {ex.Message}");
        }
    }
}

public partial class SaleItemViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid productId;

    [ObservableProperty]
    private string productName = string.Empty;

    [ObservableProperty]
    private decimal unitPrice;

    [ObservableProperty]
    private int quantity;

    [ObservableProperty]
    private string? batchNumber;

    [ObservableProperty]
    private DateTime? expiryDate;

    [ObservableProperty]
    private decimal? weight;

    [ObservableProperty]
    private bool isWeightBased;

    [ObservableProperty]
    private decimal discountPercentage;

    public decimal LineTotal => Quantity * UnitPrice * (IsWeightBased ? Math.Max(Weight ?? 1m, 0m) : 1m);
    
    public decimal DiscountAmount => LineTotal * (DiscountPercentage / 100);
    
    public decimal NetTotal => LineTotal - DiscountAmount;

    partial void OnQuantityChanged(int value)
    {
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(DiscountAmount));
        OnPropertyChanged(nameof(NetTotal));
    }

    partial void OnUnitPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(DiscountAmount));
        OnPropertyChanged(nameof(NetTotal));
    }

    partial void OnWeightChanged(decimal? value)
    {
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(DiscountAmount));
        OnPropertyChanged(nameof(NetTotal));
    }

    partial void OnDiscountPercentageChanged(decimal value)
    {
        OnPropertyChanged(nameof(DiscountAmount));
        OnPropertyChanged(nameof(NetTotal));
    }
}

// Event args for haptic feedback
public class HapticFeedbackEventArgs : EventArgs
{
    public Microsoft.Maui.Devices.HapticFeedbackType FeedbackType { get; set; }
    
    public HapticFeedbackEventArgs(Microsoft.Maui.Devices.HapticFeedbackType feedbackType)
    {
        FeedbackType = feedbackType;
    }
}