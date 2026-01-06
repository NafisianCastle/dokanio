using Shared.Core.Enums;

namespace Shared.Core.DTOs;

/// <summary>
/// DTO for configuration data transfer
/// </summary>
public class ConfigurationDto
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public ConfigurationType Type { get; set; }
    public string? Description { get; set; }
    public bool IsSystemLevel { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? ShopId { get; set; }
    public Guid? UserId { get; set; }
    public SyncStatus SyncStatus { get; set; }
}

/// <summary>
/// Request for creating or updating a configuration
/// </summary>
public class ConfigurationRequest
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public ConfigurationType Type { get; set; }
    public string? Description { get; set; }
    public bool IsSystemLevel { get; set; }
}

/// <summary>
/// Response for configuration validation
/// </summary>
public class ConfigurationValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public object? ParsedValue { get; set; }
}

/// <summary>
/// Currency settings configuration
/// </summary>
public class CurrencySettings
{
    public string CurrencyCode { get; set; } = "USD";
    public string CurrencySymbol { get; set; } = "$";
    public int DecimalPlaces { get; set; } = 2;
    public string DecimalSeparator { get; set; } = ".";
    public string ThousandsSeparator { get; set; } = ",";
    public bool SymbolBeforeAmount { get; set; } = true;
}

/// <summary>
/// Tax settings configuration
/// </summary>
public class TaxSettings
{
    public bool TaxEnabled { get; set; } = true;
    public decimal DefaultTaxRate { get; set; } = 0.0m;
    public string TaxName { get; set; } = "Tax";
    public bool TaxIncludedInPrice { get; set; } = false;
    public bool ShowTaxOnReceipt { get; set; } = true;
}

/// <summary>
/// Business settings configuration
/// </summary>
public class BusinessSettings
{
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessAddress { get; set; } = string.Empty;
    public string BusinessPhone { get; set; } = string.Empty;
    public string BusinessEmail { get; set; } = string.Empty;
    public string BusinessWebsite { get; set; } = string.Empty;
    public string BusinessLogo { get; set; } = string.Empty;
    public string ReceiptFooter { get; set; } = string.Empty;
}

/// <summary>
/// Localization settings configuration
/// </summary>
public class LocalizationSettings
{
    public string Language { get; set; } = "en";
    public string Country { get; set; } = "US";
    public string TimeZone { get; set; } = "UTC";
    public string DateFormat { get; set; } = "MM/dd/yyyy";
    public string TimeFormat { get; set; } = "HH:mm:ss";
    public string NumberFormat { get; set; } = "N2";
}

/// <summary>
/// Shop-level pricing rules configuration
/// </summary>
public class ShopPricingSettings
{
    public bool WeightBasedPricingEnabled { get; set; } = true;
    public bool BulkDiscountEnabled { get; set; } = false;
    public decimal BulkDiscountThreshold { get; set; } = 10.0m;
    public decimal BulkDiscountPercentage { get; set; } = 5.0m;
    public bool MembershipPricingEnabled { get; set; } = true;
    public bool DynamicPricingEnabled { get; set; } = false;
    public decimal MinimumProfitMargin { get; set; } = 10.0m;
    public bool RoundingEnabled { get; set; } = true;
    public decimal RoundingPrecision { get; set; } = 0.05m;
}

/// <summary>
/// Shop-level tax configuration
/// </summary>
public class ShopTaxSettings
{
    public bool TaxEnabled { get; set; } = true;
    public decimal DefaultTaxRate { get; set; } = 0.0m;
    public string TaxName { get; set; } = "Tax";
    public bool TaxIncludedInPrice { get; set; } = false;
    public bool ShowTaxOnReceipt { get; set; } = true;
    public Dictionary<string, decimal> CategoryTaxRates { get; set; } = new();
    public bool CompoundTaxEnabled { get; set; } = false;
    public List<TaxRule> TaxRules { get; set; } = new();
}

/// <summary>
/// Tax rule for specific conditions
/// </summary>
public class TaxRule
{
    public string Name { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public decimal TaxRate { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// User preferences for UI customization
/// </summary>
public class UserPreferences
{
    public Guid UserId { get; set; }
    public string Theme { get; set; } = "Light";
    public string AccentColor { get; set; } = "#0078D4";
    public int FontSize { get; set; } = 14;
    public string FontFamily { get; set; } = "Segoe UI";
    public bool HighContrastMode { get; set; } = false;
    public bool ReducedMotion { get; set; } = false;
    public string DefaultView { get; set; } = "Sales";
    public bool ShowTooltips { get; set; } = true;
    public bool AutoSaveEnabled { get; set; } = true;
    public int AutoSaveInterval { get; set; } = 30; // seconds
    public bool SoundEnabled { get; set; } = true;
    public bool HapticFeedbackEnabled { get; set; } = true;
    public string Language { get; set; } = "en";
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

/// <summary>
/// Barcode scanner configuration options
/// </summary>
public class BarcodeScannerSettings
{
    public bool ScannerEnabled { get; set; } = true;
    public string ScannerType { get; set; } = "Camera"; // Camera, USB, Bluetooth
    public List<string> SupportedFormats { get; set; } = new() { "EAN13", "EAN8", "UPC", "Code128", "Code39" };
    public bool AutoFocusEnabled { get; set; } = true;
    public bool FlashlightEnabled { get; set; } = false;
    public bool BeepOnScanEnabled { get; set; } = true;
    public bool VibrateOnScanEnabled { get; set; } = true;
    public int ScanTimeoutSeconds { get; set; } = 10;
    public bool ContinuousScanMode { get; set; } = false;
    public string ScanRegion { get; set; } = "Center"; // Center, FullScreen, Custom
    public double ScanRegionWidth { get; set; } = 0.8;
    public double ScanRegionHeight { get; set; } = 0.6;
    public bool ShowScanOverlay { get; set; } = true;
    public string OverlayColor { get; set; } = "#FF0000";
    public bool ValidateChecksum { get; set; } = true;
    public int MinBarcodeLength { get; set; } = 4;
    public int MaxBarcodeLength { get; set; } = 50;
}

/// <summary>
/// Performance tuning settings
/// </summary>
public class PerformanceSettings
{
    public int DatabaseConnectionPoolSize { get; set; } = 10;
    public int DatabaseCommandTimeoutSeconds { get; set; } = 30;
    public bool DatabaseQueryCachingEnabled { get; set; } = true;
    public int CacheExpirationMinutes { get; set; } = 15;
    public int MaxCacheSize { get; set; } = 100; // MB
    public bool LazyLoadingEnabled { get; set; } = true;
    public int PageSize { get; set; } = 50;
    public bool BackgroundSyncEnabled { get; set; } = true;
    public int SyncIntervalMinutes { get; set; } = 5;
    public int MaxConcurrentOperations { get; set; } = 5;
    public bool CompressionEnabled { get; set; } = true;
    public bool ImageOptimizationEnabled { get; set; } = true;
    public int MaxImageSizeKB { get; set; } = 500;
    public bool PreloadCriticalData { get; set; } = true;
    public int UIUpdateThrottleMs { get; set; } = 100;
    public bool MemoryOptimizationEnabled { get; set; } = true;
    public int GarbageCollectionThresholdMB { get; set; } = 50;
}
/// <summary>
/// Configuration export data for backup and migration
/// </summary>
public class ConfigurationExport
{
    public Guid ShopId { get; set; }
    public DateTime ExportedAt { get; set; }
    public ShopPricingSettings? PricingSettings { get; set; }
    public ShopTaxSettings? TaxSettings { get; set; }
    public BusinessSettings? BusinessSettings { get; set; }
    public CurrencySettings? CurrencySettings { get; set; }
    public LocalizationSettings? LocalizationSettings { get; set; }
    public string Version { get; set; } = "1.0";
}

/// <summary>
/// Configuration validation summary
/// </summary>
public class ConfigurationValidationSummary
{
    public Guid ShopId { get; set; }
    public DateTime ValidatedAt { get; set; }
    public bool IsValid { get; set; }
    public List<ConfigurationValidationResult> ValidationResults { get; set; } = new();
}

/// <summary>
/// Configuration recommendations based on analytics
/// </summary>
public class ConfigurationRecommendations
{
    public Guid ShopId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<ConfigurationRecommendation> Recommendations { get; set; } = new();
}

/// <summary>
/// Individual configuration recommendation
/// </summary>
public class ConfigurationRecommendation
{
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RecommendationPriority Priority { get; set; }
    public string RecommendedValue { get; set; } = string.Empty;
    public string CurrentValue { get; set; } = string.Empty;
    public decimal? EstimatedImpact { get; set; }
}

/// <summary>
/// Priority levels for configuration recommendations
/// </summary>
public enum RecommendationPriority
{
    Low,
    Medium,
    High,
    Critical
}