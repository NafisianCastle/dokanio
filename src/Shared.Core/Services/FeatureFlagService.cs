using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Enums;
using System.Security.Cryptography;
using System.Text;

namespace Shared.Core.Services;

/// <summary>
/// Service implementation for managing feature flags
/// </summary>
public class FeatureFlagService : IFeatureFlagService
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<FeatureFlagService> _logger;
    private readonly Dictionary<FeatureFlag, (string name, string description, bool defaultEnabled)> _featureFlagDefinitions;

    public FeatureFlagService(
        IConfigurationService configurationService,
        ILogger<FeatureFlagService> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _featureFlagDefinitions = InitializeFeatureFlagDefinitions();
    }

    /// <summary>
    /// Checks if a feature flag is enabled globally
    /// </summary>
    /// <param name="featureFlag">Feature flag to check</param>
    /// <returns>True if enabled, false otherwise</returns>
    public async Task<bool> IsFeatureEnabledAsync(FeatureFlag featureFlag)
    {
        try
        {
            var key = GetFeatureFlagKey(featureFlag);
            var isEnabled = await _configurationService.GetConfigurationAsync($"{key}.Enabled", GetDefaultEnabledState(featureFlag));
            
            if (!isEnabled)
                return false;

            // Check rollout percentage
            var rolloutPercentage = await _configurationService.GetConfigurationAsync($"{key}.RolloutPercentage", 100);
            if (rolloutPercentage >= 100)
                return true;

            // Use deterministic hash to ensure consistent rollout
            var hash = ComputeHash(featureFlag.ToString());
            var userPercentile = Math.Abs(hash) % 100;
            
            return userPercentile < rolloutPercentage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking feature flag {FeatureFlag}, returning default", featureFlag);
            return GetDefaultEnabledState(featureFlag);
        }
    }

    /// <summary>
    /// Checks if a feature flag is enabled for a specific shop
    /// </summary>
    /// <param name="featureFlag">Feature flag to check</param>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>True if enabled, false otherwise</returns>
    public async Task<bool> IsFeatureEnabledForShopAsync(FeatureFlag featureFlag, Guid shopId)
    {
        try
        {
            var key = GetFeatureFlagKey(featureFlag);
            var shopKey = $"{key}.Shop.{shopId}";
            
            // Check shop-specific override first
            var shopOverride = await _configurationService.GetShopConfigurationAsync(shopId, $"{shopKey}.Enabled", (bool?)null);
            if (shopOverride.HasValue)
                return shopOverride.Value;

            // Fall back to global setting
            return await IsFeatureEnabledAsync(featureFlag);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking feature flag {FeatureFlag} for shop {ShopId}, returning default", featureFlag, shopId);
            return GetDefaultEnabledState(featureFlag);
        }
    }

    /// <summary>
    /// Checks if a feature flag is enabled for a specific user
    /// </summary>
    /// <param name="featureFlag">Feature flag to check</param>
    /// <param name="userId">User identifier</param>
    /// <returns>True if enabled, false otherwise</returns>
    public async Task<bool> IsFeatureEnabledForUserAsync(FeatureFlag featureFlag, Guid userId)
    {
        try
        {
            var key = GetFeatureFlagKey(featureFlag);
            var userKey = $"{key}.User.{userId}";
            
            // Check user-specific override first
            var userOverride = await _configurationService.GetUserConfigurationAsync(userId, $"{userKey}.Enabled", (bool?)null);
            if (userOverride.HasValue)
                return userOverride.Value;

            // Fall back to global setting with user-specific rollout
            var isGloballyEnabled = await _configurationService.GetConfigurationAsync($"{key}.Enabled", GetDefaultEnabledState(featureFlag));
            if (!isGloballyEnabled)
                return false;

            var rolloutPercentage = await _configurationService.GetConfigurationAsync($"{key}.RolloutPercentage", 100);
            if (rolloutPercentage >= 100)
                return true;

            // Use deterministic hash based on feature flag and user ID
            var hash = ComputeHash($"{featureFlag}:{userId}");
            var userPercentile = Math.Abs(hash) % 100;
            
            return userPercentile < rolloutPercentage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking feature flag {FeatureFlag} for user {UserId}, returning default", featureFlag, userId);
            return GetDefaultEnabledState(featureFlag);
        }
    }

    /// <summary>
    /// Enables a feature flag globally
    /// </summary>
    /// <param name="featureFlag">Feature flag to enable</param>
    /// <param name="rolloutPercentage">Percentage of users to enable for (0-100)</param>
    /// <returns>Task</returns>
    public async Task EnableFeatureAsync(FeatureFlag featureFlag, int rolloutPercentage = 100)
    {
        if (rolloutPercentage < 0 || rolloutPercentage > 100)
            throw new ArgumentOutOfRangeException(nameof(rolloutPercentage), "Rollout percentage must be between 0 and 100");

        try
        {
            var key = GetFeatureFlagKey(featureFlag);
            await _configurationService.SetConfigurationAsync($"{key}.Enabled", true, $"Enable {GetFeatureFlagName(featureFlag)}", true);
            await _configurationService.SetConfigurationAsync($"{key}.RolloutPercentage", rolloutPercentage, $"Rollout percentage for {GetFeatureFlagName(featureFlag)}", true);
            await _configurationService.SetConfigurationAsync($"{key}.EnabledAt", DateTime.UtcNow, $"Enabled timestamp for {GetFeatureFlagName(featureFlag)}", true);
            
            _logger.LogInformation("Feature flag {FeatureFlag} enabled with {RolloutPercentage}% rollout", featureFlag, rolloutPercentage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling feature flag {FeatureFlag}", featureFlag);
            throw;
        }
    }

    /// <summary>
    /// Disables a feature flag globally
    /// </summary>
    /// <param name="featureFlag">Feature flag to disable</param>
    /// <returns>Task</returns>
    public async Task DisableFeatureAsync(FeatureFlag featureFlag)
    {
        try
        {
            var key = GetFeatureFlagKey(featureFlag);
            await _configurationService.SetConfigurationAsync($"{key}.Enabled", false, $"Disable {GetFeatureFlagName(featureFlag)}", true);
            await _configurationService.SetConfigurationAsync($"{key}.DisabledAt", DateTime.UtcNow, $"Disabled timestamp for {GetFeatureFlagName(featureFlag)}", true);
            
            _logger.LogInformation("Feature flag {FeatureFlag} disabled", featureFlag);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling feature flag {FeatureFlag}", featureFlag);
            throw;
        }
    }

    /// <summary>
    /// Enables a feature flag for a specific shop
    /// </summary>
    /// <param name="featureFlag">Feature flag to enable</param>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Task</returns>
    public async Task EnableFeatureForShopAsync(FeatureFlag featureFlag, Guid shopId)
    {
        try
        {
            var key = GetFeatureFlagKey(featureFlag);
            var shopKey = $"{key}.Shop.{shopId}";
            await _configurationService.SetShopConfigurationAsync(shopId, $"{shopKey}.Enabled", true, $"Enable {GetFeatureFlagName(featureFlag)} for shop");
            await _configurationService.SetShopConfigurationAsync(shopId, $"{shopKey}.EnabledAt", DateTime.UtcNow, $"Enabled timestamp for {GetFeatureFlagName(featureFlag)} for shop");
            
            _logger.LogInformation("Feature flag {FeatureFlag} enabled for shop {ShopId}", featureFlag, shopId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling feature flag {FeatureFlag} for shop {ShopId}", featureFlag, shopId);
            throw;
        }
    }

    /// <summary>
    /// Disables a feature flag for a specific shop
    /// </summary>
    /// <param name="featureFlag">Feature flag to disable</param>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Task</returns>
    public async Task DisableFeatureForShopAsync(FeatureFlag featureFlag, Guid shopId)
    {
        try
        {
            var key = GetFeatureFlagKey(featureFlag);
            var shopKey = $"{key}.Shop.{shopId}";
            await _configurationService.SetShopConfigurationAsync(shopId, $"{shopKey}.Enabled", false, $"Disable {GetFeatureFlagName(featureFlag)} for shop");
            await _configurationService.SetShopConfigurationAsync(shopId, $"{shopKey}.DisabledAt", DateTime.UtcNow, $"Disabled timestamp for {GetFeatureFlagName(featureFlag)} for shop");
            
            _logger.LogInformation("Feature flag {FeatureFlag} disabled for shop {ShopId}", featureFlag, shopId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling feature flag {FeatureFlag} for shop {ShopId}", featureFlag, shopId);
            throw;
        }
    }

    /// <summary>
    /// Enables a feature flag for a specific user
    /// </summary>
    /// <param name="featureFlag">Feature flag to enable</param>
    /// <param name="userId">User identifier</param>
    /// <returns>Task</returns>
    public async Task EnableFeatureForUserAsync(FeatureFlag featureFlag, Guid userId)
    {
        try
        {
            var key = GetFeatureFlagKey(featureFlag);
            var userKey = $"{key}.User.{userId}";
            await _configurationService.SetUserConfigurationAsync(userId, $"{userKey}.Enabled", true, $"Enable {GetFeatureFlagName(featureFlag)} for user");
            await _configurationService.SetUserConfigurationAsync(userId, $"{userKey}.EnabledAt", DateTime.UtcNow, $"Enabled timestamp for {GetFeatureFlagName(featureFlag)} for user");
            
            _logger.LogInformation("Feature flag {FeatureFlag} enabled for user {UserId}", featureFlag, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling feature flag {FeatureFlag} for user {UserId}", featureFlag, userId);
            throw;
        }
    }

    /// <summary>
    /// Disables a feature flag for a specific user
    /// </summary>
    /// <param name="featureFlag">Feature flag to disable</param>
    /// <param name="userId">User identifier</param>
    /// <returns>Task</returns>
    public async Task DisableFeatureForUserAsync(FeatureFlag featureFlag, Guid userId)
    {
        try
        {
            var key = GetFeatureFlagKey(featureFlag);
            var userKey = $"{key}.User.{userId}";
            await _configurationService.SetUserConfigurationAsync(userId, $"{userKey}.Enabled", false, $"Disable {GetFeatureFlagName(featureFlag)} for user");
            await _configurationService.SetUserConfigurationAsync(userId, $"{userKey}.DisabledAt", DateTime.UtcNow, $"Disabled timestamp for {GetFeatureFlagName(featureFlag)} for user");
            
            _logger.LogInformation("Feature flag {FeatureFlag} disabled for user {UserId}", featureFlag, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling feature flag {FeatureFlag} for user {UserId}", featureFlag, userId);
            throw;
        }
    }

    /// <summary>
    /// Gets all feature flags and their current status
    /// </summary>
    /// <returns>List of feature flag configurations</returns>
    public async Task<IEnumerable<FeatureFlagConfiguration>> GetAllFeatureFlagsAsync()
    {
        var configurations = new List<FeatureFlagConfiguration>();

        foreach (var (featureFlag, (name, description, defaultEnabled)) in _featureFlagDefinitions)
        {
            try
            {
                var key = GetFeatureFlagKey(featureFlag);
                var isEnabled = await _configurationService.GetConfigurationAsync($"{key}.Enabled", defaultEnabled);
                var rolloutPercentage = await _configurationService.GetConfigurationAsync($"{key}.RolloutPercentage", 100);
                var enabledAt = await _configurationService.GetConfigurationAsync<DateTime?>($"{key}.EnabledAt", null);
                var disabledAt = await _configurationService.GetConfigurationAsync<DateTime?>($"{key}.DisabledAt", null);

                configurations.Add(new FeatureFlagConfiguration
                {
                    FeatureFlag = featureFlag,
                    Name = name,
                    Description = description,
                    IsEnabled = isEnabled,
                    RolloutPercentage = rolloutPercentage,
                    EnabledAt = enabledAt,
                    DisabledAt = disabledAt,
                    Scope = FeatureFlagScope.Global
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting configuration for feature flag {FeatureFlag}", featureFlag);
            }
        }

        return configurations;
    }

    /// <summary>
    /// Gets feature flags for a specific shop
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>List of feature flag configurations for the shop</returns>
    public async Task<IEnumerable<FeatureFlagConfiguration>> GetShopFeatureFlagsAsync(Guid shopId)
    {
        var configurations = new List<FeatureFlagConfiguration>();

        foreach (var (featureFlag, (name, description, defaultEnabled)) in _featureFlagDefinitions)
        {
            try
            {
                var key = GetFeatureFlagKey(featureFlag);
                var shopKey = $"{key}.Shop.{shopId}";
                
                var isEnabled = await _configurationService.GetShopConfigurationAsync(shopId, $"{shopKey}.Enabled", (bool?)null);
                var enabledAt = await _configurationService.GetShopConfigurationAsync<DateTime?>(shopId, $"{shopKey}.EnabledAt", null);
                var disabledAt = await _configurationService.GetShopConfigurationAsync<DateTime?>(shopId, $"{shopKey}.DisabledAt", null);

                if (isEnabled.HasValue)
                {
                    configurations.Add(new FeatureFlagConfiguration
                    {
                        FeatureFlag = featureFlag,
                        Name = name,
                        Description = description,
                        IsEnabled = isEnabled.Value,
                        RolloutPercentage = 100, // Shop-specific overrides are always 100%
                        EnabledAt = enabledAt,
                        DisabledAt = disabledAt,
                        ShopId = shopId,
                        Scope = FeatureFlagScope.Shop
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting shop configuration for feature flag {FeatureFlag} and shop {ShopId}", featureFlag, shopId);
            }
        }

        return configurations;
    }

    /// <summary>
    /// Gets feature flags for a specific user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>List of feature flag configurations for the user</returns>
    public async Task<IEnumerable<FeatureFlagConfiguration>> GetUserFeatureFlagsAsync(Guid userId)
    {
        var configurations = new List<FeatureFlagConfiguration>();

        foreach (var (featureFlag, (name, description, defaultEnabled)) in _featureFlagDefinitions)
        {
            try
            {
                var key = GetFeatureFlagKey(featureFlag);
                var userKey = $"{key}.User.{userId}";
                
                var isEnabled = await _configurationService.GetUserConfigurationAsync(userId, $"{userKey}.Enabled", (bool?)null);
                var enabledAt = await _configurationService.GetUserConfigurationAsync<DateTime?>(userId, $"{userKey}.EnabledAt", null);
                var disabledAt = await _configurationService.GetUserConfigurationAsync<DateTime?>(userId, $"{userKey}.DisabledAt", null);

                if (isEnabled.HasValue)
                {
                    configurations.Add(new FeatureFlagConfiguration
                    {
                        FeatureFlag = featureFlag,
                        Name = name,
                        Description = description,
                        IsEnabled = isEnabled.Value,
                        RolloutPercentage = 100, // User-specific overrides are always 100%
                        EnabledAt = enabledAt,
                        DisabledAt = disabledAt,
                        UserId = userId,
                        Scope = FeatureFlagScope.User
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user configuration for feature flag {FeatureFlag} and user {UserId}", featureFlag, userId);
            }
        }

        return configurations;
    }

    /// <summary>
    /// Updates rollout percentage for a feature flag
    /// </summary>
    /// <param name="featureFlag">Feature flag to update</param>
    /// <param name="rolloutPercentage">New rollout percentage (0-100)</param>
    /// <returns>Task</returns>
    public async Task UpdateRolloutPercentageAsync(FeatureFlag featureFlag, int rolloutPercentage)
    {
        if (rolloutPercentage < 0 || rolloutPercentage > 100)
            throw new ArgumentOutOfRangeException(nameof(rolloutPercentage), "Rollout percentage must be between 0 and 100");

        try
        {
            var key = GetFeatureFlagKey(featureFlag);
            await _configurationService.SetConfigurationAsync($"{key}.RolloutPercentage", rolloutPercentage, $"Rollout percentage for {GetFeatureFlagName(featureFlag)}", true);
            
            _logger.LogInformation("Feature flag {FeatureFlag} rollout percentage updated to {RolloutPercentage}%", featureFlag, rolloutPercentage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating rollout percentage for feature flag {FeatureFlag}", featureFlag);
            throw;
        }
    }

    /// <summary>
    /// Gets feature flag usage analytics
    /// </summary>
    /// <param name="featureFlag">Feature flag to analyze</param>
    /// <param name="fromDate">Start date for analytics</param>
    /// <param name="toDate">End date for analytics</param>
    /// <returns>Feature flag analytics</returns>
    public async Task<FeatureFlagAnalytics> GetFeatureFlagAnalyticsAsync(FeatureFlag featureFlag, DateTime fromDate, DateTime toDate)
    {
        // This is a placeholder implementation
        // In a real implementation, you would query usage logs and analytics data
        return new FeatureFlagAnalytics
        {
            FeatureFlag = featureFlag,
            Name = GetFeatureFlagName(featureFlag),
            FromDate = fromDate,
            ToDate = toDate,
            TotalUsers = 0,
            EnabledUsers = 0,
            DisabledUsers = 0,
            EnabledPercentage = 0,
            TotalUsageCount = 0,
            SuccessfulUsageCount = 0,
            FailedUsageCount = 0,
            SuccessRate = 0,
            AverageResponseTime = TimeSpan.Zero
        };
    }

    /// <summary>
    /// Initializes default feature flag configurations
    /// </summary>
    /// <returns>Task</returns>
    public async Task InitializeDefaultFeatureFlagsAsync()
    {
        foreach (var (featureFlag, (name, description, defaultEnabled)) in _featureFlagDefinitions)
        {
            try
            {
                var key = GetFeatureFlagKey(featureFlag);
                var currentValue = await _configurationService.GetConfigurationAsync<bool?>($"{key}.Enabled", null);
                
                if (!currentValue.HasValue)
                {
                    await _configurationService.SetConfigurationAsync($"{key}.Enabled", defaultEnabled, description, true);
                    await _configurationService.SetConfigurationAsync($"{key}.RolloutPercentage", 100, $"Rollout percentage for {name}", true);
                    
                    _logger.LogInformation("Initialized feature flag {FeatureFlag} with default value {DefaultEnabled}", featureFlag, defaultEnabled);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing feature flag {FeatureFlag}", featureFlag);
            }
        }
    }

    private Dictionary<FeatureFlag, (string name, string description, bool defaultEnabled)> InitializeFeatureFlagDefinitions()
    {
        return new Dictionary<FeatureFlag, (string, string, bool)>
        {
            { FeatureFlag.EnhancedRealTimeCalculations, ("Enhanced Real-Time Calculations", "Improved calculation engine with sub-100ms performance", true) },
            { FeatureFlag.AdvancedDiscountProcessing, ("Advanced Discount Processing", "Enhanced discount engine with membership integration", true) },
            { FeatureFlag.ImprovedWeightBasedPricing, ("Improved Weight-Based Pricing", "Enhanced weight-based pricing with precision rounding", true) },
            { FeatureFlag.EnhancedStockValidation, ("Enhanced Stock Validation", "Real-time stock validation with reservation system", true) },
            { FeatureFlag.AdvancedPaymentProcessing, ("Advanced Payment Processing", "Enhanced payment processing with multiple methods", true) },
            { FeatureFlag.RealTimeInventoryUpdates, ("Real-Time Inventory Updates", "Immediate inventory updates upon sale completion", true) },
            { FeatureFlag.ComprehensiveAuditLogging, ("Comprehensive Audit Logging", "Complete audit trail for all operations", true) },
            { FeatureFlag.PerformanceOptimizations, ("Performance Optimizations", "Caching and performance improvements", true) },
            { FeatureFlag.AdvancedErrorHandling, ("Advanced Error Handling", "Enhanced error handling and recovery", true) },
            { FeatureFlag.EnhancedInputValidation, ("Enhanced Input Validation", "Comprehensive input validation system", true) },
            { FeatureFlag.MultiCurrencySupport, ("Multi-Currency Support", "Support for multiple currencies", false) },
            { FeatureFlag.AdvancedTaxCalculations, ("Advanced Tax Calculations", "Complex tax calculation rules", false) },
            { FeatureFlag.BulkOperationsSupport, ("Bulk Operations Support", "Support for bulk operations", false) },
            { FeatureFlag.AdvancedReporting, ("Advanced Reporting", "Enhanced reporting capabilities", false) },
            { FeatureFlag.AIPoweredRecommendations, ("AI-Powered Recommendations", "AI-based product and pricing recommendations", false) },
            { FeatureFlag.MobileOptimizations, ("Mobile Optimizations", "Mobile-specific performance optimizations", true) },
            { FeatureFlag.OfflineFirstEnhancements, ("Offline-First Enhancements", "Enhanced offline capabilities", true) },
            { FeatureFlag.AdvancedBarcodeScanning, ("Advanced Barcode Scanning", "Enhanced barcode scanning features", false) },
            { FeatureFlag.EnhancedReceiptGeneration, ("Enhanced Receipt Generation", "Advanced receipt generation and customization", false) },
            { FeatureFlag.AdvancedCustomerManagement, ("Advanced Customer Management", "Enhanced customer management features", false) },
            { FeatureFlag.EnhancedSecurity, ("Enhanced Security", "Advanced security features and monitoring", false) }
        };
    }

    private string GetFeatureFlagKey(FeatureFlag featureFlag)
    {
        return $"FeatureFlag.{featureFlag}";
    }

    private string GetFeatureFlagName(FeatureFlag featureFlag)
    {
        return _featureFlagDefinitions.TryGetValue(featureFlag, out var definition) ? definition.name : featureFlag.ToString();
    }

    private bool GetDefaultEnabledState(FeatureFlag featureFlag)
    {
        return _featureFlagDefinitions.TryGetValue(featureFlag, out var definition) ? definition.defaultEnabled : false;
    }

    private int ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToInt32(hashBytes, 0);
    }
}