using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Service for tracking and analyzing errors throughout the application
/// Provides error aggregation, pattern detection, and alerting capabilities
/// </summary>
public interface IErrorTrackingService
{
    /// <summary>
    /// Records an error occurrence for tracking and analysis
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="context">Context information about where the error occurred</param>
    /// <param name="userId">User associated with the error (if applicable)</param>
    /// <param name="businessId">Business context (if applicable)</param>
    /// <param name="deviceId">Device where the error occurred</param>
    /// <param name="additionalData">Additional error metadata</param>
    Task RecordErrorAsync(Exception exception, string context, Guid? userId = null, Guid? businessId = null, Guid deviceId = default, Dictionary<string, object>? additionalData = null);

    /// <summary>
    /// Records a custom error event for tracking
    /// </summary>
    /// <param name="errorType">Type of error</param>
    /// <param name="message">Error message</param>
    /// <param name="severity">Error severity level</param>
    /// <param name="context">Context information</param>
    /// <param name="userId">User associated with the error (if applicable)</param>
    /// <param name="businessId">Business context (if applicable)</param>
    /// <param name="deviceId">Device where the error occurred</param>
    /// <param name="additionalData">Additional error metadata</param>
    Task RecordCustomErrorAsync(string errorType, string message, ErrorSeverity severity, string context, Guid? userId = null, Guid? businessId = null, Guid deviceId = default, Dictionary<string, object>? additionalData = null);

    /// <summary>
    /// Gets error statistics for a specific time period
    /// </summary>
    /// <param name="period">Time period to analyze</param>
    /// <param name="businessId">Business filter (optional)</param>
    /// <returns>Error statistics</returns>
    Task<ErrorStatistics> GetErrorStatisticsAsync(TimeSpan period, Guid? businessId = null);

    /// <summary>
    /// Gets error trends over time
    /// </summary>
    /// <param name="period">Time period to analyze</param>
    /// <param name="businessId">Business filter (optional)</param>
    /// <returns>Error trends</returns>
    Task<ErrorTrends> GetErrorTrendsAsync(TimeSpan period, Guid? businessId = null);

    /// <summary>
    /// Gets the most frequent errors
    /// </summary>
    /// <param name="period">Time period to analyze</param>
    /// <param name="limit">Maximum number of errors to return</param>
    /// <param name="businessId">Business filter (optional)</param>
    /// <returns>Most frequent errors</returns>
    Task<List<FrequentError>> GetMostFrequentErrorsAsync(TimeSpan period, int limit = 10, Guid? businessId = null);

    /// <summary>
    /// Gets error patterns and correlations
    /// </summary>
    /// <param name="period">Time period to analyze</param>
    /// <param name="businessId">Business filter (optional)</param>
    /// <returns>Error patterns</returns>
    Task<ErrorPatterns> GetErrorPatternsAsync(TimeSpan period, Guid? businessId = null);

    /// <summary>
    /// Gets errors by severity level
    /// </summary>
    /// <param name="severity">Severity level to filter by</param>
    /// <param name="period">Time period to analyze</param>
    /// <param name="businessId">Business filter (optional)</param>
    /// <returns>Errors by severity</returns>
    Task<List<ErrorOccurrence>> GetErrorsBySeverityAsync(ErrorSeverity severity, TimeSpan period, Guid? businessId = null);

    /// <summary>
    /// Resolves an error (marks it as handled)
    /// </summary>
    /// <param name="errorId">Error ID to resolve</param>
    /// <param name="resolution">Resolution description</param>
    /// <param name="resolvedBy">User who resolved the error</param>
    Task ResolveErrorAsync(Guid errorId, string resolution, Guid resolvedBy);

    /// <summary>
    /// Gets unresolved critical errors
    /// </summary>
    /// <param name="businessId">Business filter (optional)</param>
    /// <returns>Unresolved critical errors</returns>
    Task<List<ErrorOccurrence>> GetUnresolvedCriticalErrorsAsync(Guid? businessId = null);
}

/// <summary>
/// Error statistics data structure
/// </summary>
public class ErrorStatistics
{
    public TimeSpan Period { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalErrors { get; set; }
    public int UniqueErrors { get; set; }
    public int CriticalErrors { get; set; }
    public int HighSeverityErrors { get; set; }
    public int MediumSeverityErrors { get; set; }
    public int LowSeverityErrors { get; set; }
    public double ErrorRate { get; set; }
    public List<ErrorByCategory> ErrorsByCategory { get; set; } = new();
    public List<ErrorByContext> ErrorsByContext { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Error trends over time
/// </summary>
public class ErrorTrends
{
    public TimeSpan Period { get; set; }
    public List<ErrorTrendPoint> TrendPoints { get; set; } = new();
    public TrendDirection Direction { get; set; }
    public double ChangePercentage { get; set; }
    public List<SeverityTrend> SeverityTrends { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Error trend point
/// </summary>
public class ErrorTrendPoint
{
    public DateTime Timestamp { get; set; }
    public int ErrorCount { get; set; }
    public double ErrorRate { get; set; }
    public Dictionary<ErrorSeverity, int> SeverityBreakdown { get; set; } = new();
}

/// <summary>
/// Severity trend data
/// </summary>
public class SeverityTrend
{
    public ErrorSeverity Severity { get; set; }
    public List<ErrorTrendPoint> TrendPoints { get; set; } = new();
    public TrendDirection Direction { get; set; }
    public double ChangePercentage { get; set; }
}

/// <summary>
/// Frequent error data
/// </summary>
public class FrequentError
{
    public string ErrorType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public ErrorSeverity Severity { get; set; }
    public int OccurrenceCount { get; set; }
    public DateTime FirstOccurrence { get; set; }
    public DateTime LastOccurrence { get; set; }
    public List<string> AffectedUsers { get; set; } = new();
    public List<string> AffectedDevices { get; set; } = new();
    public bool IsResolved { get; set; }
}

/// <summary>
/// Error patterns and correlations
/// </summary>
public class ErrorPatterns
{
    public TimeSpan Period { get; set; }
    public List<ErrorCorrelation> Correlations { get; set; } = new();
    public List<ErrorCluster> Clusters { get; set; } = new();
    public List<ErrorSequenceData> CommonSequences { get; set; } = new();
    public List<ErrorHotspot> Hotspots { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Error correlation data
/// </summary>
public class ErrorCorrelation
{
    public string ErrorType1 { get; set; } = string.Empty;
    public string ErrorType2 { get; set; } = string.Empty;
    public double CorrelationStrength { get; set; }
    public int CoOccurrenceCount { get; set; }
    public TimeSpan AverageTimeBetween { get; set; }
}

/// <summary>
/// Error cluster data
/// </summary>
public class ErrorCluster
{
    public string ClusterName { get; set; } = string.Empty;
    public List<string> ErrorTypes { get; set; } = new();
    public int TotalOccurrences { get; set; }
    public string CommonContext { get; set; } = string.Empty;
    public ErrorSeverity AverageSeverity { get; set; }
}

/// <summary>
/// Error sequence data
/// </summary>
public class ErrorSequenceData
{
    public List<string> ErrorSequence { get; set; } = new();
    public int OccurrenceCount { get; set; }
    public TimeSpan AverageSequenceDuration { get; set; }
    public string CommonContext { get; set; } = string.Empty;
}

/// <summary>
/// Error hotspot data
/// </summary>
public class ErrorHotspot
{
    public string Context { get; set; } = string.Empty;
    public string Component { get; set; } = string.Empty;
    public int ErrorCount { get; set; }
    public double ErrorDensity { get; set; }
    public List<string> CommonErrorTypes { get; set; } = new();
}

/// <summary>
/// Error occurrence data
/// </summary>
public class ErrorOccurrence
{
    public Guid Id { get; set; }
    public string ErrorType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public ErrorSeverity Severity { get; set; }
    public Guid? UserId { get; set; }
    public Guid? BusinessId { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime OccurredAt { get; set; }
    public bool IsResolved { get; set; }
    public string? Resolution { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// Error by category data
/// </summary>
public class ErrorByCategory
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
    public ErrorSeverity AverageSeverity { get; set; }
}

/// <summary>
/// Error by context data
/// </summary>
public class ErrorByContext
{
    public string Context { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
    public List<string> CommonErrorTypes { get; set; } = new();
}

/// <summary>
/// Optimization priority enumeration
/// </summary>
public enum OptimizationPriority
{
    Low,
    Medium,
    High,
    Critical
}