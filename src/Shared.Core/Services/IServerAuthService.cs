using Shared.Core.DTOs;

namespace Shared.Core.Services;

/// <summary>
/// Calls the remote server's /api/auth/login endpoint.
/// Used by client apps (Desktop, Mobile) to authenticate against the central server
/// when a network connection is available.
/// </summary>
public interface IServerAuthService
{
    /// <summary>
    /// Attempts to authenticate against the server.
    /// Returns null if the server is unreachable (caller should fall back to local auth).
    /// Returns a failed result if the server responded but credentials were wrong.
    /// </summary>
    Task<ServerAuthResult?> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from a server authentication attempt.
/// </summary>
public class ServerAuthResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public Guid? BusinessId { get; set; }
    public Guid? ShopId { get; set; }
    public Guid? SessionId { get; set; }
}
