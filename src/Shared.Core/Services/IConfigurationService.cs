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
    
    /// <summary>
    /// Gets shop-level pricing settings
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Shop pricing settings</returns>
    Task<ShopPricingSettings> GetShopPricingSettingsAsync(Guid shopId);
    
    /// <summary>
    /// Sets shop-level pricing settings
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="settings">Pricing settings</param>
    /// <returns>Task</returns>
    Task SetShopPricingSettingsAsync(Guid shopId, ShopPricingSettings settings);
    
    /// <summary>
    /// Gets shop-level tax settings
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Shop tax settings</returns>
    Task<ShopTaxSettings> GetShopTaxSettingsAsync(Guid shopId);
    
    /// <summary>
    /// Sets shop-level tax settings
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="settings">Tax settings</param>
    /// <returns>Task</returns>
    Task SetShopTaxSettingsAsync(Guid shopId, ShopTaxSettings settings);
    
    /// <summary>
    /// Gets user preferences for UI customization
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>User preferences</returns>
    Task<UserPreferences> GetUserPreferencesAsync(Guid userId);
    
    /// <summary>
    /// Sets user preferences for UI customization
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="preferences">User preferences</param>
    /// <returns>Task</returns>
    Task SetUserPreferencesAsync(Guid userId, UserPreferences preferences);
    
    /// <summary>
    /// Gets barcode scanner configuration
    /// </summary>
    /// <param name="deviceId">Device identifier (optional, uses current device if null)</param>
    /// <returns>Barcode scanner settings</returns>
    Task<BarcodeScannerSettings> GetBarcodeScannerSettingsAsync(Guid? deviceId = null);
    
    /// <summary>
    /// Sets barcode scanner configuration
    /// </summary>
    /// <param name="settings">Barcode scanner settings</param>
    /// <param name="deviceId">Device identifier (optional, uses current device if null)</param>
    /// <returns>Task</returns>
    Task SetBarcodeScannerSettingsAsync(BarcodeScannerSettings settings, Guid? deviceId = null);
    
    /// <summary>
    /// Gets performance tuning settings
    /// </summary>
    /// <returns>Performance settings</returns>
    Task<PerformanceSettings> GetPerformanceSettingsAsync();
    
    /// <summary>
    /// Sets performance tuning settings
    /// </summary>
    /// <param name="settings">Performance settings</param>
    /// <returns>Task</returns>
    Task SetPerformanceSettingsAsync(PerformanceSettings settings);
    
    /// <summary>
    /// Gets configuration by shop and key
    /// </summary>
    /// <typeparam name="T">Type to convert the value to</typeparam>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if configuration not found</param>
    /// <returns>Configuration value or default</returns>
    Task<T> GetShopConfigurationAsync<T>(Guid shopId, string key, T defaultValue = default!);
    
    /// <summary>
    /// Sets configuration by shop and key
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="key">Configuration key</param>
    /// <param name="value">Configuration value</param>
    /// <param name="description">Optional description</param>
    /// <returns>Task</returns>
    Task SetShopConfigurationAsync<T>(Guid shopId, string key, T value, string? description = null);
    
    /// <summary>
    /// Gets configuration by user and key
    /// </summary>
    /// <typeparam name="T">Type to convert the value to</typeparam>
    /// <param name="userId">User identifier</param>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if configuration not found</param>
    /// <returns>Configuration value or default</returns>
    Task<T> GetUserConfigurationAsync<T>(Guid userId, string key, T defaultValue = default!);
    
    /// <summary>
    /// Sets configuration by user and key
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>
    /// <param name="userId">User identifier</param>
    /// <param name="key">Configuration key</param>
    /// <param name="value">Configuration value</param>
    /// <param name="description">Optional description</param>
    /// <returns>Task</returns>
    Task SetUserConfigurationAsync<T>(Guid userId, string key, T value, string? description = null);
}