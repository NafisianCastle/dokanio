using Shared.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Shared.Core.Entities;

public class Sale : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;
    
    [Range(0, double.MaxValue)]
    public decimal TotalAmount { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal DiscountAmount { get; set; } = 0;
    
    [Range(0, double.MaxValue)]
    public decimal TaxAmount { get; set; } = 0;
    
    [Range(0, double.MaxValue)]
    public decimal MembershipDiscountAmount { get; set; } = 0;
    
    public PaymentMethod PaymentMethod { get; set; }
    
    // Customer and membership
    public Guid? CustomerId { get; set; }
    public virtual Customer? Customer { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public Guid DeviceId { get; set; }
    
    public DateTime? ServerSyncedAt { get; set; }
    
    public SyncStatus SyncStatus { get; set; } = SyncStatus.NotSynced;
    
    // Soft delete properties
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    
    // Navigation properties
    public virtual ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}