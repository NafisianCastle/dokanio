using Shared.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Shared.Core.DTOs;

/// <summary>
/// Request DTO for creating a new business
/// </summary>
public class CreateBusinessRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public BusinessType Type { get; set; }
    
    [Required]
    public Guid OwnerId { get; set; }
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [MaxLength(200)]
    public string? Address { get; set; }
    
    [MaxLength(20)]
    public string? Phone { get; set; }
    
    [MaxLength(255)]
    [EmailAddress]
    public string? Email { get; set; }
    
    [MaxLength(50)]
    public string? TaxId { get; set; }
    
    /// <summary>
    /// Business type-specific configuration as JSON
    /// </summary>
    public string? Configuration { get; set; }
}

/// <summary>
/// Request DTO for updating an existing business
/// </summary>
public class UpdateBusinessRequest
{
    [Required]
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [MaxLength(200)]
    public string? Address { get; set; }
    
    [MaxLength(20)]
    public string? Phone { get; set; }
    
    [MaxLength(255)]
    [EmailAddress]
    public string? Email { get; set; }
    
    [MaxLength(50)]
    public string? TaxId { get; set; }
    
    /// <summary>
    /// Business type-specific configuration as JSON
    /// </summary>
    public string? Configuration { get; set; }
    
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Request DTO for creating a new shop
/// </summary>
public class CreateShopRequest
{
    [Required]
    public Guid BusinessId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Address { get; set; }
    
    [MaxLength(20)]
    public string? Phone { get; set; }
    
    [MaxLength(255)]
    [EmailAddress]
    public string? Email { get; set; }
    
    /// <summary>
    /// Shop-specific configuration as JSON (tax rates, currency, pricing rules, etc.)
    /// </summary>
    public string? Configuration { get; set; }
}

/// <summary>
/// Request DTO for updating an existing shop
/// </summary>
public class UpdateShopRequest
{
    [Required]
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Address { get; set; }
    
    [MaxLength(20)]
    public string? Phone { get; set; }
    
    [MaxLength(255)]
    [EmailAddress]
    public string? Email { get; set; }
    
    /// <summary>
    /// Shop-specific configuration as JSON (tax rates, currency, pricing rules, etc.)
    /// </summary>
    public string? Configuration { get; set; }
    
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Response DTO for business information
/// </summary>
public class BusinessResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public BusinessType Type { get; set; }
    public Guid OwnerId { get; set; }
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxId { get; set; }
    public string? Configuration { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ShopResponse> Shops { get; set; } = new();
    public string OwnerName { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO for shop information
/// </summary>
public class ShopResponse
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Configuration { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public BusinessType BusinessType { get; set; }
    public int ProductCount { get; set; }
    public int InventoryCount { get; set; }
}

/// <summary>
/// Business configuration DTO for type-specific settings
/// </summary>
public class BusinessConfiguration
{
    /// <summary>
    /// Business type
    /// </summary>
    public BusinessType BusinessType { get; set; }
    
    /// <summary>
    /// Default currency for the business
    /// </summary>
    public string Currency { get; set; } = "USD";
    
    /// <summary>
    /// Default tax rate for the business
    /// </summary>
    public decimal DefaultTaxRate { get; set; } = 0.0m;
    
    /// <summary>
    /// Business type-specific settings
    /// </summary>
    public BusinessTypeSettings TypeSettings { get; set; } = new();
    
    /// <summary>
    /// Business type settings as dictionary for flexibility
    /// </summary>
    public Dictionary<string, object> BusinessTypeSettings { get; set; } = new();
    
    /// <summary>
    /// Required product attributes for this business type
    /// </summary>
    public List<string> RequiredProductAttributes { get; set; } = new();
    
    /// <summary>
    /// Optional product attributes for this business type
    /// </summary>
    public List<string> OptionalProductAttributes { get; set; } = new();
    
    /// <summary>
    /// Business settings
    /// </summary>
    public BusinessSettings BusinessSettings { get; set; } = new();
    
    /// <summary>
    /// Tax settings
    /// </summary>
    public TaxSettings TaxSettings { get; set; } = new();
    
    /// <summary>
    /// Currency settings
    /// </summary>
    public CurrencySettings CurrencySettings { get; set; } = new();
    
    /// <summary>
    /// Custom attributes for extensibility
    /// </summary>
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

/// <summary>
/// Business type-specific settings
/// </summary>
public class BusinessTypeSettings
{
    /// <summary>
    /// Enable expiry date tracking (for pharmacy)
    /// </summary>
    public bool EnableExpiryTracking { get; set; } = false;
    
    /// <summary>
    /// Enable weight-based pricing (for grocery)
    /// </summary>
    public bool EnableWeightBasedPricing { get; set; } = false;
    
    /// <summary>
    /// Enable batch tracking (for pharmacy)
    /// </summary>
    public bool EnableBatchTracking { get; set; } = false;
    
    /// <summary>
    /// Enable volume measurements (for grocery)
    /// </summary>
    public bool EnableVolumeMeasurements { get; set; } = false;
    
    /// <summary>
    /// Required product attributes for this business type
    /// </summary>
    public List<string> RequiredAttributes { get; set; } = new();
    
    /// <summary>
    /// Optional product attributes for this business type
    /// </summary>
    public List<string> OptionalAttributes { get; set; } = new();
}

/// <summary>
/// Shop configuration DTO for shop-specific settings
/// </summary>
public class ShopConfiguration
{
    /// <summary>
    /// Currency for this shop (overrides business default)
    /// </summary>
    public string Currency { get; set; } = "USD";
    
    /// <summary>
    /// Tax rate for this shop (overrides business default)
    /// </summary>
    public decimal TaxRate { get; set; } = 0.0m;
    
    /// <summary>
    /// Pricing rules specific to this shop
    /// </summary>
    public PricingRules PricingRules { get; set; } = new();
    
    /// <summary>
    /// Inventory settings for this shop
    /// </summary>
    public InventorySettings InventorySettings { get; set; } = new();
    
    /// <summary>
    /// Business settings for this shop
    /// </summary>
    public BusinessSettings BusinessSettings { get; set; } = new();
    
    /// <summary>
    /// Localization settings for this shop
    /// </summary>
    public LocalizationSettings LocalizationSettings { get; set; } = new();
    
    /// <summary>
    /// Custom settings for this shop
    /// </summary>
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

/// <summary>
/// Pricing rules configuration
/// </summary>
public class PricingRules
{
    /// <summary>
    /// Allow manual price override during sales
    /// </summary>
    public bool AllowPriceOverride { get; set; } = false;
    
    /// <summary>
    /// Maximum discount percentage allowed
    /// </summary>
    public decimal MaxDiscountPercentage { get; set; } = 0.0m;
    
    /// <summary>
    /// Enable dynamic pricing based on demand
    /// </summary>
    public bool EnableDynamicPricing { get; set; } = false;
    
    /// <summary>
    /// Require manager approval for discounts above threshold
    /// </summary>
    public bool RequireManagerApprovalForDiscounts { get; set; } = false;
    
    /// <summary>
    /// Manager approval threshold percentage
    /// </summary>
    public decimal ManagerApprovalThreshold { get; set; } = 0.1m; // 10%
    
    /// <summary>
    /// Enable bundle pricing
    /// </summary>
    public bool EnableBundlePricing { get; set; } = false;
    
    /// <summary>
    /// Enable tiered pricing
    /// </summary>
    public bool EnableTieredPricing { get; set; } = false;
}

/// <summary>
/// Inventory settings configuration
/// </summary>
public class InventorySettings
{
    /// <summary>
    /// Enable low stock alerts
    /// </summary>
    public bool EnableLowStockAlerts { get; set; } = true;
    
    /// <summary>
    /// Default low stock threshold
    /// </summary>
    public int LowStockThreshold { get; set; } = 10;
    
    /// <summary>
    /// Enable automatic reorder suggestions
    /// </summary>
    public bool EnableAutoReorder { get; set; } = false;
    
    /// <summary>
    /// Auto reorder threshold
    /// </summary>
    public int AutoReorderThreshold { get; set; } = 5;
    
    /// <summary>
    /// Auto reorder quantity
    /// </summary>
    public int AutoReorderQuantity { get; set; } = 50;
    
    /// <summary>
    /// Enable expiry alerts
    /// </summary>
    public bool EnableExpiryAlerts { get; set; } = true;
    
    /// <summary>
    /// Days before expiry to show alerts (for pharmacy)
    /// </summary>
    public int ExpiryAlertDays { get; set; } = 30;
    
    /// <summary>
    /// Enable batch tracking
    /// </summary>
    public bool EnableBatchTracking { get; set; } = false;
    
    /// <summary>
    /// Enable serial number tracking
    /// </summary>
    public bool EnableSerialNumberTracking { get; set; } = false;
}

/// <summary>
/// Business validation result
/// </summary>
public class BusinessValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Shop validation result
/// </summary>
public class ShopValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Business DTO for synchronization
/// </summary>
public class BusinessDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public BusinessType Type { get; set; }
    public Guid OwnerId { get; set; }
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxId { get; set; }
    public string? Configuration { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Shop DTO for synchronization
/// </summary>
public class ShopDto
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Configuration { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

