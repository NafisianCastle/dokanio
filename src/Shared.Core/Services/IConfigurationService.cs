using Shared.Core.DTOs;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Service interface for managing system configuration
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets a configuration value with type conversion
    /// </summary>
    /// <typeparam name="T">Type to convert the value to</typeparam>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if configuration not found</param>
    /// <returns>Configuration value or default</returns>
    Task<T> GetConfigurationAsync<T>(string key, T defaultValue = default!);
    
    /// <summary>
    /// Sets a configuration value with automatic type detection
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>
    /// <param name="key">Configuration key</param>
    /// <param name="value">Configuration value</param>
    /// <param name="description">Optional description</param>
    /// <param name="isSystemLevel">Whether this is a system-level configuration</param>
    /// <returns>Task</returns>
    Task SetConfigurationAsync<T>(string key, T value, string? description = null, bool isSystemLevel = false);
    
    /// <summary>
    /// Gets currency settings
    /// </summary>
    /// <returns>Currency settings</returns>
    Task<CurrencySettings> GetCurrencySettingsAsync();
    
    /// <summary>
    /// Gets tax settings
    /// </summary>
    /// <returns>Tax settings</returns>
    Task<TaxSettings> GetTaxSettingsAsync();
    
    /// <summary>
    /// Gets business settings
    /// </summary>
    /// <returns>Business settings</returns>
    Task<BusinessSettings> GetBusinessSettingsAsync();
    
    /// <summary>
    /// Gets localization settings
    /// </summary>
    /// <returns>Localization settings</returns>
    Task<LocalizationSettings> GetLocalizationSettingsAsync();
    
    /// <summary>
    /// Validates a configuration value against its type
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="value">Value to validate</param>
    /// <param name="type">Expected configuration type</param>
    /// <returns>Validation result</returns>
    Task<ConfigurationValidationResult> ValidateConfigurationAsync(string key, object value, ConfigurationType type);
    
    /// <summary>
    /// Gets all system configurations
    /// </summary>
    /// <returns>List of system configurations</returns>
    Task<IEnumerable<ConfigurationDto>> GetSystemConfigurationsAsync();
    
    /// <summary>
    /// Resets a configuration to its default value
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <returns>Task</returns>
    Task ResetConfigurationAsync(string key);
    
    /// <summary>
    /// Initializes default system configurations
    /// </summary>
    /// <returns>Task</returns>
    Task InitializeDefaultConfigurationsAsync();
}