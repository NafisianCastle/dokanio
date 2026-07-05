using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Services;

namespace Desktop.ViewModels;

/// <summary>
/// ViewModel for managing multiple sale tabs.
/// </summary>
public class SaleTabContainerViewModel : BaseViewModel
{
    private readonly IMultiTabSalesManager _multiTabSalesManager;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SaleTabContainerViewModel> _logger;

    [Reactive] public ObservableCollection<SaleTabViewModel> SaleTabs { get; set; } = new();
    [Reactive] public SaleTabViewModel? ActiveTab { get; set; }
    [Reactive] public object? ActiveTabContent { get; set; }
    [Reactive] public int MaxTabs { get; set; } = 5;

    private Guid _currentUserId;
    private Guid _currentDeviceId;
    private Guid _currentShopId;

    public ReactiveCommand<Unit, Unit> CreateNewTabCommand { get; }
    public ReactiveCommand<SaleTabViewModel, Unit> SwitchToTabCommand { get; }
    public ReactiveCommand<SaleTabViewModel, Unit> CloseTabCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadExistingSessionsCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveAllTabsCommand { get; }

    public SaleTabContainerViewModel(
        IMultiTabSalesManager multiTabSalesManager,
        ICurrentUserService currentUserService,
        ILogger<SaleTabContainerViewModel> logger)
    {
        _multiTabSalesManager = multiTabSalesManager;
        _currentUserService   = currentUserService;
        _logger               = logger;
        Title = "Sales Management";

        CreateNewTabCommand          = ReactiveCommand.CreateFromTask(CreateNewTabAsync);
        SwitchToTabCommand           = ReactiveCommand.CreateFromTask<SaleTabViewModel>(SwitchToTabAsync);
        CloseTabCommand              = ReactiveCommand.CreateFromTask<SaleTabViewModel>(CloseTabAsync);
        LoadExistingSessionsCommand  = ReactiveCommand.CreateFromTask(LoadExistingSessionsAsync);
        SaveAllTabsCommand           = ReactiveCommand.CreateFromTask(SaveAllTabsAsync);

        InitializeUserContext();
    }

    private void InitializeUserContext()
    {
        var user = _currentUserService.CurrentUser;
        if (user == null) return;
        _currentUserId   = user.Id;
        _currentDeviceId = Environment.MachineName.GetHashCode() != 0
            ? new Guid(Environment.MachineName.GetHashCode(), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
            : Guid.NewGuid();
        _currentShopId = user.ShopId ?? Guid.NewGuid();
    }

    private async Task CreateNewTabAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            if (!await _multiTabSalesManager.CanCreateNewSessionAsync(_currentUserId, _currentDeviceId))
            {
                SetError($"Maximum number of tabs ({MaxTabs}) reached. Please close a tab first.");
                return;
            }

            var tabName = GenerateTabName();
            var result  = await _multiTabSalesManager.CreateNewSaleSessionAsync(new CreateSaleSessionRequest
            {
                TabName  = tabName,
                ShopId   = _currentShopId,
                UserId   = _currentUserId,
                DeviceId = _currentDeviceId
            });

            if (!result.Success) { SetError($"Failed to create new tab: {result.Message}"); return; }

            var tab = new SaleTabViewModel(result.Session!, _multiTabSalesManager, _logger)
            {
                IsActive = false,
                CanClose = SaleTabs.Count > 0
            };
            SaleTabs.Add(tab);
            await SwitchToTabAsync(tab);
            _logger.LogInformation("Created new sale tab: {TabName}", tabName);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error creating new sale tab"); SetError($"Failed to create new tab: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    private async Task SwitchToTabAsync(SaleTabViewModel tab)
    {
        if (tab == ActiveTab) return;
        try
        {
            if (ActiveTab != null) { await SaveCurrentTabStateAsync(); ActiveTab.IsActive = false; }
            ActiveTab            = tab;
            ActiveTab.IsActive   = true;
            ActiveTabContent     = ActiveTab.SaleViewModel;
            await _multiTabSalesManager.SwitchToSessionAsync(tab.SessionId);
            _logger.LogDebug("Switched to tab: {TabName}", tab.TabName);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error switching to tab {TabName}", tab.TabName); SetError($"Failed to switch to tab: {ex.Message}"); }
    }

    private async Task CloseTabAsync(SaleTabViewModel tab)
    {
        if (!tab.CanClose) return;
        try
        {
            if (tab.HasUnsavedChanges) await SaveTabStateAsync(tab);
            await _multiTabSalesManager.CloseSessionAsync(tab.SessionId, true);
            SaleTabs.Remove(tab);

            if (ActiveTab == tab)
            {
                var next = SaleTabs.FirstOrDefault();
                if (next != null) await SwitchToTabAsync(next);
                else { ActiveTab = null; ActiveTabContent = null; }
            }
            UpdateTabClosePermissions();
            _logger.LogInformation("Closed sale tab: {TabName}", tab.TabName);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error closing tab {TabName}", tab.TabName); SetError($"Failed to close tab: {ex.Message}"); }
    }

    private async Task LoadExistingSessionsAsync()
    {
        IsBusy = true;
        ClearError();
        try
        {
            var sessions = await _multiTabSalesManager.GetActiveSessionsAsync(_currentUserId, _currentDeviceId);
            SaleTabs.Clear();
            foreach (var s in sessions)
                SaleTabs.Add(new SaleTabViewModel(s, _multiTabSalesManager, _logger) { IsActive = false, CanClose = sessions.Count > 1 });

            if (!SaleTabs.Any()) await CreateNewTabAsync();
            else await SwitchToTabAsync(SaleTabs.First());

            UpdateTabClosePermissions();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error loading existing sessions"); SetError($"Failed to load sessions: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    private async Task SaveAllTabsAsync()
    {
        try { foreach (var tab in SaleTabs) await SaveTabStateAsync(tab); }
        catch (Exception ex) { _logger.LogError(ex, "Error saving all tabs"); SetError($"Failed to save tabs: {ex.Message}"); }
    }

    private async Task SaveCurrentTabStateAsync()
    {
        if (ActiveTab != null) await SaveTabStateAsync(ActiveTab);
    }

    private async Task SaveTabStateAsync(SaleTabViewModel tab)
    {
        try
        {
            var result = await _multiTabSalesManager.SaveSessionStateAsync(tab.SessionId, tab.GetSessionData());
            if (result.Success) tab.HasUnsavedChanges = false;
            else _logger.LogWarning("Failed to save tab state for {TabName}: {Message}", tab.TabName, result.Message);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error saving tab state for {TabName}", tab.TabName); }
    }

    private string GenerateTabName()
    {
        var bases = new[] { "Sale", "Transaction", "Order" };
        var name  = bases[Random.Shared.Next(bases.Length)];
        var i     = 1;
        string tabName;
        do { tabName = $"{name} {i++}"; } while (SaleTabs.Any(t => t.TabName == tabName));
        return tabName;
    }

    private void UpdateTabClosePermissions()
    {
        var canClose = SaleTabs.Count > 1;
        foreach (var tab in SaleTabs) tab.CanClose = canClose;
    }

    public async Task InitializeAsync()  => await LoadExistingSessionsAsync();
    public async Task CleanupAsync()
    {
        try { await SaveAllTabsAsync(); }
        catch (Exception ex) { _logger.LogError(ex, "Error during cleanup"); }
    }
}

/// <summary>
/// ViewModel representing a single sale tab.
/// </summary>
public class SaleTabViewModel : ReactiveObject
{
    private readonly IMultiTabSalesManager _multiTabSalesManager;
    private readonly ILogger _logger;

    [Reactive] public string TabName { get; set; } = string.Empty;
    [Reactive] public bool IsActive { get; set; }
    [Reactive] public bool CanClose { get; set; } = true;
    [Reactive] public bool HasUnsavedChanges { get; set; }
    [Reactive] public SaleViewModel SaleViewModel { get; set; }

    public Guid SessionId { get; }
    public SaleSessionDto SessionData { get; private set; }

    public SaleTabViewModel(SaleSessionDto sessionData, IMultiTabSalesManager multiTabSalesManager, ILogger logger)
    {
        SessionData           = sessionData;
        SessionId             = sessionData.Id;
        TabName               = sessionData.TabName;
        _multiTabSalesManager = multiTabSalesManager;
        _logger               = logger;

        SaleViewModel = new SaleViewModel();
        LoadSessionDataIntoViewModel();
        SaleViewModel.PropertyChanged += (_, _) => HasUnsavedChanges = true;
    }

    private void LoadSessionDataIntoViewModel()
    {
        try
        {
            SaleViewModel.SaleItems.Clear();
            foreach (var item in SessionData.Items)
                SaleViewModel.SaleItems.Add(new Desktop.Models.SaleItem
                {
                    Id          = item.Id,
                    ProductId   = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity    = (int)item.Quantity,
                    UnitPrice   = item.UnitPrice,
                    BatchNumber = item.BatchNumber
                });

            SaleViewModel.SelectedPaymentMethod = (Desktop.Models.PaymentMethod)SessionData.PaymentMethod;
            if (SessionData.CustomerId.HasValue)
                SaleViewModel.CustomerName = SessionData.CustomerName ?? string.Empty;

            HasUnsavedChanges = false;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error loading session data into view model for tab {TabName}", TabName); }
    }

    public SaleSessionDto GetSessionData()
    {
        try
        {
            SessionData.TabName       = TabName;
            SessionData.PaymentMethod = (Shared.Core.Enums.PaymentMethod)SaleViewModel.SelectedPaymentMethod;
            SessionData.LastModified  = DateTime.UtcNow;

            SessionData.Items.Clear();
            foreach (var item in SaleViewModel.SaleItems)
                SessionData.Items.Add(new SaleSessionItemDto
                {
                    Id          = item.Id,
                    ProductId   = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity    = item.Quantity,
                    UnitPrice   = item.UnitPrice,
                    LineTotal   = item.Total,
                    BatchNumber = item.BatchNumber
                });

            SessionData.Calculation = new SaleSessionCalculationDto
            {
                Subtotal      = SaleViewModel.Subtotal,
                TotalTax      = SaleViewModel.Tax,
                FinalTotal    = SaleViewModel.Total,
                CalculatedAt  = DateTime.UtcNow
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
