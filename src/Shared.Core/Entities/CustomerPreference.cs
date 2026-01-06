using System.ComponentModel.DataAnnotations;
using Shared.Core.Enums;

namespace Shared.Core.Entities;

/// <summary>
/// Represents customer preferences and settings
/// </summary>
public class CustomerPreference : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Reference to the customer these preferences belong to
    /// </summary>
    [Required]
    public Guid CustomerId { get; set; }
    
    /// <summary>
    /// Preference key/name
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// Preference value
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// Category of the preference
    /// </summary>
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this preference is active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// When this preference was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this preference was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Device where this preference was created/updated
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
}