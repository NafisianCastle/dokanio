using Shared.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Shared.Core.Entities;

public class Customer : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(50)]
    public string MembershipNumber { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string? Email { get; set; }
    
    [MaxLength(20)]
    public string? Phone { get; set; }
    
    public DateTime JoinDate { get; set; } = DateTime.UtcNow;
    
    public MembershipTier Tier { get; set; } = MembershipTier.None;
    
    [Range(0, double.MaxValue)]
    public decimal TotalSpent { get; set; } = 0;
    
    [Range(0, int.MaxValue)]
    public int VisitCount { get; set; } = 0;
    
    public DateTime? LastVisit { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public Guid DeviceId { get; set; }
    
    public DateTime? ServerSyncedAt { get; set; }
    
    public SyncStatus SyncStatus { get; set; } = SyncStatus.NotSynced;
    
    // Soft delete properties
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    
    // Navigation properties
    public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();
}