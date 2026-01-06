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
                ShopId = c.ShopId,
                UserId = c.UserId,
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

        // Sanitize user-provided key before logging to prevent log forging
        var safeKey = key.Replace("\r", string.Empty).Replace("\n", string.Empty);

        try
        {
            if (_defaultConfigurations.TryGetValue(key, out var defaultConfig))
            {
                var stringValue = ConvertToString(defaultConfig.defaultValue);
                await _configurationRepository.SetConfigurationAsync(key, stringValue, defaultConfig.type, defaultConfig.description, true);
                
                _logger.LogInformation("Configuration {Key} reset to default value {Value}", safeKey, stringValue);
            }
            else
            {
                _logger.LogWarning("No default configuration found for key {Key}", safeKey);
                throw new ArgumentException($"No default configuration found for key: {key}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting configuration {Key}", safeKey);
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

    /// <summary>
    /// Gets shop-level pricing settings
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Shop pricing settings</returns>
    public async Task<ShopPricingSettings> GetShopPricingSettingsAsync(Guid shopId)
    {
        return new ShopPricingSettings
        {
            WeightBasedPricingEnabled = await GetShopConfigurationAsync(shopId, "Pricing.WeightBasedPricingEnabled", true),
            BulkDiscountEnabled = await GetShopConfigurationAsync(shopId, "Pricing.BulkDiscountEnabled", false),
            BulkDiscountThreshold = await GetShopConfigurationAsync(shopId, "Pricing.BulkDiscountThreshold", 10.0m),
            BulkDiscountPercentage = await GetShopConfigurationAsync(shopId, "Pricing.BulkDiscountPercentage", 5.0m),
            MembershipPricingEnabled = await GetShopConfigurationAsync(shopId, "Pricing.MembershipPricingEnabled", true),
            DynamicPricingEnabled = await GetShopConfigurationAsync(shopId, "Pricing.DynamicPricingEnabled", false),
            MinimumProfitMargin = await GetShopConfigurationAsync(shopId, "Pricing.MinimumProfitMargin", 10.0m),
            RoundingEnabled = await GetShopConfigurationAsync(shopId, "Pricing.RoundingEnabled", true),
            RoundingPrecision = await GetShopConfigurationAsync(shopId, "Pricing.RoundingPrecision", 0.05m)
        };
    }

    /// <summary>
    /// Sets shop-level pricing settings
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="settings">Pricing settings</param>
    /// <returns>Task</returns>
    public async Task SetShopPricingSettingsAsync(Guid shopId, ShopPricingSettings settings)
    {
        await SetShopConfigurationAsync(shopId, "Pricing.WeightBasedPricingEnabled", settings.WeightBasedPricingEnabled, "Enable weight-based pricing");
        await SetShopConfigurationAsync(shopId, "Pricing.BulkDiscountEnabled", settings.BulkDiscountEnabled, "Enable bulk discounts");
        await SetShopConfigurationAsync(shopId, "Pricing.BulkDiscountThreshold", settings.BulkDiscountThreshold, "Bulk discount threshold quantity");
        await SetShopConfigurationAsync(shopId, "Pricing.BulkDiscountPercentage", settings.BulkDiscountPercentage, "Bulk discount percentage");
        await SetShopConfigurationAsync(shopId, "Pricing.MembershipPricingEnabled", settings.MembershipPricingEnabled, "Enable membership pricing");
        await SetShopConfigurationAsync(shopId, "Pricing.DynamicPricingEnabled", settings.DynamicPricingEnabled, "Enable dynamic pricing");
        await SetShopConfigurationAsync(shopId, "Pricing.MinimumProfitMargin", settings.MinimumProfitMargin, "Minimum profit margin percentage");
        await SetShopConfigurationAsync(shopId, "Pricing.RoundingEnabled", settings.RoundingEnabled, "Enable price rounding");
        await SetShopConfigurationAsync(shopId, "Pricing.RoundingPrecision", settings.RoundingPrecision, "Price rounding precision");
    }

    /// <summary>
    /// Gets shop-level tax settings
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Shop tax settings</returns>
    public async Task<ShopTaxSettings> GetShopTaxSettingsAsync(Guid shopId)
    {
        var categoryTaxRatesJson = await GetShopConfigurationAsync(shopId, "Tax.CategoryTaxRates", "{}");
        var taxRulesJson = await GetShopConfigurationAsync(shopId, "Tax.TaxRules", "[]");

        Dictionary<string, decimal> categoryTaxRates;
        List<TaxRule> taxRules;

        try
        {
            categoryTaxRates = JsonSerializer.Deserialize<Dictionary<string, decimal>>(categoryTaxRatesJson) ?? new();
        }
        catch
        {
            categoryTaxRates = new Dictionary<string, decimal>();
        }

        try
        {
            taxRules = JsonSerializer.Deserialize<List<TaxRule>>(taxRulesJson) ?? new();
        }
        catch
        {
            taxRules = new List<TaxRule>();
        }

        return new ShopTaxSettings
        {
            TaxEnabled = await GetShopConfigurationAsync(shopId, "Tax.Enabled", true),
            DefaultTaxRate = await GetShopConfigurationAsync(shopId, "Tax.DefaultRate", 0.0m),
            TaxName = await GetShopConfigurationAsync(shopId, "Tax.Name", "Tax"),
            TaxIncludedInPrice = await GetShopConfigurationAsync(shopId, "Tax.IncludedInPrice", false),
            ShowTaxOnReceipt = await GetShopConfigurationAsync(shopId, "Tax.ShowOnReceipt", true),
            CategoryTaxRates = categoryTaxRates,
            CompoundTaxEnabled = await GetShopConfigurationAsync(shopId, "Tax.CompoundTaxEnabled", false),
            TaxRules = taxRules
        };
    }

    /// <summary>
    /// Sets shop-level tax settings
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="settings">Tax settings</param>
    /// <returns>Task</returns>
    public async Task SetShopTaxSettingsAsync(Guid shopId, ShopTaxSettings settings)
    {
        await SetShopConfigurationAsync(shopId, "Tax.Enabled", settings.TaxEnabled, "Enable tax calculation");
        await SetShopConfigurationAsync(shopId, "Tax.DefaultRate", settings.DefaultTaxRate, "Default tax rate percentage");
        await SetShopConfigurationAsync(shopId, "Tax.Name", settings.TaxName, "Tax display name");
        await SetShopConfigurationAsync(shopId, "Tax.IncludedInPrice", settings.TaxIncludedInPrice, "Tax included in product prices");
        await SetShopConfigurationAsync(shopId, "Tax.ShowOnReceipt", settings.ShowTaxOnReceipt, "Show tax details on receipt");
        await SetShopConfigurationAsync(shopId, "Tax.CompoundTaxEnabled", settings.CompoundTaxEnabled, "Enable compound tax calculation");
        
        var categoryTaxRatesJson = JsonSerializer.Serialize(settings.CategoryTaxRates);
        await SetShopConfigurationAsync(shopId, "Tax.CategoryTaxRates", categoryTaxRatesJson, "Category-specific tax rates");
        
        var taxRulesJson = JsonSerializer.Serialize(settings.TaxRules);
        await SetShopConfigurationAsync(shopId, "Tax.TaxRules", taxRulesJson, "Custom tax rules");
    }

    /// <summary>
    /// Gets user preferences for UI customization
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>User preferences</returns>
    public async Task<UserPreferences> GetUserPreferencesAsync(Guid userId)
    {
        var customSettingsJson = await GetUserConfigurationAsync(userId, "UI.CustomSettings", "{}");
        Dictionary<string, object> customSettings;

        try
        {
            customSettings = JsonSerializer.Deserialize<Dictionary<string, object>>(customSettingsJson) ?? new();
        }
        catch
        {
            customSettings = new Dictionary<string, object>();
        }

        return new UserPreferences
        {
            UserId = userId,
            Theme = await GetUserConfigurationAsync(userId, "UI.Theme", "Light"),
            AccentColor = await GetUserConfigurationAsync(userId, "UI.AccentColor", "#0078D4"),
            FontSize = await GetUserConfigurationAsync(userId, "UI.FontSize", 14),
            FontFamily = await GetUserConfigurationAsync(userId, "UI.FontFamily", "Segoe UI"),
            HighContrastMode = await GetUserConfigurationAsync(userId, "UI.HighContrastMode", false),
            ReducedMotion = await GetUserConfigurationAsync(userId, "UI.ReducedMotion", false),
            DefaultView = await GetUserConfigurationAsync(userId, "UI.DefaultView", "Sales"),
            ShowTooltips = await GetUserConfigurationAsync(userId, "UI.ShowTooltips", true),
            AutoSaveEnabled = await GetUserConfigurationAsync(userId, "UI.AutoSaveEnabled", true),
            AutoSaveInterval = await GetUserConfigurationAsync(userId, "UI.AutoSaveInterval", 30),
            SoundEnabled = await GetUserConfigurationAsync(userId, "UI.SoundEnabled", true),
            HapticFeedbackEnabled = await GetUserConfigurationAsync(userId, "UI.HapticFeedbackEnabled", true),
            Language = await GetUserConfigurationAsync(userId, "UI.Language", "en"),
            CustomSettings = customSettings
        };
    }

    /// <summary>
    /// Sets user preferences for UI customization
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="preferences">User preferences</param>
    /// <returns>Task</returns>
    public async Task SetUserPreferencesAsync(Guid userId, UserPreferences preferences)
    {
        await SetUserConfigurationAsync(userId, "UI.Theme", preferences.Theme, "UI theme");
        await SetUserConfigurationAsync(userId, "UI.AccentColor", preferences.AccentColor, "UI accent color");
        await SetUserConfigurationAsync(userId, "UI.FontSize", preferences.FontSize, "UI font size");
        await SetUserConfigurationAsync(userId, "UI.FontFamily", preferences.FontFamily, "UI font family");
        await SetUserConfigurationAsync(userId, "UI.HighContrastMode", preferences.HighContrastMode, "High contrast mode");
        await SetUserConfigurationAsync(userId, "UI.ReducedMotion", preferences.ReducedMotion, "Reduced motion mode");
        await SetUserConfigurationAsync(userId, "UI.DefaultView", preferences.DefaultView, "Default view on startup");
        await SetUserConfigurationAsync(userId, "UI.ShowTooltips", preferences.ShowTooltips, "Show tooltips");
        await SetUserConfigurationAsync(userId, "UI.AutoSaveEnabled", preferences.AutoSaveEnabled, "Enable auto-save");
        await SetUserConfigurationAsync(userId, "UI.AutoSaveInterval", preferences.AutoSaveInterval, "Auto-save interval in seconds");
        await SetUserConfigurationAsync(userId, "UI.SoundEnabled", preferences.SoundEnabled, "Enable sound effects");
        await SetUserConfigurationAsync(userId, "UI.HapticFeedbackEnabled", preferences.HapticFeedbackEnabled, "Enable haptic feedback");
        await SetUserConfigurationAsync(userId, "UI.Language", preferences.Language, "User interface language");
        
        var customSettingsJson = JsonSerializer.Serialize(preferences.CustomSettings);
        await SetUserConfigurationAsync(userId, "UI.CustomSettings", customSettingsJson, "Custom user settings");
    }

    /// <summary>
    /// Gets barcode scanner configuration
    /// </summary>
    /// <param name="deviceId">Device identifier (optional, uses current device if null)</param>
    /// <returns>Barcode scanner settings</returns>
    public async Task<BarcodeScannerSettings> GetBarcodeScannerSettingsAsync(Guid? deviceId = null)
    {
        var supportedFormatsJson = await GetConfigurationAsync("BarcodeScanner.SupportedFormats", "[\"EAN13\",\"EAN8\",\"UPC\",\"Code128\",\"Code39\"]");
        List<string> supportedFormats;

        try
        {
            supportedFormats = JsonSerializer.Deserialize<List<string>>(supportedFormatsJson) ?? new() { "EAN13", "EAN8", "UPC", "Code128", "Code39" };
        }
        catch
        {
            supportedFormats = new List<string> { "EAN13", "EAN8", "UPC", "Code128", "Code39" };
        }

        return new BarcodeScannerSettings
        {
            ScannerEnabled = await GetConfigurationAsync("BarcodeScanner.ScannerEnabled", true),
            ScannerType = await GetConfigurationAsync("BarcodeScanner.ScannerType", "Camera"),
            SupportedFormats = supportedFormats,
            AutoFocusEnabled = await GetConfigurationAsync("BarcodeScanner.AutoFocusEnabled", true),
            FlashlightEnabled = await GetConfigurationAsync("BarcodeScanner.FlashlightEnabled", false),
            BeepOnScanEnabled = await GetConfigurationAsync("BarcodeScanner.BeepOnScanEnabled", true),
            VibrateOnScanEnabled = await GetConfigurationAsync("BarcodeScanner.VibrateOnScanEnabled", true),
            ScanTimeoutSeconds = await GetConfigurationAsync("BarcodeScanner.ScanTimeoutSeconds", 10),
            ContinuousScanMode = await GetConfigurationAsync("BarcodeScanner.ContinuousScanMode", false),
            ScanRegion = await GetConfigurationAsync("BarcodeScanner.ScanRegion", "Center"),
            ScanRegionWidth = await GetConfigurationAsync("BarcodeScanner.ScanRegionWidth", 0.8),
            ScanRegionHeight = await GetConfigurationAsync("BarcodeScanner.ScanRegionHeight", 0.6),
            ShowScanOverlay = await GetConfigurationAsync("BarcodeScanner.ShowScanOverlay", true),
            OverlayColor = await GetConfigurationAsync("BarcodeScanner.OverlayColor", "#FF0000"),
            ValidateChecksum = await GetConfigurationAsync("BarcodeScanner.ValidateChecksum", true),
            MinBarcodeLength = await GetConfigurationAsync("BarcodeScanner.MinBarcodeLength", 4),
            MaxBarcodeLength = await GetConfigurationAsync("BarcodeScanner.MaxBarcodeLength", 50)
        };
    }

    /// <summary>
    /// Sets barcode scanner configuration
    /// </summary>
    /// <param name="settings">Barcode scanner settings</param>
    /// <param name="deviceId">Device identifier (optional, uses current device if null)</param>
    /// <returns>Task</returns>
    public async Task SetBarcodeScannerSettingsAsync(BarcodeScannerSettings settings, Guid? deviceId = null)
    {
        await SetConfigurationAsync("BarcodeScanner.ScannerEnabled", settings.ScannerEnabled, "Enable barcode scanner");
        await SetConfigurationAsync("BarcodeScanner.ScannerType", settings.ScannerType, "Scanner type");
        await SetConfigurationAsync("BarcodeScanner.AutoFocusEnabled", settings.AutoFocusEnabled, "Enable auto focus");
        await SetConfigurationAsync("BarcodeScanner.FlashlightEnabled", settings.FlashlightEnabled, "Enable flashlight");
        await SetConfigurationAsync("BarcodeScanner.BeepOnScanEnabled", settings.BeepOnScanEnabled, "Enable beep on scan");
        await SetConfigurationAsync("BarcodeScanner.VibrateOnScanEnabled", settings.VibrateOnScanEnabled, "Enable vibration on scan");
        await SetConfigurationAsync("BarcodeScanner.ScanTimeoutSeconds", settings.ScanTimeoutSeconds, "Scan timeout in seconds");
        await SetConfigurationAsync("BarcodeScanner.ContinuousScanMode", settings.ContinuousScanMode, "Enable continuous scan mode");
        await SetConfigurationAsync("BarcodeScanner.ScanRegion", settings.ScanRegion, "Scan region");
        await SetConfigurationAsync("BarcodeScanner.ScanRegionWidth", settings.ScanRegionWidth, "Scan region width");
        await SetConfigurationAsync("BarcodeScanner.ScanRegionHeight", settings.ScanRegionHeight, "Scan region height");
        await SetConfigurationAsync("BarcodeScanner.ShowScanOverlay", settings.ShowScanOverlay, "Show scan overlay");
        await SetConfigurationAsync("BarcodeScanner.OverlayColor", settings.OverlayColor, "Overlay color");
        await SetConfigurationAsync("BarcodeScanner.ValidateChecksum", settings.ValidateChecksum, "Validate barcode checksum");
        await SetConfigurationAsync("BarcodeScanner.MinBarcodeLength", settings.MinBarcodeLength, "Minimum barcode length");
        await SetConfigurationAsync("BarcodeScanner.MaxBarcodeLength", settings.MaxBarcodeLength, "Maximum barcode length");
        
        var supportedFormatsJson = JsonSerializer.Serialize(settings.SupportedFormats);
        await SetConfigurationAsync("BarcodeScanner.SupportedFormats", supportedFormatsJson, "Supported barcode formats");
    }

    /// <summary>
    /// Gets performance tuning settings
    /// </summary>
    /// <returns>Performance settings</returns>
    public async Task<PerformanceSettings> GetPerformanceSettingsAsync()
    {
        return new PerformanceSettings
        {
            DatabaseConnectionPoolSize = await GetConfigurationAsync("Performance.DatabaseConnectionPoolSize", 10),
            DatabaseCommandTimeoutSeconds = await GetConfigurationAsync("Performance.DatabaseCommandTimeoutSeconds", 30),
            DatabaseQueryCachingEnabled = await GetConfigurationAsync("Performance.DatabaseQueryCachingEnabled", true),
            CacheExpirationMinutes = await GetConfigurationAsync("Performance.CacheExpirationMinutes", 15),
            MaxCacheSize = await GetConfigurationAsync("Performance.MaxCacheSize", 100),
            LazyLoadingEnabled = await GetConfigurationAsync("Performance.LazyLoadingEnabled", true),
            PageSize = await GetConfigurationAsync("Performance.PageSize", 50),
            BackgroundSyncEnabled = await GetConfigurationAsync("Performance.BackgroundSyncEnabled", true),
            SyncIntervalMinutes = await GetConfigurationAsync("Performance.SyncIntervalMinutes", 5),
            MaxConcurrentOperations = await GetConfigurationAsync("Performance.MaxConcurrentOperations", 5),
            CompressionEnabled = await GetConfigurationAsync("Performance.CompressionEnabled", true),
            ImageOptimizationEnabled = await GetConfigurationAsync("Performance.ImageOptimizationEnabled", true),
            MaxImageSizeKB = await GetConfigurationAsync("Performance.MaxImageSizeKB", 500),
            PreloadCriticalData = await GetConfigurationAsync("Performance.PreloadCriticalData", true),
            UIUpdateThrottleMs = await GetConfigurationAsync("Performance.UIUpdateThrottleMs", 100),
            MemoryOptimizationEnabled = await GetConfigurationAsync("Performance.MemoryOptimizationEnabled", true),
            GarbageCollectionThresholdMB = await GetConfigurationAsync("Performance.GarbageCollectionThresholdMB", 50)
        };
    }

    /// <summary>
    /// Sets performance tuning settings
    /// </summary>
    /// <param name="settings">Performance settings</param>
    /// <returns>Task</returns>
    public async Task SetPerformanceSettingsAsync(PerformanceSettings settings)
    {
        await SetConfigurationAsync("Performance.DatabaseConnectionPoolSize", settings.DatabaseConnectionPoolSize, "Database connection pool size");
        await SetConfigurationAsync("Performance.DatabaseCommandTimeoutSeconds", settings.DatabaseCommandTimeoutSeconds, "Database command timeout in seconds");
        await SetConfigurationAsync("Performance.DatabaseQueryCachingEnabled", settings.DatabaseQueryCachingEnabled, "Enable database query caching");
        await SetConfigurationAsync("Performance.CacheExpirationMinutes", settings.CacheExpirationMinutes, "Cache expiration time in minutes");
        await SetConfigurationAsync("Performance.MaxCacheSize", settings.MaxCacheSize, "Maximum cache size in MB");
        await SetConfigurationAsync("Performance.LazyLoadingEnabled", settings.LazyLoadingEnabled, "Enable lazy loading");
        await SetConfigurationAsync("Performance.PageSize", settings.PageSize, "Default page size for pagination");
        await SetConfigurationAsync("Performance.BackgroundSyncEnabled", settings.BackgroundSyncEnabled, "Enable background synchronization");
        await SetConfigurationAsync("Performance.SyncIntervalMinutes", settings.SyncIntervalMinutes, "Sync interval in minutes");
        await SetConfigurationAsync("Performance.MaxConcurrentOperations", settings.MaxConcurrentOperations, "Maximum concurrent operations");
        await SetConfigurationAsync("Performance.CompressionEnabled", settings.CompressionEnabled, "Enable data compression");
        await SetConfigurationAsync("Performance.ImageOptimizationEnabled", settings.ImageOptimizationEnabled, "Enable image optimization");
        await SetConfigurationAsync("Performance.MaxImageSizeKB", settings.MaxImageSizeKB, "Maximum image size in KB");
        await SetConfigurationAsync("Performance.PreloadCriticalData", settings.PreloadCriticalData, "Preload critical data on startup");
        await SetConfigurationAsync("Performance.UIUpdateThrottleMs", settings.UIUpdateThrottleMs, "UI update throttle in milliseconds");
        await SetConfigurationAsync("Performance.MemoryOptimizationEnabled", settings.MemoryOptimizationEnabled, "Enable memory optimization");
        await SetConfigurationAsync("Performance.GarbageCollectionThresholdMB", settings.GarbageCollectionThresholdMB, "Garbage collection threshold in MB");
    }

    /// <summary>
    /// Gets configuration by shop and key
    /// </summary>
    /// <typeparam name="T">Type to convert the value to</typeparam>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if configuration not found</param>
    /// <returns>Configuration value or default</returns>
    public async Task<T> GetShopConfigurationAsync<T>(Guid shopId, string key, T defaultValue = default!)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Configuration key cannot be null or empty", nameof(key));

        try
        {
            var config = await _configurationRepository.GetByShopAndKeyAsync(shopId, key);
            if (config == null)
            {
                _logger.LogDebug("Shop configuration {ShopId}:{Key} not found, returning default value", shopId, key);
                return defaultValue;
            }

            return ConvertValue<T>(config.Value, config.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shop configuration {ShopId}:{Key}, returning default value", shopId, key);
            return defaultValue;
        }
    }

    /// <summary>
    /// Sets configuration by shop and key
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="key">Configuration key</param>
    /// <param name="value">Configuration value</param>
    /// <param name="description">Optional description</param>
    /// <returns>Task</returns>
    public async Task SetShopConfigurationAsync<T>(Guid shopId, string key, T value, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Configuration key cannot be null or empty", nameof(key));

        if (value == null)
            throw new ArgumentNullException(nameof(value));

        try
        {
            var stringValue = ConvertToString(value);
            var configurationType = DetectConfigurationType<T>();
            
            var validationResult = await ValidateConfigurationAsync(key, value, configurationType);
            if (!validationResult.IsValid)
            {
                throw new ArgumentException($"Invalid configuration value: {validationResult.ErrorMessage}");
            }

            await _configurationRepository.SetShopConfigurationAsync(shopId, key, stringValue, configurationType, description);
            
            _logger.LogInformation("Shop configuration {ShopId}:{Key} set to {Value}", shopId, key, SanitizeForLogging(stringValue));
        }
        catch (Exception ex)
        {
            var safeValueForLog = SanitizeForLogging(ConvertToString(value));
            _logger.LogError(ex, "Error setting shop configuration {ShopId}:{Key} = {Value}", shopId, key, safeValueForLog);
            throw;
        }
    }

    /// <summary>
    /// Gets configuration by user and key
    /// </summary>
    /// <typeparam name="T">Type to convert the value to</typeparam>
    /// <param name="userId">User identifier</param>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if configuration not found</param>
    /// <returns>Configuration value or default</returns>
    public async Task<T> GetUserConfigurationAsync<T>(Guid userId, string key, T defaultValue = default!)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Configuration key cannot be null or empty", nameof(key));

        try
        {
            var config = await _configurationRepository.GetByUserAndKeyAsync(userId, key);
            if (config == null)
            {
                _logger.LogDebug("User configuration {UserId}:{Key} not found, returning default value", userId, key);
                return defaultValue;
            }

            return ConvertValue<T>(config.Value, config.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user configuration {UserId}:{Key}, returning default value", userId, key);
            return defaultValue;
        }
    }

    /// <summary>
    /// Sets configuration by user and key
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>
    /// <param name="userId">User identifier</param>
    /// <param name="key">Configuration key</param>
    /// <param name="value">Configuration value</param>
    /// <param name="description">Optional description</param>
    /// <returns>Task</returns>
    public async Task SetUserConfigurationAsync<T>(Guid userId, string key, T value, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Configuration key cannot be null or empty", nameof(key));

        if (value == null)
            throw new ArgumentNullException(nameof(value));

        try
        {
            var stringValue = ConvertToString(value);
            var configurationType = DetectConfigurationType<T>();

            var validationResult = await ValidateConfigurationAsync(key, value, configurationType);
            if (!validationResult.IsValid)
            {
                throw new ArgumentException($"Invalid configuration value: {validationResult.ErrorMessage}");
            }

            await _configurationRepository.SetUserConfigurationAsync(userId, key, stringValue, configurationType, description);

            // Sanitize value before logging to prevent log forging via control characters
            var stringValueForLog = stringValue
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty);

            _logger.LogInformation("User configuration {UserId}:{Key} set to {Value}", userId, key, stringValueForLog);
        }
        catch (Exception ex)
        {
            var safeValueForLog = ConvertToString(value)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty);

            _logger.LogError(ex, "Error setting user configuration {UserId}:{Key} = {Value}", userId, key, safeValueForLog);
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
            { "Localization.NumberFormat", ("N2", ConfigurationType.String, "Number format pattern") },

            // Performance settings
            { "Performance.DatabaseConnectionPoolSize", (10, ConfigurationType.Number, "Database connection pool size") },
            { "Performance.DatabaseCommandTimeoutSeconds", (30, ConfigurationType.Number, "Database command timeout in seconds") },
            { "Performance.DatabaseQueryCachingEnabled", (true, ConfigurationType.Boolean, "Enable database query caching") },
            { "Performance.CacheExpirationMinutes", (15, ConfigurationType.Number, "Cache expiration time in minutes") },
            { "Performance.MaxCacheSize", (100, ConfigurationType.Number, "Maximum cache size in MB") },
            { "Performance.LazyLoadingEnabled", (true, ConfigurationType.Boolean, "Enable lazy loading") },
            { "Performance.PageSize", (50, ConfigurationType.Number, "Default page size for pagination") },
            { "Performance.BackgroundSyncEnabled", (true, ConfigurationType.Boolean, "Enable background synchronization") },
            { "Performance.SyncIntervalMinutes", (5, ConfigurationType.Number, "Sync interval in minutes") },
            { "Performance.MaxConcurrentOperations", (5, ConfigurationType.Number, "Maximum concurrent operations") },
            { "Performance.CompressionEnabled", (true, ConfigurationType.Boolean, "Enable data compression") },
            { "Performance.ImageOptimizationEnabled", (true, ConfigurationType.Boolean, "Enable image optimization") },
            { "Performance.MaxImageSizeKB", (500, ConfigurationType.Number, "Maximum image size in KB") },
            { "Performance.PreloadCriticalData", (true, ConfigurationType.Boolean, "Preload critical data on startup") },
            { "Performance.UIUpdateThrottleMs", (100, ConfigurationType.Number, "UI update throttle in milliseconds") },
            { "Performance.MemoryOptimizationEnabled", (true, ConfigurationType.Boolean, "Enable memory optimization") },
            { "Performance.GarbageCollectionThresholdMB", (50, ConfigurationType.Number, "Garbage collection threshold in MB") },

            // Barcode scanner settings
            { "BarcodeScanner.ScannerEnabled", (true, ConfigurationType.Boolean, "Enable barcode scanner") },
            { "BarcodeScanner.ScannerType", ("Camera", ConfigurationType.String, "Scanner type (Camera, USB, Bluetooth)") },
            { "BarcodeScanner.AutoFocusEnabled", (true, ConfigurationType.Boolean, "Enable auto focus") },
            { "BarcodeScanner.FlashlightEnabled", (false, ConfigurationType.Boolean, "Enable flashlight") },
            { "BarcodeScanner.BeepOnScanEnabled", (true, ConfigurationType.Boolean, "Enable beep on scan") },
            { "BarcodeScanner.VibrateOnScanEnabled", (true, ConfigurationType.Boolean, "Enable vibration on scan") },
            { "BarcodeScanner.ScanTimeoutSeconds", (10, ConfigurationType.Number, "Scan timeout in seconds") },
            { "BarcodeScanner.ContinuousScanMode", (false, ConfigurationType.Boolean, "Enable continuous scan mode") },
            { "BarcodeScanner.ScanRegion", ("Center", ConfigurationType.String, "Scan region (Center, FullScreen, Custom)") },
            { "BarcodeScanner.ScanRegionWidth", (0.8, ConfigurationType.Number, "Scan region width (0.0-1.0)") },
            { "BarcodeScanner.ScanRegionHeight", (0.6, ConfigurationType.Number, "Scan region height (0.0-1.0)") },
            { "BarcodeScanner.ShowScanOverlay", (true, ConfigurationType.Boolean, "Show scan overlay") },
            { "BarcodeScanner.OverlayColor", ("#FF0000", ConfigurationType.String, "Overlay color") },
            { "BarcodeScanner.ValidateChecksum", (true, ConfigurationType.Boolean, "Validate barcode checksum") },
            { "BarcodeScanner.MinBarcodeLength", (4, ConfigurationType.Number, "Minimum barcode length") },
            { "BarcodeScanner.MaxBarcodeLength", (50, ConfigurationType.Number, "Maximum barcode length") }
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
    private string SanitizeForLogging(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Remove carriage return and line feed characters to prevent log forging
        return input
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);
    }

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