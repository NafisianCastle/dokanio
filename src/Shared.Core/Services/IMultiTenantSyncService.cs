using Shared.Core.DTOs;
using Shared.Core.Entities;

namespace Shared.Core.Services;

/// <summary>
/// Multi-tenant synchronization service that extends existing sync capabilities
/// with business and shop-level data isolation and conflict resolution
/// </summary>
public interface IMultiTenantSyncService
{
    /// <summary>
    /// Synchronizes all data for a specific business
    /// </summary>
    /// <param name="businessId">Business ID to sync</param>
    /// <returns>Result of the business synchronization</returns>
    Task<MultiTenantSyncResult> SyncBusinessDataAsync(Guid businessId);
    
    /// <summary>
    /// Synchronizes data for a specific shop
    /// </summary>
    /// <param name="shopId">Shop ID to sync</param>
    /// <returns>Result of the shop synchronization</returns>
    Task<MultiTenantSyncResult> SyncShopDataAsync(Guid shopId);
    
    /// <summary>
    /// Resolves data conflicts using business-specific rules
    /// </summary>
    /// <param name="conflicts">Array of data conflicts to resolve</param>
    /// <returns>Result of conflict resolution</returns>
    Task<ConflictResolutionResult> ResolveDataConflictsAsync(DataConflict[] conflicts);
    
    /// <summary>
    /// Validates that data belongs to the correct tenant (business/shop)
    /// </summary>
    /// <param name="businessId">Business ID for validation</param>
    /// <param name="data">Data object to validate</param>
    /// <returns>True if data isolation is valid, false otherwise</returns>
    Task<bool> ValidateTenantIsolationAsync(Guid businessId, object data);
    
    /// <summary>
    /// Synchronizes business and shop metadata
    /// </summary>
    /// <param name="businessId">Business ID to sync metadata for</param>
    /// <returns>Result of metadata synchronization</returns>
    Task<MultiTenantSyncResult> SyncBusinessMetadataAsync(Guid businessId);
    
    /// <summary>
    /// Gets synchronization status for all shops in a business
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <returns>Synchronization status for each shop</returns>
    Task<IEnumerable<ShopSyncStatus>> GetBusinessSyncStatusAsync(Guid businessId);
    
    /// <summary>
    /// Performs bulk synchronization for multiple shops
    /// </summary>
    /// <param name="shopIds">Collection of shop IDs to sync</param>
    /// <returns>Results of bulk synchronization</returns>
    Task<BulkSyncResult> BulkSyncShopsAsync(IEnumerable<Guid> shopIds);
}

/// <summary>
/// Result of multi-tenant synchronization operation
/// </summary>
public class MultiTenantSyncResult : SyncResult
{
    public Guid BusinessId { get; set; }
    public Guid? ShopId { get; set; }
    public int BusinessRecordsSynced { get; set; }
    public int ShopRecordsSynced { get; set; }
    public List<TenantIsolationViolation> IsolationViolations { get; set; } = new();
    public Dictionary<string, object> BusinessSpecificMetrics { get; set; } = new();
}

/// <summary>
/// Result of conflict resolution operation
/// </summary>
public class ConflictResolutionResult
{
    public bool Success { get; set; }
    public int ConflictsResolved { get; set; }
    public int ConflictsRemaining { get; set; }
    public List<string> ResolutionStrategies { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public DateTime ResolvedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a data conflict between local and server data
/// </summary>
public class DataConflict
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid BusinessId { get; set; }
    public Guid? ShopId { get; set; }
    public object LocalData { get; set; } = null!;
    public object ServerData { get; set; } = null!;
    public DateTime LocalTimestamp { get; set; }
    public DateTime ServerTimestamp { get; set; }
    public ConflictType Type { get; set; }
    public string ConflictReason { get; set; } = string.Empty;
}

/// <summary>
/// Types of data conflicts
/// </summary>
public enum ConflictType
{
    UpdateConflict,
    DeleteConflict,
    CreateConflict,
    BusinessRuleViolation,
    TenantIsolationViolation
}

/// <summary>
/// Represents a tenant isolation violation
/// </summary>
public class TenantIsolationViolation
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid ExpectedBusinessId { get; set; }
    public Guid ActualBusinessId { get; set; }
    public string ViolationType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Synchronization status for a shop
/// </summary>
public class ShopSyncStatus
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public DateTime? LastSyncTime { get; set; }
    public bool IsOnline { get; set; }
    public int PendingUploads { get; set; }
    public int PendingDownloads { get; set; }
    public List<string> SyncErrors { get; set; } = new();
    public SyncHealthStatus HealthStatus { get; set; }
}

/// <summary>
/// Health status of synchronization
/// </summary>
public enum SyncHealthStatus
{
    Healthy,
    Warning,
    Error,
    Offline
}

/// <summary>
/// Result of bulk synchronization operation
/// </summary>
public class BulkSyncResult
{
    public bool Success { get; set; }
    public int TotalShops { get; set; }
    public int SuccessfulSyncs { get; set; }
    public int FailedSyncs { get; set; }
    public List<MultiTenantSyncResult> Results { get; set; } = new();
    public TimeSpan TotalDuration { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}