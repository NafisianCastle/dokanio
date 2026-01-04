using Shared.Core.Entities;

namespace Shared.Core.Services;

/// <summary>
/// Service for logging transactions to ensure data durability
/// Implements transaction logging for offline-first persistence
/// </summary>
public interface ITransactionLogService
{
    /// <summary>
    /// Logs a transaction before it's committed to ensure durability
    /// </summary>
    /// <param name="operation">The operation being performed (INSERT, UPDATE, DELETE)</param>
    /// <param name="entityType">The type of entity being modified</param>
    /// <param name="entityId">The ID of the entity being modified</param>
    /// <param name="entityData">Serialized entity data</param>
    /// <param name="deviceId">Device performing the operation</param>
    Task LogTransactionAsync(string operation, string entityType, Guid entityId, string entityData, Guid deviceId);
    
    /// <summary>
    /// Logs multiple transactions as a batch for atomic operations
    /// </summary>
    /// <param name="transactions">Collection of transaction log entries</param>
    Task LogTransactionBatchAsync(IEnumerable<TransactionLogEntry> transactions);
    
    /// <summary>
    /// Gets all unprocessed transaction logs for recovery purposes
    /// </summary>
    /// <returns>Collection of unprocessed transaction logs</returns>
    Task<IEnumerable<TransactionLogEntry>> GetUnprocessedLogsAsync();
    
    /// <summary>
    /// Marks transaction logs as processed after successful commit
    /// </summary>
    /// <param name="logIds">IDs of the logs to mark as processed</param>
    Task MarkLogsAsProcessedAsync(IEnumerable<Guid> logIds);
    
    /// <summary>
    /// Recovers data from transaction logs in case of system failure
    /// </summary>
    /// <returns>Number of transactions recovered</returns>
    Task<int> RecoverFromLogsAsync();
}

/// <summary>
/// Represents a transaction log entry for durability
/// </summary>
public class TransactionLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Operation { get; set; } = string.Empty; // INSERT, UPDATE, DELETE
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string EntityData { get; set; } = string.Empty; // JSON serialized entity
    public Guid DeviceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsProcessed { get; set; } = false;
    public DateTime? ProcessedAt { get; set; }
}