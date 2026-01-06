using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// High-level service for managing common configuration operations
/// </summary>
public class ConfigurationManagementService : IConfigurationManagementService
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<ConfigurationManagementService> _logger;

    public ConfigurationManagementService(
        IConfigurationService configurationService,
        ILogger<ConfigurationManagementService> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Applies a configuration profile for a specific business type
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="businessType">Business type</param>
    /// <returns>Task</returns>
    public async Task ApplyBusinessTypeConfigurationAsync(Guid shopId, BusinessType businessType)
    {
        try
        {
            _logger.LogInformation("Applying {BusinessType} configuration profile for shop {ShopId}", businessType, shopId);

            switch (businessType)
            {
                case BusinessType.GeneralRetail:
                    await ApplyRetailConfigurationAsync(shopId);
                    break;
                case BusinessType.Grocery:
                    await ApplyGroceryConfigurationAsync(shopId);
                    break;
                case BusinessType.Pharmacy:
                    await ApplyPharmacyConfigurationAsync(shopId);
                    break;
                case BusinessType.SuperShop:
                    await ApplySuperShopConfigurationAsync(shopId);
                    break;
                case BusinessType.Custom:
                default:
                    await ApplyDefaultConfigurationAsync(shopId);
                    break;
            }

            _logger.LogInformation("Successfully applied {BusinessType} configuration profile for shop {ShopId}", businessType, shopId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying {BusinessType} configuration profile for shop {ShopId}", businessType, shopId);
            throw;
        }
    }

    /// <summary>
    /// Exports all configurations for a shop
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Configuration export data</returns>
    public async Task<ConfigurationExport> ExportShopConfigurationAsync(Guid shopId)
    {
        try
        {
            _logger.LogInformation("Exporting configuration for shop {ShopId}", shopId);

            var export = new ConfigurationExport
            {
                ShopId = shopId,
                ExportedAt = DateTime.UtcNow,
                PricingSettings = await _configurationService.GetShopPricingSettingsAsync(shopId),
                TaxSettings = await _configurationService.GetShopTaxSettingsAsync(shopId),
                BusinessSettings = await _configurationService.GetBusinessSettingsAsync(),
                CurrencySettings = await _configurationService.GetCurrencySettingsAsync(),
                LocalizationSettings = await _configurationService.GetLocalizationSettingsAsync()
            };

            _logger.LogInformation("Successfully exported configuration for shop {ShopId}", shopId);
            return export;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting configuration for shop {ShopId}", shopId);
            throw;
        }
    }

    /// <summary>
    /// Imports configurations for a shop
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="export">Configuration export data</param>
    /// <returns>Task</returns>
    public async Task ImportShopConfigurationAsync(Guid shopId, ConfigurationExport export)
    {
        try
        {
            _logger.LogInformation("Importing configuration for shop {ShopId}", shopId);

            if (export == null)
                throw new ArgumentNullException(nameof(export));

            if (export.ShopId != shopId)
                throw new ArgumentException("Export does not belong to the specified shop.", nameof(export));

            if (export.PricingSettings != null)
            {
                await _configurationService.SetShopPricingSettingsAsync(shopId, export.PricingSettings);
            }

            if (export.TaxSettings != null)
            {
                await _configurationService.SetShopTaxSettingsAsync(shopId, export.TaxSettings);
            }

            _logger.LogInformation("Successfully imported configuration for shop {ShopId}", shopId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing configuration for shop {ShopId}", shopId);
            throw;
        }
    }

    /// <summary>
    /// Validates all configurations for a shop
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Validation results</returns>
    public async Task<ConfigurationValidationSummary> ValidateShopConfigurationAsync(Guid shopId)
    {
        try
        {
            _logger.LogInformation("Validating configuration for shop {ShopId}", shopId);

            var summary = new ConfigurationValidationSummary
            {
                ShopId = shopId,
                ValidatedAt = DateTime.UtcNow,
                ValidationResults = new List<ConfigurationValidationResult>()
            };

            // Validate pricing settings
            var pricingSettings = await _configurationService.GetShopPricingSettingsAsync(shopId);
            var pricingValidation = await ValidatePricingSettingsAsync(pricingSettings);
            summary.ValidationResults.AddRange(pricingValidation);

            // Validate tax settings
            var taxSettings = await _configurationService.GetShopTaxSettingsAsync(shopId);
            var taxValidation = await ValidateTaxSettingsAsync(taxSettings);
            summary.ValidationResults.AddRange(taxValidation);

            summary.IsValid = summary.ValidationResults.All(r => r.IsValid);

            _logger.LogInformation("Configuration validation completed for shop {ShopId}. Valid: {IsValid}", shopId, summary.IsValid);
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating configuration for shop {ShopId}", shopId);
            throw;
        }
    }

    /// <summary>
    /// Gets recommended configuration settings based on business analytics
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Configuration recommendations</returns>
    public async Task<ConfigurationRecommendations> GetConfigurationRecommendationsAsync(Guid shopId)
    {
        try
        {
            _logger.LogInformation("Generating configuration recommendations for shop {ShopId}", shopId);

            var recommendations = new ConfigurationRecommendations
            {
                ShopId = shopId,
                GeneratedAt = DateTime.UtcNow,
                Recommendations = new List<ConfigurationRecommendation>()
            };

            // Analyze current performance settings
            var performanceSettings = await _configurationService.GetPerformanceSettingsAsync();
            await AnalyzePerformanceSettingsAsync(shopId, performanceSettings, recommendations);

            // Analyze pricing settings
            var pricingSettings = await _configurationService.GetShopPricingSettingsAsync(shopId);
            await AnalyzePricingSettingsAsync(shopId, pricingSettings, recommendations);

            _logger.LogInformation("Generated {Count} configuration recommendations for shop {ShopId}", 
                recommendations.Recommendations.Count, shopId);
            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating configuration recommendations for shop {ShopId}", shopId);
            throw;
        }
    }

    #region Private Methods

    private async Task ApplyRetailConfigurationAsync(Guid shopId)
    {
        var pricingSettings = new ShopPricingSettings
        {
            WeightBasedPricingEnabled = false,
            BulkDiscountEnabled = true,
            BulkDiscountThreshold = 5,
            BulkDiscountPercentage = 5.0m,
            MembershipPricingEnabled = true,
            RoundingEnabled = true,
            RoundingPrecision = 0.05m
        };

        var taxSettings = new ShopTaxSettings
        {
            TaxEnabled = true,
            DefaultTaxRate = 8.25m,
            TaxName = "Sales Tax",
            TaxIncludedInPrice = false,
            ShowTaxOnReceipt = true
        };

        await _configurationService.SetShopPricingSettingsAsync(shopId, pricingSettings);
        await _configurationService.SetShopTaxSettingsAsync(shopId, taxSettings);
    }

    private async Task ApplyGroceryConfigurationAsync(Guid shopId)
    {
        var pricingSettings = new ShopPricingSettings
        {
            WeightBasedPricingEnabled = true,
            BulkDiscountEnabled = true,
            BulkDiscountThreshold = 10,
            BulkDiscountPercentage = 3.0m,
            MembershipPricingEnabled = true,
            RoundingEnabled = true,
            RoundingPrecision = 0.01m
        };

        var taxSettings = new ShopTaxSettings
        {
            TaxEnabled = true,
            DefaultTaxRate = 5.0m,
            TaxName = "GST",
            TaxIncludedInPrice = false,
            ShowTaxOnReceipt = true,
            CategoryTaxRates = new Dictionary<string, decimal>
            {
                { "Food", 0.0m },
                { "Beverages", 5.0m },
                { "Household", 8.0m }
            }
        };

        await _configurationService.SetShopPricingSettingsAsync(shopId, pricingSettings);
        await _configurationService.SetShopTaxSettingsAsync(shopId, taxSettings);
    }

    private async Task ApplyPharmacyConfigurationAsync(Guid shopId)
    {
        var pricingSettings = new ShopPricingSettings
        {
            WeightBasedPricingEnabled = false,
            BulkDiscountEnabled = false,
            MembershipPricingEnabled = true,
            RoundingEnabled = true,
            RoundingPrecision = 0.01m
        };

        var taxSettings = new ShopTaxSettings
        {
            TaxEnabled = true,
            DefaultTaxRate = 0.0m, // Medicines often tax-exempt
            TaxName = "Tax",
            TaxIncludedInPrice = false,
            ShowTaxOnReceipt = true,
            CategoryTaxRates = new Dictionary<string, decimal>
            {
                { "Prescription", 0.0m },
                { "OTC", 5.0m },
                { "Cosmetics", 8.0m }
            }
        };

        await _configurationService.SetShopPricingSettingsAsync(shopId, pricingSettings);
        await _configurationService.SetShopTaxSettingsAsync(shopId, taxSettings);
    }

    private async Task ApplySuperShopConfigurationAsync(Guid shopId)
    {
        var pricingSettings = new ShopPricingSettings
        {
            WeightBasedPricingEnabled = true,
            BulkDiscountEnabled = true,
            BulkDiscountThreshold = 2,
            BulkDiscountPercentage = 10.0m,
            MembershipPricingEnabled = true,
            RoundingEnabled = true,
            RoundingPrecision = 1.0m
        };

        var taxSettings = new ShopTaxSettings
        {
            TaxEnabled = true,
            DefaultTaxRate = 12.0m,
            TaxName = "Sales Tax",
            TaxIncludedInPrice = false,
            ShowTaxOnReceipt = true
        };

        await _configurationService.SetShopPricingSettingsAsync(shopId, pricingSettings);
        await _configurationService.SetShopTaxSettingsAsync(shopId, taxSettings);
    }

    private async Task ApplyDefaultConfigurationAsync(Guid shopId)
    {
        var pricingSettings = new ShopPricingSettings();
        var taxSettings = new ShopTaxSettings();

        await _configurationService.SetShopPricingSettingsAsync(shopId, pricingSettings);
        await _configurationService.SetShopTaxSettingsAsync(shopId, taxSettings);
    }

    private async Task<List<ConfigurationValidationResult>> ValidatePricingSettingsAsync(ShopPricingSettings settings)
    {
        var results = new List<ConfigurationValidationResult>();

        if (settings.BulkDiscountEnabled && settings.BulkDiscountPercentage > 50)
        {
            results.Add(new ConfigurationValidationResult
            {
                IsValid = false,
                ErrorMessage = "Bulk discount percentage should not exceed 50%"
            });
        }

        if (settings.MinimumProfitMargin < 0)
        {
            results.Add(new ConfigurationValidationResult
            {
                IsValid = false,
                ErrorMessage = "Minimum profit margin cannot be negative"
            });
        }

        return results;
    }

    private async Task<List<ConfigurationValidationResult>> ValidateTaxSettingsAsync(ShopTaxSettings settings)
    {
        var results = new List<ConfigurationValidationResult>();

        if (settings.DefaultTaxRate < 0 || settings.DefaultTaxRate > 100)
        {
            results.Add(new ConfigurationValidationResult
            {
                IsValid = false,
                ErrorMessage = "Tax rate must be between 0 and 100 percent"
            });
        }

        return results;
    }

    private async Task AnalyzePerformanceSettingsAsync(Guid shopId, PerformanceSettings settings, ConfigurationRecommendations recommendations)
    {
        if (settings.PageSize > 100)
        {
            recommendations.Recommendations.Add(new ConfigurationRecommendation
            {
                Category = "Performance",
                Title = "Reduce Page Size",
                Description = "Consider reducing page size to improve loading times on mobile devices",
                Priority = Shared.Core.DTOs.RecommendationPriority.Medium,
                RecommendedValue = "50"
            });
        }

        if (!settings.LazyLoadingEnabled)
        {
            recommendations.Recommendations.Add(new ConfigurationRecommendation
            {
                Category = "Performance",
                Title = "Enable Lazy Loading",
                Description = "Enable lazy loading to improve initial page load performance",
                Priority = Shared.Core.DTOs.RecommendationPriority.High,
                RecommendedValue = "true"
            });
        }
    }

    private async Task AnalyzePricingSettingsAsync(Guid shopId, ShopPricingSettings settings, ConfigurationRecommendations recommendations)
    {
        if (!settings.MembershipPricingEnabled)
        {
            recommendations.Recommendations.Add(new ConfigurationRecommendation
            {
                Category = "Pricing",
                Title = "Enable Membership Pricing",
                Description = "Enable membership pricing to increase customer loyalty and retention",
                Priority = Shared.Core.DTOs.RecommendationPriority.Medium,
                RecommendedValue = "true"
            });
        }

        if (settings.BulkDiscountEnabled && settings.BulkDiscountPercentage < 5)
        {
            recommendations.Recommendations.Add(new ConfigurationRecommendation
            {
                Category = "Pricing",
                Title = "Increase Bulk Discount",
                Description = "Consider increasing bulk discount percentage to encourage larger purchases",
                Priority = Shared.Core.DTOs.RecommendationPriority.Low,
                RecommendedValue = "5.0"
            });
        }
    }

    #endregion
}