using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using System.Security.Cryptography;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// Enhanced authentication service implementation for multi-business POS system
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IShopRepository _shopRepository;
    private readonly ISessionService _sessionService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEncryptionService _encryptionService;
    private readonly IAuditService _auditService;
    private readonly Dictionary<Guid, CachedCredentials> _credentialsCache;

    public AuthenticationService(
        IUserRepository userRepository,
        IShopRepository shopRepository,
        ISessionService sessionService,
        IAuthorizationService authorizationService,
        IEncryptionService encryptionService,
        IAuditService auditService)
    {
        _userRepository = userRepository;
        _shopRepository = shopRepository;
        _sessionService = sessionService;
        _authorizationService = authorizationService;
        _encryptionService = encryptionService;
        _auditService = auditService;
        _credentialsCache = new Dictionary<Guid, CachedCredentials>();
    }

    public async Task<AuthenticationResult> AuthenticateAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return new AuthenticationResult
            {
                IsSuccess = false,
                ErrorMessage = "Username and password are required"
            };
        }

        try
        {
            var user = await _userRepository.GetByUsernameAsync(request.Username);
            if (user == null || !user.IsActive)
            {
                await _auditService.LogAsync(
                    null,
                    AuditAction.SecurityViolation,
                    $"Failed login attempt for username: {request.Username}",
                    ipAddress: request.IpAddress);
                
                return new AuthenticationResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Invalid username or password"
                };
            }

            if (!_encryptionService.VerifyPassword(request.Password, user.PasswordHash, user.Salt))
            {
                await _auditService.LogAsync(
                    user.Id,
                    AuditAction.SecurityViolation,
                    $"Invalid password for user: {request.Username}",
                    ipAddress: request.IpAddress);
                
                return new AuthenticationResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Invalid username or password"
                };
            }

            // Update last login time
            user.LastLoginAt = DateTime.UtcNow;
            user.LastActivityAt = DateTime.UtcNow;
            if (request.DeviceId.HasValue)
            {
                user.DeviceId = request.DeviceId.Value;
            }
            
            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();

            // Create session
            var session = await _sessionService.CreateSessionAsync(
                user.Id, 
                request.IpAddress, 
                request.UserAgent);

            // Get user permissions
            var permissions = await GetUserPermissionsAsync(user.Id);

            // Cache credentials for offline use (24 hours default)
            await CacheCredentialsAsync(user.Id, session.SessionToken, TimeSpan.FromHours(24));

            await _auditService.LogAsync(
                user.Id,
                AuditAction.Login,
                $"User {request.Username} logged in successfully",
                ipAddress: request.IpAddress);

            return new AuthenticationResult
            {
                IsSuccess = true,
                User = user,
                Session = session,
                Permissions = permissions,
                IsOfflineMode = false
            };
        }
        catch (Exception ex)
        {
            await _auditService.LogAsync(
                null,
                AuditAction.SecurityViolation,
                $"Authentication error for user {request.Username}: {ex.Message}",
                ipAddress: request.IpAddress);
            
            return new AuthenticationResult
            {
                IsSuccess = false,
                ErrorMessage = "Authentication failed due to system error"
            };
        }
    }

    public async Task<AuthenticationResult> AuthenticateOfflineAsync(string username, string cachedToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(cachedToken))
        {
            return new AuthenticationResult
            {
                IsSuccess = false,
                ErrorMessage = "Username and cached token are required"
            };
        }

        try
        {
            var user = await _userRepository.GetByUsernameAsync(username);
            if (user == null || !user.IsActive)
            {
                return new AuthenticationResult
                {
                    IsSuccess = false,
                    ErrorMessage = "User not found or inactive"
                };
            }

            // Validate cached token
            if (!await ValidateCachedTokenAsync(user.Id, cachedToken))
            {
                await _auditService.LogAsync(
                    user.Id,
                    AuditAction.SecurityViolation,
                    $"Offline authentication failed - expired or invalid cached token for user: {username}");
                
                return new AuthenticationResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Cached credentials expired or invalid"
                };
            }

            // Update last activity
            user.LastActivityAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();

            // Get user permissions
            var permissions = await GetUserPermissionsAsync(user.Id);

            await _auditService.LogAsync(
                user.Id,
                AuditAction.Login,
                $"User {username} authenticated offline successfully");

            return new AuthenticationResult
            {
                IsSuccess = true,
                User = user,
                Permissions = permissions,
                IsOfflineMode = true
            };
        }
        catch (Exception ex)
        {
            await _auditService.LogAsync(
                null,
                AuditAction.SecurityViolation,
                $"Offline authentication error for user {username}: {ex.Message}");
            
            return new AuthenticationResult
            {
                IsSuccess = false,
                ErrorMessage = "Offline authentication failed due to system error"
            };
        }
    }

    public async Task<UserPermissions> GetUserPermissionsAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new ArgumentException($"User with ID {userId} not found");
        }

        var permissions = new UserPermissions
        {
            UserId = userId,
            Role = user.Role,
            BusinessId = user.BusinessId,
            ShopId = user.ShopId
        };

        // Get role-based permissions
        var rolePermissions = _authorizationService.GetRolePermissions(user.Role);
        foreach (var permission in rolePermissions)
        {
            permissions.Permissions.Add(permission.ToString());
        }

        // Add custom permissions from user entity if available
        if (!string.IsNullOrEmpty(user.Permissions))
        {
            try
            {
                var customPermissions = JsonSerializer.Deserialize<Dictionary<string, object>>(user.Permissions);
                if (customPermissions != null)
                {
                    permissions.CustomPermissions = customPermissions;
                }
            }
            catch (JsonException)
            {
                // Log but don't fail - custom permissions are optional
                await _auditService.LogAsync(
                    userId,
                    AuditAction.SystemConfiguration,
                    "Failed to parse custom permissions JSON");
            }
        }

        return permissions;
    }

    public async Task<bool> ValidatePermissionAsync(Guid userId, string permission, Guid? shopId = null)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || !user.IsActive)
            {
                return false;
            }

            var userPermissions = await GetUserPermissionsAsync(userId);
            
            // Check if user has the permission
            if (!userPermissions.HasPermission(permission))
            {
                return false;
            }

            // Check shop-level access if shopId is provided
            if (shopId.HasValue)
            {
                // First check if user can access the shop based on their role and assignment
                if (!userPermissions.CanAccessShop(shopId.Value))
                {
                    await _auditService.LogAsync(
                        userId,
                        AuditAction.SecurityViolation,
                        $"User attempted to access shop {shopId} without permission");
                    return false;
                }

                // CRITICAL: Check cross-business access - user should only access shops in their business
                var shop = await _shopRepository.GetByIdAsync(shopId.Value);
                
                if (shop != null && shop.BusinessId != user.BusinessId)
                {
                    await _auditService.LogAsync(
                        userId,
                        AuditAction.SecurityViolation,
                        $"User from business {user.BusinessId} attempted to access shop {shopId} from different business {shop.BusinessId}");
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            await _auditService.LogAsync(
                userId,
                AuditAction.SecurityViolation,
                $"Permission validation error: {ex.Message}");
            return false;
        }
    }

    public async Task CacheCredentialsAsync(Guid userId, string token, TimeSpan expiration)
    {
        var cachedCredentials = new CachedCredentials
        {
            UserId = userId,
            Token = token,
            ExpiresAt = DateTime.UtcNow.Add(expiration),
            CachedAt = DateTime.UtcNow
        };

        _credentialsCache[userId] = cachedCredentials;

        await _auditService.LogAsync(
            userId,
            AuditAction.SystemConfiguration,
            $"Credentials cached for offline use, expires at: {cachedCredentials.ExpiresAt}");
    }

    public async Task<bool> ValidateCachedTokenAsync(Guid userId, string token)
    {
        if (!_credentialsCache.TryGetValue(userId, out var cachedCredentials))
        {
            return false;
        }

        if (cachedCredentials.Token != token)
        {
            return false;
        }

        if (DateTime.UtcNow > cachedCredentials.ExpiresAt)
        {
            // Remove expired credentials
            _credentialsCache.Remove(userId);
            
            await _auditService.LogAsync(
                userId,
                AuditAction.SecurityViolation,
                "Cached credentials expired and removed");
            
            return false;
        }

        return true;
    }

    public async Task ClearCachedCredentialsAsync(Guid userId)
    {
        if (_credentialsCache.Remove(userId))
        {
            await _auditService.LogAsync(
                userId,
                AuditAction.SystemConfiguration,
                "Cached credentials cleared");
        }
    }

    public async Task<bool> LogoutAsync(Guid userId)
    {
        try
        {
            // End all user sessions
            var sessionsEnded = await _sessionService.EndAllUserSessionsAsync(userId);
            
            // Clear cached credentials
            await ClearCachedCredentialsAsync(userId);

            await _auditService.LogAsync(
                userId,
                AuditAction.Logout,
                $"User logged out successfully, {sessionsEnded} sessions ended");

            return true;
        }
        catch (Exception ex)
        {
            await _auditService.LogAsync(
                userId,
                AuditAction.SecurityViolation,
                $"Logout error: {ex.Message}");
            return false;
        }
    }
}