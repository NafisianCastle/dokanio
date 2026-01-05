using Shared.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Shared.Core.Entities;

public class Stock : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// The shop where this stock is maintained
    /// </summary>
    [Required]
    public Guid ShopId { get; set; }
    
    public Guid ProductId { get; set; }
    
    public int Quantity { get; set; }
    
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public Guid DeviceId { get; set; }
    
    public DateTime? ServerSyncedAt { get; set; }
    
    public SyncStatus SyncStatus { get; set; } = SyncStatus.NotSynced;
    
    // Server-side properties for conflict resolution testing
    public int? ServerQuantity { get; set; }
    public DateTime? ServerLastUpdatedAt { get; set; }
    public Guid? ServerDeviceId { get; set; }
    
    // Soft delete properties
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    
    // Navigation properties
    public virtual Shop Shop { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}