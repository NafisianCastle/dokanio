using Shared.Core.Entities;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository interface for License entity operations
/// </summary>
public interface ILicenseRepository : IRepository<License>
{
    /// <summary>
    /// Gets a license by its license key
    /// </summary>
    /// <param name="licenseKey">The license key to search for</param>
    /// <returns>The license if found, null otherwise</returns>
    Task<License?> GetByLicenseKeyAsync(string licenseKey);
    
    /// <summary>
    /// Gets the current active license for a device
    /// </summary>
    /// <param name="deviceId">The device ID</param>
    /// <returns>The active license if found, null otherwise</returns>
    Task<License?> GetCurrentLicenseAsync(Guid deviceId);
    
    /// <summary>
    /// Gets all licenses for a specific customer email
    /// </summary>
    /// <param name="customerEmail">The customer email</param>
    /// <returns>List of licenses for the customer</returns>
    Task<IEnumerable<License>> GetLicensesByCustomerEmailAsync(string customerEmail);
    
    /// <summary>
    /// Gets all expired licenses
    /// </summary>
    /// <returns>List of expired licenses</returns>
    Task<IEnumerable<License>> GetExpiredLicensesAsync();
    
    /// <summary>
    /// Gets all licenses expiring within the specified number of days
    /// </summary>
    /// <param name="days">Number of days to check for expiration</param>
    /// <returns>List of licenses expiring soon</returns>
    Task<IEnumerable<License>> GetLicensesExpiringInDaysAsync(int days);
}