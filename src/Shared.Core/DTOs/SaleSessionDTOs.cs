using Shared.Core.Enums;
using Shared.Core.Entities;

namespace Shared.Core.DTOs;

/// <summary>
/// Data transfer object for sale session information
/// </summary>
public class SaleSessionDto
{
    public Guid Id { get; set; }
    public string TabName { get; set; } = string.Empty;
    public Guid ShopId { get; set; }
    public Guid UserId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public SessionState State { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsActive { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? SaleId { get; set; }
    public List<SaleSessionItemDto> Items { get; set; } = new();
    public SaleSessionCalculationDto Calculation { get; set; } = new();
}

/// <summary>
/// Represents an item in a sale session
/// </summary>
public class SaleSessionItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Weight { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsWeightBased { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? BatchNumber { get; set; }
    public List<AppliedDiscountDto> AppliedDiscounts { get; set; } = new();
}

/// <summary>
/// Represents calculation details for a sale session
/// </summary>
public class SaleSessionCalculationDto
{
    public decimal Subtotal { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalTax { get; set; }
    public decimal FinalTotal { get; set; }
    public List<CalculationBreakdownDto> Breakdown { get; set; } = new();
    public DateTime CalculatedAt { get; set; }
}

/// <summary>
/// Represents a calculation breakdown item
/// </summary>
public class CalculationBreakdownDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public CalculationType Type { get; set; }
}

/// <summary>
/// Represents an applied discount
/// </summary>
public class AppliedDiscountDto
{
    public Guid DiscountId { get; set; }
    public string DiscountName { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Types of calculations
/// </summary>
public enum CalculationType
{
    Subtotal,
    Discount,
    Tax,
    MembershipDiscount,
    Total
}

/// <summary>
/// Result of session operations
/// </summary>
public class SessionOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public SaleSessionDto? Session { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Request to create a new sale session
/// </summary>
public class CreateSaleSessionRequest
{
    public string TabName { get; set; } = string.Empty;
    public Guid ShopId { get; set; }
    public Guid UserId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? CustomerId { get; set; }
}

/// <summary>
/// Request to update a sale session
/// </summary>
public class UpdateSaleSessionRequest
{
    public Guid SessionId { get; set; }
    public string? TabName { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public Guid? CustomerId { get; set; }
    public List<SaleSessionItemDto>? Items { get; set; }
}

/// <summary>
/// Result of session state save operation
/// </summary>
public class SaveSessionStateResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime SavedAt { get; set; }
    public List<string> Errors { get; set; } = new();
}