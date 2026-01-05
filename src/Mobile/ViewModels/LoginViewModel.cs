using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.Services;
using Shared.Core.Entities;
using Shared.Core.DTOs;
using AuthResult = Shared.Core.Services.AuthenticationResult;
using LoginReq = Shared.Core.Services.LoginRequest;

namespace Mobile.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IBusinessManagementService _businessManagementService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IConnectivityService _connectivityService;

    public LoginViewModel(
        IAuthenticationService authenticationService,
        IBusinessManagementService businessManagementService,
        ICurrentUserService currentUserService,
        IConnectivityService connectivityService)
    {
        _authenticationService = authenticationService;
        _businessManagementService = businessManagementService;
        _currentUserService = currentUserService;
        _connectivityService = connectivityService;
        Title = "Login";
        
        Businesses = new ObservableCollection<BusinessResponse>();
        Shops = new ObservableCollection<ShopResponse>();
    }

    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool rememberMe = true;

    [ObservableProperty]
    private bool isOnline;

    [ObservableProperty]
    private bool showBusinessSelection;

    [ObservableProperty]
    private bool showShopSelection;

    [ObservableProperty]
    private ObservableCollection<BusinessResponse> businesses;

    [ObservableProperty]
    private ObservableCollection<ShopResponse> shops;

    [ObservableProperty]
    private BusinessResponse? selectedBusiness;

    [ObservableProperty]
    private ShopResponse? selectedShop;

    [ObservableProperty]
    private bool canLogin;

    [ObservableProperty]
    private string loginButtonText = "Login";

    private AuthResult? _authResult;

    [RelayCommand]
    private async Task Login()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ClearError();
            LoginButtonText = "Logging in...";

            // Check connectivity
            IsOnline = _connectivityService.IsConnected;

            AuthResult result;

            if (IsOnline)
            {
                // Online authentication
                var loginRequest = new LoginReq
                {
                    Username = Username,
                    Password = Password,
                    DeviceId = _currentUserService.GetDeviceId()
                };

                result = await _authenticationService.AuthenticateAsync(loginRequest);
            }
            else
            {
                // Offline authentication with cached credentials
                var cachedToken = await GetCachedTokenAsync(Username);
                if (string.IsNullOrEmpty(cachedToken))
                {
                    SetError("No cached credentials found. Please connect to internet for first login.");
                    return;
                }

                result = await _authenticationService.AuthenticateOfflineAsync(Username, cachedToken);
            }

            if (!result.IsSuccess || result.User == null)
            {
                SetError(result.ErrorMessage ?? "Login failed");
                return;
            }

            _authResult = result;

            // Set current user
            if (result.Session != null)
            {
                _currentUserService.SetCurrentUser(result.User, result.Session);
            }

            // Cache credentials if remember me is enabled and we're online
            if (RememberMe && IsOnline && result.Session != null)
            {
                await _authenticationService.CacheCredentialsAsync(
                    result.User.Id, 
                    result.Session.SessionToken, 
                    TimeSpan.FromDays(30));
            }

            // Load businesses and shops based on user role
            await LoadUserBusinessesAndShops(result.User);

            // Navigate based on user role and business/shop selection needs
            await HandlePostLoginNavigation(result.User);
        }
        catch (Exception ex)
        {
            SetError($"Login error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            LoginButtonText = "Login";
        }
    }

    [RelayCommand]
    private async Task SelectBusiness()
    {
        if (SelectedBusiness == null) return;

        try
        {
            IsBusy = true;

            // Load shops for selected business
            var shops = await _businessManagementService.GetShopsByBusinessAsync(SelectedBusiness.Id);
            Shops.Clear();
            foreach (var shop in shops)
            {
                Shops.Add(shop);
            }

            // Show shop selection if user needs to select a shop
            if (_authResult?.User?.Role == Shared.Core.Enums.UserRole.BusinessOwner)
            {
                ShowShopSelection = Shops.Count > 1;
                if (!ShowShopSelection && Shops.Count == 1)
                {
                    SelectedShop = Shops.First();
                }
            }
            else
            {
                // For other roles, filter shops they can access
                var accessibleShops = Shops.Where(s => CanUserAccessShop(_authResult?.User, s.Id)).ToList();
                Shops.Clear();
                foreach (var shop in accessibleShops)
                {
                    Shops.Add(shop);
                }

                ShowShopSelection = Shops.Count > 1;
                if (!ShowShopSelection && Shops.Count == 1)
                {
                    SelectedShop = Shops.First();
                }
            }

            CanLogin = SelectedBusiness != null && (!ShowShopSelection || SelectedShop != null);
        }
        catch (Exception ex)
        {
            SetError($"Failed to load shops: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectShop()
    {
        CanLogin = SelectedBusiness != null && SelectedShop != null;
    }

    [RelayCommand]
    private async Task CompleteLogin()
    {
        if (!CanLogin || _authResult?.User == null) return;

        try
        {
            IsBusy = true;

            // Store selected business and shop in user context
            await StoreUserSelectionAsync(_authResult.User, SelectedBusiness, SelectedShop);

            // Navigate to main application
            await Shell.Current.GoToAsync("//main");
        }
        catch (Exception ex)
        {
            SetError($"Failed to complete login: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearForm()
    {
        Username = string.Empty;
        Password = string.Empty;
        ClearError();
        ShowBusinessSelection = false;
        ShowShopSelection = false;
        Businesses.Clear();
        Shops.Clear();
        SelectedBusiness = null;
        SelectedShop = null;
        CanLogin = false;
    }

    private async Task LoadUserBusinessesAndShops(User user)
    {
        try
        {
            IEnumerable<BusinessResponse> userBusinesses;

            if (user.Role == Shared.Core.Enums.UserRole.BusinessOwner)
            {
                // Business owners can see all their businesses
                userBusinesses = await _businessManagementService.GetBusinessesByOwnerAsync(user.Id);
            }
            else
            {
                // Other roles can only see the business they belong to
                var business = await _businessManagementService.GetBusinessByIdAsync(user.BusinessId);
                userBusinesses = business != null ? new[] { business } : Array.Empty<BusinessResponse>();
            }

            Businesses.Clear();
            foreach (var business in userBusinesses)
            {
                Businesses.Add(business);
            }

            ShowBusinessSelection = Businesses.Count > 1;
            if (!ShowBusinessSelection && Businesses.Count == 1)
            {
                SelectedBusiness = Businesses.First();
                await SelectBusiness();
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to load businesses: {ex.Message}");
        }
    }

    private async Task HandlePostLoginNavigation(User user)
    {
        // If user only has access to one business and one shop, skip selection
        if (Businesses.Count == 1 && Shops.Count <= 1)
        {
            SelectedBusiness = Businesses.First();
            SelectedShop = Shops.FirstOrDefault();
            CanLogin = true;
            await CompleteLogin();
        }
        else
        {
            // Show business/shop selection UI
            ShowBusinessSelection = Businesses.Count > 1;
            CanLogin = false;
        }
    }

    private bool CanUserAccessShop(User? user, Guid shopId)
    {
        if (user == null) return false;

        // Business owners can access all shops in their businesses
        if (user.Role == Shared.Core.Enums.UserRole.BusinessOwner)
            return true;

        // Other roles need specific shop assignment or no shop restriction
        return user.ShopId == null || user.ShopId == shopId;
    }

    private async Task<string?> GetCachedTokenAsync(string username)
    {
        try
        {
            // In a real implementation, this would retrieve from secure storage
            // For now, we'll use Preferences as a simple cache
            return Preferences.Get($"cached_token_{username}", null);
        }
        catch
        {
            return null;
        }
    }

    private async Task StoreUserSelectionAsync(User user, BusinessResponse? business, ShopResponse? shop)
    {
        try
        {
            // Store user's business and shop selection for the session
            if (business != null)
            {
                Preferences.Set("selected_business_id", business.Id.ToString());
                Preferences.Set("selected_business_name", business.Name);
            }

            if (shop != null)
            {
                Preferences.Set("selected_shop_id", shop.Id.ToString());
                Preferences.Set("selected_shop_name", shop.Name);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail login
            System.Diagnostics.Debug.WriteLine($"Failed to store user selection: {ex.Message}");
        }
    }

    public async Task CheckAutoLogin()
    {
        try
        {
            // Check if user is already authenticated
            if (_currentUserService.IsAuthenticated)
            {
                await Shell.Current.GoToAsync("//main");
                return;
            }

            // Check for cached credentials
            var lastUsername = Preferences.Get("last_username", string.Empty);
            if (!string.IsNullOrEmpty(lastUsername) && RememberMe)
            {
                Username = lastUsername;
                var cachedToken = await GetCachedTokenAsync(lastUsername);
                if (!string.IsNullOrEmpty(cachedToken))
                {
                    // Try auto-login with cached credentials
                    var result = await _authenticationService.AuthenticateOfflineAsync(lastUsername, cachedToken);
                    if (result.IsSuccess && result.User != null && result.Session != null)
                    {
                        _currentUserService.SetCurrentUser(result.User, result.Session);
                        await Shell.Current.GoToAsync("//main");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Auto-login failed: {ex.Message}");
        }
    }

    partial void OnUsernameChanged(string value)
    {
        ValidateForm();
    }

    partial void OnPasswordChanged(string value)
    {
        ValidateForm();
    }

    private void ValidateForm()
    {
        var hasCredentials = !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
        var hasSelections = !ShowBusinessSelection || (SelectedBusiness != null && (!ShowShopSelection || SelectedShop != null));
        CanLogin = hasCredentials && hasSelections;
    }
}