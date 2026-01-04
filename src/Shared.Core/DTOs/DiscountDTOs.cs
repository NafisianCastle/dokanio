using Shared.Core.Enums;

namespace Shared.Core.DTOs;

public class DiscountCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public DiscountScope Scope { get; set; }
    public Guid? ProductId { get; set; }
    public string? Category { get; set; }
    public MembershipTier? RequiredMembershipTier { get; set; }
    public int? MinimumQuantity { get; set; }
    public decimal? MinimumAmount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public bool IsActive { get; set; } = true;
}

public class DiscountDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public DiscountScope Scope { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? Category { get; set; }
    public MembershipTier? RequiredMembershipTier { get; set; }
    public int? MinimumQuantity { get; set; }
    public decimal? MinimumAmount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DiscountCalculationResult
{
    public decimal TotalDiscountAmount { get; set; }
    public List<AppliedDiscount> AppliedDiscounts { get; set; } = new();
    public List<string> DiscountReasons { get; set; } = new();
}

public class AppliedDiscount
{
    public Guid DiscountId { get; set; }
    public string DiscountName { get; set; } = string.Empty;
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public decimal CalculatedAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class DiscountValidationResult
{
    public bool IsValid { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
}