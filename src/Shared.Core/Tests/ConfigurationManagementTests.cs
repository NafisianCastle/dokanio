using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.DependencyInjection;
using Shared.Core.DTOs;
using Shared.Core.Enums;
using Shared.Core.Services;
using Xunit;

namespace Shared.Core.Tests;

/// <summary>
/// Tests for configuration management functionality
/// </summary>
public class ConfigurationManagementTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IConfigurationService _configurationService;
    private readonly IConfigurationManagementService _configurationManagementService;

    public ConfigurationManagementTests()
    {
        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        _serviceProvider = services.BuildServiceProvider();

        _configurationService = _serviceProvider.GetRequiredService<IConfigurationService>();
        _configurationManagementService = _serviceProvider.GetRequiredService<IConfigurationManagementService>();
    }

    [Fact]
    public async Task ConfigurationService_SetAndGetConfiguration_ShouldWork()
    {
        // Arrange
        const string key = "Test.Setting";
        const string value = "TestValue";

        // Act
        await _configurationService.SetConfigurationAsync(key, value, "Test setting");
        var retrievedValue = await _configurationService.GetConfigurationAsync<string>(key);

        // Assert
        Assert.Equal(value, retrievedValue);
    }

    [Fact]
    public async Task ConfigurationService_GetShopPricingSettings_ShouldReturnDefaults()
    {
        // Arrange
        var shopId = Guid.NewGuid();

        // Act
        var pricingSettings = await _configurationService.GetShopPricingSettingsAsync(shopId);

        // Assert
        Assert.NotNull(pricingSettings);
        Assert.True(pricingSettings.WeightBasedPricingEnabled);
        Assert.False(pricingSettings.BulkDiscountEnabled);
    }

    [Fact]
    public async Task ConfigurationService_SetAndGetShopPricingSettings_ShouldWork()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var settings = new ShopPricingSettings
        {
            WeightBasedPricingEnabled = false,
            BulkDiscountEnabled = true,
            BulkDiscountThreshold = 5,
            BulkDiscountPercentage = 10.0m
        };

        // Act
        await _configurationService.SetShopPricingSettingsAsync(shopId, settings);
        var retrievedSettings = await _configurationService.GetShopPricingSettingsAsync(shopId);

        // Assert
        Assert.False(retrievedSettings.WeightBasedPricingEnabled);
        Assert.True(retrievedSettings.BulkDiscountEnabled);
        Assert.Equal(5, retrievedSettings.BulkDiscountThreshold);
        Assert.Equal(10.0m, retrievedSettings.BulkDiscountPercentage);
    }

    [Fact]
    public async Task ConfigurationService_GetUserPreferences_ShouldReturnDefaults()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var preferences = await _configurationService.GetUserPreferencesAsync(userId);

        // Assert
        Assert.NotNull(preferences);
        Assert.Equal("Light", preferences.Theme);
        Assert.Equal(14, preferences.FontSize);
        Assert.True(preferences.AutoSaveEnabled);
    }

    [Fact]
    public async Task ConfigurationService_SetAndGetUserPreferences_ShouldWork()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var preferences = new UserPreferences
        {
            UserId = userId,
            Theme = "Dark",
            FontSize = 16,
            AutoSaveEnabled = false,
            Language = "es"
        };

        // Act
        await _configurationService.SetUserPreferencesAsync(userId, preferences);
        var retrievedPreferences = await _configurationService.GetUserPreferencesAsync(userId);

        // Assert
        Assert.Equal("Dark", retrievedPreferences.Theme);
        Assert.Equal(16, retrievedPreferences.FontSize);
        Assert.False(retrievedPreferences.AutoSaveEnabled);
        Assert.Equal("es", retrievedPreferences.Language);
    }

    [Fact]
    public async Task ConfigurationService_GetBarcodeScannerSettings_ShouldReturnDefaults()
    {
        // Act
        var settings = await _configurationService.GetBarcodeScannerSettingsAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.True(settings.ScannerEnabled);
        Assert.Equal("Camera", settings.ScannerType);
        Assert.True(settings.BeepOnScanEnabled);
        Assert.Contains("EAN13", settings.SupportedFormats);
    }

    [Fact]
    public async Task ConfigurationService_GetPerformanceSettings_ShouldReturnDefaults()
    {
        // Act
        var settings = await _configurationService.GetPerformanceSettingsAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.Equal(10, settings.DatabaseConnectionPoolSize);
        Assert.Equal(50, settings.PageSize);
        Assert.True(settings.LazyLoadingEnabled);
    }

    [Fact]
    public async Task ConfigurationManagementService_ApplyBusinessTypeConfiguration_ShouldSetCorrectSettings()
    {
        // Arrange
        var shopId = Guid.NewGuid();

        // Act
        await _configurationManagementService.ApplyBusinessTypeConfigurationAsync(shopId, BusinessType.Grocery);
        var pricingSettings = await _configurationService.GetShopPricingSettingsAsync(shopId);
        var taxSettings = await _configurationService.GetShopTaxSettingsAsync(shopId);

        // Assert
        Assert.True(pricingSettings.WeightBasedPricingEnabled); // Grocery stores typically use weight-based pricing
        Assert.True(pricingSettings.BulkDiscountEnabled);
        Assert.Equal(5.0m, taxSettings.DefaultTaxRate);
        Assert.Contains("Food", taxSettings.CategoryTaxRates.Keys);
    }

    [Fact]
    public async Task ConfigurationManagementService_ExportAndImportConfiguration_ShouldWork()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var originalSettings = new ShopPricingSettings
        {
            WeightBasedPricingEnabled = false,
            BulkDiscountEnabled = true,
            BulkDiscountPercentage = 15.0m
        };
        await _configurationService.SetShopPricingSettingsAsync(shopId, originalSettings);

        // Act - Export
        var export = await _configurationManagementService.ExportShopConfigurationAsync(shopId);

        // Reset settings
        var resetSettings = new ShopPricingSettings();
        await _configurationService.SetShopPricingSettingsAsync(shopId, resetSettings);

        // Act - Import
        await _configurationManagementService.ImportShopConfigurationAsync(shopId, export);
        var importedSettings = await _configurationService.GetShopPricingSettingsAsync(shopId);

        // Assert
        Assert.NotNull(export);
        Assert.Equal(shopId, export.ShopId);
        Assert.False(importedSettings.WeightBasedPricingEnabled);
        Assert.True(importedSettings.BulkDiscountEnabled);
        Assert.Equal(15.0m, importedSettings.BulkDiscountPercentage);
    }

    [Fact]
    public async Task ConfigurationManagementService_ValidateConfiguration_ShouldDetectIssues()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var invalidSettings = new ShopPricingSettings
        {
            BulkDiscountEnabled = true,
            BulkDiscountPercentage = 75.0m, // Invalid - too high
            MinimumProfitMargin = -5.0m // Invalid - negative
        };
        await _configurationService.SetShopPricingSettingsAsync(shopId, invalidSettings);

        // Act
        var validation = await _configurationManagementService.ValidateShopConfigurationAsync(shopId);

        // Assert
        Assert.False(validation.IsValid);
        Assert.True(validation.ValidationResults.Count > 0);
        Assert.Contains(validation.ValidationResults, r => r.ErrorMessage.Contains("50%"));
        Assert.Contains(validation.ValidationResults, r => r.ErrorMessage.Contains("negative"));
    }

    [Fact]
    public async Task ConfigurationManagementService_GetRecommendations_ShouldProvideUsefulSuggestions()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var suboptimalSettings = new ShopPricingSettings
        {
            MembershipPricingEnabled = false, // Should recommend enabling
            BulkDiscountEnabled = true,
            BulkDiscountPercentage = 2.0m // Should recommend increasing
        };
        await _configurationService.SetShopPricingSettingsAsync(shopId, suboptimalSettings);

        var performanceSettings = new PerformanceSettings
        {
            LazyLoadingEnabled = false, // Should recommend enabling
            PageSize = 150 // Should recommend reducing
        };
        await _configurationService.SetPerformanceSettingsAsync(performanceSettings);

        // Act
        var recommendations = await _configurationManagementService.GetConfigurationRecommendationsAsync(shopId);

        // Assert
        Assert.NotNull(recommendations);
        Assert.True(recommendations.Recommendations.Count > 0);
        
        // Should recommend enabling membership pricing
        Assert.Contains(recommendations.Recommendations, r => 
            r.Category == "Pricing" && r.Title.Contains("Membership"));
        
        // Should recommend enabling lazy loading
        Assert.Contains(recommendations.Recommendations, r => 
            r.Category == "Performance" && r.Title.Contains("Lazy Loading"));
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}