using System.ComponentModel.DataAnnotations;
using Shared.Core.Enums;

namespace Shared.Core.Entities;

/// <summary>
/// Represents a specific benefit available to a customer membership
/// </summary>
public class MembershipBenefit : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Reference to the customer membership this benefit belongs to
    /// </summary>
    [Required]
    public Guid CustomerMembershipId { get; set; }
    
    /// <summary>
    /// Name of the benefit
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of the benefit
    /// </summary>
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of benefit
    /// </summary>
    public BenefitType Type { get; set; }
    
    /// <summary>
    /// Value of the benefit (percentage, amount, etc.)
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal Value { get; set; } = 0;
    
    /// <summary>
    /// Whether this benefit is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// When this benefit starts being valid
    /// </summary>
    public DateTime? StartDate { get; set; }
    
    /// <summary>
    /// When this benefit expires
    /// </summary>
    public DateTime? EndDate { get; set; }
    
    /// <summary>
    /// Maximum number of times this benefit can be used
    /// </summary>
    public int? MaxUsages { get; set; }
    
    /// <summary>
    /// Number of times this benefit has been used
    /// </summary>
    [Range(0, int.MaxValue)]
    public int UsageCount { get; set; } = 0;
    
    /// <summary>
    /// When this benefit was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Device where this benefit was created
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
    public virtual CustomerMembership CustomerMembership { get; set; } = null!;
}

/// <summary>
/// Types of membership benefits
/// </summary>
public enum BenefitType
{
    /// <summary>
    /// Percentage discount on purchases
    /// </summary>
    PercentageDiscount = 0,
    
    /// <summary>
    /// Fixed amount discount
    /// </summary>
    FixedAmountDiscount = 1,
    
    /// <summary>
    /// Free shipping benefit
    /// </summary>
    FreeShipping = 2,
    
    /// <summary>
    /// Early access to products/sales
    /// </summary>
    EarlyAccess = 3,
    
    /// <summary>
    /// Bonus points multiplier
    /// </summary>
    BonusPoints = 4,
    
    /// <summary>
    /// Other custom benefits
    /// </summary>
    Other = 5
}