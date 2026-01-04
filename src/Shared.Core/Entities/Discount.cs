using Shared.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Shared.Core.Entities;

public class Discount : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public DiscountType Type { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal Value { get; set; }
    
    public DiscountScope Scope { get; set; }
    
    // Product-specific discount
    public Guid? ProductId { get; set; }
    public virtual Product? Product { get; set; }
    
    // Category-specific discount
    [MaxLength(100)]
    public string? Category { get; set; }
    
    // Membership requirements
    public MembershipTier? RequiredMembershipTier { get; set; }
    
    // Quantity-based conditions
    [Range(0, int.MaxValue)]
    public int? MinimumQuantity { get; set; }
    
    // Amount-based conditions
    [Range(0, double.MaxValue)]
    public decimal? MinimumAmount { get; set; }
    
    // Time-based conditions
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public Guid DeviceId { get; set; }
    
    public DateTime? ServerSyncedAt { get; set; }
    
    public SyncStatus SyncStatus { get; set; } = SyncStatus.NotSynced;
    
    // Soft delete properties
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    
    // Navigation properties
    public virtual ICollection<SaleDiscount> SaleDiscounts { get; set; } = new List<SaleDiscount>();
}