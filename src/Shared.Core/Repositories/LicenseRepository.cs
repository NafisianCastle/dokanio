using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository implementation for License entity operations
/// </summary>
public class LicenseRepository : Repository<License>, ILicenseRepository
{
    public LicenseRepository(PosDbContext context, ILogger<Repository<License>> logger) : base(context, logger)
    {
    }

    /// <summary>
    /// Gets a license by its license key
    /// </summary>
    /// <param name="licenseKey">The license key to search for</param>
    /// <returns>The license if found, null otherwise</returns>
    public async Task<License?> GetByLicenseKeyAsync(string licenseKey)
    {
        return await _context.Set<License>()
            .FirstOrDefaultAsync(l => l.LicenseKey == licenseKey);
    }

    /// <summary>
    /// Gets the current active license for a device
    /// </summary>
    /// <param name="deviceId">The device ID</param>
    /// <returns>The active license if found, null otherwise</returns>
    public async Task<License?> GetCurrentLicenseAsync(Guid deviceId)
    {
        return await _context.Set<License>()
            .Where(l => l.DeviceId == deviceId)
            .OrderByDescending(l => l.ActivationDate)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Gets all licenses for a specific customer email
    /// </summary>
    /// <param name="customerEmail">The customer email</param>
    /// <returns>List of licenses for the customer</returns>
    public async Task<IEnumerable<License>> GetLicensesByCustomerEmailAsync(string customerEmail)
    {
        return await _context.Set<License>()
            .Where(l => l.CustomerEmail == customerEmail)
            .OrderByDescending(l => l.IssueDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all expired licenses
    /// </summary>
    /// <returns>List of expired licenses</returns>
    public async Task<IEnumerable<License>> GetExpiredLicensesAsync()
    {
        var now = DateTime.UtcNow;
        return await _context.Set<License>()
            .Where(l => l.ExpiryDate < now || l.Status == LicenseStatus.Expired)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all licenses expiring within the specified number of days
    /// </summary>
    /// <param name="days">Number of days to check for expiration</param>
    /// <returns>List of licenses expiring soon</returns>
    public async Task<IEnumerable<License>> GetLicensesExpiringInDaysAsync(int days)
    {
        var now = DateTime.UtcNow;
        var expiryThreshold = now.AddDays(days);
        
        return await _context.Set<License>()
            .Where(l => l.Status == LicenseStatus.Active && 
                       l.ExpiryDate >= now && 
                       l.ExpiryDate <= expiryThreshold)
            .ToListAsync();
    }
}