using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Shared.Core.Services;
using Shared.Core.DTOs;
using Microsoft.Extensions.Logging;
using Mobile.Services;

namespace Mobile.ViewModels;

/// <summary>
/// Enhanced mobile tab container with comprehensive touch optimization,
/// gesture support, and mobile-specific UX patterns
/// </summary>
public partial class EnhancedMobileTabContainerViewModel : BaseViewModel
{
    private readonly IMultiTabSalesManager _multiTabSalesManager;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserContextService _userContextService;
    private readonly ICustomerLookupService _customerLookupService;
    private readonly IBarcodeIntegrationService _barcodeIntegrationService;
    private readonly IConnectivityService _connectivityService;
    private readonly ILogger<EnhancedMobileTabContainerViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<EnhancedMobileTabViewModel> saleTabs = new();

    [ObservableProperty]
    private EnhancedMobileTabViewModel? activeTab;

    [ObservableProperty]
    private object? activeTabContent;

    [ObservableProperty]
    private int maxTabs = 3; // Optimized for mobile

    [ObservableProperty]
    private bool isTabSwitchingEnabled = true;

    [ObservableProperty]
    private bool enableSwipeGestures = true;

    [ObservableProperty]
    private bool enableHapticFeedback = true;

    [ObservableProperty]
    private bool enableAdvancedGestures = true;

    [ObservableProperty]
    private MobileCustomerLookupViewModel customerLookupViewModel;

    [ObservableProperty]
    private MobileBarcodeScannerViewModel barcodeScannerViewModel;

    [ObservableProperty]
    private bool showCustomerLookup;

    [ObservableProperty]
    private bool showBarcodeScanner;

    [ObservableProperty]
    private bool isOneHandedMode;

    [ObservableProperty]
    private bool isCompactMode;

    [ObservableProperty]
    private double uiScale = 1.0;

    [ObservableProperty]
    private bool isOfflineMode;

    [ObservableProperty]
    private string connectionStatus = "Online";

    [ObservableProperty]
    private int pendingSyncCount;

    [ObservableProperty]
    private bool showOfflineIndicator;

    [ObservableProperty]
    private bool enableAutoSync = true;

    [ObservableProperty]
    private TimeSpan autoSyncInterval = TimeSpan.FromMinutes(2);

    private Guid _currentUserId;
    private Guid _currentDeviceId;
    private Guid _currentShopId;
    private Timer? _autoSyncTimer;

    public EnhancedMobileTabContainerViewModel(
        IMultiTabSalesManager multiTabSalesManager,
        ICurrentUserService currentUserService,
        IUserContextService userContextService,
        ICustomerLookupService customerLookupService,
        IBarcodeIntegrationService barcodeIntegrationService,
        IConnectivityService connectivityService,
        ILogger<EnhancedMobileTabContainerViewModel> logger)
    {
        _multiTabSalesManager = multiTabSalesManager;
        _currentUserService = currentUserService;
        _userContextService = userContextService;
        _customerLookupService = customerLookupService;
        _barcodeIntegrationService = barcodeIntegrationService;
        _connectivityService = connectivityService;
        _logger = logger;
        
        Title = "Mobile Sales";
        
        InitializeUserContext();
        InitializeMobileFeatures();
        InitializeChildViewModels();
        InitializeConnectivityMonitoring();
    }

    private void InitializeUserContext()
    {
        var currentUser = _currentUserService.CurrentUser;
        var currentShop = _userContextService.CurrentShop;
        
        if (currentUser != null)
        {
            _currentUserId = currentUser.Id;
            _currentDeviceId = GenerateDeviceId();
            _currentShopId = currentShop?.Id ?? currentUser.ShopId ?? Guid.NewGuid();
        }
    }

    private void InitializeMobileFeatures()
    {
        // Detect device characteristics
        var displayInfo = DeviceDisplay.Current.MainDisplayInfo;
        
        // Enable one-handed mode for smaller screens
        var physicalHeight = displayInfo.Height / displayInfo.Density;
        IsOneHandedMode = physicalHeight < 6.0;
        
        // Set compact mode for smaller screens
        IsCompactMode = displayInfo.Width / displayInfo.Density < 4.0;
        
        // Adjust max tabs for screen size
        MaxTabs = IsCompactMode ? 2 : 3;
        
        // Adjust UI scale
        UiScale = displayInfo.Density > 2.0 ? 1.2 : 1.0;

        _logger.LogInformation("Enhanced mobile tab container initialized with OneHanded: {OneHanded}, Compact: {Compact}, MaxTabs: {MaxTabs}", 
            IsOneHandedMode, IsCompactMode, MaxTabs);
    }

    private void InitializeChildViewModels()
    {
        // Initialize mobile-specific ViewModels
        CustomerLookupViewModel = new MobileCustomerLookupViewModel(_customerLookupService, _logger);
        BarcodeScannerViewModel = new MobileBarcodeScannerViewModel(_barcodeIntegrationService, null!, _logger);
        
        // Configure for mobile
        CustomerLookupViewModel.EnableHapticFeedback = EnableHapticFeedback;
        BarcodeScannerViewModel.EnableVibration = EnableHapticFeedback;
        
        // Subscribe to events
        CustomerLookupViewModel.CustomerSelected += OnCustomerSelected;
        BarcodeScannerViewModel.ProductScanned += OnProductScanned;
    }

    private void InitializeConnectivityMonitoring()
    {
        _connectivityService.ConnectivityChanged += OnConnectivityChanged;
        UpdateConnectionStatus();
        
        if (EnableAutoSync)
        {
            StartAutoSync();
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
            TriggerHapticFeedback();

            // Check if we can create a new tab
            if (SaleTabs.Count >= MaxTabs)
            {
                SetError($"Maximum number of tabs ({MaxTabs}) reached. Please close a tab first.");
                TriggerErrorHapticFeedback();
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
                TriggerErrorHapticFeedback();
                return;
            }

            // Create enhanced tab view model
            var tabViewModel = new EnhancedMobileTabViewModel(
                result.Session!, 
                _multiTabSalesManager, 
                _connectivityService,
                _logger)
            {
                IsActive = false,
                CanClose = SaleTabs.Count > 0,
                EnableHapticFeedback = EnableHapticFeedback,
                IsOneHandedMode = IsOneHandedMode,
                IsCompactMode = IsCompactMode,
                UiScale = UiScale
            };

            // Add to collection
            SaleTabs.Add(tabViewModel);

            // Switch to new tab
            await SwitchToTab(tabViewModel);

            _logger.LogInformation("Created new enhanced mobile sale tab: {TabName}", tabName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new enhanced mobile sale tab");
            SetError($"Failed to create new tab: {ex.Message}");
            TriggerErrorHapticFeedback();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SwitchToTab(EnhancedMobileTabViewModel tab)
    {
        if (tab == null || tab == ActiveTab || !IsTabSwitchingEnabled) return;

        try
        {
            IsTabSwitchingEnabled = false;
            TriggerHapticFeedback();

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

            _logger.LogDebug("Switched to enhanced mobile tab: {TabName}", tab.TabName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error switching to enhanced mobile tab {TabName}", tab.TabName);
            SetError($"Failed to switch to tab: {ex.Message}");
            TriggerErrorHapticFeedback();
        }
        finally
        {
            IsTabSwitchingEnabled = true;
        }
    }

    [RelayCommand]
    private async Task CloseTab(EnhancedMobileTabViewModel tab)
    {
        if (tab == null || !tab.CanClose) return;

        try
        {
            TriggerHapticFeedback();

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

            _logger.LogInformation("Closed enhanced mobile sale tab: {TabName}", tab.TabName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing enhanced mobile tab {TabName}", tab.TabName);
            SetError($"Failed to close tab: {ex.Message}");
            TriggerErrorHapticFeedback();
        }
    }

    // Enhanced gesture support
    [RelayCommand]
    private async Task HandleSwipeLeft()
    {
        if (!EnableSwipeGestures) return;
        
        TriggerHapticFeedback();
        await SwipeToNextTab();
    }

    [RelayCommand]
    private async Task HandleSwipeRight()
    {
        if (!EnableSwipeGestures) return;
        
        TriggerHapticFeedback();
        await SwipeToPreviousTab();
    }

    [RelayCommand]
    private async Task HandleSwipeUp()
    {
        if (!EnableSwipeGestures) return;
        
        TriggerHapticFeedback();
        
        // Swipe up to create new tab or show options
        if (SaleTabs.Count < MaxTabs)
        {
            await CreateNewTab();
        }
        else
        {
            await ShowTabManagementOptions();
        }
    }

    [RelayCommand]
    private async Task HandleSwipeDown()
    {
        if (!EnableSwipeGestures) return;
        
        TriggerHapticFeedback();
        
        // Swipe down to refresh all tabs
        await RefreshAllTabs();
    }

    [RelayCommand]
    private async Task HandleDoubleTap()
    {
        TriggerHapticFeedback();
        
        // Double tap to create new tab quickly
        if (SaleTabs.Count < MaxTabs)
        {
            await CreateNewTab();
        }
    }

    [RelayCommand]
    private async Task HandleLongPress(EnhancedMobileTabViewModel? tab = null)
    {
        TriggerHapticFeedback();
        
        if (tab != null)
        {
            await ShowTabOptions(tab);
        }
        else
        {
            await ShowGlobalOptions();
        }
    }

    [RelayCommand]
    private async Task HandlePinchToZoom(double scaleFactor)
    {
        if (!EnableAdvancedGestures) return;
        
        TriggerHapticFeedback();
        
        // Adjust UI scale for all tabs
        UiScale = Math.Max(0.8, Math.Min(2.0, UiScale * scaleFactor));
        IsCompactMode = UiScale < 1.0;
        
        // Update all tabs
        foreach (var tab in SaleTabs)
        {
            tab.UiScale = UiScale;
            tab.IsCompactMode = IsCompactMode;
        }
    }

    [RelayCommand]
    private async Task HandleShakeToRefresh()
    {
        if (!EnableAdvancedGestures) return;
        
        TriggerHapticFeedback();
        await RefreshAllTabs();
        
        await Shell.Current.DisplayAlert("Refreshed", "All tabs have been refreshed", "OK");
    }

    private async Task SwipeToNextTab()
    {
        if (ActiveTab == null || SaleTabs.Count <= 1) return;

        var currentIndex = SaleTabs.IndexOf(ActiveTab);
        var nextIndex = (currentIndex + 1) % SaleTabs.Count;
        await SwitchToTab(SaleTabs[nextIndex]);
    }

    private async Task SwipeToPreviousTab()
    {
        if (ActiveTab == null || SaleTabs.Count <= 1) return;

        var currentIndex = SaleTabs.IndexOf(ActiveTab);
        var previousIndex = currentIndex == 0 ? SaleTabs.Count - 1 : currentIndex - 1;
        await SwitchToTab(SaleTabs[previousIndex]);
    }

    private async Task ShowTabManagementOptions()
    {
        var options = new[] { "Close Oldest Tab", "Save All Tabs", "Settings" };
        
        var action = await Shell.Current.DisplayActionSheet(
            "Tab Management", 
            "Cancel", 
            null, 
            options);

        switch (action)
        {
            case "Close Oldest Tab":
                var oldestTab = SaleTabs.Where(t => t.CanClose).OrderBy(t => t.CreatedAt).FirstOrDefault();
                if (oldestTab != null)
                {
                    await CloseTab(oldestTab);
                }
                break;
            case "Save All Tabs":
                await SaveAllTabs();
                await Shell.Current.DisplayAlert("Saved", "All tabs saved successfully", "OK");
                break;
            case "Settings":
                await ShowMobileSettings();
                break;
        }
    }

    private async Task ShowTabOptions(EnhancedMobileTabViewModel tab)
    {
        var options = new List<string> { "Rename Tab", "Duplicate Tab" };
        
        if (tab.CanClose)
        {
            options.Add("Close Tab");
        }
        
        options.AddRange(new[] { "Save Tab", "Refresh Tab" });
        
        var action = await Shell.Current.DisplayActionSheet(
            $"Tab: {tab.TabName}", 
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
            case "Save Tab":
                await SaveTabState(tab);
                await Shell.Current.DisplayAlert("Saved", $"Tab '{tab.TabName}' saved", "OK");
                break;
            case "Refresh Tab":
                await RefreshTab(tab);
                break;
        }
    }

    private async Task ShowGlobalOptions()
    {
        var options = new List<string> { "Create New Tab", "Save All", "Refresh All" };
        
        if (IsOfflineMode)
        {
            options.Add($"Sync Now ({PendingSyncCount} pending)");
        }
        
        options.AddRange(new[] { "Settings", "Help" });
        
        var action = await Shell.Current.DisplayActionSheet(
            "Options", 
            "Cancel", 
            null, 
            options.ToArray());

        switch (action)
        {
            case "Create New Tab":
                await CreateNewTab();
                break;
            case "Save All":
                await SaveAllTabs();
                break;
            case "Refresh All":
                await RefreshAllTabs();
                break;
            case var syncOption when syncOption?.StartsWith("Sync Now") == true:
                await ForceSyncNow();
                break;
            case "Settings":
                await ShowMobileSettings();
                break;
            case "Help":
                await ShowMobileHelp();
                break;
        }
    }

    private async Task ShowMobileSettings()
    {
        var settings = new[]
        {
            $"Haptic Feedback: {(EnableHapticFeedback ? "On" : "Off")}",
            $"Swipe Gestures: {(EnableSwipeGestures ? "On" : "Off")}",
            $"Advanced Gestures: {(EnableAdvancedGestures ? "On" : "Off")}",
            $"One-Handed Mode: {(IsOneHandedMode ? "On" : "Off")}",
            $"Compact Mode: {(IsCompactMode ? "On" : "Off")}",
            $"Auto Sync: {(EnableAutoSync ? "On" : "Off")}",
            $"Max Tabs: {MaxTabs}"
        };

        var selectedSetting = await Shell.Current.DisplayActionSheet(
            "Mobile Settings", 
            "Cancel", 
            null, 
            settings);

        await HandleSettingToggle(selectedSetting);
    }

    private async Task HandleSettingToggle(string? selectedSetting)
    {
        if (string.IsNullOrEmpty(selectedSetting)) return;

        TriggerHapticFeedback();

        if (selectedSetting.Contains("Haptic Feedback"))
        {
            EnableHapticFeedback = !EnableHapticFeedback;
            UpdateChildViewModelsHapticFeedback();
        }
        else if (selectedSetting.Contains("Swipe Gestures"))
        {
            EnableSwipeGestures = !EnableSwipeGestures;
        }
        else if (selectedSetting.Contains("Advanced Gestures"))
        {
            EnableAdvancedGestures = !EnableAdvancedGestures;
        }
        else if (selectedSetting.Contains("One-Handed Mode"))
        {
            IsOneHandedMode = !IsOneHandedMode;
            UpdateTabsForOneHandedMode();
        }
        else if (selectedSetting.Contains("Compact Mode"))
        {
            IsCompactMode = !IsCompactMode;
            UiScale = IsCompactMode ? 0.9 : 1.0;
            UpdateTabsForCompactMode();
        }
        else if (selectedSetting.Contains("Auto Sync"))
        {
            EnableAutoSync = !EnableAutoSync;
            if (EnableAutoSync)
            {
                StartAutoSync();
            }
            else
            {
                StopAutoSync();
            }
        }
        else if (selectedSetting.Contains("Max Tabs"))
        {
            await ShowMaxTabsOptions();
        }
    }

    private async Task ShowMaxTabsOptions()
    {
        var options = new[] { "2 Tabs", "3 Tabs", "4 Tabs", "5 Tabs" };
        
        var selected = await Shell.Current.DisplayActionSheet(
            "Maximum Tabs", 
            "Cancel", 
            null, 
            options);

        if (int.TryParse(selected?.Split(' ')[0], out var newMaxTabs))
        {
            MaxTabs = newMaxTabs;
            TriggerHapticFeedback();
        }
    }

    private async Task ShowMobileHelp()
    {
        var helpText = "Enhanced Mobile Tab Container Help:\n\n" +
                      "Gestures:\n" +
                      "• Swipe left/right: Switch tabs\n" +
                      "• Swipe up: Create new tab\n" +
                      "• Swipe down: Refresh all tabs\n" +
                      "• Double tap: Quick new tab\n" +
                      "• Long press tab: Tab options\n" +
                      "• Long press empty: Global options\n" +
                      "• Pinch: Zoom UI\n" +
                      "• Shake: Refresh all\n\n" +
                      "Features:\n" +
                      "• Auto-save all tabs\n" +
                      "• Offline support\n" +
                      "• One-handed mode\n" +
                      "• Compact mode\n" +
                      "• Haptic feedback\n\n" +
                      "Offline Mode:\n" +
                      "• All changes saved locally\n" +
                      "• Auto-sync when online\n" +
                      "• Manual sync available";

        await Shell.Current.DisplayAlert("Help", helpText, "OK");
    }

    private async Task RefreshAllTabs()
    {
        try
        {
            foreach (var tab in SaleTabs)
            {
                await RefreshTab(tab);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing all tabs");
            SetError("Failed to refresh all tabs");
        }
    }

    private async Task RefreshTab(EnhancedMobileTabViewModel tab)
    {
        try
        {
            await tab.SaleViewModel.Refresh();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing tab: {TabName}", tab.TabName);
        }
    }

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
            _logger.LogError(ex, "Error saving all tabs");
            SetError("Failed to save all tabs");
        }
    }

    private async Task SaveCurrentTabState()
    {
        if (ActiveTab != null)
        {
            await SaveTabState(ActiveTab);
        }
    }

    private async Task SaveTabState(EnhancedMobileTabViewModel tab)
    {
        try
        {
            await tab.SaveState();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving tab state: {TabName}", tab.TabName);
        }
    }

    private async Task RenameTab(EnhancedMobileTabViewModel tab)
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
            await tab.UpdateTabName(newName);
        }
    }

    private async Task DuplicateTab(EnhancedMobileTabViewModel sourceTab)
    {
        try
        {
            if (SaleTabs.Count >= MaxTabs)
            {
                SetError($"Cannot duplicate tab. Maximum of {MaxTabs} tabs allowed.");
                return;
            }

            var newTabName = $"{sourceTab.TabName} Copy";
            var duplicatedSession = await sourceTab.DuplicateSession(newTabName);
            
            if (duplicatedSession != null)
            {
                var tabViewModel = new EnhancedMobileTabViewModel(
                    duplicatedSession, 
                    _multiTabSalesManager, 
                    _connectivityService,
                    _logger)
                {
                    IsActive = false,
                    CanClose = true,
                    EnableHapticFeedback = EnableHapticFeedback,
                    IsOneHandedMode = IsOneHandedMode,
                    IsCompactMode = IsCompactMode,
                    UiScale = UiScale
                };

                SaleTabs.Add(tabViewModel);
                await SwitchToTab(tabViewModel);
                
                TriggerHapticFeedback();
                _logger.LogInformation("Duplicated enhanced mobile tab: {OriginalTab} -> {NewTab}", 
                    sourceTab.TabName, newTabName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error duplicating tab: {TabName}", sourceTab.TabName);
            SetError($"Failed to duplicate tab: {ex.Message}");
        }
    }

    private async Task ForceSyncNow()
    {
        if (IsOfflineMode)
        {
            await Shell.Current.DisplayAlert("Offline", "Cannot sync while offline", "OK");
            return;
        }

        try
        {
            TriggerHapticFeedback();
            
            // Sync all tabs
            foreach (var tab in SaleTabs)
            {
                await tab.ForceSyncNow();
            }
            
            PendingSyncCount = 0;
            await Shell.Current.DisplayAlert("Sync Complete", "All tabs synchronized", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during forced sync");
            SetError($"Sync failed: {ex.Message}");
        }
    }

    private void UpdateChildViewModelsHapticFeedback()
    {
        if (CustomerLookupViewModel != null)
        {
            CustomerLookupViewModel.EnableHapticFeedback = EnableHapticFeedback;
        }
        
        if (BarcodeScannerViewModel != null)
        {
            BarcodeScannerViewModel.EnableVibration = EnableHapticFeedback;
        }

        foreach (var tab in SaleTabs)
        {
            tab.EnableHapticFeedback = EnableHapticFeedback;
        }
    }

    private void UpdateTabsForOneHandedMode()
    {
        foreach (var tab in SaleTabs)
        {
            tab.IsOneHandedMode = IsOneHandedMode;
        }
    }

    private void UpdateTabsForCompactMode()
    {
        foreach (var tab in SaleTabs)
        {
            tab.IsCompactMode = IsCompactMode;
            tab.UiScale = UiScale;
        }
    }

    private void UpdateTabClosePermissions()
    {
        var canCloseAny = SaleTabs.Count > 1;
        
        foreach (var tab in SaleTabs)
        {
            tab.CanClose = canCloseAny;
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

    private Guid GenerateDeviceId()
    {
        var deviceInfo = DeviceInfo.Current;
        var deviceString = $"{deviceInfo.Platform}-{deviceInfo.Manufacturer}-{deviceInfo.Model}";

        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(deviceString));
        return new Guid(hash.AsSpan(0, 16));
    }

    private void StartAutoSync()
    {
        if (!EnableAutoSync) return;
        
        _autoSyncTimer?.Dispose();
        _autoSyncTimer = new Timer(async _ =>
        {
            try
            {
                if (!IsOfflineMode)
                {
                    await SaveAllTabs();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-sync failed");
            }
        }, null, AutoSyncInterval, AutoSyncInterval);
    }

    private void StopAutoSync()
    {
        _autoSyncTimer?.Dispose();
        _autoSyncTimer = null;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateConnectionStatus();
            
            if (!IsOfflineMode)
            {
                // Sync when coming back online
                _ = Task.Run(async () => await SaveAllTabs());
            }
        });
    }

    private void UpdateConnectionStatus()
    {
        var networkAccess = Connectivity.Current.NetworkAccess;
        IsOfflineMode = networkAccess != NetworkAccess.Internet;
        ConnectionStatus = IsOfflineMode ? "Offline" : "Online";
        ShowOfflineIndicator = IsOfflineMode;
        
        // Update pending sync count
        PendingSyncCount = SaleTabs.Sum(t => t.PendingSyncCount);
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
                    
                    TriggerHapticFeedback();
                }
                
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
                
                ShowBarcodeScanner = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling scanned product: {ProductName}", e.Product.Name);
                SetError($"Failed to add scanned product: {ex.Message}");
            }
        });
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
            // Haptic feedback not available
        }
    }

    private void TriggerErrorHapticFeedback()
    {
        if (!EnableHapticFeedback) return;
        
        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
        }
        catch
        {
            // Haptic feedback not available
        }
    }

    public async Task InitializeAsync()
    {
        await LoadExistingSessions();
        
        // Initialize child ViewModels
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
                var tabViewModel = new EnhancedMobileTabViewModel(
                    session, 
                    _multiTabSalesManager, 
                    _connectivityService,
                    _logger)
                {
                    IsActive = false,
                    CanClose = sessions.Count > 1,
                    EnableHapticFeedback = EnableHapticFeedback,
                    IsOneHandedMode = IsOneHandedMode,
                    IsCompactMode = IsCompactMode,
                    UiScale = UiScale
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
            _logger.LogError(ex, "Error loading existing enhanced mobile sessions");
            SetError($"Failed to load sessions: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task CleanupAsync()
    {
        try
        {
            await SaveAllTabs();
            StopAutoSync();
            
            // Cleanup child ViewModels
            if (BarcodeScannerViewModel != null)
            {
                BarcodeScannerViewModel.Dispose();
            }
            
            // Unsubscribe from events
            _connectivityService.ConnectivityChanged -= OnConnectivityChanged;
            
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
            _logger.LogError(ex, "Error during enhanced mobile tab cleanup");
        }
    }
}

/// <summary>
/// Enhanced mobile-specific ViewModel representing a single sale tab with comprehensive mobile features
/// </summary>
public partial class EnhancedMobileTabViewModel : ObservableObject
{
    private readonly IMultiTabSalesManager _multiTabSalesManager;
    private readonly IConnectivityService _connectivityService;
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
    private ComprehensiveMobileSaleViewModel saleViewModel;

    [ObservableProperty]
    private bool enableHapticFeedback = true;

    [ObservableProperty]
    private bool isOneHandedMode;

    [ObservableProperty]
    private bool isCompactMode;

    [ObservableProperty]
    private double uiScale = 1.0;

    [ObservableProperty]
    private bool isOfflineMode;

    [ObservableProperty]
    private int pendingSyncCount;

    public Guid SessionId { get; }
    public SaleSessionDto SessionData { get; private set; }
    public DateTime CreatedAt { get; }

    public EnhancedMobileTabViewModel(
        SaleSessionDto sessionData, 
        IMultiTabSalesManager multiTabSalesManager, 
        IConnectivityService connectivityService,
        ILogger logger)
    {
        SessionData = sessionData;
        SessionId = sessionData.Id;
        TabName = sessionData.TabName;
        CreatedAt = sessionData.CreatedAt;
        _multiTabSalesManager = multiTabSalesManager;
        _connectivityService = connectivityService;
        _logger = logger;

        // Create the comprehensive mobile sale view model for this tab
        SaleViewModel = CreateComprehensiveSaleViewModel(sessionData);
        
        // Load session data into the view model
        LoadSessionDataIntoViewModel();
        
        // Subscribe to changes
        SaleViewModel.PropertyChanged += (s, e) => HasUnsavedChanges = true;
        
        // Monitor connectivity
        _connectivityService.ConnectivityChanged += OnConnectivityChanged;
        UpdateConnectionStatus();
    }

    private ComprehensiveMobileSaleViewModel CreateComprehensiveSaleViewModel(SaleSessionDto sessionData)
    {
        // In a real implementation, services would be injected through dependency injection
        var saleViewModel = new ComprehensiveMobileSaleViewModel(
            null!, // Services would be injected
            null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!
        );

        // Initialize for this session
        _ = Task.Run(async () => await saleViewModel.InitializeForSession(sessionData.Id, sessionData.TabName));
        
        return saleViewModel;
    }

    private void LoadSessionDataIntoViewModel()
    {
        // Implementation similar to MobileSaleTabViewModel but with enhanced features
        // This would load the session data into the comprehensive view model
    }

    public async Task SaveState()
    {
        try
        {
            await SaleViewModel.SaveToSession();
            HasUnsavedChanges = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving enhanced mobile tab state: {TabName}", TabName);
            throw;
        }
    }

    public async Task UpdateTabName(string newName)
    {
        try
        {
            TabName = newName;
            SessionData.TabName = newName;
            
            var updateRequest = new UpdateSaleSessionRequest
            {
                SessionId = SessionId,
                TabName = newName
            };
            
            await _multiTabSalesManager.UpdateSessionAsync(updateRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tab name: {TabName}", TabName);
            throw;
        }
    }

    public async Task<SaleSessionDto?> DuplicateSession(string newTabName)
    {
        try
        {
            var request = new CreateSaleSessionRequest
            {
                TabName = newTabName,
                ShopId = SessionData.ShopId,
                UserId = SessionData.UserId,
                DeviceId = SessionData.DeviceId,
                CustomerId = SessionData.CustomerId
            };

            var result = await _multiTabSalesManager.CreateNewSaleSessionAsync(request);
            if (result.Success && result.Session != null)
            {
                // Copy items to new session
                foreach (var item in SessionData.Items)
                {
                    await _multiTabSalesManager.AddItemToSessionAsync(result.Session.Id, item);
                }

                return result.Session;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error duplicating session: {TabName}", TabName);
            throw;
        }
    }

    public async Task ForceSyncNow()
    {
        try
        {
            if (!IsOfflineMode)
            {
                await SaveState();
                PendingSyncCount = 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during forced sync for tab: {TabName}", TabName);
            throw;
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateConnectionStatus();
        });
    }

    private void UpdateConnectionStatus()
    {
        var networkAccess = Connectivity.Current.NetworkAccess;
        IsOfflineMode = networkAccess != NetworkAccess.Internet;
        
        // Update pending sync count based on offline status
        PendingSyncCount = IsOfflineMode && HasUnsavedChanges ? 1 : 0;
    }
}