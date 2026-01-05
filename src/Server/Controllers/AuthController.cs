using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Core.Services;
using System.Security.Claims;

namespace Server.Controllers;

/// <summary>
/// Controller for authentication and authorization operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthenticationService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user with username and password
    /// </summary>
    /// <param name="request">Login request</param>
    /// <returns>Authentication result</returns>
    [HttpPost("login")]
    public async Task<ActionResult<SyncApiResult<AuthenticationResponse>>> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new SyncApiResult<AuthenticationResponse>
                {
                    Success = false,
                    Message = "Username and password are required",
                    StatusCode = 400,
                    Errors = new List<string> { "Invalid credentials" }
                });
            }

            // Set IP address and user agent from request
            request.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            request.UserAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            // Sanitize IP address before logging to prevent log forging
            var sanitizedIpAddress = (request.IpAddress ?? string.Empty)
                .Replace(Environment.NewLine, string.Empty)
                .Replace("\n", string.Empty)
                .Replace("\r", string.Empty);

            var result = await _authService.AuthenticateAsync(request);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Authentication failed for user {Username} from IP {IpAddress}",
                    request.Username, sanitizedIpAddress);

                return Unauthorized(new SyncApiResult<AuthenticationResponse>
                {
                    Success = false,
                    Message = result.ErrorMessage ?? "Authentication failed",
                    StatusCode = 401,
                    Errors = new List<string> { result.ErrorMessage ?? "Invalid credentials" }
                });
            }

            _logger.LogInformation("User {UserId} authenticated successfully from IP {IpAddress}",
                result.User?.Id, sanitizedIpAddress);

            return Ok(new SyncApiResult<AuthenticationResponse>
            {
                Success = true,
                Message = "Authentication successful",
                StatusCode = 200,
                Data = new AuthenticationResponse
                {
                    UserId = result.User!.Id,
                    Username = result.User.Username,
                    Email = result.User.Email,
                    Role = result.User.Role.ToString(),
                    BusinessId = result.User.BusinessId,
                    ShopId = result.User.ShopId,
                    Permissions = result.Permissions,
                    SessionId = result.Session?.Id,
                    IsOfflineMode = result.IsOfflineMode
                }
            });
        }
        catch (Exception ex)
        {
            // Sanitize username before logging to prevent log forging
            var sanitizedUsername = (request.Username ?? string.Empty)
                .Replace(Environment.NewLine, string.Empty)
                .Replace("\n", string.Empty)
                .Replace("\r", string.Empty);

            _logger.LogError(ex, "Error during authentication for user {Username}", sanitizedUsername);
            return StatusCode(500, new SyncApiResult<AuthenticationResponse>
            {
                Success = false,
                Message = "Internal server error during authentication",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Authenticates a user using cached credentials for offline mode
    /// </summary>
    /// <param name="request">Offline authentication request</param>
    /// <returns>Authentication result</returns>
    [HttpPost("offline-login")]
    public async Task<ActionResult<SyncApiResult<AuthenticationResponse>>> OfflineLogin([FromBody] OfflineLoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.CachedToken))
            {
                return BadRequest(new SyncApiResult<AuthenticationResponse>
                {
                    Success = false,
                    Message = "Username and cached token are required",
                    StatusCode = 400,
                    Errors = new List<string> { "Invalid offline credentials" }
                });
            }

            var result = await _authService.AuthenticateOfflineAsync(request.Username, request.CachedToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Offline authentication failed for user {Username}", request.Username);

                return Unauthorized(new SyncApiResult<AuthenticationResponse>
                {
                    Success = false,
                    Message = result.ErrorMessage ?? "Offline authentication failed",
                    StatusCode = 401,
                    Errors = new List<string> { result.ErrorMessage ?? "Invalid or expired cached credentials" }
                });
            }

            _logger.LogInformation("User {UserId} authenticated offline successfully", result.User?.Id);

            return Ok(new SyncApiResult<AuthenticationResponse>
            {
                Success = true,
                Message = "Offline authentication successful",
                StatusCode = 200,
                Data = new AuthenticationResponse
                {
                    UserId = result.User!.Id,
                    Username = result.User.Username,
                    Email = result.User.Email,
                    Role = result.User.Role.ToString(),
                    BusinessId = result.User.BusinessId,
                    ShopId = result.User.ShopId,
                    Permissions = result.Permissions,
                    SessionId = result.Session?.Id,
                    IsOfflineMode = result.IsOfflineMode
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during offline authentication for user {Username}", request.Username);
            return StatusCode(500, new SyncApiResult<AuthenticationResponse>
            {
                Success = false,
                Message = "Internal server error during offline authentication",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Gets user permissions for role-based access control
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>User permissions</returns>
    [HttpGet("permissions/{userId}")]
    [Authorize]
    public async Task<ActionResult<SyncApiResult<UserPermissions>>> GetUserPermissions(Guid userId)
    {
        try
        {
            var permissions = await _authService.GetUserPermissionsAsync(userId);

            return Ok(new SyncApiResult<UserPermissions>
            {
                Success = true,
                Message = "User permissions retrieved successfully",
                StatusCode = 200,
                Data = permissions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions for user {UserId}", userId);
            return StatusCode(500, new SyncApiResult<UserPermissions>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Validates if user has specific permission
    /// </summary>
    /// <param name="request">Permission validation request</param>
    /// <returns>Validation result</returns>
    [HttpPost("validate-permission")]
    [Authorize]
    public async Task<ActionResult<SyncApiResult<bool>>> ValidatePermission([FromBody] PermissionValidationRequest request)
    {
        try
        {
            var hasPermission = await _authService.ValidatePermissionAsync(request.UserId, request.Permission, request.ShopId);

            return Ok(new SyncApiResult<bool>
            {
                Success = true,
                Message = "Permission validation completed",
                StatusCode = 200,
                Data = hasPermission
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating permission {Permission} for user {UserId}", 
                request.Permission, request.UserId);
            return StatusCode(500, new SyncApiResult<bool>
            {
                Success = false,
                Message = "Internal server error during permission validation",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Caches user credentials for offline authentication
    /// </summary>
    /// <param name="request">Cache credentials request</param>
    /// <returns>Cache result</returns>
    [HttpPost("cache-credentials")]
    [Authorize]
    public async Task<ActionResult<SyncApiResult<bool>>> CacheCredentials([FromBody] CacheCredentialsRequest request)
    {
        try
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized(new SyncApiResult<bool>
                {
                    Success = false,
                    Message = "Invalid user token",
                    StatusCode = 401
                });
            }

            await _authService.CacheCredentialsAsync(userId.Value, request.Token, request.Expiration);

            _logger.LogInformation("Credentials cached for user {UserId}", userId);

            return Ok(new SyncApiResult<bool>
            {
                Success = true,
                Message = "Credentials cached successfully",
                StatusCode = 200,
                Data = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching credentials for user {UserId}", GetUserIdFromToken());
            return StatusCode(500, new SyncApiResult<bool>
            {
                Success = false,
                Message = "Internal server error during credential caching",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Validates cached token expiration
    /// </summary>
    /// <param name="request">Token validation request</param>
    /// <returns>Validation result</returns>
    [HttpPost("validate-cached-token")]
    public async Task<ActionResult<SyncApiResult<bool>>> ValidateCachedToken([FromBody] CachedTokenValidationRequest request)
    {
        try
        {
            var isValid = await _authService.ValidateCachedTokenAsync(request.UserId, request.Token);

            return Ok(new SyncApiResult<bool>
            {
                Success = true,
                Message = "Token validation completed",
                StatusCode = 200,
                Data = isValid
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating cached token for user {UserId}", request.UserId);
            return StatusCode(500, new SyncApiResult<bool>
            {
                Success = false,
                Message = "Internal server error during token validation",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Logs out user and ends all sessions
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Logout result</returns>
    [HttpPost("logout/{userId}")]
    [Authorize]
    public async Task<ActionResult<SyncApiResult<bool>>> Logout(Guid userId)
    {
        try
        {
            var result = await _authService.LogoutAsync(userId);

            _logger.LogInformation("User {UserId} logged out", userId);

            return Ok(new SyncApiResult<bool>
            {
                Success = true,
                Message = "Logout successful",
                StatusCode = 200,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout for user {UserId}", userId);
            return StatusCode(500, new SyncApiResult<bool>
            {
                Success = false,
                Message = "Internal server error during logout",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Clears cached credentials for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Clear result</returns>
    [HttpDelete("cached-credentials/{userId}")]
    [Authorize]
    public async Task<ActionResult<SyncApiResult<bool>>> ClearCachedCredentials(Guid userId)
    {
        try
        {
            await _authService.ClearCachedCredentialsAsync(userId);

            _logger.LogInformation("Cached credentials cleared for user {UserId}", userId);

            return Ok(new SyncApiResult<bool>
            {
                Success = true,
                Message = "Cached credentials cleared successfully",
                StatusCode = 200,
                Data = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cached credentials for user {UserId}", userId);
            return StatusCode(500, new SyncApiResult<bool>
            {
                Success = false,
                Message = "Internal server error during credential clearing",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    private Guid? GetUserIdFromToken()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("user_id");
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }
}

/// <summary>
/// Authentication response model
/// </summary>
public class AuthenticationResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid BusinessId { get; set; }
    public Guid? ShopId { get; set; }
    public UserPermissions? Permissions { get; set; }
    public Guid? SessionId { get; set; }
    public bool IsOfflineMode { get; set; }
}

/// <summary>
/// Offline login request model
/// </summary>
public class OfflineLoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string CachedToken { get; set; } = string.Empty;
}

/// <summary>
/// Permission validation request model
/// </summary>
public class PermissionValidationRequest
{
    public Guid UserId { get; set; }
    public string Permission { get; set; } = string.Empty;
    public Guid? ShopId { get; set; }
}

/// <summary>
/// Cache credentials request model
/// </summary>
public class CacheCredentialsRequest
{
    public string Token { get; set; } = string.Empty;
    public TimeSpan Expiration { get; set; }
}

/// <summary>
/// Cached token validation request model
/// </summary>
public class CachedTokenValidationRequest
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
}