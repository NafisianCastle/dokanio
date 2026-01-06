using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using Shared.Core.DTOs;
using Mobile.Services;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Mobile.ViewModels;

/// <summary>
/// Comprehensive mobile-specific SaleViewModel that integrates all enhanced mobile functionality
/// including tab management, touch optimization, haptic feedback, voice input, and gesture support
/// </summary>
public partial class ComprehensiveMobileSaleViewModel : SaleViewModel
{
    private readonly ILogger<ComprehensiveMobileSaleViewModel> _logger;
    private readonly IConnectivityService _connectivityService;
    private readonly IOfflineQueueService _offlineQueueService;
    
    // Enhanced mobile-specific properties
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

    [ObservableProperty]
    private ObservableCollection<QuickActionItem> quickActions = new();

    [ObservableProperty]
    private bool showOfflineIndicator;

    [ObservableProperty]
    private int pendingSyncCount;

    [ObservableProperty]
    private bool enableAdvancedGestures = true;

    [ObservableProperty]
    private bool enableShakeToRefresh = true;

    [ObservableProperty]
    private bool enablePinchToZoom = true;

    [ObservableProperty]
    private double uiScale = 1.0;

    [ObservableProperty]
    private bool isCompactMode;

    // Auto-save and sync functionality
    private Timer? _autoSaveTimer;
    private Timer? _syncTimer;
    private DateTime _lastInteraction = DateTime.UtcNow;
    private readonly Queue<PendingAction> _pendingActions = new();

    public ComprehensiveMobileSaleViewModel(
        IEnhancedSalesService enhancedSalesService,
        IProductService productService,
        IPrinterService printerService,
        IReceiptService receiptService,
        ICurrentUserService currentUserService,
        IUserContextService userContextService,
        IBusinessManagementService businessManagementService,
        IMultiTabSalesManager multiTabSalesManager,
        ICustomerLookupService customerLookupService,
        IBarcodeIntegrationService barcodeIntegrationService,
        IConnectivityService connectivityService,
        IOfflineQueueService offlineQueueService,
        ILogger<ComprehensiveMobileSaleViewModel> logger)
        : base(enhancedSalesService, productService, printerService, receiptService,
               currentUserService, userContextService, businessManagementService,
               multiTabSalesManager, customerLookupService, barcodeIntegrationService)
    {
        _connectivityService = connectivityService;
        _offlineQueueService = offlineQueueService;
        _logger = logger;
        
        InitializeQuickActions();
        InitializeMobileFeatures();
        InitializeConnectivityMonitoring();
    }

    private void InitializeQuickActions()
    {
        QuickActions.Add(new QuickActionItem
        {
            Id = "scan_barcode",
            Title = "Scan",
            Icon = "barcode_icon",
            Command = ScanBarcodeCommand,
            IsEnabled = IsBarcodeIntegrationEnabled
        });

        QuickActions.Add(new QuickActionItem
        {
            Id = "lookup_customer",
            Title = "Customer",
            Icon = "person_icon",
            Command = LookupCustomerCommand,
            IsEnabled = IsCustomerLookupEnabled
        });

        QuickActions.Add(new QuickActionItem
        {
            Id = "voice_search",
            Title = "Voice",
            Icon = "mic_icon",
            Command = VoiceSearchCommand,
            IsEnabled = EnableVoiceInput
        });

        QuickActions.Add(new QuickActionItem
        {
            Id = "complete_sale",
            Title = "Complete",
            Icon = "check_icon",
            Command = CompleteSaleCommand,
            IsEnabled = CanCompleteSale
        });

        QuickActions.Add(new QuickActionItem
        {
            Id = "settings",
            Title = "Settings",
            Icon = "settings_icon",
            Command = ShowMobileSettingsCommand,
            IsEnabled = true
        });
    }

    private void InitializeMobileFeatures()
    {
        // Detect device characteristics
        var deviceInfo = DeviceInfo.Current;
        var displayInfo = DeviceDisplay.Current.MainDisplayInfo;
        
        // Enable one-handed mode for smaller screens
        var physicalHeight = displayInfo.Height / displayInfo.Density;
        IsOneHandedMode = physicalHeight < 6.0; // Enable for screens smaller than 6 inches
        
        // Set compact mode for smaller screens
        IsCompactMode = displayInfo.Width / displayInfo.Density < 4.0;
        
        // Adjust UI scale based on screen density
        UiScale = displayInfo.Density > 2.0 ? 1.2 : 1.0;

        // Start auto-save if enabled
        if (EnableAutoSave)
        {
            StartAutoSave();
        }

        _logger.LogInformation("Comprehensive mobile sale view model initialized with device: {Device}, Screen: {Width}x{Height}, OneHanded: {OneHanded}", 
            deviceInfo.Model, displayInfo.Width, displayInfo.Height, IsOneHandedMode);
    }

    private void InitializeConnectivityMonitoring()
    {
        // Monitor connectivity changes
        _connectivityService.ConnectivityChanged += OnConnectivityChanged;
        UpdateConnectionStatus();
        
        // Start periodic sync timer
        StartSyncTimer();
    }

    [RelayCommand]
    private async Task HandleSwipeLeft()
    {
        if (!EnableSwipeGestures) return;
        
        TriggerHapticFeedback(HapticFeedbackType.Click);
        
        // Swipe left to show quick actions or next tab
        await ShowQuickActionsMenu();
    }

    [RelayCommand]
    private async Task HandleSwipeRight()
    {
        if (!EnableSwipeGestures) return;
        
        TriggerHapticFeedback(HapticFeedbackType.Click);
        
        // Swipe right to show customer lookup
        if (IsCustomerLookupEnabled)
        {
            await LookupCustomer();
        }
    }

    [RelayCommand]
    private async Task HandleSwipeUp()
    {
        if (!EnableSwipeGestures) return;
        
        TriggerHapticFeedback(HapticFeedbackType.Click);
        
        // Swipe up to complete sale if possible
        if (CanCompleteSale)
        {
            await CompleteSale();
        }
        else
        {
            await ShowQuickActionsMenu();
        }
    }

    [RelayCommand]
    private async Task HandleSwipeDown()
    {
        if (!EnableSwipeGestures) return;
        
        TriggerHapticFeedback(HapticFeedbackType.Click);
        
        // Swipe down to refresh or minimize
        await RefreshData();
    }

    [RelayCommand]
    private async Task HandleDoubleTap()
    {
        TriggerHapticFeedback(HapticFeedbackType.LongPress);
        
        // Double tap to complete sale quickly
        if (CanCompleteSale)
        {
            await CompleteSale();
        }
        else
        {
            // Show quick add menu
            await ShowQuickAddMenu();
        }
    }

    [RelayCommand]
    private async Task HandleLongPress()
    {
        TriggerHapticFeedback(HapticFeedbackType.LongPress);
        
        // Long press to show context menu
        await ShowContextMenu();
    }

    [RelayCommand]
    private async Task HandlePinchToZoom(double scaleFactor)
    {
        if (!EnablePinchToZoom) return;
        
        TriggerHapticFeedback(HapticFeedbackType.Click);
        
        // Adjust UI scale
        UiScale = Math.Max(0.8, Math.Min(2.0, UiScale * scaleFactor));
        
        // Toggle compact mode based on scale
        IsCompactMode = UiScale < 1.0;
    }

    [RelayCommand]
    private async Task HandleShakeToRefresh()
    {
        if (!EnableShakeToRefresh) return;
        
        TriggerHapticFeedback(HapticFeedbackType.LongPress);
        
        await RefreshData();
        await Shell.Current.DisplayAlert("Refreshed", "Data has been refreshed", "OK");
    }

    [RelayCommand]
    private async Task ToggleOneHandedMode()
    {
        IsOneHandedMode = !IsOneHandedMode;
        TriggerHapticFeedback(HapticFeedbackType.Click);
        
        // Update quick actions visibility based on mode
        UpdateQuickActionsForMode();
        
        await Shell.Current.DisplayAlert(
            "One-Handed Mode", 
            IsOneHandedMode ? "One-handed mode enabled" : "One-handed mode disabled", 
            "OK");
    }

    [RelayCommand]
    private async Task ToggleCompactMode()
    {
        IsCompactMode = !IsCompactMode;
        TriggerHapticFeedback(HapticFeedbackType.Click);
        
        // Adjust UI scale for compact mode
        UiScale = IsCompactMode ? 0.9 : 1.0;
        
        await Shell.Current.DisplayAlert(
            "Compact Mode", 
            IsCompactMode ? "Compact mode enabled" : "Compact mode disabled", 
            "OK");
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
            $"Compact Mode: {(IsCompactMode ? "On" : "Off")}",
            $"Auto Save: {(EnableAutoSave ? "On" : "Off")}",
            $"Shake to Refresh: {(EnableShakeToRefresh ? "On" : "Off")}",
            $"Pinch to Zoom: {(EnablePinchToZoom ? "On" : "Off")}"
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

        if (selectedSetting.Contains("Haptic Feedback"))
        {
            await ToggleHapticFeedback();
        }
        else if (selectedSetting.Contains("Voice Input"))
        {
            EnableVoiceInput = !EnableVoiceInput;
            UpdateQuickActionState("voice_search", EnableVoiceInput);
            TriggerHapticFeedback(HapticFeedbackType.Click);
        }
        else if (selectedSetting.Contains("Gesture Navigation"))
        {
            EnableGestureNavigation = !EnableGestureNavigation;
            TriggerHapticFeedback(HapticFeedbackType.Click);
        }
        else if (selectedSetting.Contains("One-Handed Mode"))
        {
            await ToggleOneHandedMode();
        }
        else if (selectedSetting.Contains("Compact Mode"))
        {
            await ToggleCompactMode();
        }
        else if (selectedSetting.Contains("Auto Save"))
        {
            EnableAutoSave = !EnableAutoSave;
            if (EnableAutoSave)
            {
                StartAutoSave();
            }
            else
            {
                StopAutoSave();
            }
            TriggerHapticFeedback(HapticFeedbackType.Click);
        }
        else if (selectedSetting.Contains("Shake to Refresh"))
        {
            EnableShakeToRefresh = !EnableShakeToRefresh;
            TriggerHapticFeedback(HapticFeedbackType.Click);
        }
        else if (selectedSetting.Contains("Pinch to Zoom"))
        {
            EnablePinchToZoom = !EnablePinchToZoom;
            TriggerHapticFeedback(HapticFeedbackType.Click);
        }
    }

    private async Task ShowQuickActionsMenu()
    {
        var availableActions = QuickActions.Where(a => a.IsEnabled && a.IsVisible).ToList();
        
        if (!availableActions.Any())
        {
            await Shell.Current.DisplayAlert("Quick Actions", "No actions available", "OK");
            return;
        }

        var actionTitles = availableActions.Select(a => a.Title).ToArray();
        
        var selectedAction = await Shell.Current.DisplayActionSheet(
            "Quick Actions", 
            "Cancel", 
            null, 
            actionTitles);

        var action = availableActions.FirstOrDefault(a => a.Title == selectedAction);
        if (action?.Command?.CanExecute(null) == true)
        {
            action.Command.Execute(null);
        }
    }

    private async Task ShowQuickAddMenu()
    {
        var options = new[] { "Scan Barcode", "Search Products", "Voice Search", "Recent Products" };
        
        var action = await Shell.Current.DisplayActionSheet(
            "Quick Add", 
            "Cancel", 
            null, 
            options);

        switch (action)
        {
            case "Scan Barcode":
                await ScanBarcode();
                break;
            case "Search Products":
                await ShowProductSearch();
                break;
            case "Voice Search":
                await VoiceSearch();
                break;
            case "Recent Products":
                await ShowRecentProducts();
                break;
        }
    }

    private async Task ShowProductSearch()
    {
        var searchTerm = await Shell.Current.DisplayPromptAsync(
            "Product Search", 
            "Enter product name or barcode:", 
            "Search", 
            "Cancel", 
            placeholder: "Product name...");

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            await SearchAndAddProduct(searchTerm);
        }
    }

    private async Task ShowRecentProducts()
    {
        try
        {
            // This would load recent products from a service
            var recentProducts = new[] { "Sample Product 1", "Sample Product 2", "Sample Product 3" };
            
            var selectedProduct = await Shell.Current.DisplayActionSheet(
                "Recent Products", 
                "Cancel", 
                null, 
                recentProducts);

            if (!string.IsNullOrEmpty(selectedProduct) && selectedProduct != "Cancel")
            {
                await SearchAndAddProduct(selectedProduct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing recent products");
            SetError("Failed to load recent products");
        }
    }

    private async Task ShowContextMenu()
    {
        var options = new List<string>();
        
        if (SaleItems.Any())
        {
            options.Add("Complete Sale");
            options.Add("Clear Sale");
            options.Add("Save Draft");
        }
        
        options.AddRange(new[] { "Settings", "Help", "Sync Now" });
        
        if (IsOfflineMode)
        {
            options.Add($"Offline ({PendingSyncCount} pending)");
        }
        
        var action = await Shell.Current.DisplayActionSheet(
            "Options", 
            "Cancel", 
            null, 
            options.ToArray());

        switch (action)
        {
            case "Complete Sale":
                await CompleteSale();
                break;
            case "Clear Sale":
                await ClearSale();
                break;
            case "Save Draft":
                await SaveToSession();
                await Shell.Current.DisplayAlert("Saved", "Sale saved as draft", "OK");
                break;
            case "Settings":
                await ShowMobileSettings();
                break;
            case "Help":
                await ShowMobileHelp();
                break;
            case "Sync Now":
                await ForceSyncNow();
                break;
        }
    }

    private async Task ShowMobileHelp()
    {
        var helpText = "Mobile POS Help:\n\n" +
                      "Gestures:\n" +
                      "• Swipe left: Quick actions\n" +
                      "• Swipe right: Customer lookup\n" +
                      "• Swipe up: Complete sale\n" +
                      "• Swipe down: Refresh\n" +
                      "• Double tap: Quick complete\n" +
                      "• Long press: Context menu\n" +
                      "• Pinch: Zoom UI\n" +
                      "• Shake: Refresh all\n\n" +
                      "Voice Commands:\n" +
                      "• 'Add [product name]'\n" +
                      "• 'Scan barcode'\n" +
                      "• 'Complete sale'\n" +
                      "• 'Clear sale'\n" +
                      "• 'Lookup customer'\n\n" +
                      "Offline Mode:\n" +
                      "• Sales are saved locally\n" +
                      "• Auto-sync when online\n" +
                      "• Check sync status in menu";

        await Shell.Current.DisplayAlert("Help", helpText, "OK");
    }

    private async Task RefreshData()
    {
        try
        {
            TriggerHapticFeedback(HapticFeedbackType.Click);
            
            // Refresh current data
            await Initialize();
            
            // Sync if online
            if (!IsOfflineMode)
            {
                await SyncPendingActions();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing data");
            SetError($"Refresh failed: {ex.Message}");
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
            TriggerHapticFeedback(HapticFeedbackType.Click);
            await SyncPendingActions();
            await Shell.Current.DisplayAlert("Sync Complete", "All data synchronized", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during forced sync");
            SetError($"Sync failed: {ex.Message}");
        }
    }

    private void UpdateQuickActionsForMode()
    {
        if (IsOneHandedMode)
        {
            // Show fewer actions in one-handed mode
            foreach (var action in QuickActions)
            {
                action.IsVisible = action.Id is "scan_barcode" or "complete_sale" or "settings";
            }
        }
        else
        {
            // Show all actions in normal mode
            foreach (var action in QuickActions)
            {
                action.IsVisible = true;
            }
        }
    }

    private void UpdateQuickActionState(string actionId, bool isEnabled)
    {
        var action = QuickActions.FirstOrDefault(a => a.Id == actionId);
        if (action != null)
        {
            action.IsEnabled = isEnabled;
        }
    }

    private void StartAutoSave()
    {
        if (!EnableAutoSave) return;
        
        _autoSaveTimer?.Dispose();
        _autoSaveTimer = new Timer(async _ =>
        {
            try
            {
                // Only auto-save if there's been recent activity
                if (DateTime.UtcNow - _lastInteraction < TimeSpan.FromMinutes(5))
                {
                    await SaveToSession();
                    _logger.LogDebug("Auto-saved comprehensive mobile sale session");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-save failed");
            }
        }, null, AutoSaveInterval, AutoSaveInterval);
    }

    private void StopAutoSave()
    {
        _autoSaveTimer?.Dispose();
        _autoSaveTimer = null;
    }

    private void StartSyncTimer()
    {
        _syncTimer?.Dispose();
        _syncTimer = new Timer(async _ =>
        {
            try
            {
                if (!IsOfflineMode && _pendingActions.Any())
                {
                    await SyncPendingActions();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background sync failed");
            }
        }, null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));
    }

    private void StopSyncTimer()
    {
        _syncTimer?.Dispose();
        _syncTimer = null;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateConnectionStatus();
            
            if (!IsOfflineMode)
            {
                // Sync any pending changes when coming back online
                _ = Task.Run(async () => await SyncPendingActions());
            }
        });
    }

    private void UpdateConnectionStatus()
    {
        var networkAccess = Connectivity.Current.NetworkAccess;
        IsOfflineMode = networkAccess != NetworkAccess.Internet;
        ConnectionStatus = IsOfflineMode ? "Offline" : "Online";
        ShowOfflineIndicator = IsOfflineMode;
        
        if (IsOfflineMode)
        {
            PendingSyncCount = _pendingActions.Count;
        }
    }

    private async Task SyncPendingActions()
    {
        if (IsOfflineMode || !_pendingActions.Any()) return;

        List<PendingAction> actionsToSync;
        try
        {
            actionsToSync = _pendingActions.ToList();
            _pendingActions.Clear();

            foreach (var action in actionsToSync)
            {
                await ProcessPendingAction(action);
            }

            PendingSyncCount = _pendingActions.Count;
            _logger.LogInformation("Synced {Count} pending actions", actionsToSync.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing pending actions");

            // Re-queue the actions we attempted (don't lose them)
            foreach (var action in actionsToSync)
            {
                _pendingActions.Enqueue(action);
            }

            PendingSyncCount = _pendingActions.Count;
        }
    }

    private async Task ProcessPendingAction(PendingAction action)
    {
        try
        {
            switch (action.Type)
            {
                case PendingActionType.SaveSession:
                    await _multiTabSalesManager.SaveSessionStateAsync(action.SessionId, action.Data);
                    break;
                case PendingActionType.CompleteSale:
                    // Process completed sale
                    break;
                case PendingActionType.CreateCustomer:
                    // Process customer creation
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending action: {ActionType}", action.Type);
            throw;
        }
    }

    // Override methods to add offline support
    public override async Task SaveToSession()
    {
        try
        {
            if (IsOfflineMode)
            {
                // Queue for later sync
                var action = new PendingAction
                {
                    Type = PendingActionType.SaveSession,
                    SessionId = CurrentSessionId ?? Guid.NewGuid(),
                    Data = GetCurrentSessionData(),
                    Timestamp = DateTime.UtcNow
                };
                
                _pendingActions.Enqueue(action);
                PendingSyncCount = _pendingActions.Count;
            }
            else
            {
                await base.SaveToSession();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving session in comprehensive mobile view model");
            throw;
        }
    }

    private object GetCurrentSessionData()
    {
        // Create session data from current state
        return new
        {
            Items = SaleItems.ToList(),
            Customer = CurrentCustomer,
            PaymentMethod = SelectedPaymentMethod,
            Totals = new { Subtotal, DiscountAmount, TaxAmount, TotalAmount }
        };
    }

    // Track user interactions for auto-save
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        _lastInteraction = DateTime.UtcNow;
        
        // Update quick actions based on property changes
        if (e.PropertyName == nameof(CanCompleteSale))
        {
            UpdateQuickActionState("complete_sale", CanCompleteSale);
        }
    }

    public override void Dispose()
    {
        StopAutoSave();
        StopSyncTimer();
        
        // Unsubscribe from connectivity events
        _connectivityService.ConnectivityChanged -= OnConnectivityChanged;
        
        base.Dispose();
    }
}

/// <summary>
/// Represents a pending action for offline sync
/// </summary>
public class PendingAction
{
    public PendingActionType Type { get; set; }
    public Guid SessionId { get; set; }
    public object? Data { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Types of pending actions for offline sync
/// </summary>
public enum PendingActionType
{
    SaveSession,
    CompleteSale,
    CreateCustomer,
    UpdateProduct,
    SyncInventory
}