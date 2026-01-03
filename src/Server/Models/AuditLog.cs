using System.ComponentModel.DataAnnotations;

namespace Server.Models;

/// <summary>
/// Represents an audit log entry for tracking data modifications
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(50)]
    public string Operation { get; set; } = string.Empty; // CREATE, UPDATE, DELETE, SYNC
    
    [Required]
    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;
    
    public Guid EntityId { get; set; }
    
    public string? OldValues { get; set; }
    
    public string? NewValues { get; set; }
    
    public Guid DeviceId { get; set; }
    
    [MaxLength(100)]
    public string? UserId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [MaxLength(45)]
    public string? IpAddress { get; set; }
    
    [MaxLength(500)]
    public string? UserAgent { get; set; }
}