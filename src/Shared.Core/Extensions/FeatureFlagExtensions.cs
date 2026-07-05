using Shared.Core.Services;

namespace Shared.Core.Extensions;

/// <summary>
/// Extension methods for feature flag checking
/// </summary>
public static class FeatureFlagExtensions
{
    /// <summary>
    /// Checks if enhanced real-time calculations are enabled
    /// </summary>
    /// <param name="configurationService">Configuration service</param>
    /// <returns>True if enabled</returns>
    public static async Task<bool> IsEnhancedRealTimeCalculationsEnabledAsync(this IConfigurationService configurationService)
    {
        var settings = await configurationService.GetFeatureFlagSettingsAsync();
        return settings.EnhancedRealTimeCalculations;
    }
    
    /// <summary>
    /// Checks if advanced discount processing is enabled
    /// </summary>
    /// <param name="configurationService">Configuration service</param>
    /// <returns>True if enabled</returns>
    public static async Task<bool> IsAdvancedDiscountProcessingEnabledAsync(this IConfigurationService configurationService)
    {
        var settings = await configurationService.GetFeatureFlagSettingsAsync();
        return settings.AdvancedDiscountProcessing;
    }
    
    /// <summary>
    /// Checks if improved weight-based pricing is enabled
    /// </summary>
    /// <param name="configurationService">Configuration service</param>
    /// <returns>True if enabled</returns>
    public static async Task<bool> IsImprovedWeightBasedPricingEnabledAsync(this IConfigurationService configurationService)
    {
        var settings = await configurationService.GetFeatureFlagSettingsAsync();
        return settings.ImprovedWeightBasedPricing;
    }
    
    /// <summary>
    /// Checks if enhanced stock validation is enabled
    /// </summary>
    /// <param name="configurationService">Configuration service</param>
    /// <returns>True if enabled</returns>
    public static async Task<bool> IsEnhancedStockValidationEnabledAsync(this IConfigurationService configurationService)
    {
        var settings = await configurationService.GetFeatureFlagSettingsAsync();
        return settings.EnhancedStockValidation;
    }
    
    /// <summary>
    /// Checks if performance optimizations are enabled
    /// </summary>
    /// <param name="configurationService">Configuration service</param>
    /// <returns>True if enabled</returns>
    public static async Task<bool> IsPerformanceOptimizationsEnabledAsync(this IConfigurationService configurationService)
    {
        var settings = await configurationService.GetFeatureFlagSettingsAsync();
        return settings.PerformanceOptimizations;
    }
    
    /// <summary>
    /// Checks if comprehensive audit logging is enabled
    /// </summary>
    /// <param name="configurationService">Configuration service</param>
    /// <returns>True if enabled</returns>
    public static async Task<bool> IsComprehensiveAuditLoggingEnabledAsync(this IConfigurationService configurationService)
    {
        var settings = await configurationService.GetFeatureFlagSettingsAsync();
        return settings.ComprehensiveAuditLogging;
    }
    
    /// <summary>
    /// Gets effective calculation timeout based on performance settings
    /// </summary>
    /// <param name="configurationService">Configuration service</param>
    /// <returns>Calculation timeout in milliseconds</returns>
    public static async Task<int> GetEffectiveCalculationTimeoutAsync(this IConfigurationService configurationService)
    {
        var performanceSettings = await configurationService.GetSalesPerformanceSettingsAsync();
        return performanceSettings.CalculationTimeoutMs;
    }
    
    /// <summary>
    /// Gets effective validation timeout based on performance settings
    /// </summary>
    /// <param name="configurationService">Configuration service</param>
    /// <returns>Validation timeout in milliseconds</returns>
    public static async Task<int> GetEffectiveValidationTimeoutAsync(this IConfigurationService configurationService)
    {
        var performanceSettings = await configurationService.GetSalesPerformanceSettingsAsync();
        return performanceSettings.ValidationTimeoutMs;
    }
    
    /// <summary>
    /// Checks if calculation caching is enabled
    /// </summary>
    /// <param name="configurationService">Configuration service</param>
    /// <returns>True if enabled</returns>
    public static async Task<bool> IsCalculationCachingEnabledAsync(this IConfigurationService configurationService)
    {
        var performanceSettings = await configurationService.GetSalesPerformanceSettingsAsync();
        return performanceSettings.EnableCalculationCaching;
    }
    
    /// <summary>
    /// Gets calculation cache expiry time
    /// </summary>
    /// <param name="configurationService">Configuration service</param>
    /// <returns>Cache expiry time in minutes</returns>
    public static async Task<int> GetCalculationCacheExpiryAsync(this IConfigurationService configurationService)
    {
        var performanceSettings = await configurationService.GetSalesPerformanceSettingsAsync();
        return performanceSettings.CalculationCacheExpiryMinutes;
    }
}