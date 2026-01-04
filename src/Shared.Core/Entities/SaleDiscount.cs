using System.ComponentModel.DataAnnotations;

namespace Shared.Core.Entities;

public class SaleDiscount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid SaleId { get; set; }
    public virtual Sale Sale { get; set; } = null!;
    
    public Guid DiscountId { get; set; }
    public virtual Discount Discount { get; set; } = null!;
    
    [Range(0, double.MaxValue)]
    public decimal DiscountAmount { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string DiscountReason { get; set; } = string.Empty;
    
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
}