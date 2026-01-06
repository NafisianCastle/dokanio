using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Shared.Core.Services;
using Shared.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace Desktop.ViewModels;

/// <summary>
/// ViewModel for managing multiple sale tabs
/// </summary>
public partial class SaleTabContainerViewModel : BaseViewModel
{
    private readonly IMultiTabSalesManager _multiTabSalesManager;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SaleTabContainerViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<SaleTabViewModel> saleTabs = new();

    [ObservableProperty]
    private SaleTabViewModel? activeTab;

    [ObservableProperty]
    private object? activeTabContent;

    [ObservableProperty]
    private int maxTabs = 5;

    private Guid _currentUserId;
    private Guid _currentDeviceId;
    private Guid _currentShopId;

    public SaleTabContainerViewModel(
        IMultiTabSalesManager multiTabSalesManager,
        ICurrentUserService currentUserService,
        ILogger<SaleTabContainerViewModel> logger)
    {
        _multiTabSalesManager = multiTabSalesManager;
        _currentUserService = currentUserService;
        _logger = logger;
        
        Title = "Sales Management";
        
        // Initialize with current user context
        InitializeUserContext();
    }

    private void InitializeUserContext()
    {
        var currentUser = _currentUserService.CurrentUser;
        if (currentUser != null)
        {
            _currentUserId = currentUser.Id;
            _currentDeviceId = Environment.MachineName.GetHashCode() != 0 
                ? new Guid(Environment.MachineName.GetHashCode(), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
                : Guid.NewGuid();
            _currentShopId = currentUser.ShopId ?? Guid.NewGuid();
        }
    }

    [RelayCommand]
    private async Task CreateNewTab()
    {
        if (IsBusy) return;

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
            var tabName = GenerateTabName();

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
            var tabViewModel = new SaleTabViewModel(result.Session!, _multiTabSalesManager, _logger)
            {
                IsActive = false,
                CanClose = SaleTabs.Count > 0 // First tab cannot be closed
            };

            // Add to collection
            SaleTabs.Add(tabViewModel);

            // Switch to new tab
            await SwitchToTab(tabViewModel);

            _logger.LogInformation("Created new sale tab: {TabName}", tabName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new sale tab");
            SetError($"Failed to create new tab: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SwitchToTab(SaleTabViewModel tab)
    {
        if (tab == null || tab == ActiveTab) return;

        try
        {
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

            _logger.LogDebug("Switched to tab: {TabName}", tab.TabName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error switching to tab {TabName}", tab.TabName);
            SetError($"Failed to switch to tab: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CloseTab(SaleTabViewModel tab)
    {
        if (tab == null || !tab.CanClose) return;

        try
        {
            // Confirm if tab has unsaved changes
            if (tab.HasUnsavedChanges)
            {
                // In a real application, show a confirmation dialog
                // For now, we'll just save the changes
                await SaveTabState(tab);
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

            _logger.LogInformation("Closed sale tab: {TabName}", tab.TabName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing tab {TabName}", tab.TabName);
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
                var tabViewModel = new SaleTabViewModel(session, _multiTabSalesManager, _logger)
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
            _logger.LogError(ex, "Error loading existing sessions");
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
            _logger.LogError(ex, "Error saving all tabs");
            SetError($"Failed to save tabs: {ex.Message}");
        }
    }

    private async Task SaveCurrentTabState()
    {
        if (ActiveTab != null)
        {
            await SaveTabState(ActiveTab);
        }
    }

    private async Task SaveTabState(SaleTabViewModel tab)
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
                _logger.LogWarning("Failed to save tab state for {TabName}: {Message}", tab.TabName, result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving tab state for {TabName}", tab.TabName);
        }
    }

    private string GenerateTabName()
    {
        var baseNames = new[] { "Sale", "Transaction", "Order" };
        var baseName = baseNames[Random.Shared.Next(baseNames.Length)];
        
        var counter = 1;
        string tabName;
        
        do
        {
            tabName = $"{baseName} {counter}";
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
            _logger.LogError(ex, "Error during cleanup");
        }
    }
}

/// <summary>
/// ViewModel representing a single sale tab
/// </summary>
public partial class SaleTabViewModel : ObservableObject
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

    public SaleTabViewModel(SaleSessionDto sessionData, IMultiTabSalesManager multiTabSalesManager, ILogger logger)
    {
        SessionData = sessionData;
        SessionId = sessionData.Id;
        TabName = sessionData.TabName;
        _multiTabSalesManager = multiTabSalesManager;
        _logger = logger;

        // Create the sale view model for this tab
        SaleViewModel = new SaleViewModel();
        
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
                var saleItem = new Desktop.Models.SaleItem
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = (int)item.Quantity,
                    UnitPrice = item.UnitPrice,
                    BatchNumber = item.BatchNumber
                };
                
                SaleViewModel.SaleItems.Add(saleItem);
            }

            // Set payment method
            SaleViewModel.SelectedPaymentMethod = (Desktop.Models.PaymentMethod)SessionData.PaymentMethod;
            
            // Set customer info if available
            if (SessionData.CustomerId.HasValue)
            {
                // Load customer data - this would typically come from a customer service
                SaleViewModel.CustomerName = SessionData.CustomerName ?? string.Empty;
            }

            HasUnsavedChanges = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading session data into view model for tab {TabName}", TabName);
        }
    }

    public SaleSessionDto GetSessionData()
    {
        try
        {
            // Update session data from view model
            SessionData.TabName = TabName;
            SessionData.PaymentMethod = (Shared.Core.Enums.PaymentMethod)SaleViewModel.SelectedPaymentMethod;
            SessionData.LastModified = DateTime.UtcNow;

            // Convert sale items back to session items
            SessionData.Items.Clear();
            foreach (var item in SaleViewModel.SaleItems)
            {
                var sessionItem = new SaleSessionItemDto
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineTotal = item.Total,
                    BatchNumber = item.BatchNumber
                };
                
                SessionData.Items.Add(sessionItem);
            }

            // Update calculation
            SessionData.Calculation = new SaleSessionCalculationDto
            {
                Subtotal = SaleViewModel.Subtotal,
                TotalTax = SaleViewModel.Tax,
                FinalTotal = SaleViewModel.Total,
                CalculatedAt = DateTime.UtcNow
            };

            return SessionData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session data from view model for tab {TabName}", TabName);
            return SessionData;
        }
    }
}