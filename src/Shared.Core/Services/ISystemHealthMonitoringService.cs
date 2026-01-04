namespace Shared.Core.Services;

/// <summary>
/// Service for system health monitoring and diagnostic capabilities
/// Provides real-time monitoring and alerting for system health issues
/// </summary>
public interface ISystemHealthMonitoringService
{
    /// <summary>
    /// Starts continuous system health monitoring
    /// </summary>
    /// <param name="monitoringInterval">Interval between health checks</param>
    /// <returns>Task representing the monitoring operation</returns>
    Task StartMonitoringAsync(TimeSpan monitoringInterval);
    
    /// <summary>
    /// Stops continuous system health monitoring
    /// </summary>
    /// <returns>Task representing the stop operation</returns>
    Task StopMonitoringAsync();
    
    /// <summary>
    /// Gets current system health status
    /// </summary>
    /// <returns>Current system health result</returns>
    Task<SystemHealthResult> GetCurrentHealthStatusAsync();
    
    /// <summary>
    /// Gets system health history within a date range
    /// </summary>
    /// <param name="from">Start date</param>
    /// <param name="to">End date</param>
    /// <returns>Collection of health check results</returns>
    Task<IEnumerable<SystemHealthResult>> GetHealthHistoryAsync(DateTime? from = null, DateTime? to = null);
    
    /// <summary>
    /// Gets system performance metrics
    /// </summary>
    /// <returns>Current performance metrics</returns>
    Task<SystemPerformanceMetrics> GetPerformanceMetricsAsync();
    
    /// <summary>
    /// Gets diagnostic information for troubleshooting
    /// </summary>
    /// <returns>Comprehensive diagnostic information</returns>
    Task<SystemDiagnosticInfo> GetDiagnosticInfoAsync();
    
    /// <summary>
    /// Event raised when system health status changes
    /// </summary>
    event EventHandler<HealthStatusChangedEventArgs> HealthStatusChanged;
    
    /// <summary>
    /// Event raised when a critical health issue is detected
    /// </summary>
    event EventHandler<CriticalHealthIssueEventArgs> CriticalHealthIssueDetected;
}

/// <summary>
/// Represents system performance metrics
/// </summary>
public class SystemPerformanceMetrics
{
    public double DatabaseResponseTime { get; set; }
    public long DatabaseSize { get; set; }
    public int ActiveConnections { get; set; }
    public long MemoryUsage { get; set; }
    public double CpuUsage { get; set; }
    public long DiskSpaceUsed { get; set; }
    public long DiskSpaceAvailable { get; set; }
    public int TransactionThroughput { get; set; }
    public int ErrorRate { get; set; }
    public DateTime MetricsTimestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents comprehensive system diagnostic information
/// </summary>
public class SystemDiagnosticInfo
{
    public string SystemVersion { get; set; } = string.Empty;
    public string DatabaseVersion { get; set; } = string.Empty;
    public Dictionary<string, string> Configuration { get; set; } = new();
    public List<string> RecentErrors { get; set; } = new();
    public SystemPerformanceMetrics PerformanceMetrics { get; set; } = new();
    public List<HealthIssue> CurrentIssues { get; set; } = new();
    public Dictionary<string, object> AdditionalInfo { get; set; } = new();
    public DateTime DiagnosticTimestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event arguments for health status changes
/// </summary>
public class HealthStatusChangedEventArgs : EventArgs
{
    public SystemHealthResult PreviousStatus { get; set; } = new();
    public SystemHealthResult CurrentStatus { get; set; } = new();
    public DateTime ChangeTimestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event arguments for critical health issues
/// </summary>
public class CriticalHealthIssueEventArgs : EventArgs
{
    public HealthIssue Issue { get; set; } = new();
    public DateTime DetectedTimestamp { get; set; } = DateTime.UtcNow;
    public bool RequiresImmediateAttention { get; set; }
}