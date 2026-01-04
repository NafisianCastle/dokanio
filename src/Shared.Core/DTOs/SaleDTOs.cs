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
}