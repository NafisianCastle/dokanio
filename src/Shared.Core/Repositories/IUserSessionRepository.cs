using Shared.Core.Entities;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository interface for UserSession entity
/// </summary>
public interface IUserSessionRepository : IRepository<UserSession>
{
    /// <summary>
    /// Gets session by token
    /// </summary>
    /// <param name="sessionToken">Session token</param>
    /// <returns>Session if found</returns>
    Task<UserSession?> GetByTokenAsync(string sessionToken);
    
    /// <summary>
    /// Gets active sessions for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of active sessions</returns>
    Task<IEnumerable<UserSession>> GetActiveSessionsByUserIdAsync(Guid userId);
    
    /// <summary>
    /// Gets expired sessions
    /// </summary>
    /// <param name="inactivityTimeoutMinutes">Inactivity timeout in minutes</param>
    /// <returns>List of expired sessions</returns>
    Task<IEnumerable<UserSession>> GetExpiredSessionsAsync(int inactivityTimeoutMinutes);
    
    /// <summary>
    /// Ends all sessions for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Number of sessions ended</returns>
    Task<int> EndAllUserSessionsAsync(Guid userId);
    
    /// <summary>
    /// Ends expired sessions
    /// </summary>
    /// <param name="inactivityTimeoutMinutes">Inactivity timeout in minutes</param>
    /// <returns>Number of sessions ended</returns>
    Task<int> EndExpiredSessionsAsync(int inactivityTimeoutMinutes);
}