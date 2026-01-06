using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository implementation for customer preference operations
/// </summary>
public class CustomerPreferenceRepository : Repository<CustomerPreference>, ICustomerPreferenceRepository
{
    public CustomerPreferenceRepository(PosDbContext context, ILogger<Repository<CustomerPreference>> logger) 
        : base(context, logger)
    {
    }

    /// <summary>
    /// Get preferences by customer ID
    /// </summary>
    public async Task<List<CustomerPreference>> GetByCustomerIdAsync(Guid customerId)
    {
        try
        {
            return await _context.CustomerPreferences
                .Where(cp => cp.CustomerId == customerId && cp.IsActive && !cp.IsDeleted)
                .OrderBy(cp => cp.Category)
                .ThenBy(cp => cp.Key)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting preferences for customer {CustomerId}", customerId);
            throw;
        }
    }

    /// <summary>
    /// Get preference by customer ID and key
    /// </summary>
    public async Task<CustomerPreference?> GetByCustomerIdAndKeyAsync(Guid customerId, string key)
    {
        try
        {
            return await _context.CustomerPreferences
                .FirstOrDefaultAsync(cp => cp.CustomerId == customerId && 
                                         cp.Key == key && 
                                         cp.IsActive && 
                                         !cp.IsDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting preference {Key} for customer {CustomerId}", key, customerId);
            throw;
        }
    }

    /// <summary>
    /// Get preferences by category
    /// </summary>
    public async Task<List<CustomerPreference>> GetByCategoryAsync(string category)
    {
        try
        {
            return await _context.CustomerPreferences
                .Include(cp => cp.Customer)
                .Where(cp => cp.Category == category && cp.IsActive && !cp.IsDeleted)
                .OrderBy(cp => cp.CustomerId)
                .ThenBy(cp => cp.Key)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting preferences by category {Category}", category);
            throw;
        }
    }

    /// <summary>
    /// Get preferences by customer ID and category
    /// </summary>
    public async Task<List<CustomerPreference>> GetByCustomerIdAndCategoryAsync(Guid customerId, string category)
    {
        try
        {
            return await _context.CustomerPreferences
                .Where(cp => cp.CustomerId == customerId && 
                           cp.Category == category && 
                           cp.IsActive && 
                           !cp.IsDeleted)
                .OrderBy(cp => cp.Key)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting preferences for customer {CustomerId} in category {Category}", customerId, category);
            throw;
        }
    }

    /// <summary>
    /// Set or update preference value
    /// </summary>
    public async Task<bool> SetPreferenceAsync(Guid customerId, string key, string value, string category = "")
    {
        try
        {
            var existingPreference = await GetByCustomerIdAndKeyAsync(customerId, key);
            
            if (existingPreference != null)
            {
                // Update existing preference
                existingPreference.Value = value;
                existingPreference.Category = category;
                existingPreference.UpdatedAt = DateTime.UtcNow;
                await UpdateAsync(existingPreference);
            }
            else
            {
                // Create new preference
                var newPreference = new CustomerPreference
                {
                    CustomerId = customerId,
                    Key = key,
                    Value = value,
                    Category = category,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    DeviceId = Guid.NewGuid(), // Should be set from context
                    SyncStatus = SyncStatus.NotSynced
                };
                
                await AddAsync(newPreference);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting preference {Key} for customer {CustomerId}", key, customerId);
            throw;
        }
    }

    /// <summary>
    /// Remove preference by key
    /// </summary>
    public async Task<bool> RemovePreferenceAsync(Guid customerId, string key)
    {
        try
        {
            var preference = await GetByCustomerIdAndKeyAsync(customerId, key);
            if (preference == null)
            {
                _logger.LogWarning("Preference {Key} not found for customer {CustomerId}", key, customerId);
                return false;
            }

            await DeleteAsync(preference.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing preference {Key} for customer {CustomerId}", key, customerId);
            throw;
        }
    }

    /// <summary>
    /// Get customer preferences as dictionary
    /// </summary>
    public async Task<Dictionary<string, string>> GetPreferencesDictionaryAsync(Guid customerId, string? category = null)
    {
        try
        {
            var query = _context.CustomerPreferences
                .Where(cp => cp.CustomerId == customerId && cp.IsActive && !cp.IsDeleted);

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(cp => cp.Category == category);
            }

            return await query.ToDictionaryAsync(cp => cp.Key, cp => cp.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting preferences dictionary for customer {CustomerId}", customerId);
            throw;
        }
    }
}