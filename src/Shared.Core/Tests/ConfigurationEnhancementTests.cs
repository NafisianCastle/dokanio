using Microsoft.Extensions.DependencyInjection;
using Shared.Core.DependencyInjection;
using Shared.Core.DTOs;
using Shared.Core.Services;
using Xunit;

namespace Shared.Core.Tests;

/// <summary>
/// Tests for configuration enhancements added in task 18
/// </summary>
public class ConfigurationEnhancementTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IConfigurationService _configurationService;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly IConfigurationInitializationService _initializationService;

    public ConfigurationEnhancementTests()
    {
        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        _serviceProvider = services.BuildServiceProvider();
        
        _configurationService = _serviceProvider.GetRequiredService<IConfigurationService>();
        _featureFlagService = _serviceProvider.GetRequiredService<IFeatureFlagService>();
        _initializationService = _serviceProvider.GetRequiredService<IConfigurationInitializationService>();
    }

    [Fact]
    public async Task GetFeatureFlagSettings_ShouldReturnDefaultValues()
    {
        // Act
        var settings = await _configurationService.GetFeatureFlagSettingsAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.True(settings.EnhancedRealTimeCalculations);
        Assert.True(settings.AdvancedDiscountProcessing);
        Assert.False(settings.MultiCurrencySupport);
        Assert.False(settings.AIPoweredRecommendations);
    }

    [Fact]
    public async Task SetFeatureFlagSettings_ShouldPersistValues()
    {
        // Arrange
        var settings = new FeatureFlagSettings
        {
            EnhancedRealTimeCalculations = false,
            AdvancedDiscountProcessing = true,
            MultiCurrencySupport = true,
            AIPoweredRecommendations = false
        };

        // Act
        await _configurationService.SetFeatureFlagSettingsAsync(settings);
        var retrievedSettings = await _configurationService.GetFeatureFlagSettingsAsync();

        // Assert
        Assert.False(retrievedSettings.EnhancedRealTimeCalculations);
        Assert.True(retrievedSettings.AdvancedDiscountProcessing);
        Assert.True(retrievedSettings.MultiCurrencySupport);
        Assert.False(retrievedSettings.AIPoweredRecommendations);
    }

    [Fact]
    public async Task GetSalesPerformanceSettings_ShouldReturnDefaultValues()
    {
        // Act
        var settings = await _configurationService.GetSalesPerformanceSettingsAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.Equal(10, settings.MaxConcurrentSales);
        Assert.Equal(100, settings.CalculationTimeoutMs);
        Assert.Equal(50, settings.ValidationTimeoutMs);
        Assert.True(settings.EnableCalculationCaching);
        Assert.Equal(5, settings.CalculationCacheExpiryMinutes);
    }

    [Fact]
    public async Task SetSalesPerformanceSettings_ShouldPersistValues()
    {
        // Arrange
        var settings = new SalesPerformanceSettings
        {
            MaxConcurrentSales = 20,
            CalculationTimeoutMs = 150,
            ValidationTimeoutMs = 75,
            EnableCalculationCaching = false,
            CalculationCacheExpiryMinutes = 10
        };

        // Act
        await _configurationService.SetSalesPerformanceSettingsAsync(settings);
        var retrievedSettings = await _configurationService.GetSalesPerformanceSettingsAsync();

        // Assert
        Assert.Equal(20, retrievedSettings.MaxConcurrentSales);
        Assert.Equal(150, retrievedSettings.CalculationTimeoutMs);
        Assert.Equal(75, retrievedSettings.ValidationTimeoutMs);
        Assert.False(retrievedSettings.EnableCalculationCaching);
        Assert.Equal(10, retrievedSettings.CalculationCacheExpiryMinutes);
    }

    [Fact]
    public async Task GetUserCalculationPreferences_ShouldReturnDefaultValues()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var preferences = await _configurationService.GetUserCalculationPreferencesAsync(userId);

        // Assert
        Assert.NotNull(preferences);
        Assert.Equal(userId, preferences.UserId);
        Assert.Equal(2, preferences.DisplayPrecision);
        Assert.True(preferences.ShowCalculationBreakdown);
        Assert.True(preferences.EnableRealTimeUpdates);
        Assert.Equal("C2", preferences.PreferredCurrencyFormat);
    }

    [Fact]
    public async Task SetUserCalculationPreferences_ShouldPersistValues()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var preferences = new UserCalculationPreferences
        {
            UserId = userId,
            DisplayPrecision = 3,
            ShowCalculationBreakdown = false,
            EnableRealTimeUpdates = false,
            PreferredCurrencyFormat = "C3",
            UseCompactNumbers = true
        };

        // Act
        await _configurationService.SetUserCalculationPreferencesAsync(userId, preferences);
        var retrievedPreferences = await _configurationService.GetUserCalculationPreferencesAsync(userId);

        // Assert
        Assert.Equal(3, retrievedPreferences.DisplayPrecision);
        Assert.False(retrievedPreferences.ShowCalculationBreakdown);
        Assert.False(retrievedPreferences.EnableRealTimeUpdates);
        Assert.Equal("C3", retrievedPreferences.PreferredCurrencyFormat);
        Assert.True(retrievedPreferences.UseCompactNumbers);
    }

    [Fact]
    public async Task GetSalesCalculationPrecisionSettings_ShouldReturnDefaultValues()
    {
        // Arrange
        var shopId = Guid.NewGuid();

        // Act
        var settings = await _configurationService.GetSalesCalculationPrecisionSettingsAsync(shopId);

        // Assert
        Assert.NotNull(settings);
        Assert.Equal(2, settings.PricePrecision);
        Assert.Equal(3, settings.WeightPrecision);
        Assert.Equal(0, settings.QuantityPrecision);
        Assert.Equal(2, settings.TaxPrecision);
        Assert.Equal(RoundingMode.MidpointToEven, settings.RoundingMode);
        Assert.False(settings.UseHighPrecisionCalculations);
        Assert.Equal(6, settings.InternalCalculationPrecision);
    }

    [Fact]
    public async Task SetSalesCalculationPrecisionSettings_ShouldPersistValues()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var settings = new SalesCalculationPrecisionSettings
        {
            PricePrecision = 4,
            WeightPrecision = 2,
            QuantityPrecision = 1,
            TaxPrecision = 3,
            RoundingMode = RoundingMode.Up,
            UseHighPrecisionCalculations = true,
            InternalCalculationPrecision = 8
        };

        // Act
        await _configurationService.SetSalesCalculationPrecisionSettingsAsync(shopId, settings);
        var retrievedSettings = await _configurationService.GetSalesCalculationPrecisionSettingsAsync(shopId);

        // Assert
        Assert.Equal(4, retrievedSettings.PricePrecision);
        Assert.Equal(2, retrievedSettings.WeightPrecision);
        Assert.Equal(1, retrievedSettings.QuantityPrecision);
        Assert.Equal(3, retrievedSettings.TaxPrecision);
        Assert.Equal(RoundingMode.Up, retrievedSettings.RoundingMode);
        Assert.True(retrievedSettings.UseHighPrecisionCalculations);
        Assert.Equal(8, retrievedSettings.InternalCalculationPrecision);
    }

    [Fact]
    public async Task InitializeDefaultConfigurations_ShouldCompleteSuccessfully()
    {
        // Act & Assert - Should not throw
        await _initializationService.InitializeDefaultConfigurationsAsync();
    }

    [Fact]
    public async Task InitializeShopConfiguration_ShouldCompleteSuccessfully()
    {
        // Arrange
        var shopId = Guid.NewGuid();

        // Act & Assert - Should not throw
        await _initializationService.InitializeShopConfigurationAsync(shopId);
    }

    [Fact]
    public async Task InitializeUserConfiguration_ShouldCompleteSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act & Assert - Should not throw
        await _initializationService.InitializeUserConfigurationAsync(userId);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}