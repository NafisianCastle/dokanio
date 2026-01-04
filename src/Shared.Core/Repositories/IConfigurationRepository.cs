using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository interface for Configuration entities
/// </summary>
public interface IConfigurationRepository : IRepository<Configuration>
{
    /// <summary>
    /// Gets a configuration by its key
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <returns>Configuration if found, null otherwise</returns>
    Task<Configuration?> GetByKeyAsync(string key);
    
    /// <summary>
    /// Gets all system-level configurations
    /// </summary>
    /// <returns>List of system-level configurations</returns>
    Task<IEnumerable<Configuration>> GetSystemConfigurationsAsync();
    
    /// <summary>
    /// Gets all configurations that need to be synced
    /// </summary>
    /// <returns>List of unsynced configurations</returns>
    Task<IEnumerable<Configuration>> GetUnsyncedAsync();
    
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
    Task<Configuration> SetConfigurationAsync(string key, string value, ConfigurationType type, 
        string? description = null, bool isSystemLevel = false, Guid? deviceId = null);
}