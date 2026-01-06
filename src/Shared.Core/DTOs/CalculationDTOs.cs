using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.DTOs;

/// <summary>
/// Result of line item calculation including pricing, discounts, and taxes
/// </summary>
public class LineItemCalculationResult
{
    public Guid SaleItemId { get; set; }
    public decimal BasePrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Weight { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineSubtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public List<AppliedDiscount> AppliedDiscounts { get; set; } = new();
    public List<AppliedTax> AppliedTaxes { get; set; } = new();
    public List<string> CalculationNotes { get; set; } = new();
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Complete order calculation with all totals and breakdowns
/// </summary>
public class OrderTotalCalculation
{
    public decimal Subtotal { get; set; }
    public decimal TotalDiscountAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public decimal FinalTotal { get; set; }
    public int TotalItems { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal? TotalWeight { get; set; }
    public List<LineItemCalculationResult> LineItems { get; set; } = new();
    public List<AppliedDiscount> OrderLevelDiscounts { get; set; } = new();
    public List<AppliedTax> OrderLevelTaxes { get; set; } = new();
    public List<PricingRuleApplication> AppliedPricingRules { get; set; } = new();
    public CalculationBreakdown Breakdown { get; set; } = new();
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    public bool IsValid { get; set; } = true;
    public List<string> ValidationMessages { get; set; } = new();
}

/// <summary>
/// Tax calculation result with detailed breakdown
/// </summary>
public class TaxCalculationResult
{
    public decimal TotalTaxAmount { get; set; }
    public List<AppliedTax> AppliedTaxes { get; set; } = new();
    public List<TaxBreakdown> TaxBreakdowns { get; set; } = new();
    public bool TaxIncludedInPrice { get; set; }
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Applied tax information
/// </summary>
public class AppliedTax
{
    public string TaxName { get; set; } = string.Empty;
    public decimal TaxRate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Tax breakdown by category or item
/// </summary>
public class TaxBreakdown
{
    public string Category { get; set; } = string.Empty;
    public decimal TaxableAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public List<Guid> ApplicableItemIds { get; set; } = new();
}

/// <summary>
/// Weight-based pricing calculation result
/// </summary>
public class WeightBasedPricingResult
{
    public decimal Weight { get; set; }
    public decimal RatePerKilogram { get; set; }
    public decimal BasePrice { get; set; }
    public decimal AdjustedPrice { get; set; }
    public string FormattedWeight { get; set; } = string.Empty;
    public string FormattedRate { get; set; } = string.Empty;
    public string FormattedPrice { get; set; } = string.Empty;
    public bool IsValid { get; set; } = true;
    public List<string> ValidationErrors { get; set; } = new();
    public List<PricingAdjustment> PricingAdjustments { get; set; } = new();
}

/// <summary>
/// Pricing rules application result
/// </summary>
public class PricingRulesResult
{
    public decimal OriginalTotal { get; set; }
    public decimal AdjustedTotal { get; set; }
    public decimal TotalAdjustment { get; set; }
    public List<PricingRuleApplication> AppliedRules { get; set; } = new();
    public List<string> RuleDescriptions { get; set; } = new();
    public bool HasMembershipBenefits { get; set; }
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Individual pricing rule application
/// </summary>
public class PricingRuleApplication
{
    public string RuleName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public decimal AdjustmentAmount { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<Guid> AffectedItemIds { get; set; } = new();
    public Dictionary<string, object> RuleParameters { get; set; } = new();
}

/// <summary>
/// Pricing adjustment for weight-based or other dynamic pricing
/// </summary>
public class PricingAdjustment
{
    public string AdjustmentType { get; set; } = string.Empty;
    public decimal AdjustmentAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal OriginalValue { get; set; }
    public decimal AdjustedValue { get; set; }
}

/// <summary>
/// Detailed calculation breakdown for transparency
/// </summary>
public class CalculationBreakdown
{
    public List<BreakdownItem> Items { get; set; } = new();
    public Dictionary<string, decimal> Totals { get; set; } = new();
    public List<string> CalculationSteps { get; set; } = new();
}

/// <summary>
/// Individual breakdown item
/// </summary>
public class BreakdownItem
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool IsAddition { get; set; } = true;
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Calculation validation result
/// </summary>
public class CalculationValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationWarning> Warnings { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
}

/// <summary>
/// Validation error details
/// </summary>
public class ValidationError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public object? Value { get; set; }
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
}

/// <summary>
/// Validation warning details
/// </summary>
public class ValidationWarning
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public object? Value { get; set; }
    public string Suggestion { get; set; } = string.Empty;
}

/// <summary>
/// Validation severity levels
/// </summary>
public enum ValidationSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Bulk pricing tier information
/// </summary>
public class BulkPricingTier
{
    public int MinimumQuantity { get; set; }
    public int? MaximumQuantity { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal? FixedPrice { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Membership pricing benefit
/// </summary>
public class MembershipPricingBenefit
{
    public MembershipTier Tier { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal? FixedDiscountAmount { get; set; }
    public List<string> ApplicableCategories { get; set; } = new();
    public List<Guid> ApplicableProductIds { get; set; } = new();
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Real-time calculation context for maintaining state during calculations
/// </summary>
public class CalculationContext
{
    public Guid SessionId { get; set; }
    public Guid ShopId { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTime CalculationTime { get; set; } = DateTime.UtcNow;
    public ShopConfiguration ShopConfiguration { get; set; } = new();
    public Customer? Customer { get; set; }
    public List<Discount> AvailableDiscounts { get; set; } = new();
    public Dictionary<string, object> ContextData { get; set; } = new();
}