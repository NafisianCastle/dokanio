using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Comprehensive monitoring service that integrates logging, performance monitoring, error tracking, and alerting
/// Provides a unified interface for all monitoring and observability needs
/// </summary>
public interface IComprehensiveMonitoringService
{
    /// <summary>
    /// Initializes comprehensive monitoring for the application
    /// </summary>
    /// <param name="configuration">Monitoring configuration</param>
    Task InitializeMonitoringAsync(ComprehensiveMonitoringConfiguration configuration);

    /// <summary>
    /// Gets a comprehensive system health report
    /// </summary>
    /// <param name="businessId">Business filter (optional)</param>
    /// <returns>System health report</returns>
    Task<SystemHealthReport> GetSystemHealthReportAsync(Guid? businessId = null);

    /// <summary>
    /// Gets a comprehensive monitoring dashboard
    /// </summary>
    /// <param name="period">Time period for analysis</param>
    /// <param name="businessId">Business filter (optional)</param>
    /// <returns>Monitoring dashboard data</returns>
    Task<MonitoringDashboard> GetMonitoringDashboardAsync(TimeSpan period, Guid? businessId = null);

    /// <summary>
    /// Records a comprehensive event with automatic categorization and alerting
    /// </summary>
    /// <param name="eventType">Type of event</param>
    /// <param name="message">Event message</param>
    /// <param name="severity">Event severity</param>
    /// <param name="context">Event context</param>
    /// <param name="userId">User associated with the event (optional)</param>
    /// <param name="businessId">Business context (optional)</param>
    /// <param name="deviceId">Device context</param>
    /// <param name="metadata">Additional event metadata</param>
    Task RecordEventAsync(string eventType, string message, EventSeverity severity, string context, Guid? userId = null, Guid? businessId = null, Guid deviceId = default, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Gets comprehensive analytics for optimization
    /// </summary>
    /// <param name="period">Analysis period</param>
    /// <param name="businessId">Business filter (optional)</param>
    /// <returns>Comprehensive analytics</returns>
    Task<ComprehensiveAnalytics> GetComprehensiveAnalyticsAsync(TimeSpan period, Guid? businessId = null);

    /// <summary>
    /// Triggers a manual system health check
    /// </summary>
    /// <param name="includePerformanceTests">Whether to include performance tests</param>
    /// <returns>Health check results</returns>
    Task<HealthCheckResult> RunHealthCheckAsync(bool includePerformanceTests = false);

    /// <summary>
    /// Exports comprehensive monitoring data
    /// </summary>
    /// <param name="period">Time period to export</param>
    /// <param name="format">Export format</param>
    /// <param name="includePersonalData">Whether to include personal data</param>
    /// <param name="businessId">Business filter (optional)</param>
    /// <returns>Export result</returns>
    Task<MonitoringDataExport> ExportMonitoringDataAsync(TimeSpan period, string format = "JSON", bool includePersonalData = false, Guid? businessId = null);

    /// <summary>
    /// Configures monitoring thresholds and rules
    /// </summary>
    /// <param name="configuration">Monitoring configuration</param>
    Task ConfigureMonitoringAsync(ComprehensiveMonitoringConfiguration configuration);

    /// <summary>
    /// Gets monitoring configuration recommendations
    /// </summary>
    /// <param name="businessId">Business context (optional)</param>
    /// <returns>Configuration recommendations</returns>
    Task<List<MonitoringRecommendation>> GetConfigurationRecommendationsAsync(Guid? businessId = null);
}

/// <summary>
/// Comprehensive monitoring configuration
/// </summary>
public class ComprehensiveMonitoringConfiguration
{
    public Guid? BusinessId { get; set; }
    public LoggingConfiguration Logging { get; set; } = new();
    public PerformanceMonitoringConfiguration Performance { get; set; } = new();
    public ErrorTrackingConfiguration ErrorTracking { get; set; } = new();
    public AlertConfiguration Alerting { get; set; } = new();
    public AnalyticsConfiguration Analytics { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
    public TimeSpan DataRetentionPeriod { get; set; } = TimeSpan.FromDays(90);
}

/// <summary>
/// Logging configuration
/// </summary>
public class LoggingConfiguration
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
    public bool EnableStructuredLogging { get; set; } = true;
    public bool EnableSensitiveDataLogging { get; set; } = false;
    public List<LogCategory> EnabledCategories { get; set; } = new();
    public Dictionary<string, object> CustomFields { get; set; } = new();
}

/// <summary>
/// Error tracking configuration
/// </summary>
public class ErrorTrackingConfiguration
{
    public bool EnableAutomaticErrorCapture { get; set; } = true;
    public bool EnableStackTraceCapture { get; set; } = true;
    public List<string> IgnoredExceptionTypes { get; set; } = new();
    public int MaxErrorsPerHour { get; set; } = 1000;
    public bool EnableErrorGrouping { get; set; } = true;
}

/// <summary>
/// Analytics configuration
/// </summary>
public class AnalyticsConfiguration
{
    public bool EnableUsageAnalytics { get; set; } = true;
    public bool EnablePerformanceAnalytics { get; set; } = true;
    public bool EnableBusinessAnalytics { get; set; } = true;
    public TimeSpan AnalyticsInterval { get; set; } = TimeSpan.FromHours(1);
    public bool EnableRealTimeAnalytics { get; set; } = false;
}

/// <summary>
/// System health report
/// </summary>
public class SystemHealthReport
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public OverallHealthStatus OverallStatus { get; set; }
    public List<ComponentHealthStatus> ComponentStatuses { get; set; } = new();
    public List<HealthIssue> Issues { get; set; } = new();
    public List<HealthRecommendation> Recommendations { get; set; } = new();
    public PerformanceSnapshot PerformanceSnapshot { get; set; } = new();
    public ErrorSummary ErrorSummary { get; set; } = new();
    public ResourceUtilization ResourceUtilization { get; set; } = new();
}

/// <summary>
/// Component health status
/// </summary>
public class ComponentHealthStatus
{
    public string ComponentName { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public TimeSpan ResponseTime { get; set; }
    public DateTime LastChecked { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = new();
}

/// <summary>
/// Health issue data
/// </summary>
public class HealthIssue
{
    public string IssueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IssueSeverity Severity { get; set; }
    public string Component { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
}

/// <summary>
/// Health recommendation data
/// </summary>
public class HealthRecommendation
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RecommendationPriority Priority { get; set; }
    public string Category { get; set; } = string.Empty;
    public List<string> ActionItems { get; set; } = new();
}

/// <summary>
/// Performance snapshot
/// </summary>
public class PerformanceSnapshot
{
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double DiskUsagePercent { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public double ThroughputPerSecond { get; set; }
    public int ActiveConnections { get; set; }
    public DateTime SnapshotTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Error summary
/// </summary>
public class ErrorSummary
{
    public int TotalErrors { get; set; }
    public int CriticalErrors { get; set; }
    public int RecentErrors { get; set; }
    public double ErrorRate { get; set; }
    public List<string> TopErrorTypes { get; set; } = new();
    public DateTime LastErrorTime { get; set; }
}

/// <summary>
/// Resource utilization
/// </summary>
public class ResourceUtilization
{
    public long MemoryUsageBytes { get; set; }
    public long DiskUsageBytes { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public TimeSpan Uptime { get; set; }
}

/// <summary>
/// Monitoring dashboard data
/// </summary>
public class MonitoringDashboard
{
    public TimeSpan Period { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public SystemHealthReport HealthReport { get; set; } = new();
    public PerformanceDashboard PerformanceDashboard { get; set; } = new();
    public ErrorStatistics ErrorStatistics { get; set; } = new();
    public UsageAnalytics UsageAnalytics { get; set; } = new();
    public List<SystemAlert> ActiveAlerts { get; set; } = new();
    public List<MonitoringTrend> Trends { get; set; } = new();
    public List<MonitoringInsight> Insights { get; set; } = new();
}

/// <summary>
/// Monitoring trend data
/// </summary>
public class MonitoringTrend
{
    public string MetricName { get; set; } = string.Empty;
    public List<TrendPoint> DataPoints { get; set; } = new();
    public TrendDirection Direction { get; set; }
    public double ChangePercentage { get; set; }
    public string Interpretation { get; set; } = string.Empty;
}

/// <summary>
/// Monitoring insight data
/// </summary>
public class MonitoringInsight
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public InsightType Type { get; set; }
    public InsightSeverity Severity { get; set; }
    public List<string> ActionItems { get; set; } = new();
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Comprehensive analytics data
/// </summary>
public class ComprehensiveAnalytics
{
    public TimeSpan Period { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public PerformanceAnalytics Performance { get; set; } = new();
    public UsageAnalytics Usage { get; set; } = new();
    public ErrorAnalytics Errors { get; set; } = new();
    public BusinessAnalytics Business { get; set; } = new();
    public List<AnalyticsRecommendation> Recommendations { get; set; } = new();
}

/// <summary>
/// Performance analytics data
/// </summary>
public class PerformanceAnalytics
{
    public double AverageResponseTime { get; set; }
    public double P95ResponseTime { get; set; }
    public double P99ResponseTime { get; set; }
    public double ThroughputTrend { get; set; }
    public List<PerformanceBottleneck> Bottlenecks { get; set; } = new();
    public List<PerformanceImprovement> Improvements { get; set; } = new();
}

/// <summary>
/// Error analytics data
/// </summary>
public class ErrorAnalytics
{
    public ErrorTrends Trends { get; set; } = new();
    public List<FrequentError> FrequentErrors { get; set; } = new();
    public ErrorPatterns Patterns { get; set; } = new();
    public double ErrorReductionOpportunity { get; set; }
}

/// <summary>
/// Business analytics data
/// </summary>
public class BusinessAnalytics
{
    public int ActiveBusinesses { get; set; }
    public int ActiveUsers { get; set; }
    public double SystemUtilization { get; set; }
    public List<BusinessPerformanceSummary> BusinessPerformance { get; set; } = new();
    public List<BusinessInsight> Insights { get; set; } = new();
}

/// <summary>
/// Business insight data
/// </summary>
public class BusinessInsight
{
    public Guid BusinessId { get; set; }
    public string InsightType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double ImpactScore { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Performance improvement data
/// </summary>
public class PerformanceImprovement
{
    public string Area { get; set; } = string.Empty;
    public double ImprovementPercentage { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime AchievedAt { get; set; }
}

/// <summary>
/// Analytics recommendation data
/// </summary>
public class AnalyticsRecommendation
{
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double PotentialImpact { get; set; }
    public RecommendationPriority Priority { get; set; }
    public List<string> Steps { get; set; } = new();
}

/// <summary>
/// Health check result
/// </summary>
public class HealthCheckResult
{
    public DateTime CheckTime { get; set; } = DateTime.UtcNow;
    public OverallHealthStatus OverallStatus { get; set; }
    public List<ComponentHealthStatus> ComponentResults { get; set; } = new();
    public List<HealthIssue> IssuesFound { get; set; } = new();
    public TimeSpan CheckDuration { get; set; }
    public bool PerformanceTestsIncluded { get; set; }
}

/// <summary>
/// Monitoring data export
/// </summary>
public class MonitoringDataExport
{
    public string ExportId { get; set; } = Guid.NewGuid().ToString();
    public string Format { get; set; } = string.Empty;
    public TimeSpan Period { get; set; }
    public long RecordCount { get; set; }
    public long FileSizeBytes { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IncludesPersonalData { get; set; }
}

/// <summary>
/// Monitoring recommendation
/// </summary>
public class MonitoringRecommendation
{
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RecommendationPriority Priority { get; set; }
    public Dictionary<string, object> SuggestedConfiguration { get; set; } = new();
    public List<string> Benefits { get; set; } = new();
}

/// <summary>
/// Event severity enumeration
/// </summary>
public enum EventSeverity
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5
}

/// <summary>
/// Overall health status enumeration
/// </summary>
public enum OverallHealthStatus
{
    Healthy,
    Warning,
    Critical,
    Unknown
}

/// <summary>
/// Health status enumeration
/// </summary>
public enum HealthStatus
{
    Healthy,
    Warning,
    Critical,
    Unknown
}

/// <summary>
/// Performance grade enumeration
/// </summary>
public enum PerformanceGrade
{
    A,
    B,
    C,
    D,
    F
}