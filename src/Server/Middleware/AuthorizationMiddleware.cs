using Microsoft.AspNetCore.Authorization;
using Shared.Core.Services;
using System.Security.Claims;

namespace Server.Middleware;

/// <summary>
/// Custom authorization middleware for role-based access control
/// </summary>
public class AuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthorizationMiddleware> _logger;

    public AuthorizationMiddleware(RequestDelegate next, ILogger<AuthorizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuthenticationService authService)
    {
        // Skip authorization for endpoints that don't require it
        if (ShouldSkipAuthorization(context))
        {
            await _next(context);
            return;
        }

        try
        {
            var endpoint = context.GetEndpoint();
            var requiresAuth = endpoint?.Metadata?.GetMetadata<AuthorizeAttribute>() != null;
            var allowsAnonymous = endpoint?.Metadata?.GetMetadata<AllowAnonymousAttribute>() != null;

            if (!requiresAuth || allowsAnonymous)
            {
                await _next(context);
                return;
            }

            // Get user information from context (set by authentication middleware)
            var userId = context.Items["UserId"] as Guid?;
            var permissions = context.Items["UserPermissions"] as UserPermissions;

            if (userId == null || permissions == null)
            {
                _logger.LogWarning("Authorization failed: Missing user context");
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Authentication required");
                return;
            }

            // Check specific permissions based on the endpoint
            var hasPermission = await ValidateEndpointPermission(context, authService, userId.Value, permissions);
            
            if (!hasPermission)
            {
                var safePath = context.Request.Path.ToString().Replace("\r", string.Empty).Replace("\n", string.Empty);
                _logger.LogWarning("Authorization failed: User {UserId} lacks permission for {Path}", 
                    userId, safePath);
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Access denied");
                return;
            }

            var authorizedSafePath = context.Request.Path.ToString().Replace("\r", string.Empty).Replace("\n", string.Empty);
            _logger.LogDebug("User {UserId} authorized for {Path}", userId, authorizedSafePath);

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in authorization middleware");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Internal server error");
        }
    }

    private bool ShouldSkipAuthorization(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();
        
        // Skip authorization for these paths
        var skipPaths = new[]
        {
            "/api/device/register",
            "/api/device/authenticate", 
            "/api/auth/login",
            "/api/auth/offline-login",
            "/api/sync", // Health check endpoint
            "/swagger",
            "/health",
            "/favicon.ico"
        };

        return skipPaths.Any(skipPath => path?.StartsWith(skipPath) == true);
    }

    private async Task<bool> ValidateEndpointPermission(
        HttpContext context, 
        IAuthenticationService authService, 
        Guid userId, 
        UserPermissions permissions)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();
        var method = context.Request.Method.ToUpperInvariant();

        // Extract business ID and shop ID from route if present
        var businessId = ExtractBusinessIdFromRoute(context);
        var shopId = ExtractShopIdFromRoute(context);

        // Validate business access
        if (businessId.HasValue && businessId != permissions.BusinessId)
        {
            _logger.LogWarning("User {UserId} attempted to access business {BusinessId} but belongs to {UserBusinessId}", 
                userId, businessId, permissions.BusinessId);
            return false;
        }

        // Validate shop access for shop-specific endpoints
        if (shopId.HasValue && !permissions.CanAccessShop(shopId.Value))
        {
            _logger.LogWarning("User {UserId} attempted to access shop {ShopId} without permission", 
                userId, shopId);
            return false;
        }

        // Check specific endpoint permissions
        return await CheckEndpointSpecificPermissions(path, method, authService, userId, permissions, shopId);
    }

    private async Task<bool> CheckEndpointSpecificPermissions(
        string? path, 
        string method, 
        IAuthenticationService authService, 
        Guid userId, 
        UserPermissions permissions, 
        Guid? shopId)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // Business management endpoints
        if (path.StartsWith("/api/business"))
        {
            return await ValidateBusinessPermissions(path, method, authService, userId, permissions);
        }

        // Analytics endpoints
        if (path.StartsWith("/api/analytics"))
        {
            return await ValidateAnalyticsPermissions(path, method, authService, userId, permissions);
        }

        // Sync endpoints
        if (path.StartsWith("/api/sync") || path.StartsWith("/api/bulksync"))
        {
            return await ValidateSyncPermissions(path, method, authService, userId, permissions, shopId);
        }

        // Default: allow if user is authenticated
        return true;
    }

    private async Task<bool> ValidateBusinessPermissions(
        string path, 
        string method, 
        IAuthenticationService authService, 
        Guid userId, 
        UserPermissions permissions)
    {
        // Business owners can do everything
        if (permissions.Role == Shared.Core.Enums.UserRole.BusinessOwner)
            return true;

        // Shop managers can read business info but not modify
        if (permissions.Role == Shared.Core.Enums.UserRole.ShopManager)
        {
            return method == "GET" || await authService.ValidatePermissionAsync(userId, "business.read");
        }

        // Other roles need specific permissions
        var requiredPermission = method switch
        {
            "GET" => "business.read",
            "POST" => "business.create",
            "PUT" => "business.update",
            "DELETE" => "business.delete",
            _ => "business.read"
        };

        return await authService.ValidatePermissionAsync(userId, requiredPermission);
    }

    private async Task<bool> ValidateAnalyticsPermissions(
        string path, 
        string method, 
        IAuthenticationService authService, 
        Guid userId, 
        UserPermissions permissions)
    {
        // Business owners and shop managers can access analytics
        if (permissions.Role == Shared.Core.Enums.UserRole.BusinessOwner || 
            permissions.Role == Shared.Core.Enums.UserRole.ShopManager)
            return true;

        // Other roles need specific permissions
        return await authService.ValidatePermissionAsync(userId, "analytics.read");
    }

    private async Task<bool> ValidateSyncPermissions(
        string path, 
        string method, 
        IAuthenticationService authService, 
        Guid userId, 
        UserPermissions permissions, 
        Guid? shopId)
    {
        // All authenticated users can sync their own data
        if (method == "GET" || method == "POST")
        {
            // Validate shop access if shop-specific sync
            if (shopId.HasValue)
            {
                return permissions.CanAccessShop(shopId.Value);
            }
            return true;
        }

        // Other operations need specific permissions
        return await authService.ValidatePermissionAsync(userId, "sync.manage", shopId);
    }

    private Guid? ExtractBusinessIdFromRoute(HttpContext context)
    {
        // Try to extract business ID from route values
        if (context.Request.RouteValues.TryGetValue("businessId", out var businessIdValue) &&
            Guid.TryParse(businessIdValue?.ToString(), out var businessId))
        {
            return businessId;
        }

        // Try to extract from query parameters
        if (context.Request.Query.TryGetValue("businessId", out var queryBusinessId) &&
            Guid.TryParse(queryBusinessId.FirstOrDefault(), out var queryBusinessIdGuid))
        {
            return queryBusinessIdGuid;
        }

        return null;
    }

    private Guid? ExtractShopIdFromRoute(HttpContext context)
    {
        // Try to extract shop ID from route values
        if (context.Request.RouteValues.TryGetValue("shopId", out var shopIdValue) &&
            Guid.TryParse(shopIdValue?.ToString(), out var shopId))
        {
            return shopId;
        }

        // Try to extract from query parameters
        if (context.Request.Query.TryGetValue("shopId", out var queryShopId) &&
            Guid.TryParse(queryShopId.FirstOrDefault(), out var queryShopIdGuid))
        {
            return queryShopIdGuid;
        }

        return null;
    }
}