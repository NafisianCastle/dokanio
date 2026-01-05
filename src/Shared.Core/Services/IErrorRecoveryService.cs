namespace Shared.Core.Services;

/// <summary>
/// Service for error recovery from storage and sync failures
/// Implements comprehensive error handling and recovery mechanisms
/// </summary>
public interface IErrorRecoveryService
{
    /// <summary>
    /// Recovers from storage errors by attempting database repair and restoration
    /// </summary>
    /// <param name="exception">The storage exception that occurred</param>
    /// <returns>Recovery result indicating success or failure</returns>
    Task<RecoveryResult> RecoverFromStorageErrorAsync(Exception exception);
    
    /// <summary>
    /// Recovers from sync errors by implementing retry logic and conflict resolution
    /// </summary>
    /// <param name="exception">The sync exception that occurred</param>
    /// <returns>Recovery result indicating success or failure</returns>
    Task<RecoveryResult> RecoverFromSyncErrorAsync(Exception exception);
    
    /// <summary>
    /// Recovers from concurrent operation errors by resolving conflicts and retrying
    /// </summary>
    /// <param name="exception">The concurrency exception that occurred</param>
    /// <returns>Recovery result indicating success or failure</returns>
    Task<RecoveryResult> RecoverFromConcurrencyErrorAsync(Exception exception);
    
    /// <summary>
    /// Recovers from data corruption by validating and repairing data integrity
    /// </summary>
    /// <param name="exception">The data corruption exception that occurred</param>
    /// <returns>Recovery result indicating success or failure</returns>
    Task<RecoveryResult> RecoverFromDataCorruptionAsync(Exception exception);
    
    /// <summary>
    /// Performs comprehensive system health check and recovery
    /// </summary>
    /// <returns>System health status and recovery actions taken</returns>
    Task<SystemHealthResult> PerformSystemHealthCheckAsync();
    
    /// <summary>
    /// Gets recovery statistics for monitoring and diagnostics
    /// </summary>
    /// <param name="from">Start date for statistics</param>
    /// <param name="to">End date for statistics</param>
    /// <returns>Recovery statistics</returns>
    Task<RecoveryStatistics> GetRecoveryStatisticsAsync(DateTime? from = null, DateTime? to = null);
}

/// <summary>
/// Represents the result of a recovery operation
/// </summary>
public class RecoveryResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> ActionsPerformed { get; set; } = new();
    public Exception? OriginalException { get; set; }
    public DateTime RecoveryTimestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan RecoveryDuration { get; set; }
}

/// <summary>
/// Represents the result of a system health check
/// </summary>
public class SystemHealthResult
{
    public bool IsHealthy { get; set; }
    public List<HealthIssue> Issues { get; set; } = new();
    public List<string> RecoveryActionsPerformed { get; set; } = new();
    public DateTime CheckTimestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan CheckDuration { get; set; }
}

/// <summary>
/// Represents a system health issue
/// </summary>
public class HealthIssue
{
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public HealthSeverity Severity { get; set; }
    public bool IsResolved { get; set; }
    public string? ResolutionAction { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
}

/// <summary>
/// Represents recovery statistics for monitoring
/// </summary>
public class RecoveryStatistics
{
    public int TotalRecoveryAttempts { get; set; }
    public int SuccessfulRecoveries { get; set; }
    public int FailedRecoveries { get; set; }
    public Dictionary<string, int> RecoveryTypeCount { get; set; } = new();
    public TimeSpan AverageRecoveryTime { get; set; }
    public DateTime StatisticsPeriodStart { get; set; }
    public DateTime StatisticsPeriodEnd { get; set; }
}

/// <summary>
/// Health issue severity levels
/// </summary>
public enum HealthSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}