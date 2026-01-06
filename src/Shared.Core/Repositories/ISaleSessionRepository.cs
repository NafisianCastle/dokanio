using Shared.Core.Entities;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository interface for sale session operations
/// </summary>
public interface ISaleSessionRepository : IRepository<SaleSession>
{
    /// <summary>
    /// Gets all active sessions for a specific user and device
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="deviceId">Device ID</param>
    /// <returns>List of active sessions</returns>
    Task<IEnumerable<SaleSession>> GetActiveSessionsAsync(Guid userId, Guid deviceId);
    
    /// <summary>
    /// Gets all active sessions for a specific shop
    /// </summary>
    /// <param name="shopId">Shop ID</param>
    /// <returns>List of active sessions</returns>
    Task<IEnumerable<SaleSession>> GetActiveSessionsByShopAsync(Guid shopId);
    
    /// <summary>
    /// Gets a session by its tab name for a specific user and device
    /// </summary>
    /// <param name="tabName">Tab name</param>
    /// <param name="userId">User ID</param>
    /// <param name="deviceId">Device ID</param>
    /// <returns>Session if found</returns>
    Task<SaleSession?> GetSessionByTabNameAsync(string tabName, Guid userId, Guid deviceId);
    
    /// <summary>
    /// Deactivates all sessions for a specific user and device
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="deviceId">Device ID</param>
    /// <returns>Number of sessions deactivated</returns>
    Task<int> DeactivateAllSessionsAsync(Guid userId, Guid deviceId);
    
    /// <summary>
    /// Gets expired sessions that need cleanup
    /// </summary>
    /// <param name="expiryThreshold">Sessions older than this will be considered expired</param>
    /// <returns>List of expired sessions</returns>
    Task<IEnumerable<SaleSession>> GetExpiredSessionsAsync(DateTime expiryThreshold);
    
    /// <summary>
    /// Updates the last modified timestamp for a session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>True if updated successfully</returns>
    Task<bool> UpdateLastModifiedAsync(Guid sessionId);
    
    /// <summary>
    /// Gets sessions by state
    /// </summary>
    /// <param name="state">Session state</param>
    /// <param name="userId">Optional user ID filter</param>
    /// <param name="shopId">Optional shop ID filter</param>
    /// <returns>List of sessions matching the criteria</returns>
    Task<IEnumerable<SaleSession>> GetSessionsByStateAsync(SessionState state, Guid? userId = null, Guid? shopId = null);
}