using Shared.Core.Enums;

namespace Shared.Core.DTOs;

public class IntegratedSaleRequest
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid DeviceId { get; set; }
    public string? MembershipNumber { get; set; }
}

public class AddItemToSaleRequest
{
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitPriceOverride { get; set; }
    public string? BatchNumber { get; set; }
}

public class AddWeightBasedItemRequest
{
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Weight { get; set; }
    public string? BatchNumber { get; set; }
}

public class CompleteSaleRequest
{
    public Guid SaleId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public bool ApplyAutomaticDiscounts { get; set; } = true;
    public List<ManualDiscountRequest> ManualDiscounts { get; set; } = new();
}

public class ManualDiscountRequest
{
    public string Name { get; set; } = string.Empty;
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class IntegratedSaleResult
{
    public EnhancedSaleDto Sale { get; set; } = new();
    public SaleCalculationResult Calculation { get; set; } = new();
    public CustomerDto? Customer { get; set; }
    public MembershipDiscount? MembershipDiscount { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool IsValid { get; set; } = true;
}

public class EnhancedSaleDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal MembershipDiscountAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? MembershipNumber { get; set; }
    public List<EnhancedSaleItemDto> Items { get; set; } = new();
    public List<AppliedDiscount> AppliedDiscounts { get; set; } = new();
}

public class EnhancedSaleItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductBarcode { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalPrice { get; set; }
    public string? BatchNumber { get; set; }
    public decimal? Weight { get; set; }
    public decimal? RatePerKilogram { get; set; }
    public bool IsWeightBased { get; set; }
}

public class SystemValidationResult
{
    public bool IsValid { get; set; } = true;
    public LicenseStatus LicenseStatus { get; set; }
    public TimeSpan? RemainingTrialTime { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> EnabledFeatures { get; set; } = new();
    public List<string> DisabledFeatures { get; set; } = new();
}

public class PosConfigurationSummary
{
    public CurrencySettings Currency { get; set; } = new();
    public TaxSettings Tax { get; set; } = new();
    public BusinessSettings Business { get; set; } = new();
    public LocalizationSettings Localization { get; set; } = new();
    public bool WeightBasedPricingEnabled { get; set; }
    public bool MembershipSystemEnabled { get; set; }
    public bool DiscountSystemEnabled { get; set; }
    public int WeightPrecision { get; set; } = 3;
}