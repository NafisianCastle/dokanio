using Shared.Core.Enums;

namespace Shared.Core.DTOs;

/// <summary>
/// Base validation result class
/// </summary>
public abstract class BaseValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationWarning> Warnings { get; set; } = new();
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Field-level validation result
/// </summary>
public class FieldValidationResult : BaseValidationResult
{
    public string FieldName { get; set; } = string.Empty;
    public object? ValidatedValue { get; set; }
    public List<string> AppliedRules { get; set; } = new();
    public string? SuggestedValue { get; set; }
}

/// <summary>
/// Multi-field validation result
/// </summary>
public class MultiFieldValidationResult : BaseValidationResult
{
    public Dictionary<string, FieldValidationResult> FieldResults { get; set; } = new();
    public List<string> CrossFieldErrors { get; set; } = new();
    public int ValidFieldCount { get; set; }
    public int TotalFieldCount { get; set; }
}

/// <summary>
/// Entity validation result
/// </summary>
public class EntityValidationResult : BaseValidationResult
{
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public Dictionary<string, FieldValidationResult> PropertyResults { get; set; } = new();
    public List<string> BusinessRuleViolations { get; set; } = new();
}

/// <summary>
/// Stock validation result
/// </summary>
public class StockValidationResult : BaseValidationResult
{
    public Guid ProductId { get; set; }
    public decimal RequestedQuantity { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public bool HasSufficientStock { get; set; }
    public string? RecommendedAction { get; set; }
}

/// <summary>
/// Expiry validation result
/// </summary>
public class ExpiryValidationResult : BaseValidationResult
{
    public Guid ProductId { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsExpired { get; set; }
    public bool IsNearExpiry { get; set; }
    public int DaysUntilExpiry { get; set; }
    public string? ExpiryWarningMessage { get; set; }
}

/// <summary>
/// Pricing validation result
/// </summary>
public class PricingValidationResult : BaseValidationResult
{
    public decimal TotalAmount { get; set; }
    public List<PricingRuleViolation> RuleViolations { get; set; } = new();
    public List<PricingAdjustment> SuggestedAdjustments { get; set; } = new();
    public bool HasUnreasonablePricing { get; set; }
}

/// <summary>
/// Real-time validation result for UI feedback
/// </summary>
public class RealTimeValidationResult : BaseValidationResult
{
    public string FieldName { get; set; } = string.Empty;
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Info;
    public string? InstantFeedback { get; set; }
    public bool ShowFeedback { get; set; } = true;
    public string? SuggestedCorrection { get; set; }
}

/// <summary>
/// Form validation result
/// </summary>
public class FormValidationResult : BaseValidationResult
{
    public string FormName { get; set; } = string.Empty;
    public Dictionary<string, FieldValidationResult> FieldResults { get; set; } = new();
    public List<string> MissingRequiredFields { get; set; } = new();
    public decimal CompletionPercentage { get; set; }
    public bool CanSubmit { get; set; }
}

/// <summary>
/// Field validation rules configuration
/// </summary>
public class FieldValidationRules
{
    public bool IsRequired { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string? RegexPattern { get; set; }
    public List<string>? AllowedValues { get; set; }
    public string? DataType { get; set; }
    public bool AllowNull { get; set; } = true;
    public List<string> CustomRules { get; set; } = new();
    public Dictionary<string, object> RuleParameters { get; set; } = new();
}

/// <summary>
/// Validation context for contextual validation
/// </summary>
public class ValidationContext
{
    public Guid? ShopId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public Dictionary<string, object> ContextData { get; set; } = new();
    public DateTime ValidationTime { get; set; } = DateTime.UtcNow;
    public string? LanguageCode { get; set; }
}

/// <summary>
/// Pricing rule violation details
/// </summary>
public class PricingRuleViolation
{
    public string RuleName { get; set; } = string.Empty;
    public string ViolationType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal ExpectedValue { get; set; }
    public decimal ActualValue { get; set; }
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Warning;
}

/// <summary>
/// Validation message template for localization
/// </summary>
public class ValidationMessageTemplate
{
    public string ErrorCode { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public string MessageTemplate { get; set; } = string.Empty;
    public List<string> ParameterNames { get; set; } = new();
    public ValidationSeverity DefaultSeverity { get; set; } = ValidationSeverity.Error;
}

/// <summary>
/// Validation rule definition
/// </summary>
public class ValidationRuleDefinition
{
    public string RuleName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
    public string? ErrorCode { get; set; }
}

/// <summary>
/// Batch validation result for multiple entities
/// </summary>
public class BatchValidationResult : BaseValidationResult
{
    public int TotalEntities { get; set; }
    public int ValidEntities { get; set; }
    public int InvalidEntities { get; set; }
    public Dictionary<string, EntityValidationResult> EntityResults { get; set; } = new();
    public List<string> BatchErrors { get; set; } = new();
    public TimeSpan ValidationDuration { get; set; }
}

/// <summary>
/// Validation performance metrics
/// </summary>
public class ValidationPerformanceMetrics
{
    public TimeSpan TotalValidationTime { get; set; }
    public int TotalValidations { get; set; }
    public int SuccessfulValidations { get; set; }
    public int FailedValidations { get; set; }
    public Dictionary<string, TimeSpan> RuleExecutionTimes { get; set; } = new();
    public Dictionary<string, int> RuleExecutionCounts { get; set; } = new();
}