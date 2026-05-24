using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using System.Collections.ObjectModel;
using Shared.Core.DTOs;
using Shared.Core.Enums;
using Shared.Core.Services;

namespace Desktop.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBusinessManagementService _businessManagementService;
    private readonly IDashboardService _dashboardService;
    private readonly IMultiTenantSyncService _multiTenantSyncService;

    [Reactive] public string CurrentUser { get; set; } = "Not Logged In";
    [Reactive] public string CurrentBusinessName { get; set; } = "";
    [Reactive] public string CurrentShopName { get; set; } = "";
    [Reactive] public UserRole CurrentUserRole { get; set; } = UserRole.Cashier;
    [Reactive] public bool IsOnline { get; set; } = true;
    [Reactive] public DateTime LastSyncTime { get; set; } = DateTime.Now;
    [Reactive] public string SyncStatus { get; set; } = "Ready";
    [Reactive] public decimal TodaysSales { get; set; }
    [Reactive] public int TodaysTransactions { get; set; }
    [Reactive] public int LowStockItems { get; set; }
    [Reactive] public int ExpiryAlerts { get; set; }
    [Reactive] public int TotalBusinesses { get; set; }
    [Reactive] public int TotalShops { get; set; }
    [Reactive] public ObservableCollection<string> RecentActivities { get; set; } = new();
    [Reactive] public ObservableCollection<AlertSummary> DashboardAlerts { get; set; } = new();
    [Reactive] public ObservableCollection<BusinessResponse> Businesses { get; set; } = new();
    [Reactive] public BusinessResponse? SelectedBusiness { get; set; }
    [Reactive] public ObservableCollection<ShopResponse> Shops { get; set; } = new();
    [Reactive] public ShopResponse? SelectedShop { get; set; }

    // Child ViewModels
    public SaleViewModel SaleViewModel { get; }
    public SupplierViewModel SupplierViewModel { get; }
    public PurchaseViewModel PurchaseViewModel { get; }
    public ProductViewModel ProductViewModel { get; }
    public ReportsViewModel ReportsViewModel { get; }
    public BusinessManagementViewModel BusinessManagementViewModel { get; }
    public UserManagementViewModel UserManagementViewModel { get; }
    public AdvancedReportsViewModel AdvancedReportsViewModel { get; }
    public AIInventoryViewModel AIInventoryViewModel { get; }

    public bool IsBusinessOwner => CurrentUserRole == UserRole.BusinessOwner;
    public bool IsShopManager   => CurrentUserRole == UserRole.ShopManager;
    public bool CanManageUsers    => _currentUserService?.HasPermission(AuditAction.ChangeUserRole) == true;
    public bool CanViewReports    => _currentUserService?.HasPermission(AuditAction.AccessReports) == true;
    public bool CanManageInventory => _currentUserService?.HasPermission(AuditAction.UpdateInventory) == true
                                   || _currentUserService?.HasPermission(AuditAction.CreateProduct) == true;

    public event EventHandler? SessionExpired;

    public ReactiveCommand<Unit, Unit> LoadBusinessesCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadShopsForBusinessCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshDashboardCommand { get; }
    public ReactiveCommand<Unit, Unit> SyncDataCommand { get; }
    public ReactiveCommand<BusinessResponse, Unit> SelectBusinessCommand { get; }
    public ReactiveCommand<ShopResponse, Unit> SelectShopCommand { get; }

    public MainViewModel(
        ICurrentUserService currentUserService,
        IBusinessManagementService businessManagementService,
        IDashboardService dashboardService,
        IMultiTenantSyncService multiTenantSyncService,
        SaleViewModel saleViewModel,
        SupplierViewModel supplierViewModel,
        PurchaseViewModel purchaseViewModel,
        ProductViewModel productViewModel,
        ReportsViewModel reportsViewModel,
        BusinessManagementViewModel businessManagementViewModel,
        UserManagementViewModel userManagementViewModel,
        AdvancedReportsViewModel advancedReportsViewModel,
        AIInventoryViewModel aiInventoryViewModel)
    {
        _currentUserService = currentUserService;
        _businessManagementService = businessManagementService;
        _dashboardService = dashboardService;
        _multiTenantSyncService = multiTenantSyncService;

        Title = "Multi-Business POS Desktop";

        SaleViewModel = saleViewModel;
        SupplierViewModel = supplierViewModel;
        PurchaseViewModel = purchaseViewModel;
        ProductViewModel = productViewModel;
        ReportsViewModel = reportsViewModel;
        BusinessManagementViewModel = businessManagementViewModel;
        UserManagementViewModel = userManagementViewModel;
        AdvancedReportsViewModel = advancedReportsViewModel;
        AIInventoryViewModel = aiInventoryViewModel;

        LoadBusinessesCommand      = ReactiveCommand.CreateFromTask(LoadBusinessesAsync);
        LoadShopsForBusinessCommand = ReactiveCommand.CreateFromTask(LoadShopsForBusinessAsync);
        RefreshDashboardCommand    = ReactiveCommand.CreateFromTask(RefreshDashboardAsync);
        SyncDataCommand            = ReactiveCommand.CreateFromTask(SyncDataAsync);
        SelectBusinessCommand      = ReactiveCommand.Create<BusinessResponse>(b => { _ = OnUserActivityDetectedAsync(); SelectedBusiness = b; });
        SelectShopCommand          = ReactiveCommand.Create<ShopResponse>(s => { _ = OnUserActivityDetectedAsync(); SelectedShop = s; });

        // React to business/shop selection changes
        this.WhenAnyValue(x => x.SelectedBusiness).Subscribe(b =>
        {
            if (b != null)
            {
                CurrentBusinessName = b.Name;
                _ = LoadShopsForBusinessAsync();
                _ = LoadDashboardData();
            }
            else
            {
                CurrentBusinessName = "";
                Shops.Clear();
                SelectedShop = null;
            }
        });

        this.WhenAnyValue(x => x.SelectedShop).Subscribe(s =>
        {
            if (s != null)
            {
                CurrentShopName = s.Name;
                _ = LoadDashboardData();
            }
            else
            {
                CurrentShopName = "";
            }
        });

        LoadUserContext();
        _ = LoadDashboardData();
        _ = StartSessionExpiryMonitorAsync();
    }

    // Design-time constructor
    public MainViewModel()
    {
        _currentUserService = null!;
        _businessManagementService = null!;
        _dashboardService = null!;
        _multiTenantSyncService = null!;

        Title = "Multi-Business POS Desktop";

        SaleViewModel = new SaleViewModel();
        SupplierViewModel = new SupplierViewModel(null!, null!);
        PurchaseViewModel = new PurchaseViewModel(null!, null!, null!, null!, null!);
        ProductViewModel = new ProductViewModel(null!, null!, null!);
        ReportsViewModel = new ReportsViewModel(null!, null!, null!, null!, null!, null!);
        BusinessManagementViewModel = new BusinessManagementViewModel(null!, null!, null!);
        UserManagementViewModel = new UserManagementViewModel(null!, null!, null!, null!);
        AdvancedReportsViewModel = new AdvancedReportsViewModel(null!, null!, null!, null!, null!);
        AIInventoryViewModel = new AIInventoryViewModel(null!, null!, null!, null!, null!);

        LoadBusinessesCommand       = ReactiveCommand.CreateFromTask(LoadBusinessesAsync);
        LoadShopsForBusinessCommand = ReactiveCommand.CreateFromTask(LoadShopsForBusinessAsync);
        RefreshDashboardCommand     = ReactiveCommand.CreateFromTask(RefreshDashboardAsync);
        SyncDataCommand             = ReactiveCommand.CreateFromTask(SyncDataAsync);
        SelectBusinessCommand       = ReactiveCommand.Create<BusinessResponse>(_ => { });
        SelectShopCommand           = ReactiveCommand.Create<ShopResponse>(_ => { });
    }

    public async Task OnUserActivityDetectedAsync()
    {
        if (_currentUserService?.IsAuthenticated == true)
        {
            try { await _currentUserService.UpdateActivityAsync(); }
            catch { /* swallow — don't crash on activity update */ }
        }
    }

    private async Task LoadBusinessesAsync()
    {
        await OnUserActivityDetectedAsync();
        if (!IsBusinessOwner) return;

        try
        {
            var user = _currentUserService?.CurrentUser;
            if (user == null) return;

            var list = await _businessManagementService!.GetBusinessesByOwnerAsync(user.Id);
            Businesses.Clear();
            foreach (var b in list) Businesses.Add(b);
            TotalBusinesses = Businesses.Count;

            if (SelectedBusiness == null && Businesses.Any())
                SelectedBusiness = Businesses.First();
        }
        catch (Exception ex)
        {
            RecentActivities.Insert(0, $"Error loading businesses: {ex.Message}");
        }
    }

    private async Task LoadShopsForBusinessAsync()
    {
        await OnUserActivityDetectedAsync();
        if (SelectedBusiness == null) return;

        try
        {
            var list = await _businessManagementService!.GetShopsByBusinessAsync(SelectedBusiness.Id);
            Shops.Clear();
            foreach (var s in list) Shops.Add(s);
            TotalShops = Shops.Count;

            if (SelectedShop == null && Shops.Any())
                SelectedShop = Shops.First();
        }
        catch (Exception ex)
        {
            RecentActivities.Insert(0, $"Error loading shops: {ex.Message}");
        }
    }

    private async Task RefreshDashboardAsync()
    {
        await OnUserActivityDetectedAsync();
        await LoadDashboardData();
    }

    private async Task SyncDataAsync()
    {
        await OnUserActivityDetectedAsync();
        if (_multiTenantSyncService == null) return;

        SyncStatus = "Syncing...";
        try
        {
            if (SelectedBusiness != null)
            {
                var result = await _multiTenantSyncService.SyncBusinessDataAsync(SelectedBusiness.Id);
                LastSyncTime = DateTime.Now;
                SyncStatus = result.Success ? "Sync completed" : "Sync failed";
                RecentActivities.Insert(0, result.Success
                    ? $"Data sync completed at {DateTime.Now:HH:mm}"
                    : $"Sync failed: {result.Message}");

                if (result.Success) await LoadDashboardData();
            }
        }
        catch (Exception ex)
        {
            SyncStatus = "Sync error";
            RecentActivities.Insert(0, $"Sync error: {ex.Message}");
        }
    }

    private void LoadUserContext()
    {
        var user = _currentUserService?.CurrentUser;
        if (user == null) return;

        CurrentUser     = user.FullName ?? user.Username;
        CurrentUserRole = user.Role;

        this.RaisePropertyChanged(nameof(CanManageUsers));
        this.RaisePropertyChanged(nameof(CanViewReports));
        this.RaisePropertyChanged(nameof(CanManageInventory));
        this.RaisePropertyChanged(nameof(IsBusinessOwner));
        this.RaisePropertyChanged(nameof(IsShopManager));

        if (IsBusinessOwner) _ = LoadBusinessesAsync();
    }

    private async Task StartSessionExpiryMonitorAsync()
    {
        if (_currentUserService == null) return;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync())
        {
            if (!_currentUserService.IsAuthenticated) break;
            try
            {
                var expired = await _currentUserService.IsSessionExpiredAsync();
                if (expired)
                {
                    _currentUserService.ClearCurrentUser();
                    SessionExpired?.Invoke(this, EventArgs.Empty);
                    break;
                }
            }
            catch { /* transient errors — keep monitoring */ }
        }
    }

    private async Task LoadDashboardData()
    {
        if (_dashboardService == null) return;
        try
        {
            RecentActivities.Clear();
            RecentActivities.Add($"Dashboard loaded at {DateTime.Now:HH:mm}");

            if (SelectedBusiness != null)
            {
                var overview = await _dashboardService.GetDashboardOverviewAsync(SelectedBusiness.Id);
                TodaysSales       = overview.RealTimeSales.TodayRevenue;
                TodaysTransactions = overview.RealTimeSales.TodayTransactionCount;
                LowStockItems     = overview.InventoryStatus.LowStockProducts;
                ExpiryAlerts      = overview.InventoryStatus.ExpiringProducts;

                DashboardAlerts.Clear();
                foreach (var alert in overview.Alerts.Take(5)) DashboardAlerts.Add(alert);

                RecentActivities.Add($"Loaded data for {SelectedBusiness.Name}");
                if (SelectedShop != null) RecentActivities.Add($"Active shop: {SelectedShop.Name}");
            }

            RecentActivities.Add("Online - Ready to sync");
            RecentActivities.Add("System initialized successfully");
        }
        catch (Exception ex)
        {
            RecentActivities.Insert(0, $"Error loading dashboard: {ex.Message}");
        }
    }
}
