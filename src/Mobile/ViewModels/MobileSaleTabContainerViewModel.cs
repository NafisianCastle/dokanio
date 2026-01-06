using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Shared.Core.Services;
using Shared.Core.DTOs;
using Microsoft.Extensions.Logging;
using Mobile.Services;

namespace Mobile.ViewModels;

/// <summary>
/// Mobile-specific ViewModel for managing multiple sale tabs
/// Enhanced with touch-optimized controls and mobile-specific features
/// </summary>
public partial class MobileSaleTabContainerViewModel : BaseViewModel
{
    private readonly IMultiTabSalesManager _multiTabSalesManager;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserContextService _userContextService;
    private readonly ICustomerLookupService _customerLookupService;
    private readonly IBarcodeIntegrationService _barcodeIntegrationService;
    private readonly ILogger<MobileSaleTabContainerViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<MobileSaleTabViewModel> saleTabs = new();

    [ObservableProperty]
    private MobileSaleTabViewModel? activeTab;

    [ObservableProperty]
    private object? activeTabContent;

    [ObservableProperty]
    private int maxTabs = 3; // Fewer tabs on mobile for better UX

    [ObservableProperty]
    private bool isTabSwitchingEnabled = true;

    [ObservableProperty]
    private bool enableSwipeGestures = true;

    [ObservableProperty]
    private bool enableHapticFeedback = true;

    [ObservableProperty]
    private MobileCustomerLookupViewModel customerLookupViewModel;

    [ObservableProperty]
    private MobileBarcodeScannerViewModel barcodeScannerViewModel;

    [ObservableProperty]
    private bool showCustomerLookup;

    [ObservableProperty]
    private bool showBarcodeScanner;

    private Guid _currentUserId;
    private Guid _currentDeviceId;
    private Guid _currentShopId;

    public MobileSaleTabContainerViewModel(
        IMultiTabSalesManager multiTabSalesManager,
        ICurrentUserService currentUserService,
        IUserContextService userContextService,
        ICustomerLookupService customerLookupService,
        IBarcodeIntegrationService barcodeIntegrationService,
        ILogger<MobileSaleTabContainerViewModel> logger)
    {
        _multiTabSalesManager = multiTabSalesManager;
        _currentUserService = currentUserService;
        _userContextService = userContextService;
        _customerLookupService = customerLookupService;
        _barcodeIntegrationService = barcodeIntegrationService;
        _logger = logger;
        
        Title = "Sales";
        
        // Initialize mobile-specific ViewModels
        CustomerLookupViewModel = new MobileCustomerLookupViewModel(_customerLookupService, logger);
        BarcodeScannerViewModel = new MobileBarcodeScannerViewModel(_barcodeIntegrationService, null!, logger);
        
        // Subscribe to events
        CustomerLookupViewModel.CustomerSelected += OnCustomerSelected;
        BarcodeScannerViewModel.ProductScanned += OnProductScanned;
        
        // Initialize with current user context
        InitializeUserContext();
    }

    private void InitializeUserContext()
    {
        var currentUser = _currentUserService.CurrentUser;
        var currentShop = _userContextService.CurrentShop;
        
        if (currentUser != null)
        {
            _currentUserId = currentUser.Id;
            var storedDeviceId = Microsoft.Maui.Storage.Preferences.Default.Get("device_id", string.Empty);
            if (!Guid.TryParse(storedDeviceId, out _currentDeviceId))
            {
                _currentDeviceId = Guid.NewGuid();
                Microsoft.Maui.Storage.Preferences.Default.Set("device_id", _currentDeviceId.ToString());
            }
            _currentShopId = currentShop?.Id ?? currentUser.ShopId ?? Guid.NewGuid();
        }
    }

    [RelayCommand]
    private async Task CreateNewTab()
    {
        if (IsBusy || !IsTabSwitchingEnabled) return;

        try
        {
            IsBusy = true;
            ClearError();

            // Check if we can create a new tab
            if (!await _multiTabSalesManager.CanCreateNewSessionAsync(_currentUserId, _currentDeviceId))
            {
                SetError($"Maximum number of tabs ({MaxTabs}) reached. Please close a tab first.");
                return;
            }

            // Generate tab name
            var tabName = GenerateMobileTabName();

            // Create new session
            var request = new CreateSaleSessionRequest
            {
                TabName = tabName,
                ShopId = _currentShopId,
                UserId = _currentUserId,
                DeviceId = _currentDeviceId
            };

            var result = await _multiTabSalesManager.CreateNewSaleSessionAsync(request);
            if (!result.Success)
            {
                SetError($"Failed to create new tab: {result.Message}");
                return;
            }

            // Create tab view model
            var tabViewModel = new MobileSaleTabViewModel(result.Session!, _multiTabSalesManager, _logger)
            {
                IsActive = false,
                CanClose = SaleTabs.Count > 0 // First tab cannot be closed
            };

            // Add to collection
            SaleTabs.Add(tabViewModel);

            // Switch to new tab
            await SwitchToTab(tabViewModel);

            // Provide haptic feedback
            TriggerHapticFeedback();

            _logger.LogInformation("Created new mobile sale tab.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new mobile sale tab");
            SetError($"Failed to create new tab: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SwitchToTab(MobileSaleTabViewModel tab)
    {
        if (tab == null || tab == ActiveTab || !IsTabSwitchingEnabled) return;

        try
        {
            IsTabSwitchingEnabled = false;

            // Save current tab state if there is one
            if (ActiveTab != null)
            {
                await SaveCurrentTabState();
                ActiveTab.IsActive = false;
            }

            // Switch to new tab
            ActiveTab = tab;
            ActiveTab.IsActive = true;
            ActiveTabContent = ActiveTab.SaleViewModel;

            // Notify the session manager
            await _multiTabSalesManager.SwitchToSessionAsync(tab.SessionId);

            // Provide haptic feedback
            TriggerHapticFeedback();

            _logger.LogDebug("Switched to mobile tab: {TabName}", tab.TabName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error switching to mobile tab {TabName}", tab.TabName);
            SetError($"Failed to switch to tab: {ex.Message}");
        }
        finally
        {
            IsTabSwitchingEnabled = true;
        }
    }

    [RelayCommand]
    private async Task CloseTab(MobileSaleTabViewModel tab)
    {
        if (tab == null || !tab.CanClose) return;

        try
        {
            // Show confirmation if tab has unsaved changes
            if (tab.HasUnsavedChanges)
            {
                var shouldSave = await Shell.Current.DisplayAlert(
                    "Unsaved Changes", 
                    $"Tab '{tab.TabName}' has unsaved changes. Save before closing?", 
                    "Save & Close", 
                    "Close Without Saving");

                if (shouldSave)
                {
                    await SaveTabState(tab);
                }
            }

            // Close the session
            await _multiTabSalesManager.CloseSessionAsync(tab.SessionId, true);

            // Remove from collection
            SaleTabs.Remove(tab);

            // If this was the active tab, switch to another tab
            if (ActiveTab == tab)
            {
                var nextTab = SaleTabs.FirstOrDefault();
                if (nextTab != null)
                {
                    await SwitchToTab(nextTab);
                }
                else
                {
                    ActiveTab = null;
                    ActiveTabContent = null;
                }
            }

            // Update close permissions for remaining tabs
            UpdateTabClosePermissions();

            // Provide haptic feedback
            TriggerHapticFeedback();

            _logger.LogInformation("Closed mobile sale tab: {TabName}", tab.TabName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing mobile tab {TabName}", tab.TabName);
            SetError($"Failed to close tab: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LoadExistingSessions()
    {
        try
        {
            IsBusy = true;
            ClearError();

            var sessions = await _multiTabSalesManager.GetActiveSessionsAsync(_currentUserId, _currentDeviceId);
            
            SaleTabs.Clear();

            foreach (var session in sessions)
            {
                var tabViewModel = new MobileSaleTabViewModel(session, _multiTabSalesManager, _logger)
                {
                    IsActive = false,
                    CanClose = sessions.Count > 1
                };

                SaleTabs.Add(tabViewModel);
            }

            // If no sessions exist, create a default one
            if (!SaleTabs.Any())
            {
                await CreateNewTab();
            }
            else
            {
                // Switch to first tab
                await SwitchToTab(SaleTabs.First());
            }

            UpdateTabClosePermissions();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading existing mobile sessions");
            SetError($"Failed to load sessions: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAllTabs()
    {
        try
        {
            foreach (var tab in SaleTabs)
            {
                await SaveTabState(tab);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving all mobile tabs");
            SetError($"Failed to save tabs: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SwipeToNextTab()
    {
        if (!EnableSwipeGestures || ActiveTab == null || SaleTabs.Count <= 1) return;

        var currentIndex = SaleTabs.IndexOf(ActiveTab);
        var nextIndex = (currentIndex + 1) % SaleTabs.Count;
        await SwitchToTab(SaleTabs[nextIndex]);
    }

    [RelayCommand]
    private async Task SwipeToPreviousTab()
    {
        if (!EnableSwipeGestures || ActiveTab == null || SaleTabs.Count <= 1) return;

        var currentIndex = SaleTabs.IndexOf(ActiveTab);
        var previousIndex = currentIndex == 0 ? SaleTabs.Count - 1 : currentIndex - 1;
        await SwitchToTab(SaleTabs[previousIndex]);
    }

    [RelayCommand]
    private async Task ShowCustomerLookupPanel()
    {
        ShowCustomerLookup = true;
        ShowBarcodeScanner = false;
        TriggerHapticFeedback();
        
        // Initialize customer lookup if needed
        if (CustomerLookupViewModel != null)
        {
            CustomerLookupViewModel.ClearError();
        }
    }

    [RelayCommand]
    private async Task HideCustomerLookupPanel()
    {
        ShowCustomerLookup = false;
        TriggerHapticFeedback();
    }

    [RelayCommand]
    private async Task ShowBarcodeScannerPanel()
    {
        ShowBarcodeScanner = true;
        ShowCustomerLookup = false;
        TriggerHapticFeedback();
        
        // Initialize barcode scanner
        if (BarcodeScannerViewModel != null)
        {
            await BarcodeScannerViewModel.InitializeAsync();
        }
    }

    [RelayCommand]
    private async Task HideBarcodeScannerPanel()
    {
        ShowBarcodeScanner = false;
        TriggerHapticFeedback();
        
        // Stop scanning if active
        if (BarcodeScannerViewModel?.IsScannerActive == true)
        {
            await BarcodeScannerViewModel.StopScanning();
        }
    }

    [RelayCommand]
    private async Task ToggleHapticFeedback()
    {
        EnableHapticFeedback = !EnableHapticFeedback;
        
        if (EnableHapticFeedback)
        {
            TriggerHapticFeedback();
        }
        
        // Update child ViewModels
        if (CustomerLookupViewModel != null)
        {
            CustomerLookupViewModel.EnableHapticFeedback = EnableHapticFeedback;
        }
        
        if (BarcodeScannerViewModel != null)
        {
            BarcodeScannerViewModel.EnableVibration = EnableHapticFeedback;
        }
    }

    [RelayCommand]
    private async Task ToggleSwipeGestures()
    {
        EnableSwipeGestures = !EnableSwipeGestures;
        TriggerHapticFeedback();
    }

    [RelayCommand]
    private async Task QuickAddTab()
    {
        // Quick add with haptic feedback and optimized for mobile
        TriggerHapticFeedback();
        await CreateNewTab();
    }

    [RelayCommand]
    private async Task LongPressTabOptions(MobileSaleTabViewModel tab)
    {
        if (tab == null) return;

        TriggerHapticFeedback();
        
        var options = new List<string> { "Rename Tab", "Duplicate Tab" };
        if (tab.CanClose)
        {
            options.Add("Close Tab");
        }
        
        var action = await Shell.Current.DisplayActionSheet(
            $"Tab Options: {tab.TabName}", 
            "Cancel", 
            null, 
            options.ToArray());

        switch (action)
        {
            case "Rename Tab":
                await RenameTab(tab);
                break;
            case "Duplicate Tab":
                await DuplicateTab(tab);
                break;
            case "Close Tab":
                await CloseTab(tab);
                break;
        }
    }

    [RelayCommand]
    private async Task PinchToZoom()
    {
        // Handle pinch gesture for zooming UI elements
        TriggerHapticFeedback();

        // Toggle between compact and expanded view
        var isCompactView = await Shell.Current.DisplayAlert(
            "View Mode",
            "Switch to compact view for more tabs?",
            "Compact",
            "Expanded");

        MaxTabs = isCompactView ? 5 : 3;
    }

    [RelayCommand]
    private async Task ShakeToRefresh()
    {
        // Handle shake gesture to refresh all tabs
        TriggerHapticFeedback();
        
        try
        {
            foreach (var tab in SaleTabs)
            {
                await tab.SaleViewModel.Refresh();
            }
            
            await Shell.Current.DisplayAlert("Refreshed", "All tabs have been refreshed", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during shake to refresh");
            SetError($"Refresh failed: {ex.Message}");
        }
    }

    private async Task RenameTab(MobileSaleTabViewModel tab)
    {
        var newName = await Shell.Current.DisplayPromptAsync(
            "Rename Tab", 
            "Enter new tab name:", 
            "Rename", 
            "Cancel", 
            tab.TabName);

        if (!string.IsNullOrWhiteSpace(newName) && newName != tab.TabName)
        {
            tab.TabName = newName;
            TriggerHapticFeedback();
            
            // Update session
            var updateRequest = new UpdateSaleSessionRequest
            {
                SessionId = tab.SessionId,
                TabName = newName
            };
            
            await _multiTabSalesManager.UpdateSessionAsync(updateRequest);
        }
    }

    private async Task DuplicateTab(MobileSaleTabViewModel sourceTab)
    {
        try
        {
            var sessionData = sourceTab.GetSessionData();
            var newTabName = $"{sourceTab.TabName} Copy";
            
            var request = new CreateSaleSessionRequest
            {
                TabName = newTabName,
                ShopId = _currentShopId,
                UserId = _currentUserId,
                DeviceId = _currentDeviceId,
                CustomerId = sessionData.CustomerId
            };

            var result = await _multiTabSalesManager.CreateNewSaleSessionAsync(request);
            if (result.Success && result.Session != null)
            {
                // Copy items to new session
                foreach (var item in sessionData.Items)
                {
                    await _multiTabSalesManager.AddItemToSessionAsync(result.Session.Id, item);
                }

                // Create tab view model
                var tabViewModel = new MobileSaleTabViewModel(result.Session, _multiTabSalesManager, _logger)
                {
                    IsActive = false,
                    CanClose = true
                };

                SaleTabs.Add(tabViewModel);
                await SwitchToTab(tabViewModel);
                
                TriggerHapticFeedback();
                _logger.LogInformation("Duplicated mobile sale tab: {OriginalTab} -> {NewTab}", 
                    sourceTab.TabName, newTabName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error duplicating mobile tab: {TabName}", sourceTab.TabName);
            SetError($"Failed to duplicate tab: {ex.Message}");
        }
    }

    private async Task SaveCurrentTabState()
    {
        if (ActiveTab != null)
        {
            await SaveTabState(ActiveTab);
        }
    }

    private async Task SaveTabState(MobileSaleTabViewModel tab)
    {
        try
        {
            var sessionData = tab.GetSessionData();
            var result = await _multiTabSalesManager.SaveSessionStateAsync(tab.SessionId, sessionData);
            
            if (result.Success)
            {
                tab.HasUnsavedChanges = false;
            }
            else
            {
                _logger.LogWarning("Failed to save mobile tab state for {TabName}: {Message}", tab.TabName, result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving mobile tab state for {TabName}", tab.TabName);
        }
    }

    private string GenerateMobileTabName()
    {
        var baseNames = new[] { "Sale", "Order", "Customer" };
        var baseName = baseNames[Random.Shared.Next(baseNames.Length)];
        
        var counter = 1;
        string tabName;
        
        do
        {
            tabName = SaleTabs.Count == 0 ? baseName : $"{baseName} {counter}";
            counter++;
        } 
        while (SaleTabs.Any(t => t.TabName == tabName));
        
        return tabName;
    }

    private void UpdateTabClosePermissions()
    {
        // At least one tab must remain open
        var canCloseAny = SaleTabs.Count > 1;
        
        foreach (var tab in SaleTabs)
        {
            tab.CanClose = canCloseAny;
        }
    }

    private void TriggerHapticFeedback()
    {
        if (!EnableHapticFeedback) return;
        
        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch
        {
            // Haptic feedback not available on this device
        }
    }

    private void OnCustomerSelected(object? sender, CustomerSelectedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (ActiveTab?.SaleViewModel != null)
                {
                    ActiveTab.SaleViewModel.CurrentCustomer = e.Customer;
                    ActiveTab.SaleViewModel.CustomerMobileNumber = e.Customer.Phone ?? string.Empty;
                    ActiveTab.SaleViewModel.ShowCustomerDetails = true;
                    
                    // Apply membership discounts if available
                    if (e.Customer.AvailableDiscounts?.Any() == true)
                    {
                        // This would be implemented in the SaleViewModel
                        // await ActiveTab.SaleViewModel.ApplyMembershipDiscounts(e.Customer.AvailableDiscounts);
                    }
                    
                    TriggerHapticFeedback();
                }
                
                // Auto-hide customer lookup panel
                ShowCustomerLookup = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling customer selection");
                SetError($"Failed to select customer: {ex.Message}");
            }
        });
    }

    private void OnProductScanned(object? sender, ProductScannedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (ActiveTab?.SaleViewModel != null)
                {
                    await ActiveTab.SaleViewModel.AddProduct(e.Product);
                    TriggerHapticFeedback();
                }
                
                // Auto-hide barcode scanner panel after successful scan
                ShowBarcodeScanner = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling scanned product: {ProductName}", e.Product.Name);
                SetError($"Failed to add scanned product: {ex.Message}");
            }
        });
    }

    public async Task InitializeAsync()
    {
        await LoadExistingSessions();
        
        // Initialize mobile-specific components
        if (CustomerLookupViewModel != null)
        {
            CustomerLookupViewModel.EnableHapticFeedback = EnableHapticFeedback;
        }
        
        if (BarcodeScannerViewModel != null)
        {
            BarcodeScannerViewModel.EnableVibration = EnableHapticFeedback;
            await BarcodeScannerViewModel.InitializeAsync();
        }
    }

    public async Task CleanupAsync()
    {
        try
        {
            await SaveAllTabs();
            
            // Cleanup mobile-specific components
            if (BarcodeScannerViewModel != null)
            {
                BarcodeScannerViewModel.Dispose();
            }
            
            // Unsubscribe from events
            if (CustomerLookupViewModel != null)
            {
                CustomerLookupViewModel.CustomerSelected -= OnCustomerSelected;
            }
            
            if (BarcodeScannerViewModel != null)
            {
                BarcodeScannerViewModel.ProductScanned -= OnProductScanned;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during mobile tab cleanup");
        }
    }

    public async Task HandleBackgroundAsync()
    {
        await SaveAllTabs();
        
        // Stop barcode scanning when app goes to background
        if (BarcodeScannerViewModel?.IsScannerActive == true)
        {
            await BarcodeScannerViewModel.StopScanning();
        }
    }

    public async Task HandleForegroundAsync()
    {
        // Refresh active session data when app comes to foreground
        if (ActiveTab != null)
        {
            var sessionData = await _multiTabSalesManager.GetSaleSessionAsync(ActiveTab.SessionId);
            if (sessionData != null)
            {
                ActiveTab.UpdateFromSessionData(sessionData);
            }
        }
        
        // Re-initialize barcode scanner if needed
        if (ShowBarcodeScanner && BarcodeScannerViewModel != null)
        {
            await BarcodeScannerViewModel.InitializeAsync();
        }
    }

    // Mobile-specific gesture handling
    public async Task HandleSwipeLeft()
    {
        if (EnableSwipeGestures)
        {
            await SwipeToNextTab();
        }
    }

    public async Task HandleSwipeRight()
    {
        if (EnableSwipeGestures)
        {
            await SwipeToPreviousTab();
        }
    }

    public async Task HandleLongPress(MobileSaleTabViewModel tab)
    {
        await LongPressTabOptions(tab);
    }

    // Touch-optimized tab management
    public async Task HandleDoubleTap()
    {
        if (SaleTabs.Count < MaxTabs)
        {
            await QuickAddTab();
        }
    }
}

/// <summary>
/// Mobile-specific ViewModel representing a single sale tab
/// </summary>
public partial class MobileSaleTabViewModel : ObservableObject
{
    private readonly IMultiTabSalesManager _multiTabSalesManager;
    private readonly ILogger _logger;

    [ObservableProperty]
    private string tabName = string.Empty;

    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private bool canClose = true;

    [ObservableProperty]
    private bool hasUnsavedChanges;

    [ObservableProperty]
    private SaleViewModel saleViewModel;

    public Guid SessionId { get; }
    public SaleSessionDto SessionData { get; private set; }

    public MobileSaleTabViewModel(SaleSessionDto sessionData, IMultiTabSalesManager multiTabSalesManager, ILogger logger)
    {
        SessionData = sessionData;
        SessionId = sessionData.Id;
        TabName = sessionData.TabName;
        _multiTabSalesManager = multiTabSalesManager;
        _logger = logger;

        // Create the mobile sale view model for this tab
        SaleViewModel = saleViewModelFactory();
        
        // Load session data into the view model
        LoadSessionDataIntoViewModel();
        
        // Subscribe to changes
        SaleViewModel.PropertyChanged += (s, e) => HasUnsavedChanges = true;
    }

    private SaleViewModel CreateSaleViewModelForTab(SaleSessionDto sessionData)
    {
        // This is a simplified version - in a real implementation, 
        // services would be injected through dependency injection
        var saleViewModel = new SaleViewModel(
            null!, // IEnhancedSalesService - would be injected
            null!, // IProductService - would be injected
    // Create the mobile sale view model for this tab
    SaleViewModel = CreateSaleViewModelForTab(sessionData);

    // Load session data into the view model
    LoadSessionDataIntoViewModel();

    // Subscribe to changes
            null!  // IBarcodeIntegrationService - would be injected
        );

        // Initialize for this session
        _ = Task.Run(async () => await saleViewModel.InitializeForSession(sessionData.Id, sessionData.TabName));
        
        return saleViewModel;
    }

    private void LoadSessionDataIntoViewModel()
    {
        try
        {
            // Load items from session data
            SaleViewModel.SaleItems.Clear();
            
            foreach (var item in SessionData.Items)
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
                    DiscountPercentage = item.DiscountAmount > 0 ? (item.DiscountAmount / item.LineTotal) * 100 : 0
                };
                
                SaleViewModel.SaleItems.Add(saleItemViewModel);
            }

            // Set payment method
            SaleViewModel.SelectedPaymentMethod = SessionData.PaymentMethod;
            
            // Set totals
            SaleViewModel.Subtotal = SessionData.Calculation.Subtotal;
            SaleViewModel.DiscountAmount = SessionData.Calculation.TotalDiscount;
            SaleViewModel.TaxAmount = SessionData.Calculation.TotalTax;
            SaleViewModel.TotalAmount = SessionData.Calculation.FinalTotal;

            HasUnsavedChanges = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading session data into mobile view model for tab {TabName}", TabName);
        }
    }

    public SaleSessionDto GetSessionData()
    {
        try
        {
            // Update session data from view model
            SessionData.TabName = TabName;
            SessionData.PaymentMethod = SaleViewModel.SelectedPaymentMethod;
            SessionData.LastModified = DateTime.UtcNow;

            // Convert sale items back to session items
            SessionData.Items.Clear();
            foreach (var item in SaleViewModel.SaleItems)
            {
                var sessionItem = new SaleSessionItemDto
                {
                    Id = Guid.NewGuid(), // Generate new ID for session items
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineTotal = item.LineTotal,
                    DiscountAmount = item.DiscountAmount,
                    BatchNumber = item.BatchNumber,
                    Weight = item.Weight,
                    IsWeightBased = item.IsWeightBased
                };
                
                SessionData.Items.Add(sessionItem);
            }

            // Update calculation
            SessionData.Calculation = new SaleSessionCalculationDto
            {
                Subtotal = SaleViewModel.Subtotal,
                TotalDiscount = SaleViewModel.DiscountAmount,
                TotalTax = SaleViewModel.TaxAmount,
                FinalTotal = SaleViewModel.TotalAmount,
                CalculatedAt = DateTime.UtcNow
            };

            return SessionData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session data from mobile view model for tab {TabName}", TabName);
            return SessionData;
        }
    }

    public void UpdateFromSessionData(SaleSessionDto sessionData)
    {
        SessionData = sessionData;
        TabName = sessionData.TabName;
        LoadSessionDataIntoViewModel();
    }
}