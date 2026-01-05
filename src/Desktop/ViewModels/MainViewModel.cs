using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using System.Collections.ObjectModel;

namespace Desktop.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBusinessManagementService _businessManagementService;
    private readonly IDashboardService _dashboardService;
    private readonly IMultiTenantSyncService _multiTenantSyncService;

    [ObservableProperty]
    private string currentUser = "Not Logged In";

    [ObservableProperty]
    private string currentBusinessName = "";

    [ObservableProperty]
    private string currentShopName = "";

    [ObservableProperty]
    private UserRole currentUserRole = UserRole.Cashier;

    [ObservableProperty]
    private bool isOnline = true;

    [ObservableProperty]
    private DateTime lastSyncTime = DateTime.Now;

    [ObservableProperty]
    private string syncStatus = "Ready";

    [ObservableProperty]
    private decimal todaysSales = 0m;

    [ObservableProperty]
    private int todaysTransactions = 0;

    [ObservableProperty]
    private int lowStockItems = 0;

    [ObservableProperty]
    private int expiryAlerts = 0;

    [ObservableProperty]
    private int totalBusinesses = 0;

    [ObservableProperty]
    private int totalShops = 0;

    [ObservableProperty]
    private ObservableCollection<string> recentActivities = new();

    [ObservableProperty]
    private ObservableCollection<AlertSummary> dashboardAlerts = new();

    // Multi-business context
    [ObservableProperty]
    private ObservableCollection<BusinessResponse> businesses = new();

    [ObservableProperty]
    private BusinessResponse? selectedBusiness;

    [ObservableProperty]
    private ObservableCollection<ShopResponse> shops = new();

    [ObservableProperty]
    private ShopResponse? selectedShop;

    // ViewModels
    public SaleViewModel SaleViewModel { get; }
    public SupplierViewModel SupplierViewModel { get; }
    public PurchaseViewModel PurchaseViewModel { get; }
    public ProductViewModel ProductViewModel { get; }
    public ReportsViewModel ReportsViewModel { get; }
    public BusinessManagementViewModel BusinessManagementViewModel { get; }
    public UserManagementViewModel UserManagementViewModel { get; }
    public AdvancedReportsViewModel AdvancedReportsViewModel { get; }
    public AIInventoryViewModel AIInventoryViewModel { get; }

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
        
        LoadUserContext();
        _ = LoadDashboardData();
    }

    // Parameterless constructor for design-time support
    public MainViewModel()
    {
        _currentUserService = null!;
        _businessManagementService = null!;
        _dashboardService = null!;
        _multiTenantSyncService = null!;
        
        Title = "Multi-Business POS Desktop";
        SaleViewModel = new SaleViewModel();
        SupplierViewModel = new SupplierViewModel();
        PurchaseViewModel = new PurchaseViewModel();
        ProductViewModel = new ProductViewModel();
        ReportsViewModel = new ReportsViewModel();
        BusinessManagementViewModel = new BusinessManagementViewModel(null!, null!, null!);
        UserManagementViewModel = new UserManagementViewModel(null!, null!, null!, null!);
        AdvancedReportsViewModel = new AdvancedReportsViewModel(null!, null!, null!, null!);
        AIInventoryViewModel = new AIInventoryViewModel(null!, null!, null!, null!);
    }

    public bool IsBusinessOwner => CurrentUserRole == UserRole.BusinessOwner;
    public bool IsShopManager => CurrentUserRole == UserRole.ShopManager;
    public bool CanManageUsers => IsBusinessOwner || IsShopManager;
    public bool CanViewReports => IsBusinessOwner || IsShopManager;
    public bool CanManageInventory => CurrentUserRole != UserRole.Cashier;

    [RelayCommand]
    private async Task LoadBusinessesAsync()
    {
        if (!IsBusinessOwner) return;

        try
        {
            var user = _currentUserService?.CurrentUser;
            if (user == null) return;

            var businessList = await _businessManagementService!.GetBusinessesByOwnerAsync(user.Id);
            Businesses.Clear();
            foreach (var business in businessList)
            {
                Businesses.Add(business);
            }

            TotalBusinesses = Businesses.Count;

            // Auto-select first business if none selected
            if (SelectedBusiness == null && Businesses.Any())
            {
                SelectedBusiness = Businesses.First();
            }
        }
        catch (Exception ex)
        {
            RecentActivities.Insert(0, $"Error loading businesses: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LoadShopsForBusinessAsync()
    {
        if (SelectedBusiness == null) return;

        try
        {
            var shopList = await _businessManagementService!.GetShopsByBusinessAsync(SelectedBusiness.Id);
            Shops.Clear();
            foreach (var shop in shopList)
            {
                Shops.Add(shop);
            }

            TotalShops = Shops.Count;

            // Auto-select first shop if none selected
            if (SelectedShop == null && Shops.Any())
            {
                SelectedShop = Shops.First();
            }
        }
        catch (Exception ex)
        {
            RecentActivities.Insert(0, $"Error loading shops: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshDashboardAsync()
    {
        await LoadDashboardData();
    }

    [RelayCommand]
    private async Task SyncDataAsync()
    {
        if (_multiTenantSyncService == null) return;

        SyncStatus = "Syncing...";
        
        try
        {
            if (SelectedBusiness != null)
            {
                var result = await _multiTenantSyncService.SyncBusinessDataAsync(SelectedBusiness.Id);
                
                LastSyncTime = DateTime.Now;
                SyncStatus = result.Success ? "Sync completed" : "Sync failed";
                
                var message = result.Success 
                    ? $"Data sync completed at {DateTime.Now:HH:mm}"
                    : $"Sync failed: {result.Message}";
                
                RecentActivities.Insert(0, message);

                if (result.Success)
                {
                    await LoadDashboardData();
                }
            }
        }
        catch (Exception ex)
        {
            SyncStatus = "Sync error";
            RecentActivities.Insert(0, $"Sync error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SelectBusiness(BusinessResponse business)
    {
        SelectedBusiness = business;
    }

    [RelayCommand]
    private void SelectShop(ShopResponse shop)
    {
        SelectedShop = shop;
    }

    partial void OnSelectedBusinessChanged(BusinessResponse? value)
    {
        if (value != null)
        {
            CurrentBusinessName = value.Name;
            _ = LoadShopsForBusinessAsync();
            _ = LoadDashboardData();
        }
        else
        {
            CurrentBusinessName = "";
            Shops.Clear();
            SelectedShop = null;
        }
    }

    partial void OnSelectedShopChanged(ShopResponse? value)
    {
        if (value != null)
        {
            CurrentShopName = value.Name;
            _ = LoadDashboardData();
        }
        else
        {
            CurrentShopName = "";
        }
    }

    private void LoadUserContext()
    {
        var user = _currentUserService?.CurrentUser;
        if (user != null)
        {
            CurrentUser = user.FullName ?? user.Username;
            CurrentUserRole = user.Role;
            
            if (IsBusinessOwner)
            {
                _ = LoadBusinessesAsync();
            }
        }
    }

    private async Task LoadDashboardData()
    {
        if (_dashboardService == null) return;

        try
        {
            // Update recent activities
            RecentActivities.Clear();
            RecentActivities.Add($"Dashboard loaded at {DateTime.Now:HH:mm}");

            if (SelectedBusiness != null)
            {
                // Load dashboard overview
                var overview = await _dashboardService.GetDashboardOverviewAsync(SelectedBusiness.Id);
                
                TodaysSales = overview.RealTimeSales.TodayRevenue;
                TodaysTransactions = overview.RealTimeSales.TodayTransactionCount;
                LowStockItems = overview.InventoryStatus.LowStockProducts;
                ExpiryAlerts = overview.InventoryStatus.ExpiringProducts;

                // Load alerts
                DashboardAlerts.Clear();
                foreach (var alert in overview.Alerts.Take(5)) // Show top 5 alerts
                {
                    DashboardAlerts.Add(alert);
                }

                RecentActivities.Add($"Loaded data for {SelectedBusiness.Name}");
                
                if (SelectedShop != null)
                {
                    RecentActivities.Add($"Active shop: {SelectedShop.Name}");
                }
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