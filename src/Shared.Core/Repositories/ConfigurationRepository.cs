using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository implementation for Configuration entities
/// </summary>
public class ConfigurationRepository : Repository<Configuration>, IConfigurationRepository
{
    public ConfigurationRepository(PosDbContext context, ILogger<ConfigurationRepository> logger) 
        : base(context, logger)
    {
    }

    /// <summary>
    /// Gets a configuration by its key
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <returns>Configuration if found, null otherwise</returns>
    public async Task<Configuration?> GetByKeyAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Configuration key cannot be null or empty", nameof(key));

        try
        {
            return await _context.Set<Configuration>()
                .FirstOrDefaultAsync(c => c.Key == key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting configuration by key: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// Gets all system-level configurations
    /// </summary>
    /// <returns>List of system-level configurations</returns>
    public async Task<IEnumerable<Configuration>> GetSystemConfigurationsAsync()
    {
        try
        {
            return await _context.Set<Configuration>()
                .Where(c => c.IsSystemLevel)
                .OrderBy(c => c.Key)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system configurations");
            throw;
        }
    }

    /// <summary>
    /// Gets all configurations that need to be synced
    /// </summary>
    /// <returns>List of unsynced configurations</returns>
    public async Task<IEnumerable<Configuration>> GetUnsyncedAsync()
    {
        try
        {
            return await _context.Set<Configuration>()
                .Where(c => c.SyncStatus == SyncStatus.NotSynced || c.SyncStatus == SyncStatus.SyncFailed)
                .OrderBy(c => c.UpdatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unsynced configurations");
            throw;
        }
    }

    /// <summary>
    /// Sets or updates a configuration value
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="value">Configuration value</param>
    /// <param name="type">Configuration type</param>
    /// <param name="description">Optional description</param>
    /// <param name="isSystemLevel">Whether this is a system-level configuration</param>
    /// <param name="deviceId">Device ID</param>
    /// <returns>The created or updated configuration</returns>
    public async Task<Configuration> SetConfigurationAsync(string key, string value, ConfigurationType type, 
        string? description = null, bool isSystemLevel = false, Guid? deviceId = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Configuration key cannot be null or empty", nameof(key));

        if (value == null)
            throw new ArgumentNullException(nameof(value));

        try
        {
            var existing = await GetByKeyAsync(key);
            var now = DateTime.UtcNow;
            var currentDeviceId = deviceId ?? Guid.Empty;

            if (existing != null)
            {
                // Update existing configuration
                existing.Value = value;
                existing.Type = type;
                existing.Description = description;
                existing.IsSystemLevel = isSystemLevel;
                existing.UpdatedAt = now;
                existing.SyncStatus = SyncStatus.NotSynced;
                
                // Only update DeviceId if provided
                if (deviceId.HasValue)
                {
                    existing.DeviceId = currentDeviceId;
                }

                await UpdateAsync(existing);
                return existing;
            }
            else
            {
                // Create new configuration
                var newConfig = new Configuration
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    Value = value,
                    Type = type,
                    Description = description,
                    IsSystemLevel = isSystemLevel,
                    UpdatedAt = now,
                    DeviceId = currentDeviceId,
                    SyncStatus = SyncStatus.NotSynced
                };

                await AddAsync(newConfig);
                return newConfig;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting configuration {Key} = {Value}", key, value);
            throw;
        }
    }
}