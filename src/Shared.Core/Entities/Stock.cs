using Shared.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Shared.Core.Entities;

public class Stock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid ProductId { get; set; }
    
    public int Quantity { get; set; }
    
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    
    public Guid DeviceId { get; set; }
    
    public DateTime? ServerSyncedAt { get; set; }
    
    public SyncStatus SyncStatus { get; set; } = SyncStatus.NotSynced;
    
    // Navigation properties
    public virtual Product Product { get; set; } = null!;
}