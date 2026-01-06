namespace Shared.Core.Services;

/// <summary>
/// Enhanced error recovery service that integrates all recovery mechanisms
/// Provides a unified interface for comprehensive error recovery and resilience
/// </summary>
public interface IEnhancedErrorRecoveryService
{
    /// <summary>
    /// Performs comprehensive error recovery including transaction state, offline queue, and crash recovery
    /// </summary>
    /// <param name="exception">The exception that triggered recovery</param>
    /// <param name="userId">User ID for context</param>
    /// <param name="deviceId">Device ID for context</param>
    /// <returns>Comprehensive recovery result</returns>
    Task<ComprehensiveRecoveryResult> PerformComprehensiveRecoveryAsync(Exception exception, Guid userId, Guid deviceId);
    
    /// <summary>
    /// Initializes error recovery system on application startup
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="deviceId">Device ID</param>
    /// <returns>Initialization result</returns>
    Task<RecoveryInitializationResult> InitializeRecoverySystemAsync(Guid userId, Guid deviceId);
    
    /// <summary>
    /// Performs graceful shutdown with state preservation
    /// </summary>
    /// <param name="sessionId">Application session ID</param>
    /// <returns>Shutdown result</returns>
    Task<ShutdownResult> PerformGracefulShutdownAsync(Guid sessionId);
    
    /// <summary>
    /// Gets comprehensive recovery status and statistics
    /// </summary>
    /// <returns>Recovery status information</returns>
    Task<RecoveryStatusResult> GetRecoveryStatusAsync();
}

/// <summary>
/// Result of comprehensive recovery operation
/// </summary>
public class ComprehensiveRecoveryResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> RecoveryActions { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public TimeSpan RecoveryDuration { get; set; }
    
    // Individual recovery results
    public RecoveryResult? StorageRecovery { get; set; }
    public RecoveryResult? SyncRecovery { get; set; }
    public CrashRecoveryResult? CrashRecovery { get; set; }
    public QueueProcessingResult? QueueProcessing { get; set; }
    
    // Recovery statistics
    public int TransactionStatesRestored { get; set; }
    public int OfflineOperationsProcessed { get; set; }
    public int CrashRecoveryItemsRestored { get; set; }
}

/// <summary>
/// Result of recovery system initialization
/// </summary>
public class RecoveryInitializationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid ApplicationSessionId { get; set; }
    public bool CrashDetected { get; set; }
    public int RecoverableWorkItems { get; set; }
    public List<string> InitializationActions { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Result of graceful shutdown
/// </summary>
public class ShutdownResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TransactionStatesSaved { get; set; }
    public int QueuedOperations { get; set; }
    public List<string> ShutdownActions { get; set; } = new();
}

/// <summary>
/// Current recovery status and statistics
/// </summary>
public class RecoveryStatusResult
{
    public bool SystemHealthy { get; set; }
    public int ActiveTransactionStates { get; set; }
    public int PendingOfflineOperations { get; set; }
    public int UnresolvedCrashRecoveryItems { get; set; }
    public DateTime LastHealthCheck { get; set; }
    public List<string> CurrentIssues { get; set; } = new();
    public Dictionary<string, object> Statistics { get; set; } = new();
}