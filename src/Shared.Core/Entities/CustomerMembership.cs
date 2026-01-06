using System.ComponentModel.DataAnnotations;
using Shared.Core.Enums;

namespace Shared.Core.Entities;

/// <summary>
/// Represents customer membership details and benefits
/// Provides detailed membership information separate from basic customer data
/// </summary>
public class CustomerMembership : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Reference to the customer this membership belongs to
    /// </summary>
    [Required]
    public Guid CustomerId { get; set; }
    
    /// <summary>
    /// Current membership tier
    /// </summary>
    public MembershipTier Tier { get; set; } = MembershipTier.Bronze;
    
    /// <summary>
    /// When the customer joined the membership program
    /// </summary>
    public DateTime JoinDate { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the membership expires (null for lifetime memberships)
    /// </summary>
    public DateTime? ExpiryDate { get; set; }
    
    /// <summary>
    /// Discount percentage for this membership tier
    /// </summary>
    [Range(0, 100)]
    public decimal DiscountPercentage { get; set; } = 0;
    
    /// <summary>
    /// Points accumulated by the customer
    /// </summary>
    [Range(0, int.MaxValue)]
    public int Points { get; set; } = 0;
    
    /// <summary>
    /// Total amount spent to achieve current tier
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal TotalSpentForTier { get; set; } = 0;
    
    /// <summary>
    /// Whether the membership is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// When the membership was last updated
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Device where this membership was created/updated
    /// </summary>
    public Guid DeviceId { get; set; }
    
    /// <summary>
    /// Server synchronization timestamp
    /// </summary>
    public DateTime? ServerSyncedAt { get; set; }
    
    /// <summary>
    /// Synchronization status
    /// </summary>
    public SyncStatus SyncStatus { get; set; } = SyncStatus.NotSynced;
    
    // Soft delete properties
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    
    // Navigation properties
    public virtual Customer Customer { get; set; } = null!;
    public virtual ICollection<MembershipBenefit> Benefits { get; set; } = new List<MembershipBenefit>();
}