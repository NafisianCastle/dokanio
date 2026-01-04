using Shared.Core.Entities;

namespace Shared.Core.Services;

/// <summary>
/// Service for managing user sessions and automatic logout
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Creates a new user session
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="ipAddress">IP address</param>
    /// <param name="userAgent">User agent</param>
    /// <returns>Created session</returns>
    Task<UserSession> CreateSessionAsync(Guid userId, string? ipAddress = null, string? userAgent = null);
    
    /// <summary>
    /// Updates session activity timestamp
    /// </summary>
    /// <param name="sessionToken">Session token</param>
    /// <returns>True if session updated</returns>
    Task<bool> UpdateSessionActivityAsync(string sessionToken);
    
    /// <summary>
    /// Ends a user session
    /// </summary>
    /// <param name="sessionToken">Session token</param>
    /// <returns>True if session ended</returns>
    Task<bool> EndSessionAsync(string sessionToken);
    
    /// <summary>
    /// Gets active session by token
    /// </summary>
    /// <param name="sessionToken">Session token</param>
    /// <returns>Session if active</returns>
    Task<UserSession?> GetActiveSessionAsync(string sessionToken);
    
    /// <summary>
    /// Checks if session is expired based on inactivity timeout
    /// </summary>
    /// <param name="sessionToken">Session token</param>
    /// <param name="inactivityTimeoutMinutes">Inactivity timeout in minutes</param>
    /// <returns>True if session is expired</returns>
    Task<bool> IsSessionExpiredAsync(string sessionToken, int inactivityTimeoutMinutes = 30);
    
    /// <summary>
    /// Ends all expired sessions
    /// </summary>
    /// <param name="inactivityTimeoutMinutes">Inactivity timeout in minutes</param>
    /// <returns>Number of sessions ended</returns>
    Task<int> EndExpiredSessionsAsync(int inactivityTimeoutMinutes = 30);
    
    /// <summary>
    /// Gets all active sessions for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of active sessions</returns>
    Task<IEnumerable<UserSession>> GetUserActiveSessionsAsync(Guid userId);
    
    /// <summary>
    /// Ends all sessions for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Number of sessions ended</returns>
    Task<int> EndAllUserSessionsAsync(Guid userId);
}