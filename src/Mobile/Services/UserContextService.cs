using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;

namespace Mobile.Services;

/// <summary>
/// Service for managing user context including selected business and shop
/// </summary>
public interface IUserContextService
{
    /// <summary>
    /// Gets the currently selected business
    /// </summary>
    BusinessResponse? CurrentBusiness { get; }
    
    /// <summary>
    /// Gets the currently selected shop
    /// </summary>
    ShopResponse? CurrentShop { get; }
    
    /// <summary>
    /// Sets the current business and shop context
    /// </summary>
    /// <param name="business">Selected business</param>
    /// <param name="shop">Selected shop</param>
    void SetContext(BusinessResponse business, ShopResponse? shop = null);
    
    /// <summary>
    /// Clears the current context
    /// </summary>
    void ClearContext();
    
    /// <summary>
    /// Checks if user has permission for current context
    /// </summary>
    /// <param name="permission">Permission to check</param>
    /// <returns>True if user has permission</returns>
    Task<bool> HasPermissionAsync(string permission);
    
    /// <summary>
    /// Gets user permissions for current context
    /// </summary>
    /// <returns>User permissions</returns>
    Task<UserPermissions?> GetUserPermissionsAsync();
    
    /// <summary>
    /// Event fired when context changes
    /// </summary>
    event EventHandler<ContextChangedEventArgs>? ContextChanged;
}

public class UserContextService : IUserContextService
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthenticationService _authenticationService;

    public UserContextService(
        ICurrentUserService currentUserService,
        IAuthenticationService authenticationService)
    {
        _currentUserService = currentUserService;
        _authenticationService = authenticationService;
        
        LoadContextFromPreferences();
    }

    public BusinessResponse? CurrentBusiness { get; private set; }
    public ShopResponse? CurrentShop { get; private set; }

    public event EventHandler<ContextChangedEventArgs>? ContextChanged;

    public void SetContext(BusinessResponse business, ShopResponse? shop = null)
    {
        var oldBusiness = CurrentBusiness;
        var oldShop = CurrentShop;
        
        CurrentBusiness = business;
        CurrentShop = shop;
        
        // Save to preferences
        SaveContextToPreferences();
        
        // Fire event
        ContextChanged?.Invoke(this, new ContextChangedEventArgs
        {
            OldBusiness = oldBusiness,
            NewBusiness = CurrentBusiness,
            OldShop = oldShop,
            NewShop = CurrentShop
        });
    }

    public void ClearContext()
    {
        var oldBusiness = CurrentBusiness;
        var oldShop = CurrentShop;
        
        CurrentBusiness = null;
        CurrentShop = null;
        
        // Clear preferences
        ClearContextFromPreferences();
        
        // Fire event
        ContextChanged?.Invoke(this, new ContextChangedEventArgs
        {
            OldBusiness = oldBusiness,
            NewBusiness = null,
            OldShop = oldShop,
            NewShop = null
        });
    }

    public async Task<bool> HasPermissionAsync(string permission)
    {
        var user = _currentUserService.CurrentUser;
        if (user == null) return false;

        try
        {
            return await _authenticationService.ValidatePermissionAsync(
                user.Id, 
                permission, 
                CurrentShop?.Id);
        }
        catch
        {
            return false;
        }
    }

    public async Task<UserPermissions?> GetUserPermissionsAsync()
    {
        var user = _currentUserService.CurrentUser;
        if (user == null) return null;

        try
        {
            return await _authenticationService.GetUserPermissionsAsync(user.Id);
        }
        catch
        {
            return null;
        }
    }

    private void LoadContextFromPreferences()
    {
        try
        {
            var businessIdStr = Preferences.Get("selected_business_id", string.Empty);
            var businessName = Preferences.Get("selected_business_name", string.Empty);
            var shopIdStr = Preferences.Get("selected_shop_id", string.Empty);
            var shopName = Preferences.Get("selected_shop_name", string.Empty);

            if (!string.IsNullOrEmpty(businessIdStr) && Guid.TryParse(businessIdStr, out var businessId))
            {
                CurrentBusiness = new BusinessResponse
                {
                    Id = businessId,
                    Name = businessName
                };

                if (!string.IsNullOrEmpty(shopIdStr) && Guid.TryParse(shopIdStr, out var shopId))
                {
                    CurrentShop = new ShopResponse
                    {
                        Id = shopId,
                        Name = shopName,
                        BusinessId = businessId
                    };
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load context from preferences: {ex.Message}");
        }
    }

    private void SaveContextToPreferences()
    {
        try
        {
            if (CurrentBusiness != null)
            {
                Preferences.Set("selected_business_id", CurrentBusiness.Id.ToString());
                Preferences.Set("selected_business_name", CurrentBusiness.Name);
            }

            if (CurrentShop != null)
            {
                Preferences.Set("selected_shop_id", CurrentShop.Id.ToString());
                Preferences.Set("selected_shop_name", CurrentShop.Name);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save context to preferences: {ex.Message}");
        }
    }

    private void ClearContextFromPreferences()
    {
        try
        {
            Preferences.Remove("selected_business_id");
            Preferences.Remove("selected_business_name");
            Preferences.Remove("selected_shop_id");
            Preferences.Remove("selected_shop_name");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to clear context from preferences: {ex.Message}");
        }
    }
}

/// <summary>
/// Event arguments for context changes
/// </summary>
public class ContextChangedEventArgs : EventArgs
{
    public BusinessResponse? OldBusiness { get; set; }
    public BusinessResponse? NewBusiness { get; set; }
    public ShopResponse? OldShop { get; set; }
    public ShopResponse? NewShop { get; set; }
}

/// <summary>
/// Common permissions for role-based access control
/// </summary>
public static class Permissions
{
    public const string ViewSales = "view_sales";
    public const string CreateSales = "create_sales";
    public const string ModifySales = "modify_sales";
    public const string DeleteSales = "delete_sales";
    
    public const string ViewProducts = "view_products";
    public const string CreateProducts = "create_products";
    public const string ModifyProducts = "modify_products";
    public const string DeleteProducts = "delete_products";
    
    public const string ViewInventory = "view_inventory";
    public const string ModifyInventory = "modify_inventory";
    
    public const string ViewReports = "view_reports";
    public const string ViewAdvancedReports = "view_advanced_reports";
    
    public const string ManageUsers = "manage_users";
    public const string ManageShops = "manage_shops";
    public const string ManageBusinesses = "manage_businesses";
    
    public const string ViewDashboard = "view_dashboard";
    public const string ViewAnalytics = "view_analytics";
    
    public const string ProcessRefunds = "process_refunds";
    public const string ApplyDiscounts = "apply_discounts";
    public const string OverridePrices = "override_prices";
}