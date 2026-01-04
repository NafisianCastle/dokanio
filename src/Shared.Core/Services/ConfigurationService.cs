using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using System.Globalization;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// Service implementation for managing system configuration
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly IConfigurationRepository _configurationRepository;
    private readonly ILogger<ConfigurationService> _logger;
    private readonly Dictionary<string, (object defaultValue, ConfigurationType type, string description)> _defaultConfigurations;

    public ConfigurationService(
        IConfigurationRepository configurationRepository,
        ILogger<ConfigurationService> logger)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _defaultConfigurations = InitializeDefaultConfigurationDefinitions();
    }

    /// <summary>
    /// Gets a configuration value with type conversion
    /// </summary>
    /// <typeparam name="T">Type to convert the value to</typeparam>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if configuration not found</param>
    /// <returns>Configuration value or default</returns>
    public async Task<T> GetConfigurationAsync<T>(string key, T defaultValue = default!)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Configuration key cannot be null or empty", nameof(key));

        try
        {
            var config = await _configurationRepository.GetByKeyAsync(key);
            if (config == null)
            {
                _logger.LogDebug("Configuration {Key} not found, returning default value", key);
                return defaultValue;
            }

            return ConvertValue<T>(config.Value, config.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting configuration {Key}, returning default value", key);
            return defaultValue;
        }
    }

    /// <summary>
    /// Sets a configuration value with automatic type detection
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>
    /// <param name="key">Configuration key</param>
    /// <param name="value">Configuration value</param>
    /// <param name="description">Optional description</param>
    /// <param name="isSystemLevel">Whether this is a system-level configuration</param>
    /// <returns>Task</returns>
    public async Task SetConfigurationAsync<T>(string key, T value, string? description = null, bool isSystemLevel = false)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Configuration key cannot be null or empty", nameof(key));

        if (value == null)
            throw new ArgumentNullException(nameof(value));

        try
        {
            var stringValue = ConvertToString(value);
            var configurationType = DetectConfigurationType<T>();
            
            // Validate the value
            var validationResult = await ValidateConfigurationAsync(key, value, configurationType);
            if (!validationResult.IsValid)
            {
                throw new ArgumentException($"Invalid configuration value: {validationResult.ErrorMessage}");
            }

            await _configurationRepository.SetConfigurationAsync(key, stringValue, configurationType, description, isSystemLevel);
            
            _logger.LogInformation("Configuration {Key} set to {Value}", key, stringValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting configuration {Key} = {Value}", key, value);
            throw;
        }
    }

    /// <summary>
    /// Gets currency settings
    /// </summary>
    /// <returns>Currency settings</returns>
    public async Task<CurrencySettings> GetCurrencySettingsAsync()
    {
        return new CurrencySettings
        {
            CurrencyCode = await GetConfigurationAsync("Currency.Code", "USD"),
            CurrencySymbol = await GetConfigurationAsync("Currency.Symbol", "$"),
            DecimalPlaces = await GetConfigurationAsync("Currency.DecimalPlaces", 2),
            DecimalSeparator = await GetConfigurationAsync("Currency.DecimalSeparator", "."),
            ThousandsSeparator = await GetConfigurationAsync("Currency.ThousandsSeparator", ","),
            SymbolBeforeAmount = await GetConfigurationAsync("Currency.SymbolBeforeAmount", true)
        };
    }

    /// <summary>
    /// Gets tax settings
    /// </summary>
    /// <returns>Tax settings</returns>
    public async Task<TaxSettings> GetTaxSettingsAsync()
    {
        return new TaxSettings
        {
            TaxEnabled = await GetConfigurationAsync("Tax.Enabled", true),
            DefaultTaxRate = await GetConfigurationAsync("Tax.DefaultRate", 0.0m),
            TaxName = await GetConfigurationAsync("Tax.Name", "Tax"),
            TaxIncludedInPrice = await GetConfigurationAsync("Tax.IncludedInPrice", false),
            ShowTaxOnReceipt = await GetConfigurationAsync("Tax.ShowOnReceipt", true)
        };
    }

    /// <summary>
    /// Gets business settings
    /// </summary>
    /// <returns>Business settings</returns>
    public async Task<BusinessSettings> GetBusinessSettingsAsync()
    {
        return new BusinessSettings
        {
            BusinessName = await GetConfigurationAsync("Business.Name", ""),
            BusinessAddress = await GetConfigurationAsync("Business.Address", ""),
            BusinessPhone = await GetConfigurationAsync("Business.Phone", ""),
            BusinessEmail = await GetConfigurationAsync("Business.Email", ""),
            BusinessWebsite = await GetConfigurationAsync("Business.Website", ""),
            BusinessLogo = await GetConfigurationAsync("Business.Logo", ""),
            ReceiptFooter = await GetConfigurationAsync("Business.ReceiptFooter", "Thank you for your business!")
        };
    }

    /// <summary>
    /// Gets localization settings
    /// </summary>
    /// <returns>Localization settings</returns>
    public async Task<LocalizationSettings> GetLocalizationSettingsAsync()
    {
        return new LocalizationSettings
        {
            Language = await GetConfigurationAsync("Localization.Language", "en"),
            Country = await GetConfigurationAsync("Localization.Country", "US"),
            TimeZone = await GetConfigurationAsync("Localization.TimeZone", "UTC"),
            DateFormat = await GetConfigurationAsync("Localization.DateFormat", "MM/dd/yyyy"),
            TimeFormat = await GetConfigurationAsync("Localization.TimeFormat", "HH:mm:ss"),
            NumberFormat = await GetConfigurationAsync("Localization.NumberFormat", "N2")
        };
    }

    /// <summary>
    /// Validates a configuration value against its type
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="value">Value to validate</param>
    /// <param name="type">Expected configuration type</param>
    /// <returns>Validation result</returns>
    public async Task<ConfigurationValidationResult> ValidateConfigurationAsync(string key, object value, ConfigurationType type)
    {
        if (value == null)
        {
            return new ConfigurationValidationResult
            {
                IsValid = false,
                ErrorMessage = "Configuration value cannot be null"
            };
        }

        try
        {
            object? parsedValue = null;
            var stringValue = value.ToString() ?? "";

            switch (type)
            {
                case ConfigurationType.String:
                    parsedValue = stringValue;
                    break;

                case ConfigurationType.Number:
                    if (!decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var numberValue))
                    {
                        return new ConfigurationValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = "Value must be a valid number"
                        };
                    }
                    parsedValue = numberValue;
                    break;

                case ConfigurationType.Boolean:
                    if (!bool.TryParse(stringValue, out var boolValue))
                    {
                        return new ConfigurationValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = "Value must be true or false"
                        };
                    }
                    parsedValue = boolValue;
                    break;

                case ConfigurationType.Currency:
                    if (!decimal.TryParse(stringValue, NumberStyles.Currency, CultureInfo.InvariantCulture, out var currencyValue) || currencyValue < 0)
                    {
                        return new ConfigurationValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = "Value must be a valid non-negative currency amount"
                        };
                    }
                    parsedValue = currencyValue;
                    break;

                case ConfigurationType.Percentage:
                    if (!decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var percentValue) || 
                        percentValue < 0 || percentValue > 100)
                    {
                        return new ConfigurationValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = "Value must be a percentage between 0 and 100"
                        };
                    }
                    parsedValue = percentValue;
                    break;

                default:
                    return new ConfigurationValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Unknown configuration type"
                    };
            }

            // Additional key-specific validation
            var keyValidationResult = await ValidateKeySpecificRules(key, parsedValue);
            if (!keyValidationResult.IsValid)
            {
                return keyValidationResult;
            }

            return new ConfigurationValidationResult
            {
                IsValid = true,
                ParsedValue = parsedValue
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating configuration {Key} = {Value}", key, value);
            return new ConfigurationValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Validation error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets all system configurations
    /// </summary>
    /// <returns>List of system configurations</returns>
    public async Task<IEnumerable<ConfigurationDto>> GetSystemConfigurationsAsync()
    {
        try
        {
            var configurations = await _configurationRepository.GetSystemConfigurationsAsync();
            return configurations.Select(c => new ConfigurationDto
            {
                Id = c.Id,
                Key = c.Key,
                Value = c.Value,
                Type = c.Type,
                Description = c.Description,
                IsSystemLevel = c.IsSystemLevel,
                UpdatedAt = c.UpdatedAt,
                DeviceId = c.DeviceId,
                SyncStatus = c.SyncStatus
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system configurations");
            throw;
        }
    }

    /// <summary>
    /// Resets a configuration to its default value
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <returns>Task</returns>
    public async Task ResetConfigurationAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Configuration key cannot be null or empty", nameof(key));

        try
        {
            if (_defaultConfigurations.TryGetValue(key, out var defaultConfig))
            {
                var stringValue = ConvertToString(defaultConfig.defaultValue);
                await _configurationRepository.SetConfigurationAsync(key, stringValue, defaultConfig.type, defaultConfig.description, true);
                
                _logger.LogInformation("Configuration {Key} reset to default value {Value}", key, stringValue);
            }
            else
            {
                _logger.LogWarning("No default configuration found for key {Key}", key);
                throw new ArgumentException($"No default configuration found for key: {key}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting configuration {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// Initializes default system configurations
    /// </summary>
    /// <returns>Task</returns>
    public async Task InitializeDefaultConfigurationsAsync()
    {
        try
        {
            _logger.LogInformation("Initializing default system configurations");

            foreach (var (key, (defaultValue, type, description)) in _defaultConfigurations)
            {
                var existing = await _configurationRepository.GetByKeyAsync(key);
                if (existing == null)
                {
                    var stringValue = ConvertToString(defaultValue);
                    await _configurationRepository.SetConfigurationAsync(key, stringValue, type, description, true);
                    _logger.LogDebug("Initialized default configuration {Key} = {Value}", key, stringValue);
                }
            }

            _logger.LogInformation("Default system configurations initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing default configurations");
            throw;
        }
    }

    private Dictionary<string, (object defaultValue, ConfigurationType type, string description)> InitializeDefaultConfigurationDefinitions()
    {
        return new Dictionary<string, (object, ConfigurationType, string)>
        {
            // Currency settings
            { "Currency.Code", ("USD", ConfigurationType.String, "Currency code (ISO 4217)") },
            { "Currency.Symbol", ("$", ConfigurationType.String, "Currency symbol") },
            { "Currency.DecimalPlaces", (2, ConfigurationType.Number, "Number of decimal places for currency") },
            { "Currency.DecimalSeparator", (".", ConfigurationType.String, "Decimal separator character") },
            { "Currency.ThousandsSeparator", (",", ConfigurationType.String, "Thousands separator character") },
            { "Currency.SymbolBeforeAmount", (true, ConfigurationType.Boolean, "Whether to show currency symbol before amount") },

            // Tax settings
            { "Tax.Enabled", (true, ConfigurationType.Boolean, "Whether tax calculation is enabled") },
            { "Tax.DefaultRate", (0.0m, ConfigurationType.Percentage, "Default tax rate percentage") },
            { "Tax.Name", ("Tax", ConfigurationType.String, "Display name for tax") },
            { "Tax.IncludedInPrice", (false, ConfigurationType.Boolean, "Whether tax is included in product prices") },
            { "Tax.ShowOnReceipt", (true, ConfigurationType.Boolean, "Whether to show tax details on receipt") },

            // Business settings
            { "Business.Name", ("", ConfigurationType.String, "Business name") },
            { "Business.Address", ("", ConfigurationType.String, "Business address") },
            { "Business.Phone", ("", ConfigurationType.String, "Business phone number") },
            { "Business.Email", ("", ConfigurationType.String, "Business email address") },
            { "Business.Website", ("", ConfigurationType.String, "Business website URL") },
            { "Business.Logo", ("", ConfigurationType.String, "Business logo path or URL") },
            { "Business.ReceiptFooter", ("Thank you for your business!", ConfigurationType.String, "Footer text for receipts") },

            // Localization settings
            { "Localization.Language", ("en", ConfigurationType.String, "Application language code") },
            { "Localization.Country", ("US", ConfigurationType.String, "Country code") },
            { "Localization.TimeZone", ("UTC", ConfigurationType.String, "Time zone identifier") },
            { "Localization.DateFormat", ("MM/dd/yyyy", ConfigurationType.String, "Date format pattern") },
            { "Localization.TimeFormat", ("HH:mm:ss", ConfigurationType.String, "Time format pattern") },
            { "Localization.NumberFormat", ("N2", ConfigurationType.String, "Number format pattern") }
        };
    }

    private T ConvertValue<T>(string value, ConfigurationType type)
    {
        if (typeof(T) == typeof(string))
        {
            return (T)(object)value;
        }

        switch (type)
        {
            case ConfigurationType.Number:
            case ConfigurationType.Currency:
            case ConfigurationType.Percentage:
                if (typeof(T) == typeof(decimal) || typeof(T) == typeof(decimal?))
                {
                    return (T)(object)decimal.Parse(value, CultureInfo.InvariantCulture);
                }
                if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
                {
                    return (T)(object)int.Parse(value, CultureInfo.InvariantCulture);
                }
                if (typeof(T) == typeof(double) || typeof(T) == typeof(double?))
                {
                    return (T)(object)double.Parse(value, CultureInfo.InvariantCulture);
                }
                break;

            case ConfigurationType.Boolean:
                if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
                {
                    return (T)(object)bool.Parse(value);
                }
                break;
        }

        // Fallback to JSON deserialization for complex types
        try
        {
            return JsonSerializer.Deserialize<T>(value) ?? default!;
        }
        catch
        {
            // Final fallback - try direct conversion
            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }
    }

    private string ConvertToString<T>(T value)
    {
        if (value == null)
            return string.Empty;

        if (value is string stringValue)
            return stringValue;

        if (value is decimal || value is double || value is float)
            return value.ToString()!;

        if (value is bool boolValue)
            return boolValue.ToString().ToLowerInvariant();

        if (value is int || value is long || value is short)
            return value.ToString()!;

        // For complex types, use JSON serialization
        try
        {
            return JsonSerializer.Serialize(value);
        }
        catch
        {
            return value.ToString() ?? string.Empty;
        }
    }

    private ConfigurationType DetectConfigurationType<T>()
    {
        var type = typeof(T);
        
        if (type == typeof(string))
            return ConfigurationType.String;
        
        if (type == typeof(bool) || type == typeof(bool?))
            return ConfigurationType.Boolean;
        
        if (type == typeof(decimal) || type == typeof(decimal?) ||
            type == typeof(double) || type == typeof(double?) ||
            type == typeof(float) || type == typeof(float?) ||
            type == typeof(int) || type == typeof(int?) ||
            type == typeof(long) || type == typeof(long?) ||
            type == typeof(short) || type == typeof(short?))
            return ConfigurationType.Number;
        
        return ConfigurationType.String; // Default fallback
    }

    private async Task<ConfigurationValidationResult> ValidateKeySpecificRules(string key, object? value)
    {
        // Add key-specific validation rules here
        switch (key)
        {
            case "Currency.DecimalPlaces":
                if (value is int decimalPlaces && (decimalPlaces < 0 || decimalPlaces > 4))
                {
                    return new ConfigurationValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Decimal places must be between 0 and 4"
                    };
                }
                break;

            case "Tax.DefaultRate":
                if (value is decimal taxRate && (taxRate < 0 || taxRate > 100))
                {
                    return new ConfigurationValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Tax rate must be between 0 and 100 percent"
                    };
                }
                break;

            case "Business.Email":
                if (value is string email && !string.IsNullOrEmpty(email))
                {
                    if (!IsValidEmail(email))
                    {
                        return new ConfigurationValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = "Invalid email address format"
                        };
                    }
                }
                break;
        }

        return new ConfigurationValidationResult { IsValid = true };
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}