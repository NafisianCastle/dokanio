using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.Services;
using Shared.Core.DTOs;
using Shared.Core.Enums;
using Mobile.Services;
using AppPermissions = Mobile.Services.Permissions;

namespace Mobile.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly ISyncEngine _syncEngine;
    private readonly IConnectivityService _connectivityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserContextService _userContextService;
    private readonly IAuthenticationService _authenticationService;

    public MainViewModel(
        ISyncEngine syncEngine, 
        IConnectivityService connectivityService,
        ICurrentUserService currentUserService,
        IUserContextService userContextService,
        IAuthenticationService authenticationService)
    {
        _syncEngine = syncEngine;
        _connectivityService = connectivityService;
        _currentUserService = currentUserService;
        _userContextService = userContextService;
        _authenticationService = authenticationService;
        
        Title = "Multi-Business POS";
        
        // Subscribe to context changes
        _userContextService.ContextChanged += OnContextChanged;
        
        // Start background sync
        _ = Task.Run(async () => await _syncEngine.StartBackgroundSyncAsync());
    }

    [ObservableProperty]
    private bool isOnline;

    [ObservableProperty]
    private DateTime lastSyncTime;

    [ObservableProperty]
    private string syncStatus = "Ready";

    [ObservableProperty]
    private string welcomeMessage = "Welcome";

    [ObservableProperty]
    private string businessName = string.Empty;

    [ObservableProperty]
    private string shopName = string.Empty;

    [ObservableProperty]
    private string userRole = string.Empty;

    [ObservableProperty]
    private bool canViewSales = true;

    [ObservableProperty]
    private bool canCreateSales = true;

    [ObservableProperty]
    private bool canViewProducts = true;

    [ObservableProperty]
    private bool canViewReports = true;

    [ObservableProperty]
    private bool canViewAnalytics = false;

    [ObservableProperty]
    private bool canManageInventory = false;

    [ObservableProperty]
    private bool canManageUsers = false;

    [ObservableProperty]
    private bool showAdvancedFeatures = false;

    [RelayCommand]
    private async Task NavigateToProductList()
    {
        if (!CanViewProducts) return;
        await Shell.Current.GoToAsync("//products");
    }

    [RelayCommand]
    private async Task NavigateToNewSale()
    {
        if (!CanCreateSales) return;
        await Shell.Current.GoToAsync("//sale");
    }

    [RelayCommand]
    private async Task NavigateToDailySales()
    {
        if (!CanViewReports) return;
        await Shell.Current.GoToAsync("//dailysales");
    }

    [RelayCommand]
    private async Task NavigateToScanner()
    {
        if (!CanCreateSales) return;
        await Shell.Current.GoToAsync("//scanner");
    }

    [RelayCommand]
    private async Task NavigateToAnalytics()
    {
        if (!CanViewAnalytics) return;
        await Shell.Current.GoToAsync("//analytics");
    }

    [RelayCommand]
    private async Task NavigateToInventory()
    {
        if (!CanManageInventory) return;
        await Shell.Current.GoToAsync("//inventory");
    }

    [RelayCommand]
    private async Task NavigateToUserManagement()
    {
        if (!CanManageUsers) return;
        await Shell.Current.GoToAsync("//users");
    }

    [RelayCommand]
    private async Task SwitchBusinessShop()
    {
        await Shell.Current.GoToAsync("//businessselection");
    }

    [RelayCommand]
    private async Task ManualSync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            SyncStatus = "Syncing...";
            
            var result = await _syncEngine.SyncAllAsync();
            
            if (result.Success)
            {
                SyncStatus = "Sync completed";
                LastSyncTime = DateTime.Now;
            }
            else
            {
                SyncStatus = $"Sync failed: {result.Message}";
                SetError(result.Message ?? "Sync failed");
            }
        }
        catch (Exception ex)
        {
            SyncStatus = "Sync failed";
            SetError($"Sync error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Logout()
    {
        try
        {
            var user = _currentUserService.CurrentUser;
            if (user != null)
            {
                await _authenticationService.LogoutAsync(user.Id);
            }
            
            _currentUserService.ClearCurrentUser();
            _userContextService.ClearContext();
            
            await Shell.Current.GoToAsync("//login");
        }
        catch (Exception ex)
        {
            SetError($"Logout failed: {ex.Message}");
        }
    }

    public async Task Initialize()
    {
        await CheckConnectivity();
        await UpdateUserInfo();
        await UpdatePermissions();
    }

    public async Task CheckConnectivity()
    {
        IsOnline = _connectivityService.IsConnected;
    }

    private async Task UpdateUserInfo()
    {
        try
        {
            var user = _currentUserService.CurrentUser;
            if (user != null)
            {
                WelcomeMessage = $"Welcome, {user.FullName}";
                UserRole = user.Role.ToString();
            }

            var business = _userContextService.CurrentBusiness;
            if (business != null)
            {
                BusinessName = business.Name;
            }

            var shop = _userContextService.CurrentShop;
            if (shop != null)
            {
                ShopName = shop.Name;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update user info: {ex.Message}");
        }
    }

    private async Task UpdatePermissions()
    {
        try
        {
            var user = _currentUserService.CurrentUser;
            if (user == null) return;

            // Update permissions based on user role and context
            CanViewSales = await _userContextService.HasPermissionAsync(AppPermissions.ViewSales);
            CanCreateSales = await _userContextService.HasPermissionAsync(AppPermissions.CreateSales);
            CanViewProducts = await _userContextService.HasPermissionAsync(AppPermissions.ViewProducts);
            CanViewReports = await _userContextService.HasPermissionAsync(AppPermissions.ViewReports);
            CanViewAnalytics = await _userContextService.HasPermissionAsync(AppPermissions.ViewAnalytics);
            CanManageInventory = await _userContextService.HasPermissionAsync(AppPermissions.ModifyInventory);
            CanManageUsers = await _userContextService.HasPermissionAsync(AppPermissions.ManageUsers);

            // Show advanced features for business owners and managers
            ShowAdvancedFeatures = user.Role == Shared.Core.Enums.UserRole.BusinessOwner || 
                                  user.Role == Shared.Core.Enums.UserRole.ShopManager ||
                                  user.Role == Shared.Core.Enums.UserRole.Manager;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update permissions: {ex.Message}");
            
            // Default permissions for safety
            CanViewSales = true;
            CanCreateSales = true;
            CanViewProducts = true;
            CanViewReports = false;
            CanViewAnalytics = false;
            CanManageInventory = false;
            CanManageUsers = false;
            ShowAdvancedFeatures = false;
        }
    }

    private async void OnContextChanged(object? sender, ContextChangedEventArgs e)
    {
        await UpdateUserInfo();
        await UpdatePermissions();
    }
}