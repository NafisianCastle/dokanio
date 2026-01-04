using Shared.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Shared.Core.Entities;

/// <summary>
/// Represents a user in the multi-business POS system
/// </summary>
public class User : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// The business this user belongs to
    /// </summary>
    [Required]
    public Guid BusinessId { get; set; }
    
    /// <summary>
    /// The specific shop this user is assigned to (nullable for business owners)
    /// </summary>
    public Guid? ShopId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    [Required]
    public string Salt { get; set; } = string.Empty;
    
    public UserRole Role { get; set; } = UserRole.Cashier;
    
    /// <summary>
    /// JSON representation of user permissions for fine-grained access control
    /// </summary>
    public string? Permissions { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastLoginAt { get; set; }
    
    public DateTime? LastActivityAt { get; set; }
    
    public Guid DeviceId { get; set; }
    
    public DateTime? ServerSyncedAt { get; set; }
    
    public SyncStatus SyncStatus { get; set; } = SyncStatus.NotSynced;
    
    // Soft delete properties
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    
    // Navigation properties
    public virtual Business Business { get; set; } = null!;
    public virtual Shop? Shop { get; set; }
    public virtual ICollection<Business> OwnedBusinesses { get; set; } = new List<Business>();
    public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}