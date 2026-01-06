using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.DTOs;

/// <summary>
/// Result of a grid operation (add, update, remove)
/// </summary>
public class GridOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public SalesGridState? UpdatedGridState { get; set; }
    public GridCalculationResult? CalculationResult { get; set; }
    
    public static GridOperationResult SuccessResult(string message = "Operation completed successfully")
    {
        return new GridOperationResult { Success = true, Message = message };
    }
    
    public static GridOperationResult ErrorResult(string error)
    {
        return new GridOperationResult { Success = false, Errors = new List<string> { error } };
    }
    
    public static GridOperationResult ErrorResult(List<string> errors)
    {
        return new GridOperationResult { Success = false, Errors = errors };
    }
}

/// <summary>
/// Result of grid calculations
/// </summary>
public class GridCalculationResult
{
    public decimal Subtotal { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalTax { get; set; }
    public decimal FinalTotal { get; set; }
    public List<GridLineItemCalculation> LineItemCalculations { get; set; } = new();
    public List<AppliedDiscount> AppliedDiscounts { get; set; } = new();
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Calculation details for a single line item
/// </summary>
public class GridLineItemCalculation
{
    public Guid SaleItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Weight { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? RatePerKilogram { get; set; }
    public decimal LineSubtotal { get; set; }
    public decimal LineDiscount { get; set; }
    public decimal LineTax { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsWeightBased { get; set; }
    public List<string> CalculationNotes { get; set; } = new();
}

/// <summary>
/// Validation result for the sales grid
/// </summary>
public class GridValidationResult
{
    public bool IsValid { get; set; }
    public List<GridValidationError> Errors { get; set; } = new();
    public List<GridValidationWarning> Warnings { get; set; } = new();
    public Dictionary<Guid, List<string>> ItemValidationErrors { get; set; } = new();
    
    public static GridValidationResult Valid()
    {
        return new GridValidationResult { IsValid = true };
    }
    
    public static GridValidationResult Invalid(List<GridValidationError> errors)
    {
        return new GridValidationResult { IsValid = false, Errors = errors };
    }
}

/// <summary>
/// Validation error for the sales grid
/// </summary>
public class GridValidationError
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? SaleItemId { get; set; }
    public GridValidationErrorType Type { get; set; }
}

/// <summary>
/// Validation warning for the sales grid
/// </summary>
public class GridValidationWarning
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? SaleItemId { get; set; }
    public GridValidationWarningType Type { get; set; }
}

/// <summary>
/// Current state of the sales grid
/// </summary>
public class SalesGridState
{
    public Guid SaleSessionId { get; set; }
    public List<SalesGridItem> Items { get; set; } = new();
    public GridCalculationResult Calculations { get; set; } = new();
    public Customer? Customer { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public decimal SaleLevelDiscount { get; set; }
    public string SaleLevelDiscountReason { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public bool HasUnsavedChanges { get; set; }
}

/// <summary>
/// Sales grid item representation
/// </summary>
public class SalesGridItem
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Weight { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? RatePerKilogram { get; set; }
    public decimal LineDiscount { get; set; }
    public decimal LineTax { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsWeightBased { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsEditable { get; set; } = true;
    public List<string> ValidationErrors { get; set; } = new();
    public List<string> ValidationWarnings { get; set; } = new();
    
    // Stock information
    public decimal AvailableStock { get; set; }
    public bool HasSufficientStock { get; set; } = true;
}

/// <summary>
/// Types of validation errors
/// </summary>
public enum GridValidationErrorType
{
    InvalidQuantity,
    InvalidWeight,
    InvalidPrice,
    InvalidDiscount,
    InsufficientStock,
    ExpiredProduct,
    InvalidBatchNumber,
    MissingRequiredField,
    BusinessRuleViolation
}

/// <summary>
/// Types of validation warnings
/// </summary>
public enum GridValidationWarningType
{
    LowStock,
    NearExpiry,
    HighDiscount,
    UnusualQuantity,
    PricingAlert,
    PerformanceWarning
}

/// <summary>
/// Request to update a grid item
/// </summary>
public class GridItemUpdateRequest
{
    public Guid SaleItemId { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Weight { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? LineDiscount { get; set; }
    public string? BatchNumber { get; set; }
    public string UpdateReason { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for grid calculations
/// </summary>
public class GridCalculationConfig
{
    public decimal TaxRate { get; set; }
    public bool CalculateTaxOnDiscountedAmount { get; set; } = true;
    public int DecimalPlaces { get; set; } = 2;
    public MidpointRounding RoundingMode { get; set; } = MidpointRounding.AwayFromZero;
    public bool AllowNegativeQuantities { get; set; } = false;
    public bool AllowZeroQuantities { get; set; } = false;
    public decimal MaxDiscountPercentage { get; set; } = 100;
    public bool ValidateStockLevels { get; set; } = true;
    public bool CheckProductExpiry { get; set; } = true;
}