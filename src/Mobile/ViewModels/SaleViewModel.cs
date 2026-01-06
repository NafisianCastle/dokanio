using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using Shared.Core.DTOs;
using Mobile.Services;
using System.Globalization;
using Microsoft.Maui.Authentication;
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
    private readonly IMultiTabSalesManager _multiTabSalesManager;
    private readonly ICustomerLookupService _customerLookupService;
    private readonly IBarcodeIntegrationService _barcodeIntegrationService;

    // Event for haptic feedback
    public event EventHandler<HapticFeedbackEventArgs>? HapticFeedbackRequested;

    public SaleViewModel(
        IEnhancedSalesService enhancedSalesService,
        IProductService productService,
        IPrinterService printerService,
        IReceiptService receiptService,
        ICurrentUserService currentUserService,
        IUserContextService userContextService,
        IBusinessManagementService businessManagementService,
        IMultiTabSalesManager multiTabSalesManager,
        ICustomerLookupService customerLookupService,
        IBarcodeIntegrationService barcodeIntegrationService)
    {
        _enhancedSalesService = enhancedSalesService;
        _productService = productService;
        _printerService = printerService;
        _receiptService = receiptService;
        _currentUserService = currentUserService;
        _userContextService = userContextService;
        _businessManagementService = businessManagementService;
        _multiTabSalesManager = multiTabSalesManager;
        _customerLookupService = customerLookupService;
        _barcodeIntegrationService = barcodeIntegrationService;
        
        Title = "New Sale";
        SaleItems = new ObservableCollection<SaleItemViewModel>();
        PaymentMethods = Enum.GetValues<PaymentMethod>().ToList();
        SelectedPaymentMethod = PaymentMethod.Cash;
        
        // Initialize mobile-specific properties
        IsTabManagementEnabled = true;
        IsBarcodeIntegrationEnabled = true;
        IsCustomerLookupEnabled = true;
        
        // Subscribe to barcode events
        _barcodeIntegrationService.BarcodeProcessed += OnBarcodeProcessed;
        _barcodeIntegrationService.ScanError += OnBarcodeScanError;
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

    // Mobile-specific properties for enhanced functionality
    [ObservableProperty]
    private bool isTabManagementEnabled;

    [ObservableProperty]
    private bool isBarcodeIntegrationEnabled;

    [ObservableProperty]
    private bool isCustomerLookupEnabled;

    [ObservableProperty]
    private string customerMobileNumber = string.Empty;

    [ObservableProperty]
    private CustomerLookupResult? currentCustomer;

    [ObservableProperty]
    private bool isCustomerLookupInProgress;

    [ObservableProperty]
    private string customerLookupStatus = string.Empty;

    [ObservableProperty]
    private bool showCustomerDetails;

    [ObservableProperty]
    private Guid? currentSessionId;

    [ObservableProperty]
    private string sessionTabName = string.Empty;

    [ObservableProperty]
    private bool isBarcodeScanning;

    [ObservableProperty]
    private string barcodeScanStatus = string.Empty;

    [ObservableProperty]
    private bool showBarcodeScanner;

    [ObservableProperty]
    private bool enableHapticFeedback = true;

    [ObservableProperty]
    private bool isTouchOptimized = true;

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
        if (!IsBarcodeIntegrationEnabled)
        {
            SetError("Barcode scanning is not available");
            return;
        }

        try
        {
            IsBarcodeScanning = true;
            BarcodeScanStatus = "Initializing scanner...";
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);

            // Initialize barcode service if needed
            var isInitialized = await _barcodeIntegrationService.InitializeAsync();
            if (!isInitialized)
            {
                SetError("Failed to initialize barcode scanner");
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
                return;
            }

            BarcodeScanStatus = "Ready to scan...";
            ShowBarcodeScanner = true;

            // Configure scan options for mobile
            var scanOptions = new ScanOptions
            {
                ShopId = _userContextService.CurrentShop?.Id ?? Guid.Empty,
                SessionId = CurrentSessionId,
                EnableContinuousMode = false,
                ScanTimeout = TimeSpan.FromSeconds(30),
                EnableBeep = true,
                EnableVibration = EnableHapticFeedback,
                AutoAddToSale = true
            };

            // Perform scan
            var result = await _barcodeIntegrationService.ScanBarcodeAsync(scanOptions);
            
            if (result.IsSuccess && result.Product != null)
            {
                BarcodeScanStatus = $"Found: {result.Product.Name}";
                await AddProduct(result.Product);
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
            }
            else
            {
                BarcodeScanStatus = result.ErrorMessage ?? "Scan failed";
                SetError(result.ErrorMessage ?? "Failed to scan barcode");
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
            }
        }
        catch (Exception ex)
        {
            SetError($"Barcode scan error: {ex.Message}");
            BarcodeScanStatus = "Scan failed";
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
        }
        finally
        {
            IsBarcodeScanning = false;
            ShowBarcodeScanner = false;
            
            // Clear status after delay
            await Task.Delay(3000);
            if (BarcodeScanStatus != "Ready to scan...")
            {
                BarcodeScanStatus = string.Empty;
            }
        }
    }

    [RelayCommand]
    private async Task LookupCustomer()
    {
        if (!IsCustomerLookupEnabled || string.IsNullOrWhiteSpace(CustomerMobileNumber))
        {
            return;
        }

        try
        {
            IsCustomerLookupInProgress = true;
            CustomerLookupStatus = "Looking up customer...";
            ClearError();
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);

            // Validate mobile number format first
            var validationResult = await _customerLookupService.ValidateMobileNumberAsync(CustomerMobileNumber);
            if (!validationResult.IsValid)
            {
                SetError(validationResult.ErrorMessage ?? "Invalid mobile number format");
                CustomerLookupStatus = "Invalid number format";
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
                return;
            }

            // Perform customer lookup
            var customer = await _customerLookupService.LookupByMobileNumberAsync(CustomerMobileNumber);
            
            if (customer != null)
            {
                CurrentCustomer = customer;
                CustomerLookupStatus = $"Found: {customer.Name}";
                ShowCustomerDetails = true;
                
                // Apply membership discounts if available
                if (customer.AvailableDiscounts?.Any() == true)
                {
                    await ApplyMembershipDiscounts(customer.AvailableDiscounts);
                }
                
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
            }
            else
            {
                CustomerLookupStatus = "Customer not found";
                ShowCustomerDetails = false;
                
                // Offer to create new customer
                var shouldCreate = await Shell.Current.DisplayAlert(
                    "Customer Not Found", 
                    $"No customer found with mobile number {CustomerMobileNumber}. Would you like to create a new customer?", 
                    "Create", 
                    "Cancel");
                
                if (shouldCreate)
                {
                    await CreateNewCustomer();
                }
                else
                {
                    TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
                }
            }
        }
        catch (Exception ex)
        {
            SetError($"Customer lookup failed: {ex.Message}");
            CustomerLookupStatus = "Lookup failed";
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
        }
        finally
        {
            IsCustomerLookupInProgress = false;
            
            // Clear status after delay
            await Task.Delay(3000);
            if (CustomerLookupStatus != "Looking up customer...")
            {
                CustomerLookupStatus = string.Empty;
            }
        }
    }

    [RelayCommand]
    private async Task CreateNewCustomer()
    {
        try
        {
            var customerName = await Shell.Current.DisplayPromptAsync(
                "New Customer", 
                "Enter customer name:", 
                "Create", 
                "Cancel", 
                placeholder: "Customer Name");

            if (string.IsNullOrWhiteSpace(customerName))
                return;

            var email = await Shell.Current.DisplayPromptAsync(
                "Customer Email", 
                "Enter customer email (optional):", 
                "OK", 
                "Skip", 
                placeholder: "email@example.com",
                keyboard: Keyboard.Email);

            var request = new CustomerCreationRequest
            {
                Name = customerName,
                MobileNumber = CustomerMobileNumber,
                Email = string.IsNullOrWhiteSpace(email) ? null : email,
                ShopId = _userContextService.CurrentShop?.Id ?? Guid.Empty,
                InitialTier = MembershipTier.Bronze
            };

            var result = await _customerLookupService.CreateNewCustomerAsync(request);
            
            if (result.Success && result.Customer != null)
            {
                CurrentCustomer = result.Customer;
                CustomerLookupStatus = $"Created: {result.Customer.Name}";
                ShowCustomerDetails = true;
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
                
                await Shell.Current.DisplayAlert("Success", "New customer created successfully!", "OK");
            }
            else
            {
                SetError(result.ErrorMessage ?? "Failed to create customer");
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to create customer: {ex.Message}");
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
        }
    }

    [RelayCommand]
    private async Task ClearCustomer()
    {
        CurrentCustomer = null;
        CustomerMobileNumber = string.Empty;
        CustomerLookupStatus = string.Empty;
        ShowCustomerDetails = false;
        TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
        
        // Recalculate totals without membership discounts
        await CalculateTotal();
    }

    [RelayCommand]
    private async Task ToggleHapticFeedback()
    {
        EnableHapticFeedback = !EnableHapticFeedback;
        
        if (EnableHapticFeedback)
        {
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
        }
    }

    [RelayCommand]
    private async Task QuickAddQuantity(SaleItemViewModel item)
    {
        if (item == null) return;

        try
        {
            var quantityStr = await Shell.Current.DisplayPromptAsync(
                "Update Quantity", 
                $"Enter quantity for {item.ProductName}:", 
                "Update", 
                "Cancel", 
                item.Quantity.ToString(),
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(quantityStr))
                return;

            if (int.TryParse(quantityStr, out var newQuantity) && newQuantity > 0)
            {
                item.Quantity = newQuantity;
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
                await CalculateTotal();
            }
            else
            {
                SetError("Invalid quantity entered");
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to update quantity: {ex.Message}");
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
        }
    }

    [RelayCommand]
    private async Task SwipeToRemoveItem(SaleItemViewModel item)
    {
        if (item == null) return;

        var shouldRemove = await Shell.Current.DisplayAlert(
            "Remove Item", 
            $"Remove {item.ProductName} from sale?", 
            "Remove", 
            "Cancel");

        if (shouldRemove)
        {
            await RemoveItem(item);
        }
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
        if (!EnableHapticFeedback) return;
        
        try
        {
            HapticFeedback.Default.Perform(feedbackType);
            HapticFeedbackRequested?.Invoke(this, new HapticFeedbackEventArgs(feedbackType));
        }
        catch
        {
            // Haptic feedback not available on this device
        }
    }

    private void OnBarcodeProcessed(object? sender, BarcodeProcessedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (e.Product != null)
                {
                    BarcodeScanStatus = $"Added: {e.Product.Name}";
                    await AddProduct(e.Product);
                    TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
                }
            }
            catch (Exception ex)
            {
                SetError($"Failed to process scanned barcode: {ex.Message}");
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
            }
        });
    }

    private void OnBarcodeScanError(object? sender, ScanErrorEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SetError(e.ErrorMessage ?? "Barcode scan error");
            BarcodeScanStatus = "Scan failed";
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
        });
    }

    private async Task ApplyMembershipDiscounts(List<MembershipDiscount> discounts)
    {
        try
        {
            foreach (var discount in discounts)
            {
                foreach (var item in SaleItems)
                {
                    var discountAmount = item.LineTotal * (discount.DiscountPercentage / 100);
                    item.DiscountPercentage = Math.Max(item.DiscountPercentage, discount.DiscountPercentage);
                }
            }
            
            await CalculateTotal();
        }
        catch (Exception ex)
        {
            SetError($"Failed to apply membership discounts: {ex.Message}");
        }
    }

    // Mobile-specific initialization for tab management
    public async Task InitializeForSession(Guid sessionId, string tabName)
    {
        try
        {
            CurrentSessionId = sessionId;
            SessionTabName = tabName;
            
            // Load session data if it exists
            var sessionData = await _multiTabSalesManager.GetSaleSessionAsync(sessionId);
            if (sessionData != null)
            {
                await LoadFromSessionData(sessionData);
            }
            
            await Initialize();
        }
        catch (Exception ex)
        {
            SetError($"Failed to initialize session: {ex.Message}");
        }
    }

    private async Task LoadFromSessionData(SaleSessionDto sessionData)
    {
        try
        {
            // Clear existing items
            SaleItems.Clear();
            
            // Load items from session
            foreach (var item in sessionData.Items)
            {
                var saleItemViewModel = new SaleItemViewModel
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = (int)item.Quantity,
                    UnitPrice = item.UnitPrice,
                    BatchNumber = item.BatchNumber,
                    Weight = item.Weight,
                    IsWeightBased = item.IsWeightBased,
                    DiscountPercentage = (item.DiscountAmount > 0 && item.LineTotal > 0)
                        ? (item.DiscountAmount / item.LineTotal) * 100
                        : 0
                };
                
                SaleItems.Add(saleItemViewModel);
            }

            // Load customer if available
            if (sessionData.CustomerId.HasValue)
            {
                var customer = await _customerLookupService.LookupByMobileNumberAsync(string.Empty);
                // Note: We'd need customer phone lookup by ID, but this is a simplified version
            }

            // Set payment method
            SelectedPaymentMethod = sessionData.PaymentMethod;
            
            // Update totals
            await CalculateTotal();
        }
        catch (Exception ex)
        {
            SetError($"Failed to load session data: {ex.Message}");
        }
    }

    public async Task SaveToSession()
    {
        if (!CurrentSessionId.HasValue) return;

        try
        {
            var sessionData = new SaleSessionDto
            {
                Id = CurrentSessionId.Value,
                TabName = SessionTabName,
                ShopId = _userContextService.CurrentShop?.Id ?? Guid.Empty,
                UserId = _currentUserService.CurrentUser?.Id ?? Guid.Empty,
                CustomerId = CurrentCustomer?.Id,
                PaymentMethod = SelectedPaymentMethod,
                Items = SaleItems.Select(item => new SaleSessionItemDto
                {
                    Id = Guid.NewGuid(),
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineTotal = item.LineTotal,
                    DiscountAmount = item.DiscountAmount,
                    BatchNumber = item.BatchNumber,
                    Weight = item.Weight,
                    IsWeightBased = item.IsWeightBased
                }).ToList(),
                Calculation = new SaleSessionCalculationDto
                {
                    Subtotal = Subtotal,
                    TotalDiscount = DiscountAmount,
                    TotalTax = TaxAmount,
                    FinalTotal = TotalAmount,
                    CalculatedAt = DateTime.UtcNow
                }
            };

            await _multiTabSalesManager.SaveSessionStateAsync(CurrentSessionId.Value, sessionData);
        }
        catch (Exception ex)
        {
            SetError($"Failed to save session: {ex.Message}");
        }
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

    // Enhanced mobile-specific commands for touch optimization
    [RelayCommand]
    private async Task SwipeToAddProduct(Product product)
    {
        if (product == null) return;
        
        TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
        await AddProduct(product);
    }

    [RelayCommand]
    private async Task LongPressItemOptions(SaleItemViewModel item)
    {
        if (item == null) return;

        TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
        
        var options = new List<string> { "Edit Quantity", "Apply Discount", "Remove Item" };
        
        var action = await Shell.Current.DisplayActionSheet(
            $"Options for {item.ProductName}", 
            "Cancel", 
            null, 
            options.ToArray());

        switch (action)
        {
            case "Edit Quantity":
                await QuickAddQuantity(item);
                break;
            case "Apply Discount":
                await ApplyDiscount(item);
                break;
            case "Remove Item":
                await SwipeToRemoveItem(item);
                break;
        }
    }

    [RelayCommand]
    private async Task DoubleTapToComplete()
    {
        if (CanCompleteSale)
        {
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
            await CompleteSale();
        }
    }

    [RelayCommand]
    private async Task VoiceSearch()
    {
        if (!EnableVoiceInput)
        {
            SetError("Voice input is disabled");
            return;
        }

        try
        {
            IsVoiceInputActive = true;
            VoiceInputStatus = "Listening...";
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);

            var isAvailable = await SpeechToText.Default.GetAvailabilityAsync();
            if (isAvailable != SpeechToTextAuthorizationStatus.Authorized)
            {
                SetError("Voice input permission not granted");
                VoiceInputStatus = "Permission denied";
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
                return;
            }

            var result = await SpeechToText.Default.ListenAsync(
                CultureInfo.GetCultureInfo("en-us"),
                new Progress<string>(partialText =>
                {
                    VoiceInputStatus = $"Listening: {partialText}";
                }),
                CancellationToken.None);

            if (result.IsSuccessful && !string.IsNullOrWhiteSpace(result.Text))
            {
                VoiceInputStatus = $"Processing: {result.Text}";
                await ProcessVoiceCommand(result.Text);
            }
            else
            {
                VoiceInputStatus = "No speech detected";
                SetError("No speech detected or recognition failed");
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
            }
        }
        catch (Exception ex)
        {
            SetError($"Voice search failed: {ex.Message}");
            VoiceInputStatus = "Voice search failed";
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
        }
        finally
        {
            IsVoiceInputActive = false;
            
            // Clear status after delay
            _ = Task.Delay(3000).ContinueWith(_ =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!IsVoiceInputActive)
                    {
                        VoiceInputStatus = string.Empty;
                    }
                });
            });
        }
    }



    // Enhanced tab management integration
    public async Task SwitchToTab(Guid sessionId)
    {
        try
        {
            if (CurrentSessionId == sessionId) return;

            // Save current session state
            await SaveToSession();

            // Switch to new session
            var success = await _multiTabSalesManager.SwitchToSessionAsync(sessionId);
            if (success)
            {
                CurrentSessionId = sessionId;
                
                // Load new session data
                var sessionData = await _multiTabSalesManager.GetSaleSessionAsync(sessionId);
                if (sessionData != null)
                {
                    await LoadFromSessionData(sessionData);
                }
                
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
            }
            else
            {
                SetError("Failed to switch to tab");
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
            }
        }
        catch (Exception ex)
        {
            SetError($"Tab switch failed: {ex.Message}");
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
        }
    }

    // Enhanced mobile-specific functionality
    [ObservableProperty]
    private bool isOneHandedMode;

    [ObservableProperty]
    private bool enableGestureNavigation = true;

    [ObservableProperty]
    private bool enableVoiceInput = true;

    [ObservableProperty]
    private bool enableAutoSave = true;

    [ObservableProperty]
    private TimeSpan autoSaveInterval = TimeSpan.FromSeconds(30);

    [ObservableProperty]
    private bool showQuickActions = true;

    [ObservableProperty]
    private bool enableSwipeGestures = true;

    [ObservableProperty]
    private string voiceInputStatus = string.Empty;

    [ObservableProperty]
    private bool isVoiceInputActive;

    [ObservableProperty]
    private bool isOfflineMode;

    [ObservableProperty]
    private string connectionStatus = "Online";

    // Auto-save functionality for mobile
    private CancellationTokenSource? _autoSaveCts;
    private Task? _autoSaveTask;
    private DateTime _lastInteraction = DateTime.UtcNow;

    private async Task StartAutoSaveAsync()
    {
        if (!EnableAutoSave) return;

        await StopAutoSaveAsync();

        _autoSaveCts = new CancellationTokenSource();
        var token = _autoSaveCts.Token;

        _autoSaveTask = Task.Run(async () =>
        {
            try
            {
                using var timer = new PeriodicTimer(AutoSaveInterval);

                while (await timer.WaitForNextTickAsync(token))
                {
                    // Only auto-save if there's been recent activity
                    if (DateTime.UtcNow - _lastInteraction < TimeSpan.FromMinutes(5))
                    {
                        await SaveToSession();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // expected on stop
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-save failed: {ex.Message}");
            }
        }, token);
    }

    [RelayCommand]
    private async Task ToggleAutoSave()
    {
        EnableAutoSave = !EnableAutoSave;

        if (EnableAutoSave)
        {
            await StartAutoSaveAsync();
        }
        else
        {
            await StopAutoSaveAsync();
        }

        TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
    }

    [RelayCommand]
    private async Task ShowMobileSettings()
    {
        var settings = new[]
        {
            $"Haptic Feedback: {(EnableHapticFeedback ? "On" : "Off")}",
            $"Voice Input: {(EnableVoiceInput ? "On" : "Off")}",
            $"Gesture Navigation: {(EnableGestureNavigation ? "On" : "Off")}",
            $"One-Handed Mode: {(IsOneHandedMode ? "On" : "Off")}",
            $"Auto Save: {(EnableAutoSave ? "On" : "Off")}"
        };

        var selectedSetting = await Shell.Current.DisplayActionSheet(
            "Mobile Settings", 
            "Cancel", 
            null, 
            settings);

        // Handle setting toggles
        if (selectedSetting?.Contains("Haptic Feedback") == true)
        {
            await ToggleHapticFeedback();
        }
        else if (selectedSetting?.Contains("Voice Input") == true)
        {
            await ToggleVoiceInput();
        }
        else if (selectedSetting?.Contains("Gesture Navigation") == true)
        {
            EnableGestureNavigation = !EnableGestureNavigation;
            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
        }
        else if (selectedSetting?.Contains("One-Handed Mode") == true)
        {
            await ToggleOneHandedMode();
        }
        else if (selectedSetting?.Contains("Auto Save") == true)
        {
            await ToggleAutoSave();
        }
    }

    [RelayCommand]
    private async Task HandleSwipeLeft()
    {
        if (!EnableSwipeGestures) return;
        
        TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
        
        // Swipe left could switch to next tab or show quick actions
        if (ShowQuickActions)
        {
            await ShowQuickActionsMenu();
        }
    }

    [RelayCommand]
    private async Task HandleSwipeRight()
    {
        if (!EnableSwipeGestures) return;
        
        TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
        
        // Swipe right could show customer lookup
        if (IsCustomerLookupEnabled)
        {
            await LookupCustomer();
        }
    }

    [RelayCommand]
    private async Task HandleSwipeUp()
    {
        if (!EnableSwipeGestures) return;
        
        TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
        
        // Swipe up to complete sale if possible
        if (CanCompleteSale)
        {
            await CompleteSale();
        }
        else
        {
            await Shell.Current.DisplayAlert("Swipe Up", "Add items to complete sale", "OK");
        }
    }

    [RelayCommand]
    private async Task HandleSwipeDown()
    {
        if (!EnableSwipeGestures) return;
        
        TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
        
        // Swipe down to clear sale
        var shouldClear = await Shell.Current.DisplayAlert(
            "Clear Sale", 
            "Are you sure you want to clear this sale?", 
            "Clear", 
            "Cancel");
            
        if (shouldClear)
        {
            await ClearSale();
        }
    }

    private async Task ShowQuickActionsMenu()
    {
        var actions = new List<string>();
        
        if (IsBarcodeIntegrationEnabled)
        {
            actions.Add("Scan Barcode");
        }
        
        if (IsCustomerLookupEnabled)
        {
            actions.Add("Lookup Customer");
        }
        
        if (EnableVoiceInput)
        {
            actions.Add("Voice Search");
        }
        
        actions.AddRange(new[] { "Settings", "Help" });
        
        var selectedAction = await Shell.Current.DisplayActionSheet(
            "Quick Actions", 
            "Cancel", 
            null, 
            actions.ToArray());

        switch (selectedAction)
        {
            case "Scan Barcode":
                await ScanBarcode();
                break;
            case "Lookup Customer":
                await LookupCustomer();
                break;
            case "Voice Search":
                await VoiceSearch();
                break;
            case "Settings":
                await ShowMobileSettings();
                break;
            case "Help":
                await ShowMobileHelp();
                break;
        }
    }

    private async Task ShowMobileHelp()
    {
        var helpText = "Mobile POS Help:\n\n" +
                      "• Swipe left: Quick actions\n" +
                      "• Swipe right: Customer lookup\n" +
                      "• Swipe up: Complete sale\n" +
                      "• Swipe down: Clear sale\n" +
                      "• Double tap: Quick complete\n" +
                      "• Long press: Item options\n" +
                      "• Voice: Say 'add [product]' or 'scan barcode'\n" +
                      "• Shake device: Refresh";

        await Shell.Current.DisplayAlert("Help", helpText, "OK");
    }

    // Enhanced voice search with better mobile integration
    private async Task ProcessVoiceCommand(string command)
    {
        try
        {
            var lowerCommand = command.ToLowerInvariant();

            // Handle different voice commands
            if (lowerCommand.Contains("scan") || lowerCommand.Contains("barcode"))
            {
                VoiceInputStatus = "Opening scanner...";
                await ScanBarcode();
            }
            else if (lowerCommand.Contains("customer") || lowerCommand.Contains("lookup"))
            {
                VoiceInputStatus = "Opening customer lookup...";
                await LookupCustomer();
            }
            else if (lowerCommand.Contains("complete") || lowerCommand.Contains("finish"))
            {
                VoiceInputStatus = "Completing sale...";
                await CompleteSale();
            }
            else if (lowerCommand.Contains("clear") || lowerCommand.Contains("reset"))
            {
                VoiceInputStatus = "Clearing sale...";
                await ClearSale();
            }
            else if (lowerCommand.Contains("add") || lowerCommand.Contains("search"))
            {
                // Extract product name from command
                var productName = ExtractProductNameFromCommand(command);
                if (!string.IsNullOrWhiteSpace(productName))
                {
                    VoiceInputStatus = $"Searching for {productName}...";
                    await SearchAndAddProduct(productName);
                }
                else
                {
                    VoiceInputStatus = "Could not understand product name";
                    SetError("Could not understand product name");
                }
            }
            else
            {
                // Try to search for products with the entire command
                VoiceInputStatus = $"Searching for {command}...";
                await SearchAndAddProduct(command);
            }
        }
        catch (Exception ex)
        {
            VoiceInputStatus = "Voice command failed";
            SetError($"Voice command failed: {ex.Message}");
        }
    }

    private string ExtractProductNameFromCommand(string command)
    {
        // Simple extraction - remove common command words
        var commandWords = new[] { "add", "search", "find", "get", "buy" };
        var words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        var productWords = words.Where(w => !commandWords.Contains(w.ToLowerInvariant())).ToArray();
        return string.Join(" ", productWords);
    }

    private async Task SearchAndAddProduct(string searchTerm)
    {
        try
        {
            var products = await _productService.SearchProductsAsync(searchTerm, 5);
            
            if (products.Any())
            {
                if (products.Count == 1)
                {
                    await AddProduct(products.First());
                    VoiceInputStatus = $"Added: {products.First().Name}";
                    TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
                }
                else
                {
                    // Show selection for multiple matches
                    var productNames = products.Select(p => p.Name).ToArray();
                    var selectedProduct = await Shell.Current.DisplayActionSheet(
                        "Select Product", 
                        "Cancel", 
                        null, 
                        productNames);

                    if (!string.IsNullOrEmpty(selectedProduct) && selectedProduct != "Cancel")
                    {
                        var product = products.FirstOrDefault(p => p.Name == selectedProduct);
                        if (product != null)
                        {
                            await AddProduct(product);
                            VoiceInputStatus = $"Added: {product.Name}";
                            TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.Click);
                        }
                    }
                }
            }
            else
            {
                VoiceInputStatus = "Product not found";
                SetError($"No products found for '{searchTerm}'");
                TriggerHapticFeedback(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
            }
        }
        catch (Exception ex)
        {
            VoiceInputStatus = "Search failed";
            SetError($"Product search failed: {ex.Message}");
        }
    }

    // Track user interactions for auto-save
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        _lastInteraction = DateTime.UtcNow;
    }

    public void Dispose()
    {
        StopAutoSave();
        
        // Unsubscribe from events
        _barcodeIntegrationService.BarcodeProcessed -= OnBarcodeProcessed;
        _barcodeIntegrationService.ScanError -= OnBarcodeScanError;
    }

    // Override Initialize to start auto-save and initialize mobile features
    public new async Task Initialize()
    {
        await base.Initialize();
        
        // Initialize mobile-specific features
        InitializeMobileFeatures();
        
        if (EnableAutoSave)
        {
            StartAutoSave();
        }
    }

    private bool _connectivitySubscribed;

    private void InitializeMobileFeatures()
    {
        // Enable one-handed mode for smaller screens
        var screenHeight = DeviceDisplay.Current.MainDisplayInfo.Height;
        var screenDensity = DeviceDisplay.Current.MainDisplayInfo.Density;
        var physicalHeight = screenHeight / screenDensity;

        IsOneHandedMode = physicalHeight < 6.0;

        UpdateConnectionStatus();

        if (!_connectivitySubscribed)
        {
            Connectivity.ConnectivityChanged += OnConnectivityChanged;
            _connectivitySubscribed = true;
        }
    }

    public void Dispose()
    {
        StopAutoSave();

        _barcodeIntegrationService.BarcodeProcessed -= OnBarcodeProcessed;
        _barcodeIntegrationService.ScanError -= OnBarcodeScanError;

        if (_connectivitySubscribed)
        {
            Connectivity.ConnectivityChanged -= OnConnectivityChanged;
            _connectivitySubscribed = false;
        }
    }

    private void UpdateConnectionStatus()
    {
        var networkAccess = Connectivity.Current.NetworkAccess;
        IsOfflineMode = networkAccess != NetworkAccess.Internet;
        ConnectionStatus = IsOfflineMode ? "Offline" : "Online";
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateConnectionStatus();
            
            if (!IsOfflineMode)
            {
                // Sync any pending changes when coming back online
                _ = Task.Run(async () => await SyncPendingChanges());
            }
        });
    }

    private async Task SyncPendingChanges()
    {
        try
        {
            // Sync session data when coming back online
            if (CurrentSessionId.HasValue)
            {
                await SaveToSession();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sync failed: {ex.Message}");
        }
    }ces.HapticFeedbackType.LongPress);
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