using System.ComponentModel.DataAnnotations;

namespace Server.Models;

/// <summary>
/// Represents a registered POS device
/// </summary>
public class Device
{
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(500)]
    public string ApiKey { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastSyncAt { get; set; }
    
    public string? LastSyncVersion { get; set; }
}