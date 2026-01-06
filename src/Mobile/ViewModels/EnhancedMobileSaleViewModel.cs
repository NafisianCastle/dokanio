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
/// Enhanced mobile-specific SaleViewModel with comprehensive touch optimization,
/// haptic feedback, voice input, and gesture support
/// </summary>
public partial class EnhancedMobileSaleViewModel : SaleViewModel
{
    private readonly ILogger<EnhancedMobileSaleViewModel> _logger;
    private Timer? _autoSaveTimer;
    private DateTime _lastInteraction = DateTime.UtcNow;

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
    private ObservableCollection<QuickActionItem> quickActions = new();

    public EnhancedMobileSaleViewModel(
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
        ILogger<EnhancedMobileSaleViewModel> logger)
        : base(enhancedSalesService, productService, printerService, receiptService,
               currentUserService, userContextService, businessManagementService,
               multiTabSalesManager, customerLookupService, barcodeIntegrationService)
    {
        _logger = logger;
        
        InitializeQuickActions();
        InitializeMobileFeatures();
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
    }

    private void InitializeMobileFeatures()
    {
        // Enable one-handed mode for smaller screens
        var screenHeight = DeviceDisplay.Current.MainDisplayInfo.Height;
        var screenDensity = DeviceDisplay.Current.MainDisplayInfo.Density;
        var physicalHeight = screenHeight / screenDensity;
        
        IsOneHandedMode = physicalHeight < 6.0; // Enable for screens smaller than 6 inches

        // Start auto-save if enabled
        if (EnableAutoSave)
        {
            StartAutoSave();
        }

        _logger.LogInformation("Enhanced mobile sale view model initialized with one-handed mode: {OneHandedMode}", IsOneHandedMode);
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
            TriggerHapticFeedback(HapticFeedbackType.Click);

            var isAvailable = await SpeechToText.Default.GetAvailabilityAsync();
            if (isAvailable != SpeechToTextAuthorizationStatus.Authorized)
            {
                SetError("Voice input permission not granted");
                VoiceInputStatus = "Permission denied";
                TriggerErrorHapticFeedback();
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
                TriggerErrorHapticFeedback();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Voice search failed");
            SetError($"Voice search failed: {ex.Message}");
            VoiceInputStatus = "Voice search failed";
            TriggerErrorHapticFeedback();
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

    private async Task ProcessVoiceCommand(string command)
    {
        try
        {
            var lowerCommand = command.ToLowerInvariant();

            // Handle different voice commands
            if (lowerCommand.Contains("scan") || lowerCommand.Contains("barcode"))
            {
                await ScanBarcode();
            }
            else if (lowerCommand.Contains("customer") || lowerCommand.Contains("lookup"))
            {
                await LookupCustomer();
            }
            else if (lowerCommand.Contains("complete") || lowerCommand.Contains("finish"))
            {
                await CompleteSale();
            }
            else if (lowerCommand.Contains("clear") || lowerCommand.Contains("reset"))
            {
                await ClearSale();
            }
            else if (lowerCommand.Contains("add") || lowerCommand.Contains("search"))
            {
                // Extract product name from command
                var productName = ExtractProductNameFromCommand(command);
                if (!string.IsNullOrWhiteSpace(productName))
                {
                    await SearchAndAddProduct(productName);
                }
                else
                {
                    SetError("Could not understand product name");
                }
            }
            else
            {
                // Try to search for products with the entire command
                await SearchAndAddProduct(command);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing voice command: {Command}", command);
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
                    TriggerHapticFeedback(HapticFeedbackType.Click);
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
                            TriggerHapticFeedback(HapticFeedbackType.Click);
                        }
                    }
                }
            }
            else
            {
                VoiceInputStatus = "Product not found";
                SetError($"No products found for '{searchTerm}'");
                TriggerErrorHapticFeedback();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for product: {SearchTerm}", searchTerm);
            SetError($"Product search failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SwipeLeft()
    {
        if (!EnableSwipeGestures) return;

        TriggerHapticFeedback(HapticFeedbackType.Click);
        
        // Swipe left to go to next tab or action
        if (CurrentSessionId.HasValue)
        {
            // This would be handled by the tab container
            // For now, just provide feedback
            await Shell.Current.DisplayAlert("Swipe", "Swipe left detected", "OK");
        }
    }

    [RelayCommand]
    private async Task SwipeRight()
    {
        if (!EnableSwipeGestures) return;

        TriggerHapticFeedback(HapticFeedbackType.Click);
        
        // Swipe right to go to previous tab or action
        if (CurrentSessionId.HasValue)
        {
            // This would be handled by the tab container
            // For now, just provide feedback
            await Shell.Current.DisplayAlert("Swipe", "Swipe right detected", "OK");
        }
    }

    [RelayCommand]
    private async Task SwipeUp()
    {
        if (!EnableSwipeGestures) return;

        TriggerHapticFeedback(HapticFeedbackType.Click);
        
        // Swipe up to show more options or complete sale
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
    private async Task SwipeDown()
    {
        if (!EnableSwipeGestures) return;

        TriggerHapticFeedback(HapticFeedbackType.Click);
        
        // Swipe down to clear or minimize
        await ClearSale();
    }

    [RelayCommand]
    private async Task DoubleTap()
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
    private async Task LongPress()
    {
        TriggerHapticFeedback(HapticFeedbackType.LongPress);
        
        // Long press to show context menu
        await ShowContextMenu();
    }

    private async Task ShowQuickAddMenu()
    {
        var options = new[] { "Scan Barcode", "Search Products", "Add Customer", "Voice Search" };
        
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
            case "Add Customer":
                await LookupCustomer();
                break;
            case "Voice Search":
                await VoiceSearch();
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

    private async Task ShowContextMenu()
    {
        var options = new List<string>();
        
        if (SaleItems.Any())
        {
            options.Add("Complete Sale");
            options.Add("Clear Sale");
        }
        
        options.AddRange(new[] { "Settings", "Help", "Refresh" });
        
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
            case "Settings":
                await ShowMobileSettings();
                break;
            case "Help":
                await ShowHelp();
                break;
            case "Refresh":
                await Refresh();
                break;
        }
    }

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
            EnableVoiceInput = !EnableVoiceInput;
            TriggerHapticFeedback(HapticFeedbackType.Click);
        }
        else if (selectedSetting?.Contains("Gesture Navigation") == true)
        {
            EnableGestureNavigation = !EnableGestureNavigation;
            TriggerHapticFeedback(HapticFeedbackType.Click);
        }
        else if (selectedSetting?.Contains("One-Handed Mode") == true)
        {
            IsOneHandedMode = !IsOneHandedMode;
            TriggerHapticFeedback(HapticFeedbackType.Click);
        }
        else if (selectedSetting?.Contains("Auto Save") == true)
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
    }

    private async Task ShowHelp()
    {
        var helpText = "Mobile POS Help:\n\n" +
                      "• Swipe left/right: Navigate tabs\n" +
                      "• Swipe up: Complete sale\n" +
                      "• Swipe down: Clear sale\n" +
                      "• Double tap: Quick complete\n" +
                      "• Long press: Context menu\n" +
                      "• Voice: Say 'add [product]' or 'scan barcode'\n" +
                      "• Shake device: Refresh all tabs";

        await Shell.Current.DisplayAlert("Help", helpText, "OK");
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

    private void UpdateQuickActionsForMode()
    {
        // In one-handed mode, show fewer quick actions
        if (IsOneHandedMode)
        {
            ShowQuickActions = true;
            // Keep only essential actions visible
            foreach (var action in QuickActions)
            {
                action.IsVisible = action.Id is "scan_barcode" or "complete_sale";
            }
        }
        else
        {
            // Show all quick actions in normal mode
            foreach (var action in QuickActions)
            {
                action.IsVisible = true;
            }
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
                    _logger.LogDebug("Auto-saved mobile sale session");
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

    private void TriggerErrorHapticFeedback()
    {
        TriggerHapticFeedback(HapticFeedbackType.LongPress);
    }

    // Track user interactions for auto-save
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        _lastInteraction = DateTime.UtcNow;
    }

    public override void Dispose()
    {
        StopAutoSave();
        base.Dispose();
    }
}

/// <summary>
/// Represents a quick action item for mobile interface
/// </summary>
public partial class QuickActionItem : ObservableObject
{
    [ObservableProperty]
    private string id = string.Empty;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string icon = string.Empty;

    [ObservableProperty]
    private bool isEnabled = true;

    [ObservableProperty]
    private bool isVisible = true;

    public IRelayCommand? Command { get; set; }
}