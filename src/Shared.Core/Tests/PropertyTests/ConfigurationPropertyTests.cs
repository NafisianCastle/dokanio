using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Shared.Core.Services;
using System.Globalization;
using Xunit;

namespace Shared.Core.Tests.PropertyTests;

/// <summary>
/// Property-based tests for configuration validation functionality
/// **Feature: offline-first-pos, Property 25: Configuration Validation**
/// **Validates: Requirements 16.7**
/// </summary>
public class ConfigurationPropertyTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;
    private readonly IConfigurationService _configurationService;
    private readonly IConfigurationRepository _configurationRepository;

    public ConfigurationPropertyTests()
    {
        var services = new ServiceCollection();
        
        // Add Entity Framework Core with In-Memory database
        services.AddDbContext<PosDbContext>(options =>
        {
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}");
            options.EnableSensitiveDataLogging(true);
        });
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning));
        
        // Register repositories and services
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        services.AddScoped<IConfigurationService, ConfigurationService>();
        
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        _configurationService = _serviceProvider.GetRequiredService<IConfigurationService>();
        _configurationRepository = _serviceProvider.GetRequiredService<IConfigurationRepository>();
        
        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    /// <summary>
    /// Property 25: Configuration Validation
    /// For any configuration change, the new value should be validated against the configuration type constraints 
    /// and business rules before being persisted
    /// **Validates: Requirements 16.7**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property ConfigurationValidation_ValidatesTypeConstraints()
    {
        return Prop.ForAll(
            GenerateSimpleConfigurationTestData(),
            testData =>
            {
                try
                {
                    // Act: Validate the configuration value
                    var validationResult = _configurationService.ValidateConfigurationAsync(
                        testData.Key, testData.Value, testData.Type).Result;

                    // Assert: Validation result should match expected validity
                    var expectedValidity = IsValueValidForType(testData.Value, testData.Type);
                    
                    if (expectedValidity)
                    {
                        return validationResult.IsValid;
                    }
                    else
                    {
                        return !validationResult.IsValid && !string.IsNullOrEmpty(validationResult.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    // Unexpected exceptions should not occur during validation
                    Console.WriteLine($"Unexpected exception during validation: {ex.Message}");
                    return false;
                }
            });
    }

    /// <summary>
    /// Property: Valid configurations can be set and retrieved
    /// For any valid configuration value, setting it should succeed and retrieving it should return the same value
    /// </summary>
    [Property(MaxTest = 20)]
    public Property ValidConfiguration_CanBeSetAndRetrieved()
    {
        return Prop.ForAll(
            GenerateValidConfigurationData(),
            testData =>
            {
                try
                {
                    // Arrange: Clear any existing configuration
                    var existing = _configurationRepository.GetByKeyAsync(testData.Key).Result;
                    if (existing != null)
                    {
                        _configurationRepository.DeleteAsync(existing.Id).Wait();
                        _configurationRepository.SaveChangesAsync().Wait();
                    }

                    // Act: Set the configuration
                    _configurationService.SetConfigurationAsync(testData.Key, testData.Value, 
                        testData.Description, testData.IsSystemLevel).Wait();

                    // Act: Retrieve the configuration
                    var retrievedValue = _configurationService.GetConfigurationAsync<object>(testData.Key).Result;

                    // Assert: Retrieved value should match the set value (accounting for type conversion)
                    return AreValuesEquivalent(testData.Value, retrievedValue, testData.Type);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected exception: {ex.Message}");
                    return false;
                }
            });
    }

    /// <summary>
    /// Property: Invalid configurations are rejected
    /// For any invalid configuration value, attempting to set it should fail with appropriate error message
    /// </summary>
    [Property(MaxTest = 20)]
    public Property InvalidConfiguration_IsRejected()
    {
        return Prop.ForAll(
            GenerateInvalidConfigurationData(),
            testData =>
            {
                try
                {
                    // Act: Attempt to set invalid configuration
                    _configurationService.SetConfigurationAsync(testData.Key, testData.Value, 
                        testData.Description, testData.IsSystemLevel).Wait();

                    // If we get here without exception, the test failed
                    return false;
                }
                catch (AggregateException ex) when (ex.InnerException is ArgumentException)
                {
                    // Expected exception for invalid configuration
                    return true;
                }
                catch (ArgumentException)
                {
                    // Expected exception for invalid configuration
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected exception type: {ex.GetType().Name} - {ex.Message}");
                    return false;
                }
            });
    }

    /// <summary>
    /// Property: Configuration type detection works correctly
    /// For any value, the detected configuration type should be appropriate for that value type
    /// </summary>
    [Property(MaxTest = 20)]
    public Property ConfigurationTypeDetection_IsCorrect()
    {
        return Prop.ForAll(
            GenerateTypedValues(),
            testData =>
            {
                try
                {
                    // Act: Validate with detected type
                    var validationResult = _configurationService.ValidateConfigurationAsync(
                        "test.key", testData.Value, testData.ExpectedType).Result;

                    // Assert: Validation should succeed for correctly typed values
                    return validationResult.IsValid;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception during type detection test: {ex.Message}");
                    return false;
                }
            });
    }

    private static Arbitrary<ConfigurationTestData> GenerateSimpleConfigurationTestData()
    {
        var testData = new[]
        {
            // Valid configurations
            new ConfigurationTestData { Key = "Currency.Code", Value = "USD", Type = ConfigurationType.String, Description = "Currency code", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Tax.Rate", Value = "15.5", Type = ConfigurationType.Percentage, Description = "Tax rate", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Business.Name", Value = "Test Shop", Type = ConfigurationType.String, Description = "Business name", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Currency.DecimalPlaces", Value = "2", Type = ConfigurationType.Number, Description = "Decimal places", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Tax.Enabled", Value = "true", Type = ConfigurationType.Boolean, Description = "Tax enabled", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Product.Price", Value = "99.99", Type = ConfigurationType.Currency, Description = "Product price", IsSystemLevel = false },
            
            // Invalid configurations
            new ConfigurationTestData { Key = "Tax.Rate", Value = "150", Type = ConfigurationType.Percentage, Description = "Invalid percentage > 100", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Tax.Rate", Value = "-10", Type = ConfigurationType.Percentage, Description = "Invalid negative percentage", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Currency.DecimalPlaces", Value = "10", Type = ConfigurationType.Number, Description = "Too many decimal places", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Test.Boolean", Value = "maybe", Type = ConfigurationType.Boolean, Description = "Invalid boolean", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Test.Number", Value = "not-a-number", Type = ConfigurationType.Number, Description = "Invalid number", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Test.Currency", Value = "-50.00", Type = ConfigurationType.Currency, Description = "Negative currency", IsSystemLevel = true }
        };

        return Gen.Elements(testData).ToArbitrary();
    }

    private static Arbitrary<ConfigurationTestData> GenerateValidConfigurationData()
    {
        var validConfigurations = new[]
        {
            new ConfigurationTestData { Key = "Currency.Code", Value = "USD", Type = ConfigurationType.String, Description = "Currency code", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Tax.Rate", Value = "15.5", Type = ConfigurationType.Percentage, Description = "Tax rate", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Business.Name", Value = "Test Shop", Type = ConfigurationType.String, Description = "Business name", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Currency.DecimalPlaces", Value = "2", Type = ConfigurationType.Number, Description = "Decimal places", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Tax.Enabled", Value = "true", Type = ConfigurationType.Boolean, Description = "Tax enabled", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Product.Price", Value = "99.99", Type = ConfigurationType.Currency, Description = "Product price", IsSystemLevel = false }
        };

        return Gen.Elements(validConfigurations).ToArbitrary();
    }

    private static Arbitrary<ConfigurationTestData> GenerateInvalidConfigurationData()
    {
        var invalidConfigurations = new[]
        {
            new ConfigurationTestData { Key = "Tax.Rate", Value = "150", Type = ConfigurationType.Percentage, Description = "Invalid percentage > 100", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Tax.Rate", Value = "-10", Type = ConfigurationType.Percentage, Description = "Invalid negative percentage", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Currency.DecimalPlaces", Value = "10", Type = ConfigurationType.Number, Description = "Too many decimal places", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Currency.DecimalPlaces", Value = "-1", Type = ConfigurationType.Number, Description = "Negative decimal places", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Business.Email", Value = "invalid-email", Type = ConfigurationType.String, Description = "Invalid email format", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Test.Boolean", Value = "maybe", Type = ConfigurationType.Boolean, Description = "Invalid boolean", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Test.Number", Value = "not-a-number", Type = ConfigurationType.Number, Description = "Invalid number", IsSystemLevel = true },
            new ConfigurationTestData { Key = "Test.Currency", Value = "-50.00", Type = ConfigurationType.Currency, Description = "Negative currency", IsSystemLevel = true }
        };

        return Gen.Elements(invalidConfigurations).ToArbitrary();
    }

    private static Arbitrary<TypedValueTestData> GenerateTypedValues()
    {
        var typedValues = new[]
        {
            new TypedValueTestData { Value = "test string", ExpectedType = ConfigurationType.String },
            new TypedValueTestData { Value = "123", ExpectedType = ConfigurationType.Number },
            new TypedValueTestData { Value = "true", ExpectedType = ConfigurationType.Boolean },
            new TypedValueTestData { Value = "false", ExpectedType = ConfigurationType.Boolean },
            new TypedValueTestData { Value = "99.99", ExpectedType = ConfigurationType.Currency },
            new TypedValueTestData { Value = "25.5", ExpectedType = ConfigurationType.Percentage }
        };

        return Gen.Elements(typedValues).ToArbitrary();
    }

    private static bool IsValueValidForType(object value, ConfigurationType type)
    {
        var stringValue = value?.ToString() ?? "";

        return type switch
        {
            ConfigurationType.String => true, // All values can be strings
            ConfigurationType.Number => decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            ConfigurationType.Boolean => bool.TryParse(stringValue, out _),
            ConfigurationType.Currency => decimal.TryParse(stringValue, NumberStyles.Currency, CultureInfo.InvariantCulture, out var currencyValue) && currencyValue >= 0,
            ConfigurationType.Percentage => decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var percentValue) && percentValue >= 0 && percentValue <= 100,
            _ => false
        };
    }

    private static bool AreValuesEquivalent(object originalValue, object retrievedValue, ConfigurationType type)
    {
        if (originalValue == null && retrievedValue == null)
            return true;

        if (originalValue == null || retrievedValue == null)
            return false;

        var originalString = originalValue.ToString();
        var retrievedString = retrievedValue.ToString();

        return type switch
        {
            ConfigurationType.String => originalString == retrievedString,
            ConfigurationType.Boolean => bool.TryParse(originalString, out var origBool) && 
                                        bool.TryParse(retrievedString, out var retBool) && 
                                        origBool == retBool,
            ConfigurationType.Number or ConfigurationType.Currency or ConfigurationType.Percentage => 
                decimal.TryParse(originalString, NumberStyles.Any, CultureInfo.InvariantCulture, out var origDecimal) &&
                decimal.TryParse(retrievedString, NumberStyles.Any, CultureInfo.InvariantCulture, out var retDecimal) &&
                Math.Abs(origDecimal - retDecimal) < 0.001m,
            _ => originalString == retrievedString
        };
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }

    private class ConfigurationTestData
    {
        public string Key { get; set; } = string.Empty;
        public object Value { get; set; } = string.Empty;
        public ConfigurationType Type { get; set; }
        public string? Description { get; set; }
        public bool IsSystemLevel { get; set; }
    }

    private class TypedValueTestData
    {
        public object Value { get; set; } = string.Empty;
        public ConfigurationType ExpectedType { get; set; }
    }
}