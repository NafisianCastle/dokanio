using Shared.Core.Entities;

namespace Shared.Core.Services;

/// <summary>
/// Service for managing offline operations queue
/// Handles data queuing when network connectivity is unavailable
/// </summary>
public interface IOfflineQueueService
{
    /// <summary>
    /// Queues an operation for later execution when online
    /// </summary>
    /// <param name="operation">Operation to queue</param>
    /// <returns>True if queued successfully</returns>
    Task<bool> QueueOperationAsync(OfflineOperation operation);
    
    /// <summary>
    /// Queues a sale for sync when online
    /// </summary>
    /// <param name="sale">Sale to queue</param>
    /// <param name="priority">Priority level</param>
    /// <returns>True if queued successfully</returns>
    Task<bool> QueueSaleAsync(Sale sale, OperationPriority priority = OperationPriority.Normal);
    
    /// <summary>
    /// Queues a product update for sync when online
    /// </summary>
    /// <param name="product">Product to queue</param>
    /// <param name="operationType">Type of operation (Create, Update, Delete)</param>
    /// <returns>True if queued successfully</returns>
    Task<bool> QueueProductUpdateAsync(Product product, OperationType operationType);
    
    /// <summary>
    /// Queues a stock update for sync when online
    /// </summary>
    /// <param name="stock">Stock to queue</param>
    /// <param name="operationType">Type of operation</param>
    /// <returns>True if queued successfully</returns>
    Task<bool> QueueStockUpdateAsync(Stock stock, OperationType operationType);
    
    /// <summary>
    /// Gets all queued operations
    /// </summary>
    /// <param name="priority">Filter by priority (optional)</param>
    /// <returns>List of queued operations</returns>
    Task<List<OfflineOperation>> GetQueuedOperationsAsync(OperationPriority? priority = null);
    
    /// <summary>
    /// Processes all queued operations when connectivity is restored
    /// </summary>
    /// <returns>Processing result with success/failure counts</returns>
    Task<QueueProcessingResult> ProcessQueuedOperationsAsync();
    
    /// <summary>
    /// Removes a processed operation from the queue
    /// </summary>
    /// <param name="operationId">Operation ID to remove</param>
    /// <returns>True if removed successfully</returns>
    Task<bool> RemoveProcessedOperationAsync(Guid operationId);
    
    /// <summary>
    /// Clears all queued operations (use with caution)
    /// </summary>
    /// <returns>Number of operations cleared</returns>
    Task<int> ClearQueueAsync();
    
    /// <summary>
    /// Gets queue statistics
    /// </summary>
    /// <returns>Queue statistics</returns>
    Task<QueueStatistics> GetQueueStatisticsAsync();
    
    /// <summary>
    /// Starts monitoring connectivity and auto-processing queue
    /// </summary>
    /// <returns>True if monitoring started</returns>
    Task<bool> StartQueueMonitoringAsync();
    
    /// <summary>
    /// Stops queue monitoring
    /// </summary>
    /// <returns>True if monitoring stopped</returns>
    Task<bool> StopQueueMonitoringAsync();
}

/// <summary>
/// Represents an offline operation to be queued
/// </summary>
public class OfflineOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OperationType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string SerializedData { get; set; } = string.Empty;
    public OperationPriority Priority { get; set; } = OperationPriority.Normal;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public bool IsProcessed { get; set; } = false;
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
    public string? ErrorMessage { get; set; }
    public Guid UserId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid ShopId { get; set; }
}

/// <summary>
/// Operation types for offline queue
/// </summary>
public enum OperationType
{
    Create = 0,
    Update = 1,
    Delete = 2,
    Sync = 3
}

/// <summary>
/// Priority levels for offline operations
/// </summary>
public enum OperationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Result of processing queued operations
/// </summary>
public class QueueProcessingResult
{
    public int TotalOperations { get; set; }
    public int SuccessfulOperations { get; set; }
    public int FailedOperations { get; set; }
    public int SkippedOperations { get; set; }
    public List<string> Errors { get; set; } = new();
    public TimeSpan ProcessingDuration { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Statistics about the offline queue
/// </summary>
public class QueueStatistics
{
    public int TotalQueuedOperations { get; set; }
    public int PendingOperations { get; set; }
    public int ProcessedOperations { get; set; }
    public int FailedOperations { get; set; }
    public Dictionary<OperationPriority, int> OperationsByPriority { get; set; } = new();
    public Dictionary<string, int> OperationsByType { get; set; } = new();
    public DateTime? OldestOperationDate { get; set; }
    public DateTime? NewestOperationDate { get; set; }
    public long QueueSizeBytes { get; set; }
}