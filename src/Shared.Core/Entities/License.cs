using Shared.Core.Enums;

namespace Shared.Core.Entities;

/// <summary>
/// Represents a software license for the POS system
/// </summary>
public class License : ISoftDeletable
{
    /// <summary>
    /// Unique identifier for the license
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// Unique license key for activation
    /// </summary>
    public string LicenseKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of license (Trial, Basic, Professional, Enterprise)
    /// </summary>
    public LicenseType Type { get; set; }
    
    /// <summary>
    /// Date when the license was issued
    /// </summary>
    public DateTime IssueDate { get; set; }
    
    /// <summary>
    /// Date when the license expires
    /// </summary>
    public DateTime ExpiryDate { get; set; }
    
    /// <summary>
    /// Current status of the license
    /// </summary>
    public LicenseStatus Status { get; set; }
    
    /// <summary>
    /// Name of the customer who owns this license
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Email of the customer who owns this license
    /// </summary>
    public string CustomerEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// Maximum number of devices allowed for this license
    /// </summary>
    public int MaxDevices { get; set; } = 1;
    
    /// <summary>
    /// List of features enabled for this license
    /// </summary>
    public List<string> Features { get; set; } = new();
    
    /// <summary>
    /// Date when the license was activated (null if not activated)
    /// </summary>
    public DateTime? ActivationDate { get; set; }
    
    /// <summary>
    /// Device that owns this license
    /// </summary>
    public Guid DeviceId { get; set; }
    
    // ISoftDeletable implementation
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}