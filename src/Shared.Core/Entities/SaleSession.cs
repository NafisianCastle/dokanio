using System.ComponentModel.DataAnnotations;
using Shared.Core.Enums;

namespace Shared.Core.Entities;

/// <summary>
/// Represents a sale session for multi-tab sales functionality
/// Provides state isolation between concurrent sales transactions
/// </summary>
public class SaleSession : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(100)]
    public string TabName { get; set; } = string.Empty;
    
    [Required]
    public Guid ShopId { get; set; }
    
    [Required]
    public Guid UserId { get; set; }
    
    /// <summary>
    /// Associated customer for this session (optional)
    /// </summary>
    public Guid? CustomerId { get; set; }
    
    /// <summary>
    /// Current payment method selected for this session
    /// </summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    
    /// <summary>
    /// Current state of the session
    /// </summary>
    public SessionState State { get; set; } = SessionState.Active;
    
    /// <summary>
    /// When the session was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the session was last modified
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Whether this session is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Device where this session is running
    /// </summary>
    public Guid DeviceId { get; set; }
    
    /// <summary>
    /// Serialized session data for state persistence
    /// </summary>
    public string? SessionData { get; set; }
    
    /// <summary>
    /// Associated sale if one has been created
    /// </summary>
    public Guid? SaleId { get; set; }
    
    // Soft delete properties
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    
    // Navigation properties
    public virtual Shop Shop { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual Customer? Customer { get; set; }
    public virtual Sale? Sale { get; set; }
}

/// <summary>
/// Represents the state of a sale session
/// </summary>
public enum SessionState
{
    /// <summary>
    /// Session is active and can be modified
    /// </summary>
    Active = 0,
    
    /// <summary>
    /// Session is temporarily suspended
    /// </summary>
    Suspended = 1,
    
    /// <summary>
    /// Session has been completed successfully
    /// </summary>
    Completed = 2,
    
    /// <summary>
    /// Session has been cancelled
    /// </summary>
    Cancelled = 3,
    
    /// <summary>
    /// Session has expired due to inactivity
    /// </summary>
    Expired = 4
}