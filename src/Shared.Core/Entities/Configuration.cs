using Shared.Core.Enums;

namespace Shared.Core.Entities;

/// <summary>
/// Represents a system configuration setting
/// </summary>
public class Configuration : ISoftDeletable
{
    /// <summary>
    /// Unique identifier for the configuration
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// Configuration key (unique identifier for the setting)
    /// </summary>
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// Configuration value stored as string
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of the configuration value
    /// </summary>
    public ConfigurationType Type { get; set; }
    
    /// <summary>
    /// Human-readable description of the configuration
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Indicates if this is a system-level configuration (not user-specific)
    /// </summary>
    public bool IsSystemLevel { get; set; }
    
    /// <summary>
    /// When the configuration was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// Device that created/owns this configuration
    /// </summary>
    public Guid DeviceId { get; set; }
    
    /// <summary>
    /// Last time this configuration was synced to server
    /// </summary>
    public DateTime? ServerSyncedAt { get; set; }
    
    /// <summary>
    /// Current sync status
    /// </summary>
    public SyncStatus SyncStatus { get; set; }
    
    // ISoftDeletable implementation
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}