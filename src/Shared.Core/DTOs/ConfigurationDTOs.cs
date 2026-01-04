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