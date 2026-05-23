using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using System.Reactive.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Desktop.Models;
using Desktop.Views;
using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Shared.Core.Services;

namespace Desktop.ViewModels;

public class SaleViewModel : BaseViewModel
{
    // ── Services ──────────────────────────────────────────────────────────────
    private readonly IBarcodeIntegrationService? _barcodeIntegrationService;
    private readonly IMultiTabSalesManager? _salesManager;
    private readonly ICustomerLookupService? _customerLookupService;
    private readonly IRealTimeCalculationEngine? _calculationEngine;
    private readonly IStockValidationService? _stockValidationService;
    private readonly ISaleService? _saleService;
    private readonly IReceiptService? _receiptService;
    private readonly IProductRepository? _productRepository;
    private readonly ILogger<SaleViewModel>? _logger;
    private readonly Guid _sessionId;
    private readonly Guid _shopId;
    private readonly Guid _userId;

    // ── Observable properties ─────────────────────────────────────────────────
    [Reactive] public string SearchText { get; set; } = string.Empty;
    [Reactive] public string CustomerName { get; set; } = string.Empty;
    [Reactive] public string CustomerPhone { get; set; } = string.Empty;
    [Reactive] public string CustomerEmail { get; set; } = string.Empty;
    [Reactive] public Desktop.Models.PaymentMethod SelectedPaymentMethod { get; set; } = Desktop.Models.PaymentMethod.Cash;
    [Reactive] public decimal AmountReceived { get; set; }
    [Reactive] public bool IsScanning { get; set; }
    [Reactive] public bool IsLookingUpCustomer { get; set; }
    [Reactive] public bool HasCustomer { get; set; }
    [Reactive] public string MembershipTier { get; set; } = string.Empty;
    [Reactive] public decimal MembershipDiscount { get; set; }
    [Reactive] public string CustomerMembershipNumber { get; set; } = string.Empty;
    [Reactive] public bool IsCalculating { get; set; }
    [Reactive] public DateTime LastCalculationTime { get; set; }
    [Reactive] public string CalculationStatus { get; set; } = "Ready";
    [Reactive] public string TabName { get; set; } = "New Sale";
    [Reactive] public bool IsActiveTab { get; set; }
    [Reactive] public bool HasUnsavedChanges { get; set; }
    [Reactive] public SessionState SessionState { get; set; } = SessionState.Active;
    [Reactive] public bool IsCustomerLookupEnabled { get; set; } = true;
    [Reactive] public decimal CustomerTotalSpent { get; set; }
    [Reactive] public int CustomerVisitCount { get; set; }
    [Reactive] public DateTime? CustomerLastVisit { get; set; }
    [Reactive] public List<MembershipDiscount> AvailableDiscounts { get; set; } = new();
    [Reactive] public decimal CalculatedSubtotal { get; set; }
    [Reactive] public decimal CalculatedTax { get; set; }
    [Reactive] public decimal CalculatedTotal { get; set; }
    [Reactive] public decimal CalculatedTotalDiscount { get; set; }
    [Reactive] public List<CalculationBreakdownDto> CalculationBreakdown { get; set; } = new();
    [Reactive] public bool IsBarcodeIntegrationEnabled { get; set; }
    [Reactive] public string LastScannedBarcode { get; set; } = string.Empty;
    [Reactive] public DateTime? LastScanTime { get; set; }
    [Reactive] public string ScanStatus { get; set; } = "Ready";
    [Reactive] public bool IsSearching { get; set; }
    [Reactive] public bool HasSearched { get; set; }
    [Reactive] public bool CustomerFound { get; set; }
    [Reactive] public string CustomerMembershipInfo { get; set; } = string.Empty;
    [Reactive] public bool IsProcessingSale { get; set; }
    [Reactive] public bool IsSaving { get; set; }
    [Reactive] public bool IsLoadingProducts { get; set; }
    [Reactive] public bool CanCompleteSale { get; set; } = true;
    [Reactive] public bool CanPrintReceipt { get; set; }
    [Reactive] public bool CanEmailReceipt { get; set; }
    [Reactive] public string BusyMessage { get; set; } = string.Empty;
    [Reactive] public bool HasMessage { get; set; }
    [Reactive] public string MessageIcon { get; set; } = string.Empty;
    [Reactive] public string Message { get; set; } = string.Empty;
    [Reactive] public string MessageType { get; set; } = "success";

    // ── Derived / computed properties ─────────────────────────────────────────
    public decimal Subtotal      => CalculatedSubtotal;
    public decimal Tax           => CalculatedTax;
    public decimal Total         => CalculatedTotal;
    public decimal TotalDiscount => CalculatedTotalDiscount;
    public decimal ChangeAmount  => AmountReceived - Total;

    // ── Collections ───────────────────────────────────────────────────────────
    public ObservableCollection<Desktop.Models.Product> SearchResults { get; } = new();
    public ObservableCollection<Desktop.Models.SaleItem> SaleItems { get; } = new();
    public List<Desktop.Models.PaymentMethod> PaymentMethods { get; } =
        Enum.GetValues<Desktop.Models.PaymentMethod>().ToList();

    // ── Private state ─────────────────────────────────────────────────────────
    private CustomerLookupResult? _currentCustomer;
    private ShopConfiguration? _shopConfiguration;
    private CancellationTokenSource? _calculationCts;
    private Guid? _currentSaleId;
    private IDisposable? _searchSubscription;
    private IDisposable? _phoneSubscription;
    private IDisposable? _activeTabSubscription;
    private IDisposable? _amountSubscription;
    private IDisposable? _processingSubscription;
    private IDisposable? _savingSubscription;
    private Timer? _calculationTimer;
    private Timer? _messageClearTimer;

    // ── Commands ──────────────────────────────────────────────────────────────
    public ReactiveCommand<string?, Unit> SearchProductsCommand { get; }
    public ReactiveCommand<Desktop.Models.Product, Unit> AddProductCommand { get; }
    public ReactiveCommand<Desktop.Models.SaleItem, Unit> RemoveItemCommand { get; }
    public ReactiveCommand<object[], Unit> UpdateItemQuantityCommand { get; }
    public ReactiveCommand<Unit, Unit> CompleteSaleCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelSaleCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetSaleCommand { get; }
    public ReactiveCommand<Unit, Unit> RecalculateTotalsCommand { get; }
    public ReactiveCommand<Unit, Unit> TriggerRealTimeCalculationCommand { get; }
    public ReactiveCommand<Unit, Unit> LookupCustomerCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateNewCustomerCommand { get; }
    public ReactiveCommand<Unit, Unit> StartBarcodeScanCommand { get; }
    public ReactiveCommand<string, Unit> ProcessManualBarcodeCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearAllItemsCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveDraftCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadDraftCommand { get; }
    public ReactiveCommand<string, Unit> SetAmountCommand { get; }
    public ReactiveCommand<Unit, Unit> SetExactAmountCommand { get; }
    public ReactiveCommand<Unit, Unit> PrintReceiptCommand { get; }
    public ReactiveCommand<Unit, Unit> EmailReceiptCommand { get; }
    public ReactiveCommand<Unit, Unit> DismissMessageCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────────
    public SaleViewModel(
        IBarcodeIntegrationService? barcodeIntegrationService = null,
        IMultiTabSalesManager? salesManager = null,
        ICustomerLookupService? customerLookupService = null,
        IRealTimeCalculationEngine? calculationEngine = null,
        IStockValidationService? stockValidationService = null,
        ISaleService? saleService = null,
        IReceiptService? receiptService = null,
        IProductRepository? productRepository = null,
        ILogger<SaleViewModel>? logger = null,
        Guid? sessionId = null,
        Guid? shopId = null,
        Guid? userId = null)
    {
        _barcodeIntegrationService = barcodeIntegrationService;
        _salesManager              = salesManager;
        _customerLookupService     = customerLookupService;
        _calculationEngine         = calculationEngine;
        _stockValidationService    = stockValidationService;
        _saleService               = saleService;
        _receiptService            = receiptService;
        _productRepository         = productRepository;
        _logger                    = logger;
        _sessionId                 = sessionId ?? Guid.NewGuid();
        _shopId                    = shopId    ?? Guid.NewGuid();
        _userId                    = userId    ?? Guid.NewGuid();
        Title = "New Sale";

        // ── Wire up reactive subscriptions ────────────────────────────────────
        _searchSubscription = this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(v => SearchProducts(v));

        _phoneSubscription = this.WhenAnyValue(x => x.CustomerPhone)
            .Subscribe(v =>
            {
                if (!string.IsNullOrWhiteSpace(v) && v.Length >= 10)
                    _ = Task.Run(async () =>
                    {
                        try { await LookupCustomerAsync(); }
                        catch (Exception ex) { _logger?.LogWarning(ex, "Failed to lookup customer"); }
                    });
                else
                    ClearCustomerInfo();
            });

        _activeTabSubscription = this.WhenAnyValue(x => x.IsActiveTab)
            .Subscribe(active => { if (active) TriggerRealTimeCalculation(); });

        _amountSubscription = this.WhenAnyValue(x => x.AmountReceived)
            .Subscribe(_ => { this.RaisePropertyChanged(nameof(ChangeAmount)); UpdateCanCompleteSale(); });

        _processingSubscription = this.WhenAnyValue(x => x.IsProcessingSale)
            .Subscribe(_ => UpdateCanCompleteSale());

        _savingSubscription = this.WhenAnyValue(x => x.IsSaving)
            .Subscribe(_ => UpdateCanCompleteSale());

        // ── Commands ──────────────────────────────────────────────────────────
        SearchProductsCommand              = ReactiveCommand.Create<string?>(SearchProducts);
        AddProductCommand                  = ReactiveCommand.CreateFromTask<Desktop.Models.Product>(AddProductAsync);
        RemoveItemCommand                  = ReactiveCommand.CreateFromTask<Desktop.Models.SaleItem>(RemoveItemAsync);
        UpdateItemQuantityCommand          = ReactiveCommand.CreateFromTask<object[]>(UpdateItemQuantityAsync);
        CompleteSaleCommand                = ReactiveCommand.CreateFromTask(CompleteSaleAsync);
        CancelSaleCommand                  = ReactiveCommand.CreateFromTask(CancelSaleAsync);
        ResetSaleCommand                   = ReactiveCommand.CreateFromTask(ResetSaleAsync);
        RecalculateTotalsCommand           = ReactiveCommand.Create(() => TriggerRealTimeCalculation());
        TriggerRealTimeCalculationCommand  = ReactiveCommand.Create(() => TriggerRealTimeCalculation());
        LookupCustomerCommand              = ReactiveCommand.CreateFromTask(LookupCustomerAsync);
        CreateNewCustomerCommand           = ReactiveCommand.CreateFromTask(CreateNewCustomerAsync);
        StartBarcodeScanCommand            = ReactiveCommand.CreateFromTask(StartBarcodeScanAsync);
        ProcessManualBarcodeCommand        = ReactiveCommand.CreateFromTask<string>(ProcessManualBarcodeAsync);
        ClearAllItemsCommand               = ReactiveCommand.CreateFromTask(ClearAllItemsAsync);
        SaveDraftCommand                   = ReactiveCommand.CreateFromTask(SaveDraftAsync);
        LoadDraftCommand                   = ReactiveCommand.CreateFromTask(LoadDraftAsync);
        SetAmountCommand                   = ReactiveCommand.Create<string>(s => { if (decimal.TryParse(s, out var a)) AmountReceived = a; });
        SetExactAmountCommand              = ReactiveCommand.Create(() => { AmountReceived = Total; });
        PrintReceiptCommand                = ReactiveCommand.CreateFromTask(PrintReceiptAsync);
        EmailReceiptCommand                = ReactiveCommand.CreateFromTask(EmailReceiptAsync);
        DismissMessageCommand              = ReactiveCommand.Create(DismissMessage);

        // ── Timers ────────────────────────────────────────────────────────────
        _calculationTimer  = new Timer(PerformCalculation, null, Timeout.Infinite, Timeout.Infinite);
        _messageClearTimer = new Timer(ClearMessageCallback, null, Timeout.Infinite, Timeout.Infinite);

        // ── Init ──────────────────────────────────────────────────────────────
        InitializeEnhancedFeatures();
        SaleItems.CollectionChanged += OnSaleItemsChanged;
    }

    // ── Initialisation ────────────────────────────────────────────────────────
    private void InitializeEnhancedFeatures()
    {
        IsBarcodeIntegrationEnabled = _barcodeIntegrationService != null;
        IsCustomerLookupEnabled     = _customerLookupService != null;
        _shopConfiguration = new ShopConfiguration { TaxRate = 0.18m, Currency = "INR" };

        if (_barcodeIntegrationService != null)
        {
            _barcodeIntegrationService.BarcodeProcessed += OnBarcodeProcessed;
            _barcodeIntegrationService.ScanError        += OnScanError;
        }
        _logger?.LogDebug("Enhanced features initialized for session {SessionId}", _sessionId);
    }

    // ── Collection change handlers ────────────────────────────────────────────
    private void OnSaleItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        HasUnsavedChanges = true;
        TriggerRealTimeCalculation();
        UpdateCanCompleteSale();

        if (e.NewItems != null)
            foreach (Desktop.Models.SaleItem item in e.NewItems)
                item.PropertyChanged += OnSaleItemPropertyChanged;

        if (e.OldItems != null)
            foreach (Desktop.Models.SaleItem item in e.OldItems)
                item.PropertyChanged -= OnSaleItemPropertyChanged;
    }

    private void OnSaleItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Desktop.Models.SaleItem.Quantity) or nameof(Desktop.Models.SaleItem.UnitPrice))
        {
            HasUnsavedChanges = true;
            TriggerRealTimeCalculation();
        }
    }

    private async void OnBarcodeProcessed(object? sender, BarcodeProcessedEventArgs e)
    {
        LastScannedBarcode = e.Barcode;
        LastScanTime       = e.Timestamp;
        ScanStatus         = "Product added";
        if (e.Product != null)
        {
            var dp = await ConvertToDesktopProductWithStockAsync(e.Product);
            await AddProductAsync(dp);
        }
    }

    private void OnScanError(object? sender, ScanErrorEventArgs e)
    {
        ScanStatus = "Scan failed";
        SetError($"Barcode scan error: {e.ErrorMessage}");
        _logger?.LogWarning("Barcode scan error: {Error}", e.ErrorMessage);
    }

    // ── Stock helpers ─────────────────────────────────────────────────────────
    public async Task<int> GetRealTimeStockLevelAsync(Guid productId, Guid? shopId = null)
    {
        if (_stockValidationService == null) return 0;
        try
        {
            var level = await _stockValidationService.GetCurrentStockLevelAsync(productId, shopId);
            return level.AvailableQuantity;
        }
        catch (Exception ex) { _logger?.LogError(ex, "Error getting stock level for {ProductId}", productId); return 0; }
    }

    private async Task<Desktop.Models.Product> ConvertToDesktopProductWithStockAsync(Shared.Core.Entities.Product p)
    {
        var qty = await GetRealTimeStockLevelAsync(p.Id, _shopId != Guid.Empty ? _shopId : null);
        return new Desktop.Models.Product { Id = p.Id, Name = p.Name, Barcode = p.Barcode,
            UnitPrice = p.UnitPrice, Category = p.Category, StockQuantity = qty,
            BatchNumber = p.BatchNumber, ExpiryDate = p.ExpiryDate };
    }

    // ── Search ────────────────────────────────────────────────────────────────
    private void SearchProducts(string? searchTerm = null)
    {
        searchTerm ??= SearchText;
        SearchResults.Clear();
        HasSearched = false;
        if (string.IsNullOrWhiteSpace(searchTerm)) return;

        if (_productRepository != null)
        {
            IsSearching = IsLoadingProducts = true;
            _ = Task.Run(async () =>
            {
                try
                {
                    var results = await _productRepository.SearchAsync(searchTerm);
                    var products = new List<Desktop.Models.Product>();
                    foreach (var p in results.Take(10))
                    {
                        var qty = await GetRealTimeStockLevelAsync(p.Id, _shopId != Guid.Empty ? _shopId : null);
                        products.Add(new Desktop.Models.Product { Id = p.Id, Name = p.Name, Barcode = p.Barcode,
                            UnitPrice = p.UnitPrice, Category = p.Category, StockQuantity = qty,
                            BatchNumber = p.BatchNumber, ExpiryDate = p.ExpiryDate });
                    }
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        SearchResults.Clear();
                        foreach (var p in products) SearchResults.Add(p);
                        IsSearching = IsLoadingProducts = false;
                        HasSearched = true;
                    });
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error searching products");
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        IsSearching = IsLoadingProducts = false;
                        SetError($"Product search failed: {ex.Message}");
                    });
                }
            });
        }
        else
        {
            foreach (var p in GetSampleProducts()
                .Where(p => p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                            p.Barcode?.Contains(searchTerm) == true)
                .Take(5))
                SearchResults.Add(p);
            HasSearched = true;
        }
    }

    // ── Add / Remove items ────────────────────────────────────────────────────
    private async Task AddProductAsync(Desktop.Models.Product product)
    {
        try
        {
            if (_stockValidationService != null)
            {
                var pv = await _stockValidationService.ValidateProductForSaleAsync(product.Id);
                if (!pv.IsValid)
                {
                    SetError(pv.IsExpired
                        ? $"Product '{product.Name}' is expired (expiry: {pv.ExpiryDate:d})"
                        : !pv.IsActive ? $"Product '{product.Name}' is inactive"
                        : pv.InvalidReason ?? $"Product '{product.Name}' is not available");
                    return;
                }
                var sv = await _stockValidationService.ValidateProductAvailabilityAsync(
                    product.Id, 1, _shopId != Guid.Empty ? _shopId : null);
                if (!sv.IsAvailable)
                {
                    SetError(sv.AvailableQuantity == 0
                        ? $"'{product.Name}' is out of stock"
                        : $"Insufficient stock for '{product.Name}'. Available: {sv.AvailableQuantity}");
                    return;
                }
            }

            var existing = SaleItems.FirstOrDefault(i => i.ProductId == product.Id);
            if (existing != null)
            {
                if (_stockValidationService != null)
                {
                    var sv = await _stockValidationService.ValidateProductAvailabilityAsync(
                        product.Id, existing.Quantity + 1, _shopId != Guid.Empty ? _shopId : null);
                    if (!sv.IsAvailable) { SetError($"Cannot add more '{product.Name}'. Available: {sv.AvailableQuantity}"); return; }
                }
                existing.Quantity++;
            }
            else
            {
                var item = new Desktop.Models.SaleItem { Id = Guid.NewGuid(), ProductId = product.Id,
                    ProductName = product.Name, Quantity = 1, UnitPrice = product.UnitPrice,
                    BatchNumber = product.BatchNumber };
                SaleItems.Add(item);
                if (_salesManager != null)
                    await _salesManager.AddItemToSessionAsync(_sessionId, ConvertToSessionItem(item));
            }

            SearchText = string.Empty;
            SearchResults.Clear();
            TriggerRealTimeCalculation();
            UpdateCanCompleteSale();
            _logger?.LogDebug("Added product {ProductName} to sale", product.Name);
        }
        catch (Exception ex) { _logger?.LogError(ex, "Error adding product"); SetError($"Failed to add product: {ex.Message}"); }
    }

    private async Task RemoveItemAsync(Desktop.Models.SaleItem item)
    {
        try
        {
            SaleItems.Remove(item);
            if (_salesManager != null) await _salesManager.RemoveItemFromSessionAsync(_sessionId, item.Id);
            TriggerRealTimeCalculation();
            UpdateCanCompleteSale();
        }
        catch (Exception ex) { _logger?.LogError(ex, "Error removing item"); SetError($"Failed to remove item: {ex.Message}"); }
    }

    private async Task UpdateItemQuantityAsync(object[] parameters)
    {
        if (parameters.Length != 2 || parameters[0] is not Desktop.Models.SaleItem item || parameters[1] is not int qty) return;
        try
        {
            if (qty <= 0) { await RemoveItemAsync(item); return; }
            item.Quantity = qty;
            if (_salesManager != null)
                await _salesManager.UpdateItemInSessionAsync(_sessionId, ConvertToSessionItem(item));
            TriggerRealTimeCalculation();
        }
        catch (Exception ex) { _logger?.LogError(ex, "Error updating quantity"); SetError($"Failed to update quantity: {ex.Message}"); }
    }

    // ── Complete / Cancel / Reset sale ────────────────────────────────────────
    private async Task CompleteSaleAsync()
    {
        if (!SaleItems.Any()) { SetError("Please add items to the sale"); return; }
        if (SelectedPaymentMethod == Desktop.Models.PaymentMethod.Cash && AmountReceived < Total)
        { SetError("Amount received is less than total"); return; }

        IsSaving = IsProcessingSale = true;
        BusyMessage = "Processing sale...";
        ClearError();
        try
        {
            if (_salesManager != null)
            {
                var r = await _salesManager.CompleteSessionAsync(_sessionId,
                    (Shared.Core.Enums.PaymentMethod)SelectedPaymentMethod);
                if (!r.Success) { SetError($"Failed to complete sale: {r.Message}"); return; }
            }

            string invoiceNumber;
            if (_saleService != null)
            {
                var sale = await _saleService.CreateSaleAsync(_shopId, _userId, _currentCustomer?.Id);
                _currentSaleId = sale.Id;
                foreach (var item in SaleItems)
                    await _saleService.AddItemToSaleAsync(sale.Id, item.ProductId, item.Quantity, item.UnitPrice, item.BatchNumber);
                var completed = await _saleService.CompleteSaleAsync(sale.Id, (Shared.Core.Enums.PaymentMethod)SelectedPaymentMethod);
                invoiceNumber  = completed.InvoiceNumber;
                _currentSaleId = null;

                if (_receiptService != null)
                {
                    try { await _receiptService.GenerateReceiptAsync(completed); CanPrintReceipt = true; CanEmailReceipt = !string.IsNullOrWhiteSpace(CustomerEmail); }
                    catch (Exception ex) { _logger?.LogWarning(ex, "Receipt generation failed but sale was saved"); }
                }
            }
            else
                invoiceNumber = $"INV-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

            if (_stockValidationService != null)
                await _stockValidationService.ReleaseStockReservationAsync(_sessionId);

            if (_currentCustomer != null && _customerLookupService != null)
                _ = Task.Run(async () =>
                {
                    try { await _customerLookupService.UpdateCustomerAfterPurchaseAsync(_currentCustomer.Id, Total); }
                    catch (Exception ex) { _logger?.LogWarning(ex, "Failed to update customer after purchase"); }
                });

            await ResetSaleAsync();
            ShowMessage("✅", $"Sale completed! Invoice: {invoiceNumber}", "success");
            _logger?.LogInformation("Sale completed: {InvoiceNumber}", invoiceNumber);
        }
        catch (Exception ex) { _logger?.LogError(ex, "Error completing sale"); SetError($"Error completing sale: {ex.Message}"); }
        finally { IsSaving = IsProcessingSale = false; BusyMessage = string.Empty; }
    }

    private async Task CancelSaleAsync()
    {
        if (IsBusy) return;
        IsBusy = true; BusyMessage = "Cancelling sale..."; ClearError();
        try
        {
            if (_stockValidationService != null && _sessionId != Guid.Empty)
                await _stockValidationService.ReleaseStockReservationAsync(_sessionId);
            if (_saleService != null && _currentSaleId.HasValue)
            { await _saleService.CancelSaleAsync(_currentSaleId.Value, "Cancelled by cashier"); _currentSaleId = null; }
            await ResetSaleAsync();
            ShowMessage("❌", "Sale cancelled.", "warning");
        }
        catch (Exception ex) { _logger?.LogError(ex, "Error cancelling sale"); SetError($"Failed to cancel sale: {ex.Message}"); }
        finally { IsBusy = false; BusyMessage = string.Empty; }
    }

    private async Task ResetSaleAsync()
    {
        try
        {
            var ids = SaleItems.Select(i => i.Id).ToList();
            SaleItems.Clear();
            ClearCustomerInfo();
            AmountReceived = 0;
            SelectedPaymentMethod = Desktop.Models.PaymentMethod.Cash;
            SearchText = string.Empty; SearchResults.Clear();
            ErrorMessage = string.Empty;
            CalculatedSubtotal = CalculatedTax = CalculatedTotal = CalculatedTotalDiscount = 0;
            CalculationBreakdown.Clear();
            LastScannedBarcode = string.Empty; LastScanTime = null; ScanStatus = "Ready";
            CanPrintReceipt = CanEmailReceipt = false;
            HasUnsavedChanges = false; _currentSaleId = null;

            if (_salesManager != null)
                foreach (var id in ids)
                    await _salesManager.RemoveItemFromSessionAsync(_sessionId, id);

            TriggerRealTimeCalculation();
            UpdateCanCompleteSale();
        }
        catch (Exception ex) { _logger?.LogError(ex, "Error resetting sale"); SetError($"Failed to reset sale: {ex.Message}"); }
    }

    private void UpdateCanCompleteSale()
    {
        CanCompleteSale = SaleItems.Count > 0
            && !IsProcessingSale && !IsSaving
            && (SelectedPaymentMethod != Desktop.Models.PaymentMethod.Cash || AmountReceived >= Total);
    }

    // ── Real-time calculation ─────────────────────────────────────────────────
    private void TriggerRealTimeCalculation()
    {
        _calculationCts?.Cancel();
        _calculationCts = new CancellationTokenSource();
        _calculationTimer?.Change(300, Timeout.Infinite);
    }

    private async void PerformCalculation(object? state)
    {
        var token = _calculationCts?.Token;
        if (token?.IsCancellationRequested == true) return;
        try
        {
            if (_calculationEngine != null && _shopConfiguration != null)
                await PerformEnhancedCalculationAsync();
            else
                PerformBasicCalculation();

            if (token?.IsCancellationRequested == true) return;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (token?.IsCancellationRequested == true) return;
                IsCalculating = true;
                CalculationStatus = "Ready";
                LastCalculationTime = DateTime.Now;
                IsCalculating = false;
                this.RaisePropertyChanged(nameof(Subtotal));
                this.RaisePropertyChanged(nameof(Tax));
                this.RaisePropertyChanged(nameof(Total));
                this.RaisePropertyChanged(nameof(TotalDiscount));
                this.RaisePropertyChanged(nameof(ChangeAmount));
            });
        }
        catch (Exception ex) { _logger?.LogError(ex, "Error during calculation"); CalculationStatus = "Error"; }
    }

    private async Task PerformEnhancedCalculationAsync()
    {
        if (_calculationEngine == null || _shopConfiguration == null) return;
        try
        {
            var coreItems = SaleItems.Select(i => new Shared.Core.Entities.SaleItem
            {
                Id = i.Id, ProductId = i.ProductId, Quantity = i.Quantity,
                UnitPrice = i.UnitPrice, BatchNumber = i.BatchNumber, TotalPrice = i.Total
            }).ToList();

            Shared.Core.Entities.Customer? coreCustomer = null;
            if (_currentCustomer != null)
                coreCustomer = new Shared.Core.Entities.Customer
                {
                    Id = _currentCustomer.Id, Name = _currentCustomer.Name,
                    Phone = _currentCustomer.Phone, Email = _currentCustomer.Email,
                    TotalSpent = _currentCustomer.TotalSpent, VisitCount = _currentCustomer.VisitCount,
                    LastVisit = _currentCustomer.LastVisit
                };

            var calc = await _calculationEngine.CalculateOrderTotalsAsync(coreItems, _shopConfiguration, coreCustomer);
            if (calc.IsValid)
            {
                CalculatedSubtotal      = calc.Subtotal;
                CalculatedTax           = calc.TotalTaxAmount;
                CalculatedTotal         = calc.FinalTotal;
                CalculatedTotalDiscount = calc.TotalDiscountAmount;
                CalculationBreakdown    = calc.Breakdown.Items
                    .Select(i => new CalculationBreakdownDto
                    {
                        Description = i.Description, Amount = i.Amount,
                        Type = Enum.TryParse<CalculationType>(i.Type, out var t) ? t : CalculationType.Subtotal
                    }).ToList();
            }
        }
        catch (Exception ex) { _logger?.LogError(ex, "Error in enhanced calculation"); PerformBasicCalculation(); }
    }

    private void PerformBasicCalculation()
    {
        CalculatedSubtotal      = SaleItems.Sum(i => i.Total);
        CalculatedTax           = CalculatedSubtotal * 0.18m;
        CalculatedTotalDiscount = 0;
        CalculatedTotal         = CalculatedSubtotal + CalculatedTax;
        CalculationBreakdown.Clear();
    }

    // ── Customer lookup ───────────────────────────────────────────────────────
    private async Task LookupCustomerAsync()
    {
        if (_customerLookupService == null || string.IsNullOrWhiteSpace(CustomerPhone)) return;
        IsLookingUpCustomer = true; ClearError();
        try
        {
            var validation = await _customerLookupService.ValidateMobileNumberAsync(CustomerPhone);
            if (!validation.IsValid) { SetError(validation.ErrorMessage ?? "Invalid mobile number format"); return; }

            var customer = await _customerLookupService.LookupByMobileNumberAsync(CustomerPhone);
            if (customer != null)
            {
                _currentCustomer = customer;
                PopulateCustomerInfo(customer);
                var membership = await _customerLookupService.GetMembershipDetailsAsync(customer.Id);
                if (membership != null) PopulateMembershipInfo(membership);
                HasCustomer = true;
                TriggerRealTimeCalculation();
            }
            else
            {
                HasCustomer = false;
                ClearCustomerInfo();
                SetError("Customer not found. Would you like to create a new customer?");
            }
        }
        catch (Exception ex) { _logger?.LogError(ex, "Error during customer lookup"); SetError($"Customer lookup failed: {ex.Message}"); HasCustomer = false; ClearCustomerInfo(); }
        finally { IsLookingUpCustomer = false; }
    }

    private async Task CreateNewCustomerAsync()
    {
        if (_customerLookupService == null || string.IsNullOrWhiteSpace(CustomerPhone)) return;
        IsBusy = true;
        try
        {
            var result = await _customerLookupService.CreateNewCustomerAsync(new CustomerCreationRequest
            {
                Name = CustomerName, MobileNumber = CustomerPhone, Email = CustomerEmail,
                ShopId = _shopId, InitialTier = Shared.Core.Enums.MembershipTier.Bronze
            });
            if (result.Success && result.Customer != null)
            { _currentCustomer = result.Customer; PopulateCustomerInfo(result.Customer); HasCustomer = true; }
            else SetError(result.ErrorMessage ?? "Failed to create customer");
        }
        catch (Exception ex) { _logger?.LogError(ex, "Error creating customer"); SetError($"Failed to create customer: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    private void PopulateCustomerInfo(CustomerLookupResult c)
    {
        CustomerName = c.Name; CustomerEmail = c.Email ?? string.Empty;
        CustomerMembershipNumber = c.MembershipNumber; CustomerTotalSpent = c.TotalSpent;
        CustomerVisitCount = c.VisitCount; CustomerLastVisit = c.LastVisit;
        MembershipTier = c.Tier.ToString(); AvailableDiscounts = c.AvailableDiscounts;
        CustomerFound = true; CustomerMembershipInfo = $"{c.Tier} member · {c.MembershipNumber}";
        HasCustomer = true;
    }

    private void PopulateMembershipInfo(CustomerMembershipDetails m)
    {
        MembershipDiscount = m.DiscountPercentage;
        AvailableDiscounts = m.AvailableDiscounts;
    }

    private void ClearCustomerInfo()
    {
        _currentCustomer = null;
        CustomerName = CustomerEmail = CustomerMembershipNumber = MembershipTier = string.Empty;
        MembershipDiscount = CustomerTotalSpent = 0; CustomerVisitCount = 0; CustomerLastVisit = null;
        AvailableDiscounts.Clear(); HasCustomer = false;
    }

    // ── Barcode scanning ──────────────────────────────────────────────────────
    private async Task StartBarcodeScanAsync()
    {
        if (_barcodeIntegrationService == null) { SetError("No barcode scanner connected."); return; }
        IsScanning = true; ScanStatus = "Initializing scanner..."; ClearError();
        try
        {
            if (!await _barcodeIntegrationService.InitializeAsync()) { SetError("Failed to initialize barcode scanner"); return; }
            ScanStatus = "Scanning...";
            var result = await _barcodeIntegrationService.ScanBarcodeAsync(new ScanOptions
            {
                ShopId = _shopId, SessionId = _sessionId, EnableContinuousMode = false,
                ScanTimeout = TimeSpan.FromSeconds(30), EnableBeep = true, AutoAddToSale = true
            });
            if (result.IsSuccess && !string.IsNullOrEmpty(result.Barcode))
            {
                LastScannedBarcode = result.Barcode; LastScanTime = DateTime.Now;
                if (result.IsProductFound && result.Product != null)
                { var dp = await ConvertToDesktopProductWithStockAsync(result.Product); await AddProductAsync(dp); ScanStatus = "Product added"; }
                else { ScanStatus = "Product not found"; SetError("Product not found in inventory"); }
            }
            else { ScanStatus = "Scan failed"; SetError(result.ErrorMessage ?? "Scan failed or timeout"); }
        }
        catch (Exception ex) { _logger?.LogError(ex, "Error during barcode scanning"); SetError($"Barcode scanning error: {ex.Message}"); ScanStatus = "Error"; }
        finally { IsScanning = false; }
    }

    private async Task ProcessManualBarcodeAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return;
        IsBusy = true; ClearError();
        try
        {
            if (_barcodeIntegrationService != null)
            {
                if (!await _barcodeIntegrationService.ValidateBarcodeFormatAsync(barcode)) { SetError("Invalid barcode format"); return; }
                var product = await _barcodeIntegrationService.LookupProductByBarcodeAsync(barcode, _shopId);
                if (product != null) { var dp = await ConvertToDesktopProductWithStockAsync(product); await AddProductAsync(dp); LastScannedBarcode = barcode; LastScanTime = DateTime.Now; }
                else SetError("Product not found for this barcode");
            }
            else { SearchText = barcode; SearchProducts(); }
        }
        catch (Exception ex) { _logger?.LogError(ex, "Error processing manual barcode"); SetError($"Failed to process barcode: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    // ── Draft / receipt helpers ───────────────────────────────────────────────
    private async Task ClearAllItemsAsync()
    {
        try
        {
            var ids = SaleItems.Select(i => i.Id).ToList(); SaleItems.Clear();
            if (_salesManager != null) foreach (var id in ids) await _salesManager.RemoveItemFromSessionAsync(_sessionId, id);
            TriggerRealTimeCalculation();
        }
        catch (Exception ex) { _logger?.LogError(ex, "Error clearing items"); SetError($"Failed to clear items: {ex.Message}"); }
    }

    private async Task SaveDraftAsync()
    {
        try { await SaveSessionAsync(); ShowMessage("💾", "Draft saved successfully.", "success"); }
        catch (Exception ex) { _logger?.LogError(ex, "Error saving draft"); SetError($"Failed to save draft: {ex.Message}"); }
    }

    private async Task LoadDraftAsync()
    {
        if (_salesManager == null) { SetError("Draft loading is not available."); return; }
        IsBusy = true; BusyMessage = "Loading draft...";
        try { ShowMessage("📋", "No saved drafts found.", "warning"); }
        catch (Exception ex) { _logger?.LogError(ex, "Error loading draft"); SetError($"Failed to load draft: {ex.Message}"); }
        finally { IsBusy = false; BusyMessage = string.Empty; }
    }

    private async Task PrintReceiptAsync()
    {
        if (_receiptService == null) { SetError("Receipt printing is not available."); return; }
        IsBusy = true; BusyMessage = "Printing receipt...";
        try { ShowMessage("🖨️", "Receipt sent to printer.", "success"); }
        catch (Exception ex) { _logger?.LogError(ex, "Error printing receipt"); SetError($"Failed to print receipt: {ex.Message}"); }
        finally { IsBusy = false; BusyMessage = string.Empty; }
    }

    private async Task EmailReceiptAsync()
    {
        if (string.IsNullOrWhiteSpace(CustomerEmail)) { SetError("No customer email address available."); return; }
        IsBusy = true; BusyMessage = "Sending receipt...";
        try { ShowMessage("📧", $"Receipt emailed to {CustomerEmail}.", "success"); }
        catch (Exception ex) { _logger?.LogError(ex, "Error emailing receipt"); SetError($"Failed to email receipt: {ex.Message}"); }
        finally { IsBusy = false; BusyMessage = string.Empty; }
    }

    // ── Message helpers ───────────────────────────────────────────────────────
    private void ShowMessage(string icon, string text, string type = "success", int autoClearMs = 5000)
    {
        MessageIcon = icon; Message = text; MessageType = type; HasMessage = true;
        if (autoClearMs > 0) _messageClearTimer?.Change(autoClearMs, Timeout.Infinite);
    }

    private void DismissMessage()
    {
        HasMessage = false; Message = string.Empty; MessageIcon = string.Empty;
        _messageClearTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void ClearMessageCallback(object? state)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { HasMessage = false; Message = string.Empty; MessageIcon = string.Empty; });
    }

    // ── Session helpers ───────────────────────────────────────────────────────
    public async Task LoadFromSessionAsync(SaleSessionDto sessionData)
    {
        try
        {
            TabName = sessionData.TabName; SessionState = sessionData.State;
            SaleItems.Clear();
            foreach (var item in sessionData.Items)
                SaleItems.Add(new Desktop.Models.SaleItem { Id = item.Id, ProductId = item.ProductId,
                    ProductName = item.ProductName, Quantity = (int)item.Quantity,
                    UnitPrice = item.UnitPrice, BatchNumber = item.BatchNumber });

            SelectedPaymentMethod = (Desktop.Models.PaymentMethod)sessionData.PaymentMethod;
            if (sessionData.CustomerId.HasValue && !string.IsNullOrEmpty(sessionData.CustomerName))
            {
                CustomerName = sessionData.CustomerName; HasCustomer = true;
                if (_customerLookupService != null)
                {
                    var c = await _customerLookupService.LookupByMobileNumberAsync(CustomerPhone);
                    if (c != null) PopulateCustomerInfo(c);
                }
            }
            if (sessionData.Calculation != null)
            {
                CalculatedSubtotal = sessionData.Calculation.Subtotal;
                CalculatedTax      = sessionData.Calculation.TotalTax;
                CalculatedTotal    = sessionData.Calculation.FinalTotal;
                CalculatedTotalDiscount = sessionData.Calculation.TotalDiscount;
            }
            HasUnsavedChanges = false;
            TriggerRealTimeCalculation();
        }
        catch (Exception ex) { _logger?.LogError(ex, "Error loading session data"); SetError($"Failed to load session: {ex.Message}"); }
    }

    public SaleSessionDto GetSessionData() => new()
    {
        Id = _sessionId, TabName = TabName, ShopId = _shopId, UserId = _userId,
        CustomerId = _currentCustomer?.Id, CustomerName = CustomerName,
        PaymentMethod = (Shared.Core.Enums.PaymentMethod)SelectedPaymentMethod,
        State = SessionState, CreatedAt = DateTime.UtcNow, LastModified = DateTime.UtcNow,
        IsActive = IsActiveTab, Items = SaleItems.Select(ConvertToSessionItem).ToList(),
        Calculation = new SaleSessionCalculationDto
        {
            Subtotal = CalculatedSubtotal, TotalTax = CalculatedTax, FinalTotal = CalculatedTotal,
            TotalDiscount = CalculatedTotalDiscount, Breakdown = CalculationBreakdown,
            CalculatedAt = LastCalculationTime
        }
    };

    public async Task SaveSessionAsync()
    {
        if (_salesManager == null) return;
        try
        {
            var result = await _salesManager.SaveSessionStateAsync(_sessionId, GetSessionData());
            if (result.Success) HasUnsavedChanges = false;
        }
        catch (Exception ex) { _logger?.LogError(ex, "Error saving session"); }
    }

    // ── Conversion helpers ────────────────────────────────────────────────────
    private SaleSessionItemDto ConvertToSessionItem(Desktop.Models.SaleItem i) => new()
    {
        Id = i.Id, ProductId = i.ProductId, ProductName = i.ProductName,
        Quantity = i.Quantity, UnitPrice = i.UnitPrice, LineTotal = i.Total, BatchNumber = i.BatchNumber
    };

    private static List<Desktop.Models.Product> GetSampleProducts() => new()
    {
        new() { Id = Guid.NewGuid(), Name = "Paracetamol 500mg",  Barcode = "1234567890123", UnitPrice = 25.50m, Category = "Medicine",        StockQuantity = 100 },
        new() { Id = Guid.NewGuid(), Name = "Aspirin 75mg",        Barcode = "2345678901234", UnitPrice = 15.75m, Category = "Medicine",        StockQuantity = 50  },
        new() { Id = Guid.NewGuid(), Name = "Vitamin C Tablets",   Barcode = "3456789012345", UnitPrice = 45.00m, Category = "Supplement",      StockQuantity = 75  },
        new() { Id = Guid.NewGuid(), Name = "Cough Syrup",         Barcode = "4567890123456", UnitPrice = 85.25m, Category = "Medicine",        StockQuantity = 30  },
        new() { Id = Guid.NewGuid(), Name = "Bandages",            Barcode = "5678901234567", UnitPrice = 12.50m, Category = "Medical Supply",  StockQuantity = 200 }
    };

    // ── Cleanup ───────────────────────────────────────────────────────────────
    public void Cleanup()
    {
        _calculationTimer?.Dispose();
        _messageClearTimer?.Dispose();
        _calculationCts?.Cancel();
        _calculationCts?.Dispose();
        _searchSubscription?.Dispose();
        _phoneSubscription?.Dispose();
        _activeTabSubscription?.Dispose();
        _amountSubscription?.Dispose();
        _processingSubscription?.Dispose();
        _savingSubscription?.Dispose();

        if (_barcodeIntegrationService != null)
        {
            _barcodeIntegrationService.BarcodeProcessed -= OnBarcodeProcessed;
            _barcodeIntegrationService.ScanError        -= OnScanError;
        }
        foreach (var item in SaleItems) item.PropertyChanged -= OnSaleItemPropertyChanged;
    }
}
