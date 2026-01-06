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
/// </summary>
public partial class MobileSaleTabContainerViewModel : BaseViewModel
{
    private readonly IMultiTabSalesManager _multiTabSalesManager;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserContextService _userContextService;
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

    private Guid _currentUserId;
    private Guid _currentDeviceId;
    private Guid _currentShopId;

    public MobileSaleTabContainerViewModel(
        IMultiTabSalesManager multiTabSalesManager,
        ICurrentUserService currentUserService,
        IUserContextService userContextService,
        ILogger<MobileSaleTabContainerViewModel> logger)
    {
        _multiTabSalesManager = multiTabSalesManager;
        _currentUserService = currentUserService;
        _userContextService = userContextService;
        _logger = logger;
        
        Title = "Sales";
        
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
            _currentDeviceId = Microsoft.Maui.Devices.DeviceInfo.Current.Idiom == DeviceIdiom.Phone 
                ? new Guid(Microsoft.Maui.Devices.DeviceInfo.Current.Model.GetHashCode(), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
                : Guid.NewGuid();
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
        if (ActiveTab == null || SaleTabs.Count <= 1) return;

        var currentIndex = SaleTabs.IndexOf(ActiveTab);
        var nextIndex = (currentIndex + 1) % SaleTabs.Count;
        await SwitchToTab(SaleTabs[nextIndex]);
    }

    [RelayCommand]
    private async Task SwipeToPreviousTab()
    {
        if (ActiveTab == null || SaleTabs.Count <= 1) return;

        var currentIndex = SaleTabs.IndexOf(ActiveTab);
        var previousIndex = currentIndex == 0 ? SaleTabs.Count - 1 : currentIndex - 1;
        await SwitchToTab(SaleTabs[previousIndex]);
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
        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch
        {
            // Haptic feedback not available on this device
        }
    }

    public async Task InitializeAsync()
    {
        await LoadExistingSessions();
    }

    public async Task CleanupAsync()
    {
        try
        {
            await SaveAllTabs();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during mobile tab cleanup");
        }
    }

    public async Task HandleBackgroundAsync()
    {
        await SaveAllTabs();
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
        SaleViewModel = new SaleViewModel(
            null!, // These would be injected in a real implementation
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
        
        // Load session data into the view model
        LoadSessionDataIntoViewModel();
        
        // Subscribe to changes
        SaleViewModel.PropertyChanged += (s, e) => HasUnsavedChanges = true;
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