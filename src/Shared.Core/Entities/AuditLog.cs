using Shared.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Shared.Core.Entities;

/// <summary>
/// Represents an audit log entry for security-sensitive operations
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid? UserId { get; set; }
    
    [MaxLength(100)]
    public string? Username { get; set; }
    
    public AuditAction Action { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? EntityType { get; set; }
    
    public Guid? EntityId { get; set; }
    
    [MaxLength(1000)]
    public string? OldValues { get; set; }
    
    [MaxLength(1000)]
    public string? NewValues { get; set; }
    
    [MaxLength(45)]
    public string? IpAddress { get; set; }
    
    [MaxLength(500)]
    public string? UserAgent { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public Guid DeviceId { get; set; }
    
    public DateTime? ServerSyncedAt { get; set; }
    
    public SyncStatus SyncStatus { get; set; } = SyncStatus.NotSynced;
    
    // Navigation properties
    public virtual User? User { get; set; }
}