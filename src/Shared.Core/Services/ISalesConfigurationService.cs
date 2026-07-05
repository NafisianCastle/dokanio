using Shared.Core.DTOs;

namespace Shared.Core.Services;

/// <summary>
/// Service interface for managing sales-specific configuration
/// </summary>
public interface ISalesConfigurationService
{
    /// <summary>
    /// Gets sales calculation precision settings for a shop
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Calculation precision settings</returns>
    Task<SalesCalculationPrecisionSettings> GetCalculationPrecisionSettingsAsync(Guid shopId);
    
    /// <summary>
    /// Sets sales calculation precision settings for a shop
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="settings">Calculation precision settings</param>
    /// <returns>Task</returns>
    Task SetCalculationPrecisionSettingsAsync(Guid shopId, SalesCalculationPrecisionSettings settings);
    
    /// <summary>
    /// Gets sales performance settings for a shop
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Sales performance settings</returns>
    Task<SalesPerformanceSettings> GetSalesPerformanceSettingsAsync(Guid shopId);
    
    /// <summary>
    /// Sets sales performance settings for a shop
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="settings">Sales performance settings</param>
    /// <returns>Task</returns>
    Task SetSalesPerformanceSettingsAsync(Guid shopId, SalesPerformanceSettings settings);
    
    /// <summary>
    /// Gets complete shop sales configuration
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Complete shop sales configuration</returns>
    Task<ShopSalesConfiguration> GetShopSalesConfigurationAsync(Guid shopId);
    
    /// <summary>
    /// Sets complete shop sales configuration
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="configuration">Complete shop sales configuration</param>
    /// <param name="updatedBy">User who updated the configuration</param>
    /// <returns>Task</returns>
    Task SetShopSalesConfigurationAsync(Guid shopId, ShopSalesConfiguration configuration, Guid updatedBy);
    
    /// <summary>
    /// Gets user calculation preferences
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>User calculation preferences</returns>
    Task<UserCalculationPreferences> GetUserCalculationPreferencesAsync(Guid userId);
    
    /// <summary>
    /// Sets user calculation preferences
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="preferences">User calculation preferences</param>
    /// <returns>Task</returns>
    Task SetUserCalculationPreferencesAsync(Guid userId, UserCalculationPreferences preferences);
    
    /// <summary>
    /// Gets the effective calculation precision for a user in a shop
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Effective calculation precision settings</returns>
    Task<SalesCalculationPrecisionSettings> GetEffectiveCalculationPrecisionAsync(Guid userId, Guid shopId);
    
    /// <summary>
    /// Gets the effective performance settings for a shop
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Effective performance settings</returns>
    Task<SalesPerformanceSettings> GetEffectivePerformanceSettingsAsync(Guid shopId);
    
    /// <summary>
    /// Validates sales configuration settings
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="configuration">Configuration to validate</param>
    /// <returns>Validation result</returns>
    Task<ConfigurationValidationSummary> ValidateSalesConfigurationAsync(Guid shopId, ShopSalesConfiguration configuration);
    
    /// <summary>
    /// Gets configuration recommendations for a shop based on usage patterns
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Configuration recommendations</returns>
    Task<ConfigurationRecommendations> GetConfigurationRecommendationsAsync(Guid shopId);
    
    /// <summary>
    /// Exports shop sales configuration for backup or migration
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Configuration export data</returns>
    Task<ConfigurationExport> ExportShopConfigurationAsync(Guid shopId);
    
    /// <summary>
    /// Imports shop sales configuration from backup or migration
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="configurationExport">Configuration export data</param>
    /// <param name="importedBy">User who imported the configuration</param>
    /// <returns>Task</returns>
    Task ImportShopConfigurationAsync(Guid shopId, ConfigurationExport configurationExport, Guid importedBy);
    
    /// <summary>
    /// Resets shop sales configuration to defaults
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="resetBy">User who reset the configuration</param>
    /// <returns>Task</returns>
    Task ResetShopConfigurationToDefaultsAsync(Guid shopId, Guid resetBy);
    
    /// <summary>
    /// Gets configuration change history for a shop
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="fromDate">Start date for history</param>
    /// <param name="toDate">End date for history</param>
    /// <returns>Configuration change history</returns>
    Task<IEnumerable<ConfigurationChangeEvent>> GetConfigurationChangeHistoryAsync(Guid shopId, DateTime fromDate, DateTime toDate);
    
    /// <summary>
    /// Initializes default sales configuration for a new shop
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Task</returns>
    Task InitializeDefaultSalesConfigurationAsync(Guid shopId);
}

/// <summary>
/// Configuration change event for audit trail
/// </summary>
public class ConfigurationChangeEvent
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public string ConfigurationKey { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string NewValue { get; set; } = string.Empty;
    public Guid ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
    public string ChangeReason { get; set; } = string.Empty;
    public string? AdditionalContext { get; set; }
}