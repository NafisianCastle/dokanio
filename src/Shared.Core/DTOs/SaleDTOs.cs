using Shared.Core.Enums;

namespace Shared.Core.DTOs;

public class SaleCalculationResult
{
    public decimal BaseTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal MembershipDiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal FinalTotal { get; set; }
    public List<AppliedDiscount> AppliedDiscounts { get; set; } = new();
    public List<string> DiscountReasons { get; set; } = new();
    public List<BusinessRuleApplication> AppliedBusinessRules { get; set; } = new();
    public ShopConfiguration? ShopConfiguration { get; set; }
}

/// <summary>
/// Represents the result of validating a product or operation
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object> ValidationContext { get; set; } = new();

    public static ValidationResult Success() => new() { IsValid = true };
    
    public static ValidationResult Failure(params string[] errors) => new() 
    { 
        IsValid = false, 
        Errors = errors.ToList() 
    };

    public static ValidationResult Warning(string warning) => new()
    {
        IsValid = true,
        Warnings = new List<string> { warning }
    };
}

/// <summary>
/// Represents AI-powered recommendations for a sale
/// </summary>
public class RecommendationResult
{
    public List<ProductRecommendation> ProductRecommendations { get; set; } = new();
    public List<BundleRecommendation> BundleRecommendations { get; set; } = new();
    public List<PricingRecommendation> PricingRecommendations { get; set; } = new();
    public decimal ConfidenceScore { get; set; }
    public string RecommendationReason { get; set; } = string.Empty;
}

/// <summary>
/// Represents a product recommendation
/// </summary>
public class ProductRecommendation
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string RecommendationType { get; set; } = string.Empty; // "cross-sell", "up-sell", "complementary"
    public decimal ConfidenceScore { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Represents a bundle recommendation
/// </summary>
public class BundleRecommendation
{
    public List<Guid> ProductIds { get; set; } = new();
    public string BundleName { get; set; } = string.Empty;
    public decimal BundlePrice { get; set; }
    public decimal SavingsAmount { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Represents a pricing recommendation
/// </summary>
public class PricingRecommendation
{
    public Guid ProductId { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal RecommendedPrice { get; set; }
    public string RecommendationType { get; set; } = string.Empty; // "discount", "markup", "dynamic"
    public decimal ConfidenceScore { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Represents the application of a business rule
/// </summary>
public class BusinessRuleApplication
{
    public string RuleName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty; // "validation", "pricing", "discount", "tax"
    public BusinessType BusinessType { get; set; }
    public Dictionary<string, object> RuleParameters { get; set; } = new();
    public string Result { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
}