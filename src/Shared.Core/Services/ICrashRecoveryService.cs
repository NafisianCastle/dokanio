using Shared.Core.Services;

namespace Shared.Core.Services;

/// <summary>
/// Service for crash recovery and unsaved work restoration
/// Handles application crashes and restores user work
/// </summary>
public interface ICrashRecoveryService
{
    /// <summary>
    /// Detects if the application crashed during the last session
    /// </summary>
    /// <param name="userId">User ID to check for</param>
    /// <param name="deviceId">Device ID to check for</param>
    /// <returns>True if a crash was detected</returns>
    Task<bool> DetectCrashAsync(Guid userId, Guid deviceId);
    
    /// <summary>
    /// Gets recoverable work from the last session
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="deviceId">Device ID</param>
    /// <returns>List of recoverable work items</returns>
    Task<List<RecoverableWork>> GetRecoverableWorkAsync(Guid userId, Guid deviceId);
    
    /// <summary>
    /// Restores a specific work item
    /// </summary>
    /// <param name="workItem">Work item to restore</param>
    /// <returns>Restoration result</returns>
    Task<RestorationResult> RestoreWorkItemAsync(RecoverableWork workItem);
    
    /// <summary>
    /// Marks work as successfully restored
    /// </summary>
    /// <param name="workItemId">Work item ID</param>
    /// <returns>True if marked successfully</returns>
    Task<bool> MarkWorkAsRestoredAsync(Guid workItemId);
    
    /// <summary>
    /// Discards recoverable work (user chose not to restore)
    /// </summary>
    /// <param name="workItemId">Work item ID</param>
    /// <returns>True if discarded successfully</returns>
    Task<bool> DiscardRecoverableWorkAsync(Guid workItemId);
    
    /// <summary>
    /// Records application startup to detect future crashes
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="deviceId">Device ID</param>
    /// <returns>Session ID for tracking</returns>
    Task<Guid> RecordApplicationStartupAsync(Guid userId, Guid deviceId);
    
    /// <summary>
    /// Records clean application shutdown
    /// </summary>
    /// <param name="sessionId">Session ID from startup</param>
    /// <returns>True if recorded successfully</returns>
    Task<bool> RecordCleanShutdownAsync(Guid sessionId);
    
    /// <summary>
    /// Performs automatic crash recovery on application startup
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="deviceId">Device ID</param>
    /// <returns>Crash recovery result</returns>
    Task<CrashRecoveryResult> PerformAutomaticRecoveryAsync(Guid userId, Guid deviceId);
    
    /// <summary>
    /// Gets crash recovery statistics
    /// </summary>
    /// <param name="from">Start date for statistics</param>
    /// <param name="to">End date for statistics</param>
    /// <returns>Crash recovery statistics</returns>
    Task<CrashRecoveryStatistics> GetRecoveryStatisticsAsync(DateTime? from = null, DateTime? to = null);
    
    /// <summary>
    /// Cleans up old crash recovery data
    /// </summary>
    /// <param name="olderThanDays">Clean data older than this many days</param>
    /// <returns>Number of items cleaned</returns>
    Task<int> CleanupOldRecoveryDataAsync(int olderThanDays = 30);
}

/// <summary>
/// Represents recoverable work from a crashed session
/// </summary>
public class RecoverableWork
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string WorkType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public Guid UserId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? SaleSessionId { get; set; }
    public string SerializedData { get; set; } = string.Empty;
    public WorkPriority Priority { get; set; } = WorkPriority.Normal;
    public bool IsRestored { get; set; } = false;
    public DateTime? RestoredAt { get; set; }
    public string? RestorationNotes { get; set; }
}

/// <summary>
/// Priority levels for recoverable work
/// </summary>
public enum WorkPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Result of work restoration
/// </summary>
public class RestorationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? RestoredSessionId { get; set; }
    public List<string> ActionsPerformed { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Exception? Exception { get; set; }
}

/// <summary>
/// Result of crash recovery process
/// </summary>
public class CrashRecoveryResult
{
    public bool CrashDetected { get; set; }
    public int RecoverableWorkItems { get; set; }
    public int SuccessfulRestorations { get; set; }
    public int FailedRestorations { get; set; }
    public List<RecoverableWork> AvailableWork { get; set; } = new();
    public List<string> RecoveryActions { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public TimeSpan RecoveryDuration { get; set; }
}

/// <summary>
/// Statistics about crash recovery
/// </summary>
public class CrashRecoveryStatistics
{
    public int TotalCrashes { get; set; }
    public int TotalRecoveryAttempts { get; set; }
    public int SuccessfulRecoveries { get; set; }
    public int FailedRecoveries { get; set; }
    public Dictionary<string, int> WorkTypeRecoveries { get; set; } = new();
    public TimeSpan AverageRecoveryTime { get; set; }
    public DateTime? LastCrashDate { get; set; }
    public DateTime StatisticsPeriodStart { get; set; }
    public DateTime StatisticsPeriodEnd { get; set; }
}

/// <summary>
/// Application session for crash detection
/// </summary>
public class ApplicationSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public bool CleanShutdown { get; set; } = false;
    public string? CrashReason { get; set; }
    public string ApplicationVersion { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
}