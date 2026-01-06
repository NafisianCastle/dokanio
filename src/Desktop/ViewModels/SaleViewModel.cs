using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.Models;
using Desktop.Views;
using Shared.Core.Services;
using Shared.Core.Entities;
using Shared.Core.DTOs;
using Shared.Core.Enums;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace Desktop.ViewModels;

public partial class SaleViewModel : BaseViewModel
{
    private readonly IBarcodeIntegrationService? _barcodeIntegrationService;
    private readonly IMultiTabSalesManager? _salesManager;
    private readonly ICustomerLookupService? _customerLookupService;
    private readonly IRealTimeCalculationEngine? _calculationEngine;
    private readonly ILogger<SaleViewModel>? _logger;
    private readonly Guid _sessionId;
    private readonly Guid _shopId;
    private readonly Guid _userId;
    private readonly Timer? _calculationTimer;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string customerName = string.Empty;

    [ObservableProperty]
    private string customerPhone = string.Empty;

    [ObservableProperty]
    private Desktop.Models.PaymentMethod selectedPaymentMethod = Desktop.Models.PaymentMethod.Cash;

    [ObservableProperty]
    private decimal amountReceived;

    [ObservableProperty]
    private bool isScanning;

    [ObservableProperty]
    private bool isLookingUpCustomer;

    [ObservableProperty]
    private bool hasCustomer;

    [ObservableProperty]
    private string membershipTier = string.Empty;

    [ObservableProperty]
    private decimal membershipDiscount;

    [ObservableProperty]
    private string customerMembershipNumber = string.Empty;

    [ObservableProperty]
    private bool isCalculating;

    [ObservableProperty]
    private DateTime lastCalculationTime;

    [ObservableProperty]
    private string calculationStatus = "Ready";

    // Multi-tab support properties
    [ObservableProperty]
    private string tabName = "New Sale";

    [ObservableProperty]
    private bool isActiveTab;

    [ObservableProperty]
    private bool hasUnsavedChanges;

    [ObservableProperty]
    private SessionState sessionState = SessionState.Active;

    // Enhanced customer properties
    [ObservableProperty]
    private bool isCustomerLookupEnabled = true;

    [ObservableProperty]
    private string customerEmail = string.Empty;

    [ObservableProperty]
    private decimal customerTotalSpent;

    [ObservableProperty]
    private int customerVisitCount;

    [ObservableProperty]
    private DateTime? customerLastVisit;

    [ObservableProperty]
    private List<MembershipDiscount> availableDiscounts = new();

    // Real-time calculation properties
    [ObservableProperty]
    private decimal calculatedSubtotal;

    [ObservableProperty]
    private decimal calculatedTax;

    [ObservableProperty]
    private decimal calculatedTotal;

    [ObservableProperty]
    private decimal calculatedTotalDiscount;

    [ObservableProperty]
    private List<CalculationBreakdownDto> calculationBreakdown = new();

    // Barcode scanning properties
    [ObservableProperty]
    private bool isBarcodeIntegrationEnabled;

    [ObservableProperty]
    private string lastScannedBarcode = string.Empty;

    [ObservableProperty]
    private DateTime? lastScanTime;

    [ObservableProperty]
    private string scanStatus = "Ready";

    public ObservableCollection<Desktop.Models.Product> SearchResults { get; } = new();
    public ObservableCollection<Desktop.Models.SaleItem> SaleItems { get; } = new();
    public List<Desktop.Models.PaymentMethod> PaymentMethods { get; } = Enum.GetValues<Desktop.Models.PaymentMethod>().ToList();

    // Enhanced calculation properties with real-time updates
    public decimal Subtotal => CalculatedSubtotal;
    public decimal Tax => CalculatedTax;
    public decimal Total => CalculatedTotal;
    public decimal TotalDiscount => CalculatedTotalDiscount;
    public decimal ChangeAmount => AmountReceived - Total;

    // Customer information
    private CustomerLookupResult? _currentCustomer;
    private ShopConfiguration? _shopConfiguration;
    private CancellationTokenSource? _calculationCancellationToken;

    public SaleViewModel(
        IBarcodeIntegrationService? barcodeIntegrationService = null,
        IMultiTabSalesManager? salesManager = null,
        ICustomerLookupService? customerLookupService = null,
        IRealTimeCalculationEngine? calculationEngine = null,
        ILogger<SaleViewModel>? logger = null,
        Guid? sessionId = null,
        Guid? shopId = null,
        Guid? userId = null)
    {
        _barcodeIntegrationService = barcodeIntegrationService;
        _salesManager = salesManager;
        _customerLookupService = customerLookupService;
        _calculationEngine = calculationEngine;
        _logger = logger;
        _sessionId = sessionId ?? Guid.NewGuid();
        _shopId = shopId ?? Guid.NewGuid();
        _userId = userId ?? Guid.NewGuid();
        
        Title = "New Sale";
        
        // Initialize enhanced features
        InitializeEnhancedFeatures();
        
        // Sample products for demo
        LoadSampleProducts();
        
        // Subscribe to collection changes for real-time calculations
        SaleItems.CollectionChanged += OnSaleItemsChanged;
        
        // Initialize calculation timer for debounced calculations
        _calculationTimer = new Timer(PerformCalculation, null, Timeout.Infinite, Timeout.Infinite);
    }

    partial void OnSearchTextChanged(string value)
    {
        SearchProducts(value);
    }

    partial void OnCustomerPhoneChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Length >= 10)
        {
            _ = LookupCustomerAsync();
        }
        else
        {
            ClearCustomerInfo();
        }
    }

    partial void OnIsActiveTabChanged(bool value)
    {
        if (value)
        {
            // Tab became active - refresh calculations
            TriggerRealTimeCalculation();
        }
    }

    private void InitializeEnhancedFeatures()
    {
        // Check if services are available
        IsBarcodeIntegrationEnabled = _barcodeIntegrationService != null;
        IsCustomerLookupEnabled = _customerLookupService != null;
        
        // Initialize shop configuration (in real app, this would be loaded from service)
        _shopConfiguration = new ShopConfiguration
        {
            TaxRate = 0.18m, // 18% GST
            Currency = "INR"
        };
        
        // Subscribe to barcode events if available
        if (_barcodeIntegrationService != null)
        {
            _barcodeIntegrationService.BarcodeProcessed += OnBarcodeProcessed;
            _barcodeIntegrationService.ScanError += OnScanError;
        }
        
        _logger?.LogDebug("Enhanced features initialized for session {SessionId}", _sessionId);
    }

    private void OnSaleItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        HasUnsavedChanges = true;
        TriggerRealTimeCalculation();
        
        // Subscribe to property changes on new items
        if (e.NewItems != null)
        {
            foreach (Desktop.Models.SaleItem item in e.NewItems)
            {
                item.PropertyChanged += OnSaleItemPropertyChanged;
            }
        }
        
        // Unsubscribe from removed items
        if (e.OldItems != null)
        {
            foreach (Desktop.Models.SaleItem item in e.OldItems)
            {
                item.PropertyChanged -= OnSaleItemPropertyChanged;
            }
        }
    }

    private void OnSaleItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Desktop.Models.SaleItem.Quantity) || 
            e.PropertyName == nameof(Desktop.Models.SaleItem.UnitPrice))
        {
            HasUnsavedChanges = true;
            TriggerRealTimeCalculation();
        }
    }

    private async void OnBarcodeProcessed(object? sender, BarcodeProcessedEventArgs e)
    {
        LastScannedBarcode = e.Barcode;
        LastScanTime = e.Timestamp;
        ScanStatus = "Product added";
    
        if (e.Product != null)
        {
            // Convert to desktop model and add to sale
            var desktopProduct = ConvertToDesktopProduct(e.Product);
            await AddProduct(desktopProduct);
        }
    }

    private void OnScanError(object? sender, ScanErrorEventArgs e)
    {
        ScanStatus = "Scan failed";
        SetError($"Barcode scan error: {e.ErrorMessage}");
        _logger?.LogWarning("Barcode scan error: {Error}", e.ErrorMessage);
    }

    private Desktop.Models.Product ConvertToDesktopProduct(Shared.Core.Entities.Product coreProduct)
    {
        return new Desktop.Models.Product
        {
            Id = coreProduct.Id,
            Name = coreProduct.Name,
            Barcode = coreProduct.Barcode,
            UnitPrice = coreProduct.UnitPrice,
            Category = coreProduct.Category,
            StockQuantity = 100, // This would come from stock service
            BatchNumber = coreProduct.BatchNumber,
            ExpiryDate = coreProduct.ExpiryDate
        };
    }

    [RelayCommand]
    private void SearchProducts(string? searchTerm = null)
    {
        searchTerm ??= SearchText;
        
        SearchResults.Clear();
        
        if (string.IsNullOrWhiteSpace(searchTerm))
            return;

        // Sample search logic - in real app this would query a database
        var sampleProducts = GetSampleProducts()
            .Where(p => p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                       (p.Barcode?.Contains(searchTerm) == true))
            .Take(5);

        foreach (var product in sampleProducts)
        {
            SearchResults.Add(product);
        }
    }

    [RelayCommand]
    private async Task AddProduct(Desktop.Models.Product product)
    {
        try
        {
            var existingItem = SaleItems.FirstOrDefault(item => item.ProductId == product.Id);
            
            if (existingItem != null)
            {
                existingItem.Quantity++;
            }
            else
            {
                var newItem = new Desktop.Models.SaleItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = 1,
                    UnitPrice = product.UnitPrice,
                    BatchNumber = product.BatchNumber
                };
                
                SaleItems.Add(newItem);
                
                // Add to session if multi-tab manager is available
                if (_salesManager != null)
                {
                    var sessionItem = ConvertToSessionItem(newItem);
                    await _salesManager.AddItemToSessionAsync(_sessionId, sessionItem);
                }
            }

            SearchText = string.Empty;
            SearchResults.Clear();
            
            TriggerRealTimeCalculation();
            
            _logger?.LogDebug("Added product {ProductName} to sale", product.Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding product to sale");
            SetError($"Failed to add product: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RemoveItem(Desktop.Models.SaleItem item)
    {
        try
        {
            SaleItems.Remove(item);
            
            // Remove from session if multi-tab manager is available
            if (_salesManager != null)
            {
                await _salesManager.RemoveItemFromSessionAsync(_sessionId, item.Id);
            }
            
            TriggerRealTimeCalculation();
            
            _logger?.LogDebug("Removed item {ProductName} from sale", item.ProductName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error removing item from sale");
            SetError($"Failed to remove item: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task UpdateItemQuantity(object[] parameters)
    {
        if (parameters.Length != 2 || 
            parameters[0] is not Desktop.Models.SaleItem item || 
            parameters[1] is not int newQuantity)
            return;

        try
        {
            if (newQuantity <= 0)
            {
                await RemoveItem(item);
                return;
            }
            
            item.Quantity = newQuantity;
            
            // Update in session if multi-tab manager is available
            if (_salesManager != null)
            {
                var sessionItem = ConvertToSessionItem(item);
                await _salesManager.UpdateItemInSessionAsync(_sessionId, sessionItem);
            }
            
            TriggerRealTimeCalculation();
            
            _logger?.LogDebug("Updated quantity for {ProductName} to {Quantity}", item.ProductName, newQuantity);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating item quantity");
            SetError($"Failed to update quantity: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CompleteSale()
    {
        if (!SaleItems.Any())
        {
            ErrorMessage = "Please add items to the sale";
            return;
        }

        if (SelectedPaymentMethod == Desktop.Models.PaymentMethod.Cash && AmountReceived < Total)
        {
            ErrorMessage = "Amount received is less than total";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            // Complete session if multi-tab manager is available
            if (_salesManager != null)
            {
                var result = await _salesManager.CompleteSessionAsync(_sessionId, 
                    (Shared.Core.Enums.PaymentMethod)SelectedPaymentMethod);
                
                if (!result.Success)
                {
                    SetError($"Failed to complete sale: {result.Message}");
                    return;
                }
            }
            
            // Simulate sale processing
            await Task.Delay(1000);

            var sale = new Desktop.Models.Sale
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"INV-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
                TotalAmount = Total,
                PaymentMethod = SelectedPaymentMethod,
                CustomerName = CustomerName,
                CustomerPhone = CustomerPhone,
                CreatedAt = DateTime.Now,
                Items = SaleItems.ToList()
            };

            // Update customer after purchase if available
            if (_currentCustomer != null && _customerLookupService != null)
            {
                _ = _customerLookupService.UpdateCustomerAfterPurchaseAsync(_currentCustomer.Id, Total);
            }
            
            // Reset the form
            ResetSale();
            
            // Show success message (in real app, might show receipt dialog)
            ErrorMessage = $"Sale completed successfully! Invoice: {sale.InvoiceNumber}";
            
            _logger?.LogInformation("Sale completed successfully: {InvoiceNumber}", sale.InvoiceNumber);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error completing sale");
            ErrorMessage = $"Error completing sale: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TriggerRealTimeCalculationCommand()
    {
        TriggerRealTimeCalculation();
    }

    [RelayCommand]
    private void RecalculateTotals()
    {
        TriggerRealTimeCalculation();
    }

    private void TriggerRealTimeCalculation()
    {
        // Cancel any pending calculation
        _calculationCancellationToken?.Cancel();
        _calculationCancellationToken = new CancellationTokenSource();
        
        // Debounce calculations to avoid excessive calls
        _calculationTimer?.Change(300, Timeout.Infinite); // 300ms delay
    }

    private async void PerformCalculation(object? state)
    {
        var token = _calculationCancellationToken?.Token;
        if (token?.IsCancellationRequested == true)
            return;

        try
        {
            // Do the work off-thread, but marshal UI updates back to the UI thread.
            // Capture current values first to avoid partially-updated UI state.
            decimal subtotal, tax, total, totalDiscount;
            List<CalculationBreakdownDto> breakdown;

            if (_calculationEngine != null && _shopConfiguration != null)
            {
                await PerformEnhancedCalculation();
            }
            else
            {
                PerformBasicCalculation();
            }

            if (token?.IsCancellationRequested == true)
                return;

            subtotal = CalculatedSubtotal;
            tax = CalculatedTax;
            total = CalculatedTotal;
            totalDiscount = CalculatedTotalDiscount;
            breakdown = CalculationBreakdown.ToList();

            var sync = SynchronizationContext.Current;
            if (sync != null)
            {
                sync.Post(_ =>
                {
                    if (token?.IsCancellationRequested == true) return;

                    IsCalculating = true;
                    CalculationStatus = "Ready";
                    LastCalculationTime = DateTime.Now;

                    CalculatedSubtotal = subtotal;
                    CalculatedTax = tax;
                    CalculatedTotal = total;
                    CalculatedTotalDiscount = totalDiscount;
                    CalculationBreakdown = breakdown;

                    OnPropertyChanged(nameof(Subtotal));
                    OnPropertyChanged(nameof(Tax));
                    OnPropertyChanged(nameof(Total));
                    OnPropertyChanged(nameof(TotalDiscount));
                    OnPropertyChanged(nameof(ChangeAmount));

                    IsCalculating = false;
                }, null);
            }
            else
            {
                // Fallback if no context is available (tests/background usage)
                LastCalculationTime = DateTime.Now;
                CalculationStatus = "Ready";
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(Tax));
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(TotalDiscount));
                OnPropertyChanged(nameof(ChangeAmount));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during calculation");
            CalculationStatus = "Error";
        }
        finally
        {
            // If UI-thread marshalling is used above, avoid fighting over this flag here.
        }
    }

    private async Task PerformEnhancedCalculation()
    {
        if (_calculationEngine == null || _shopConfiguration == null)
            return;

        try
        {
            // Convert desktop sale items to core entities
            var coreItems = SaleItems.Select(ConvertToCoreEntity).ToList();
            
            // Get customer for membership calculations
            Shared.Core.Entities.Customer? coreCustomer = null;
            if (_currentCustomer != null)
            {
                coreCustomer = new Shared.Core.Entities.Customer
                {
                    Id = _currentCustomer.Id,
                    Name = _currentCustomer.Name,
                    Phone = _currentCustomer.Phone,
                    Email = _currentCustomer.Email,
                    TotalSpent = _currentCustomer.TotalSpent,
                    VisitCount = _currentCustomer.VisitCount,
                    LastVisit = _currentCustomer.LastVisit
                };
            }
            
            // Perform calculation
            var calculation = await _calculationEngine.CalculateOrderTotalsAsync(
                coreItems, 
                _shopConfiguration, 
                coreCustomer);
            
            if (calculation.IsValid)
            {
                CalculatedSubtotal = calculation.Subtotal;
                CalculatedTax = calculation.TotalTaxAmount;
                CalculatedTotal = calculation.FinalTotal;
                CalculatedTotalDiscount = calculation.TotalDiscountAmount;
                
                // Update breakdown
                CalculationBreakdown = calculation.Breakdown.Items
                    .Select(item => new CalculationBreakdownDto
                    {
                        Description = item.Description,
                        Amount = item.Amount,
                        Type = Enum.TryParse<CalculationType>(item.Type, out var type) ? type : CalculationType.Subtotal
                    })
                    .ToList();
            }
            else
            {
                _logger?.LogWarning("Calculation validation failed: {Messages}", 
                    string.Join(", ", calculation.ValidationMessages));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in enhanced calculation");
            // Fallback to basic calculation
            PerformBasicCalculation();
        }
    }

    private void PerformBasicCalculation()
    {
        CalculatedSubtotal = SaleItems.Sum(item => item.Total);
        CalculatedTax = CalculatedSubtotal * 0.18m; // 18% GST
        CalculatedTotalDiscount = 0; // Basic calculation doesn't include discounts
        CalculatedTotal = CalculatedSubtotal + CalculatedTax - CalculatedTotalDiscount;
        
        // Clear breakdown for basic calculation
        CalculationBreakdown.Clear();
    }

    private Shared.Core.Entities.SaleItem ConvertToCoreEntity(Desktop.Models.SaleItem desktopItem)
    {
        return new Shared.Core.Entities.SaleItem
        {
            Id = desktopItem.Id,
            ProductId = desktopItem.ProductId,
            Quantity = desktopItem.Quantity,
            UnitPrice = desktopItem.UnitPrice,
            BatchNumber = desktopItem.BatchNumber,
            TotalPrice = desktopItem.Total
        };
    }

    private SaleSessionItemDto ConvertToSessionItem(Desktop.Models.SaleItem desktopItem)
    {
        return new SaleSessionItemDto
        {
            Id = desktopItem.Id,
            ProductId = desktopItem.ProductId,
            ProductName = desktopItem.ProductName,
            Quantity = desktopItem.Quantity,
            UnitPrice = desktopItem.UnitPrice,
            LineTotal = desktopItem.Total,
            BatchNumber = desktopItem.BatchNumber
        };
    }

    private void PopulateCustomerInfo(CustomerLookupResult customer)
    {
        CustomerName = customer.Name;
        CustomerEmail = customer.Email ?? string.Empty;
        CustomerMembershipNumber = customer.MembershipNumber;
        CustomerTotalSpent = customer.TotalSpent;
        CustomerVisitCount = customer.VisitCount;
        CustomerLastVisit = customer.LastVisit;
        MembershipTier = customer.Tier.ToString();
        AvailableDiscounts = customer.AvailableDiscounts;
    }

    private void PopulateMembershipInfo(CustomerMembershipDetails membership)
    {
        MembershipDiscount = membership.DiscountPercentage;
        AvailableDiscounts = membership.AvailableDiscounts;
    }

    private void ClearCustomerInfo()
    {
        _currentCustomer = null;
        CustomerName = string.Empty;
        CustomerEmail = string.Empty;
        CustomerMembershipNumber = string.Empty;
        MembershipTier = string.Empty;
        MembershipDiscount = 0;
        CustomerTotalSpent = 0;
        CustomerVisitCount = 0;
        CustomerLastVisit = null;
        AvailableDiscounts.Clear();
        HasCustomer = false;
    }

    [RelayCommand]
    private async Task ResetSale()
    {
        try
        {
            SaleItems.Clear();
            ClearCustomerInfo();
            AmountReceived = 0;
            SelectedPaymentMethod = Desktop.Models.PaymentMethod.Cash;
            SearchText = string.Empty;
            SearchResults.Clear();
            ErrorMessage = string.Empty;
            
            // Reset calculation values
            CalculatedSubtotal = 0;
            CalculatedTax = 0;
            CalculatedTotal = 0;
            CalculatedTotalDiscount = 0;
            CalculationBreakdown.Clear();
            
            // Reset barcode scanning state
            LastScannedBarcode = string.Empty;
            LastScanTime = null;
            ScanStatus = "Ready";
            
            HasUnsavedChanges = false;
            
            // Reset session state if multi-tab manager is available
            if (_salesManager != null)
            {
                var request = new CreateSaleSessionRequest
                {
                    TabName = TabName,
                    ShopId = _shopId,
                    UserId = _userId,
                    DeviceId = CreateDeterministicGuidFromString(Environment.MachineName)
                };
                
                await _salesManager.CreateNewSaleSessionAsync(request);
            }
            
            TriggerRealTimeCalculation();
            
            _logger?.LogDebug("Sale reset completed");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error resetting sale");
            SetError($"Failed to reset sale: {ex.Message}");
        }
    }

    // Session management methods for multi-tab support
    public async Task LoadFromSessionAsync(SaleSessionDto sessionData)
    {
        try
        {
            TabName = sessionData.TabName;
            SessionState = sessionData.State;
            
            // Load items
            SaleItems.Clear();
            foreach (var item in sessionData.Items)
            {
                var saleItem = new Desktop.Models.SaleItem
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = (int)item.Quantity,
                    UnitPrice = item.UnitPrice,
                    BatchNumber = item.BatchNumber
                };
                
                SaleItems.Add(saleItem);
            }
            
            // Load payment method
            SelectedPaymentMethod = (Desktop.Models.PaymentMethod)sessionData.PaymentMethod;
            
            // Load customer info if available
            if (sessionData.CustomerId.HasValue && !string.IsNullOrEmpty(sessionData.CustomerName))
            {
                CustomerName = sessionData.CustomerName;
                HasCustomer = true;
                
                // Try to load full customer details
                if (_customerLookupService != null)
                {
                    var customer = await _customerLookupService.LookupByMobileNumberAsync(CustomerPhone);
                    if (customer != null)
                    {
                        PopulateCustomerInfo(customer);
                    }
                }
            }
            
            // Load calculation
            if (sessionData.Calculation != null)
            {
                CalculatedSubtotal = sessionData.Calculation.Subtotal;
                CalculatedTax = sessionData.Calculation.TotalTax;
                CalculatedTotal = sessionData.Calculation.FinalTotal;
                CalculatedTotalDiscount = sessionData.Calculation.TotalDiscount;
            }
            
            HasUnsavedChanges = false;
            TriggerRealTimeCalculation();
            
            _logger?.LogDebug("Loaded session data for tab {TabName}", TabName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading session data");
            SetError($"Failed to load session: {ex.Message}");
        }
    }

    public SaleSessionDto GetSessionData()
    {
        return new SaleSessionDto
        {
            Id = _sessionId,
            TabName = TabName,
            ShopId = _shopId,
            UserId = _userId,
            CustomerId = _currentCustomer?.Id,
            CustomerName = CustomerName,
            PaymentMethod = (Shared.Core.Enums.PaymentMethod)SelectedPaymentMethod,
            State = SessionState,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            IsActive = IsActiveTab,
            Items = SaleItems.Select(ConvertToSessionItem).ToList(),
            Calculation = new SaleSessionCalculationDto
            {
                Subtotal = CalculatedSubtotal,
                TotalTax = CalculatedTax,
                FinalTotal = CalculatedTotal,
                TotalDiscount = CalculatedTotalDiscount,
                Breakdown = CalculationBreakdown,
                CalculatedAt = LastCalculationTime
            }
        };
    }

    public async Task SaveSessionAsync()
    {
        if (_salesManager == null)
            return;

        try
        {
            var sessionData = GetSessionData();
            var result = await _salesManager.SaveSessionStateAsync(_sessionId, sessionData);
            
            if (result.Success)
            {
                HasUnsavedChanges = false;
                _logger?.LogDebug("Session saved successfully");
            }
            else
            {
                _logger?.LogWarning("Failed to save session: {Message}", result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving session");
        }
    }

    // Cleanup method
    public void Cleanup()
    {
        _calculationTimer?.Dispose();
        _calculationCancellationToken?.Cancel();
        _calculationCancellationToken?.Dispose();
        
        // Unsubscribe from events
        if (_barcodeIntegrationService != null)
        {
            _barcodeIntegrationService.BarcodeProcessed -= OnBarcodeProcessed;
            _barcodeIntegrationService.ScanError -= OnScanError;
        }
        
        // Unsubscribe from sale items
        foreach (var item in SaleItems)
        {
            item.PropertyChanged -= OnSaleItemPropertyChanged;
        }
    }

    [RelayCommand]
    private async Task LookupCustomerAsync()
    {
        if (_customerLookupService == null || string.IsNullOrWhiteSpace(CustomerPhone))
            return;

        try
        {
            IsLookingUpCustomer = true;
            ClearError();
            
            // Validate mobile number format first
            var validation = await _customerLookupService.ValidateMobileNumberAsync(CustomerPhone);
            if (!validation.IsValid)
            {
                SetError(validation.ErrorMessage ?? "Invalid mobile number format");
                return;
            }
            
            // Lookup customer
            var customer = await _customerLookupService.LookupByMobileNumberAsync(CustomerPhone);
            
            if (customer != null)
            {
                _currentCustomer = customer;
                PopulateCustomerInfo(customer);
                
                // Get membership details
                var membershipDetails = await _customerLookupService.GetMembershipDetailsAsync(customer.Id);
                if (membershipDetails != null)
                {
                    PopulateMembershipInfo(membershipDetails);
                }
                
                HasCustomer = true;
                TriggerRealTimeCalculation(); // Recalculate with membership discounts
                
                _logger?.LogDebug("Customer lookup successful for {Phone}", CustomerPhone);
            }
            else
            {
                // Customer not found - offer to create new customer
                HasCustomer = false;
                ClearCustomerInfo();
                
                // In a real app, you might show a dialog to create new customer
                SetError("Customer not found. Would you like to create a new customer?");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during customer lookup");
            SetError($"Customer lookup failed: {ex.Message}");
            HasCustomer = false;
            ClearCustomerInfo();
        }
        finally
        {
            IsLookingUpCustomer = false;
        }
    }

    [RelayCommand]
    private async Task CreateNewCustomerAsync()
    {
        if (_customerLookupService == null || string.IsNullOrWhiteSpace(CustomerPhone))
            return;

        try
        {
            IsBusy = true;
            
            var request = new CustomerCreationRequest
            {
                Name = CustomerName,
                MobileNumber = CustomerPhone,
                Email = CustomerEmail,
                ShopId = _shopId,
                InitialTier = Shared.Core.Enums.MembershipTier.Bronze
            };
            
            var result = await _customerLookupService.CreateNewCustomerAsync(request);
            
            if (result.Success && result.Customer != null)
            {
                _currentCustomer = result.Customer;
                PopulateCustomerInfo(result.Customer);
                HasCustomer = true;
                
                _logger?.LogInformation("Created new customer {Phone}", CustomerPhone);
            }
            else
            {
                SetError(result.ErrorMessage ?? "Failed to create customer");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating new customer");
            SetError($"Failed to create customer: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StartBarcodeScanAsync()
    {
        if (_barcodeIntegrationService == null)
        {
            // Fallback to simple simulation for demo
            IsScanning = true;
            ScanStatus = "Scanning...";
            
            await Task.Delay(2000);
            
            IsScanning = false;
            SearchText = "1234567890123"; // Sample barcode
            ScanStatus = "Ready";
            SearchProducts();
            return;
        }

        try
        {
            IsScanning = true;
            ScanStatus = "Initializing scanner...";
            ClearError();
            
            // Initialize scanner if needed
            var initialized = await _barcodeIntegrationService.InitializeAsync();
            if (!initialized)
            {
                SetError("Failed to initialize barcode scanner");
                return;
            }
            
            ScanStatus = "Scanning...";
            
            var scanOptions = new ScanOptions
            {
                ShopId = _shopId,
                SessionId = _sessionId,
                EnableContinuousMode = false,
                ScanTimeout = TimeSpan.FromSeconds(30),
                EnableBeep = true,
                EnableVibration = false,
                AutoAddToSale = true
            };

            var result = await _barcodeIntegrationService.ScanBarcodeAsync(scanOptions);
            
            if (result.IsSuccess && !string.IsNullOrEmpty(result.Barcode))
            {
                LastScannedBarcode = result.Barcode;
                LastScanTime = DateTime.Now;
                
                if (result.IsProductFound && result.Product != null)
                {
                    var desktopProduct = ConvertToDesktopProduct(result.Product);
                    await AddProduct(desktopProduct);
                    ScanStatus = "Product added";
                }
                else
                {
                    ScanStatus = "Product not found";
                    SetError("Product not found in inventory");
                }
            }
            else
            {
                ScanStatus = "Scan failed";
                SetError(result.ErrorMessage ?? "Scan failed or timeout");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during barcode scanning");
            SetError($"Barcode scanning error: {ex.Message}");
            ScanStatus = "Error";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task ProcessManualBarcodeAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return;

        try
        {
            IsBusy = true;
            ClearError();
            
            if (_barcodeIntegrationService != null)
            {
                // Validate barcode format
                var isValid = await _barcodeIntegrationService.ValidateBarcodeFormatAsync(barcode);
                if (!isValid)
                {
                    SetError("Invalid barcode format");
                    return;
                }

                // Lookup product
                var product = await _barcodeIntegrationService.LookupProductByBarcodeAsync(barcode, _shopId);
                
                if (product != null)
                {
                    var desktopProduct = ConvertToDesktopProduct(product);
                    await AddProduct(desktopProduct);
                    LastScannedBarcode = barcode;
                    LastScanTime = DateTime.Now;
                }
                else
                {
                    SetError("Product not found for this barcode");
                }
            }
            else
            {
                // Fallback - search in sample products
                SearchText = barcode;
                SearchProducts();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing manual barcode");
            SetError($"Failed to process barcode: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadSampleProducts()
    {
        // This would be loaded from database in real app
    }

    private List<Desktop.Models.Product> GetSampleProducts()
    {
        return new List<Desktop.Models.Product>
        {
            new() { Id = Guid.NewGuid(), Name = "Paracetamol 500mg", Barcode = "1234567890123", UnitPrice = 25.50m, Category = "Medicine", StockQuantity = 100 },
            new() { Id = Guid.NewGuid(), Name = "Aspirin 75mg", Barcode = "2345678901234", UnitPrice = 15.75m, Category = "Medicine", StockQuantity = 50 },
            new() { Id = Guid.NewGuid(), Name = "Vitamin C Tablets", Barcode = "3456789012345", UnitPrice = 45.00m, Category = "Supplement", StockQuantity = 75 },
            new() { Id = Guid.NewGuid(), Name = "Cough Syrup", Barcode = "4567890123456", UnitPrice = 85.25m, Category = "Medicine", StockQuantity = 30 },
            new() { Id = Guid.NewGuid(), Name = "Bandages", Barcode = "5678901234567", UnitPrice = 12.50m, Category = "Medical Supply", StockQuantity = 200 }
        };
    }
}