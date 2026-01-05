using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Enhanced authentication service for multi-business POS system
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticates a user with username and password
    /// </summary>
    /// <param name="request">Login request containing credentials</param>
    /// <returns>Authentication result with user and session information</returns>
    Task<AuthenticationResult> AuthenticateAsync(LoginRequest request);
    
    /// <summary>
    /// Authenticates a user using cached credentials for offline mode
    /// </summary>
    /// <param name="username">Username</param>
    /// <param name="cachedToken">Cached authentication token</param>
    /// <returns>Authentication result if token is valid and not expired</returns>
    Task<AuthenticationResult> AuthenticateOfflineAsync(string username, string cachedToken);
    
    /// <summary>
    /// Gets user permissions for role-based access control
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>User permissions object</returns>
    Task<UserPermissions> GetUserPermissionsAsync(Guid userId);
    
    /// <summary>
    /// Validates if user has specific permission for multi-tenant access
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="permission">Permission to validate</param>
    /// <param name="shopId">Optional shop ID for shop-specific permissions</param>
    /// <returns>True if user has permission</returns>
    Task<bool> ValidatePermissionAsync(Guid userId, string permission, Guid? shopId = null);
    
    /// <summary>
    /// Caches user credentials for offline authentication
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="token">Authentication token</param>
    /// <param name="expiration">Token expiration time</param>
    /// <returns>Task</returns>
    Task CacheCredentialsAsync(Guid userId, string token, TimeSpan expiration);
    
    /// <summary>
    /// Validates cached token expiration
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="token">Cached token</param>
    /// <returns>True if token is valid and not expired</returns>
    Task<bool> ValidateCachedTokenAsync(Guid userId, string token);
    
    /// <summary>
    /// Clears cached credentials for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Task</returns>
    Task ClearCachedCredentialsAsync(Guid userId);
    
    /// <summary>
    /// Logs out user and ends all sessions
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>True if logout successful</returns>
    Task<bool> LogoutAsync(Guid userId);
}

/// <summary>
/// Login request model
/// </summary>
public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public Guid? DeviceId { get; set; }
}

/// <summary>
/// Authentication result model
/// </summary>
public class AuthenticationResult
{
    public bool IsSuccess { get; set; }
    public User? User { get; set; }
    public UserSession? Session { get; set; }
    public UserPermissions? Permissions { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsOfflineMode { get; set; }
}

/// <summary>
/// User permissions model for role-based access control
/// </summary>
public class UserPermissions
{
    public Guid UserId { get; set; }
    public UserRole Role { get; set; }
    public Guid BusinessId { get; set; }
    public Guid? ShopId { get; set; }
    public HashSet<string> Permissions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object> CustomPermissions { get; set; } = new();
    
    /// <summary>
    /// Checks if user has specific permission
    /// </summary>
    /// <param name="permission">Permission to check</param>
    /// <returns>True if user has permission</returns>
    public bool HasPermission(string permission)
    {
        return Permissions.Contains(permission);
    }
    
    /// <summary>
    /// Checks if user can access specific shop
    /// </summary>
    /// <param name="shopId">Shop ID to check</param>
    /// <returns>True if user can access shop</returns>
    public bool CanAccessShop(Guid shopId)
    {
        // Business owners can access all shops in their business
        if (Role == UserRole.BusinessOwner)
            return true;
            
        // Other roles need specific shop assignment or no shop restriction
        return ShopId == null || ShopId == shopId;
    }
}

/// <summary>
/// Cached credentials model for offline authentication
/// </summary>
public class CachedCredentials
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}