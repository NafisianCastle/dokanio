using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Enhanced performance monitoring service with comprehensive metrics collection and alerting
/// Extends the base performance monitoring with additional capabilities for production use
/// </summary>
public interface IEnhancedPerformanceMonitoringService : IPerformanceMonitoringService
{
    /// <summary>
    /// Records business-specific performance metrics
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="metricName">Name of the metric</param>
    /// <param name="value">Metric value</param>
    /// <param name="unit">Unit of measurement</param>
    /// <param name="metadata">Additional metric metadata</param>
    Task RecordBusinessMetricAsync(Guid businessId, string metricName, double value, string? unit = null, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Records user interaction performance metrics
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="action">User action</param>
    /// <param name="duration">Action duration</param>
    /// <param name="success">Whether the action was successful</param>
    /// <param name="deviceId">Device used</param>
    /// <param name="metadata">Additional metadata</param>
    Task RecordUserInteractionAsync(Guid userId, string action, TimeSpan duration, bool success, Guid deviceId, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Records database performance metrics
    /// </summary>
    /// <param name="queryType">Type of database query</param>
    /// <param name="duration">Query duration</param>
    /// <param name="recordsAffected">Number of records affected</param>
    /// <param name="success">Whether the query was successful</param>
    /// <param name="businessId">Business context (optional)</param>
    Task RecordDatabaseMetricAsync(string queryType, TimeSpan duration, int recordsAffected, bool success, Guid? businessId = null);

    /// <summary>
    /// Records API endpoint performance metrics
    /// </summary>
    /// <param name="endpoint">API endpoint</param>
    /// <param name="method">HTTP method</param>
    /// <param name="statusCode">Response status code</param>
    /// <param name="duration">Request duration</param>
    /// <param name="requestSize">Request size in bytes</param>
    /// <param name="responseSize">Response size in bytes</param>
    /// <param name="userId">User making the request (optional)</param>
    /// <param name="businessId">Business context (optional)</param>
    Task RecordApiMetricAsync(string endpoint, string method, int statusCode, TimeSpan duration, long requestSize, long responseSize, Guid? userId = null, Guid? businessId = null);

    /// <summary>
    /// Gets comprehensive performance report for a business
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="period">Time period to analyze</param>
    /// <returns>Comprehensive performance report</returns>
    Task<BusinessPerformanceReport> GetBusinessPerformanceReportAsync(Guid businessId, TimeSpan period);

    /// <summary>
    /// Gets system-wide performance dashboard data
    /// </summary>
    /// <param name="period">Time period to analyze</param>
    /// <returns>Performance dashboard data</returns>
    Task<PerformanceDashboard> GetPerformanceDashboardAsync(TimeSpan period);

    /// <summary>
    /// Gets performance comparison between time periods
    /// </summary>
    /// <param name="currentPeriod">Current time period</param>
    /// <param name="comparisonPeriod">Comparison time period</param>
    /// <param name="businessId">Business filter (optional)</param>
    /// <returns>Performance comparison</returns>
    Task<PerformanceComparison> GetPerformanceComparisonAsync(TimeSpan currentPeriod, TimeSpan comparisonPeriod, Guid? businessId = null);

    /// <summary>
    /// Gets performance bottlenecks and recommendations
    /// </summary>
    /// <param name="businessId">Business filter (optional)</param>
    /// <param name="period">Analysis period</param>
    /// <returns>Performance bottlenecks and recommendations</returns>
    Task<PerformanceAnalysis> GetPerformanceAnalysisAsync(Guid? businessId, TimeSpan period);

    /// <summary>
    /// Configures performance monitoring thresholds and alerts
    /// </summary>
    /// <param name="configuration">Performance monitoring configuration</param>
    Task ConfigurePerformanceMonitoringAsync(PerformanceMonitoringConfiguration configuration);

    /// <summary>
    /// Gets real-time performance metrics for monitoring dashboards
    /// </summary>
    /// <param name="businessId">Business filter (optional)</param>
    /// <returns>Real-time performance metrics</returns>
    Task<RealTimePerformanceMetrics> GetRealTimeMetricsAsync(Guid? businessId = null);

    /// <summary>
    /// Exports performance data for external analysis
    /// </summary>
    /// <param name="period">Time period to export</param>
    /// <param name="format">Export format (CSV, JSON, etc.)</param>
    /// <param name="businessId">Business filter (optional)</param>
    /// <returns>Exported performance data</returns>
    Task<PerformanceDataExport> ExportPerformanceDataAsync(TimeSpan period, string format = "JSON", Guid? businessId = null);
}

/// <summary>
/// Business performance report data structure
/// </summary>
public class BusinessPerformanceReport
{
    public Guid BusinessId { get; set; }
    public TimeSpan Period { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public PerformanceOverview Overview { get; set; } = new();
    public List<UserPerformanceMetric> UserMetrics { get; set; } = new();
    public List<ApiPerformanceMetric> ApiMetrics { get; set; } = new();
    public List<DatabasePerformanceMetric> DatabaseMetrics { get; set; } = new();
    public List<PerformanceIssue> Issues { get; set; } = new();
    public List<PerformanceRecommendation> Recommendations { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Performance overview data
/// </summary>
public class PerformanceOverview
{
    public double AverageResponseTime { get; set; }
    public double P95ResponseTime { get; set; }
    public double P99ResponseTime { get; set; }
    public double ThroughputPerSecond { get; set; }
    public double ErrorRate { get; set; }
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public double UptimePercentage { get; set; }
}

/// <summary>
/// User performance metric data
/// </summary>
public class UserPerformanceMetric
{
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public double AverageResponseTime { get; set; }
    public int ActionCount { get; set; }
    public double SuccessRate { get; set; }
    public List<string> CommonIssues { get; set; } = new();
}

/// <summary>
/// API performance metric data
/// </summary>
public class ApiPerformanceMetric
{
    public string Endpoint { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public double AverageResponseTime { get; set; }
    public double P95ResponseTime { get; set; }
    public int RequestCount { get; set; }
    public double ErrorRate { get; set; }
    public long AverageRequestSize { get; set; }
    public long AverageResponseSize { get; set; }
    public Dictionary<int, int> StatusCodeBreakdown { get; set; } = new();
}

/// <summary>
/// Database performance metric data
/// </summary>
public class DatabasePerformanceMetric
{
    public string QueryType { get; set; } = string.Empty;
    public double AverageExecutionTime { get; set; }
    public double P95ExecutionTime { get; set; }
    public int QueryCount { get; set; }
    public double SuccessRate { get; set; }
    public int AverageRecordsAffected { get; set; }
    public List<string> SlowQueries { get; set; } = new();
}

/// <summary>
/// Performance dashboard data structure
/// </summary>
public class PerformanceDashboard
{
    public TimeSpan Period { get; set; }
    public SystemPerformanceOverview SystemOverview { get; set; } = new();
    public List<BusinessPerformanceSummary> BusinessSummaries { get; set; } = new();
    public List<PerformanceAlert> ActiveAlerts { get; set; } = new();
    public List<PerformanceTrendData> Trends { get; set; } = new();
    public List<TopPerformanceIssue> TopIssues { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// System performance overview
/// </summary>
public class SystemPerformanceOverview
{
    public double SystemCpuUsage { get; set; }
    public double SystemMemoryUsage { get; set; }
    public double SystemDiskUsage { get; set; }
    public int ActiveConnections { get; set; }
    public int TotalBusinesses { get; set; }
    public int ActiveUsers { get; set; }
    public double OverallResponseTime { get; set; }
    public double OverallThroughput { get; set; }
    public double OverallErrorRate { get; set; }
}

/// <summary>
/// Business performance summary
/// </summary>
public class BusinessPerformanceSummary
{
    public Guid BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public int ActiveUsers { get; set; }
    public double AverageResponseTime { get; set; }
    public double ErrorRate { get; set; }
    public int RequestCount { get; set; }
    public PerformanceHealthStatus HealthStatus { get; set; }
}

/// <summary>
/// Performance comparison data structure
/// </summary>
public class PerformanceComparison
{
    public TimeSpan CurrentPeriod { get; set; }
    public TimeSpan ComparisonPeriod { get; set; }
    public PerformanceMetricComparison ResponseTime { get; set; } = new();
    public PerformanceMetricComparison Throughput { get; set; } = new();
    public PerformanceMetricComparison ErrorRate { get; set; } = new();
    public PerformanceMetricComparison CpuUsage { get; set; } = new();
    public PerformanceMetricComparison MemoryUsage { get; set; } = new();
    public List<PerformanceChangeHighlight> Highlights { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Performance metric comparison
/// </summary>
public class PerformanceMetricComparison
{
    public double CurrentValue { get; set; }
    public double ComparisonValue { get; set; }
    public double ChangePercentage { get; set; }
    public PerformanceChangeDirection Direction { get; set; }
    public bool IsSignificant { get; set; }
}

/// <summary>
/// Performance analysis data structure
/// </summary>
public class PerformanceAnalysis
{
    public TimeSpan Period { get; set; }
    public List<PerformanceBottleneck> Bottlenecks { get; set; } = new();
    public List<PerformanceRecommendation> Recommendations { get; set; } = new();
    public List<PerformanceOptimization> OptimizationOpportunities { get; set; } = new();
    public PerformanceScore OverallScore { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Performance bottleneck data
/// </summary>
public class PerformanceBottleneck
{
    public string Component { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BottleneckSeverity Severity { get; set; }
    public double ImpactScore { get; set; }
    public List<string> AffectedOperations { get; set; } = new();
    public Dictionary<string, object> Metrics { get; set; } = new();
}

/// <summary>
/// Performance recommendation data
/// </summary>
public class PerformanceRecommendation
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RecommendationPriority Priority { get; set; }
    public double EstimatedImpact { get; set; }
    public string Implementation { get; set; } = string.Empty;
    public List<string> Benefits { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Performance optimization opportunity
/// </summary>
public class PerformanceOptimization
{
    public string Area { get; set; } = string.Empty;
    public string Opportunity { get; set; } = string.Empty;
    public double PotentialImprovement { get; set; }
    public string Effort { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = new();
}

/// <summary>
/// Performance monitoring configuration
/// </summary>
public class PerformanceMonitoringConfiguration
{
    public Guid? BusinessId { get; set; }
    public PerformanceThresholds Thresholds { get; set; } = new();
    public List<MetricConfiguration> Metrics { get; set; } = new();
    public AlertConfiguration AlertConfiguration { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
    public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(30);
}

/// <summary>
/// Metric configuration
/// </summary>
public class MetricConfiguration
{
    public string MetricName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public TimeSpan CollectionInterval { get; set; } = TimeSpan.FromMinutes(1);
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Real-time performance metrics
/// </summary>
public class RealTimePerformanceMetrics
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double CurrentCpuUsage { get; set; }
    public double CurrentMemoryUsage { get; set; }
    public double CurrentResponseTime { get; set; }
    public double CurrentThroughput { get; set; }
    public double CurrentErrorRate { get; set; }
    public int ActiveConnections { get; set; }
    public int QueuedRequests { get; set; }
    public List<RealTimeAlert> ActiveAlerts { get; set; } = new();
    public Dictionary<string, double> CustomMetrics { get; set; } = new();
}

/// <summary>
/// Real-time alert data
/// </summary>
public class RealTimeAlert
{
    public string AlertType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public DateTime TriggeredAt { get; set; }
}

/// <summary>
/// Performance data export
/// </summary>
public class PerformanceDataExport
{
    public string Format { get; set; } = string.Empty;
    public TimeSpan Period { get; set; }
    public int RecordCount { get; set; }
    public long FileSizeBytes { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Performance issue data
/// </summary>
public class PerformanceIssue
{
    public string IssueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IssueSeverity Severity { get; set; }
    public DateTime FirstDetected { get; set; }
    public DateTime LastDetected { get; set; }
    public int OccurrenceCount { get; set; }
    public List<string> AffectedComponents { get; set; } = new();
}

/// <summary>
/// Performance trend data
/// </summary>
public class PerformanceTrendData
{
    public string MetricName { get; set; } = string.Empty;
    public List<TrendPoint> TrendPoints { get; set; } = new();
    public TrendDirection Direction { get; set; }
    public double ChangePercentage { get; set; }
}

/// <summary>
/// Trend point data
/// </summary>
public class TrendPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
}

/// <summary>
/// Top performance issue data
/// </summary>
public class TopPerformanceIssue
{
    public string Issue { get; set; } = string.Empty;
    public int ImpactScore { get; set; }
    public int AffectedUsers { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
}

/// <summary>
/// Performance change highlight
/// </summary>
public class PerformanceChangeHighlight
{
    public string Metric { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double ChangePercentage { get; set; }
    public PerformanceChangeDirection Direction { get; set; }
    public bool IsPositive { get; set; }
}

/// <summary>
/// Performance score data
/// </summary>
public class PerformanceScore
{
    public double OverallScore { get; set; }
    public double ResponseTimeScore { get; set; }
    public double ThroughputScore { get; set; }
    public double ReliabilityScore { get; set; }
    public double EfficiencyScore { get; set; }
    public PerformanceGrade Grade { get; set; }
}

/// <summary>
/// Performance health status enumeration
/// </summary>
public enum PerformanceHealthStatus
{
    Excellent,
    Good,
    Fair,
    Poor,
    Critical
}

/// <summary>
/// Performance change direction enumeration
/// </summary>
public enum PerformanceChangeDirection
{
    Improved,
    Degraded,
    Stable
}

/// <summary>
/// Bottleneck severity enumeration
/// </summary>
public enum BottleneckSeverity
{
    Low,
    Medium,
    High,
    Critical
}



