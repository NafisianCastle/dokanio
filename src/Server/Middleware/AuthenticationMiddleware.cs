using Microsoft.AspNetCore.Authorization;
using Shared.Core.Services;
using System.Security.Claims;

namespace Server.Middleware;

/// <summary>
/// Custom authentication middleware for multi-tenant POS system
/// </summary>
public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthenticationMiddleware> _logger;

    public AuthenticationMiddleware(RequestDelegate next, ILogger<AuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuthenticationService authService)
    {
        // Skip authentication for certain endpoints
        if (ShouldSkipAuthentication(context))
        {
            await _next(context);
            return;
        }

        try
        {
            // Check if endpoint requires authorization
            var endpoint = context.GetEndpoint();
            var requiresAuth = endpoint?.Metadata?.GetMetadata<AuthorizeAttribute>() != null;
            var allowsAnonymous = endpoint?.Metadata?.GetMetadata<AllowAnonymousAttribute>() != null;

            if (!requiresAuth || allowsAnonymous)
            {
                await _next(context);
                return;
            }

            // Extract user information from JWT token (already validated by JWT middleware)
            var userId = GetUserIdFromContext(context);
            if (userId == null)
            {
                _logger.LogWarning("Authentication failed: No valid user ID found in token");
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Authentication required");
                return;
            }

            // Get user permissions for role-based access control
            var permissions = await authService.GetUserPermissionsAsync(userId.Value);
            if (permissions == null)
            {
                _logger.LogWarning("Authentication failed: No permissions found for user {UserId}", userId);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid user permissions");
                return;
            }

            // Add permissions to context for use in controllers
            context.Items["UserPermissions"] = permissions;
            context.Items["UserId"] = userId.Value;
            context.Items["BusinessId"] = permissions.BusinessId;
            context.Items["ShopId"] = permissions.ShopId;

            _logger.LogDebug("User {UserId} authenticated with role {Role} for business {BusinessId}", 
                userId, permissions.Role, permissions.BusinessId);

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in authentication middleware");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Internal server error");
        }
    }

    private bool ShouldSkipAuthentication(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();
        
        // Skip authentication for these paths
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

    private Guid? GetUserIdFromContext(HttpContext context)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier) ?? 
                         context.User.FindFirst("user_id");
        
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        return null;
    }
}