using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Service for tracking and analyzing user behavior and feature usage
/// Provides insights for feature optimization and user experience improvements
/// </summary>
public interface IUsageAnalyticsService
{
    /// <summary>
    /// Records a user action for analytics tracking
    /// </summary>
    /// <param name="action">The action performed</param>
    /// <param name="userId">User who performed the action</param>
    /// <param name="businessId">Business context</param>
    /// <param name="deviceId">Device used</param>
    /// <param name="metadata">Additional action metadata</param>
    Task RecordUserActionAsync(string action, Guid userId, Guid businessId, Guid deviceId, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Records feature usage for optimization analysis
    /// </summary>
    /// <param name="featureName">Name of the feature used</param>
    /// <param name="userId">User who used the feature</param>
    /// <param name="businessId">Business context</param>
    /// <param name="duration">Time spent using the feature</param>
    /// <param name="success">Whether the feature usage was successful</param>
    /// <param name="metadata">Additional feature metadata</param>
    Task RecordFeatureUsageAsync(string featureName, Guid userId, Guid businessId, TimeSpan duration, bool success, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Records performance metrics for user interactions
    /// </summary>
    /// <param name="operation">Operation name</param>
    /// <param name="duration">Operation duration</param>
    /// <param name="userId">User performing the operation</param>
    /// <param name="businessId">Business context</param>
    /// <param name="success">Whether the operation was successful</param>
    Task RecordPerformanceMetricAsync(string operation, TimeSpan duration, Guid userId, Guid businessId, bool success);

    /// <summary>
    /// Gets usage analytics for a specific business
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="period">Analysis period</param>
    /// <returns>Usage analytics data</returns>
    Task<UsageAnalytics> GetUsageAnalyticsAsync(Guid businessId, TimeSpan period);

    /// <summary>
    /// Gets feature usage statistics
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="period">Analysis period</param>
    /// <returns>Feature usage statistics</returns>
    Task<FeatureUsageStatistics> GetFeatureUsageStatisticsAsync(Guid businessId, TimeSpan period);

    /// <summary>
    /// Gets user behavior patterns
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="period">Analysis period</param>
    /// <returns>User behavior patterns</returns>
    Task<UserBehaviorPatterns> GetUserBehaviorPatternsAsync(Guid businessId, TimeSpan period);

    /// <summary>
    /// Gets performance insights for optimization
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="period">Analysis period</param>
    /// <returns>Performance insights</returns>
    Task<PerformanceInsights> GetPerformanceInsightsAsync(Guid businessId, TimeSpan period);

    /// <summary>
    /// Gets usage trends over time
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="period">Analysis period</param>
    /// <returns>Usage trends</returns>
    Task<UsageTrends> GetUsageTrendsAsync(Guid businessId, TimeSpan period);

    /// <summary>
    /// Gets feature adoption metrics
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="featureName">Specific feature name (optional)</param>
    /// <returns>Feature adoption metrics</returns>
    Task<FeatureAdoptionMetrics> GetFeatureAdoptionMetricsAsync(Guid businessId, string? featureName = null);
}

/// <summary>
/// Usage analytics data structure
/// </summary>
public class UsageAnalytics
{
    public Guid BusinessId { get; set; }
    public TimeSpan Period { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalSessions { get; set; }
    public TimeSpan AverageSessionDuration { get; set; }
    public int TotalActions { get; set; }
    public double ActionsPerSession { get; set; }
    public List<TopAction> TopActions { get; set; } = new();
    public List<DeviceUsage> DeviceBreakdown { get; set; } = new();
    public List<HourlyUsage> HourlyUsagePattern { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Feature usage statistics
/// </summary>
public class FeatureUsageStatistics
{
    public Guid BusinessId { get; set; }
    public TimeSpan Period { get; set; }
    public List<FeatureUsage> Features { get; set; } = new();
    public List<FeatureUsage> MostUsedFeatures { get; set; } = new();
    public List<FeatureUsage> LeastUsedFeatures { get; set; } = new();
    public List<FeatureUsage> ProblematicFeatures { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Individual feature usage data
/// </summary>
public class FeatureUsage
{
    public string FeatureName { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public int UniqueUsers { get; set; }
    public TimeSpan AverageUsageDuration { get; set; }
    public double SuccessRate { get; set; }
    public double AdoptionRate { get; set; }
    public List<string> CommonIssues { get; set; } = new();
}

/// <summary>
/// User behavior patterns
/// </summary>
public class UserBehaviorPatterns
{
    public Guid BusinessId { get; set; }
    public TimeSpan Period { get; set; }
    public List<UserSegment> UserSegments { get; set; } = new();
    public List<WorkflowPattern> CommonWorkflows { get; set; } = new();
    public List<UsagePattern> PeakUsageTimes { get; set; } = new();
    public List<UserJourney> TypicalUserJourneys { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// User segment data
/// </summary>
public class UserSegment
{
    public string SegmentName { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public double Percentage { get; set; }
    public List<string> CharacteristicActions { get; set; } = new();
    public TimeSpan AverageSessionDuration { get; set; }
    public double EngagementScore { get; set; }
}

/// <summary>
/// Workflow pattern data
/// </summary>
public class WorkflowPattern
{
    public string WorkflowName { get; set; } = string.Empty;
    public List<string> ActionSequence { get; set; } = new();
    public int Frequency { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public double CompletionRate { get; set; }
}

/// <summary>
/// Usage pattern data
/// </summary>
public class UsagePattern
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public int UserCount { get; set; }
    public int ActionCount { get; set; }
    public double IntensityScore { get; set; }
}

/// <summary>
/// User journey data
/// </summary>
public class UserJourney
{
    public string JourneyName { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = new();
    public TimeSpan AverageDuration { get; set; }
    public double CompletionRate { get; set; }
    public List<string> DropOffPoints { get; set; } = new();
}

/// <summary>
/// Performance insights for optimization
/// </summary>
public class PerformanceInsights
{
    public Guid BusinessId { get; set; }
    public TimeSpan Period { get; set; }
    public List<SlowOperation> SlowestOperations { get; set; } = new();
    public List<ErrorProneOperation> ErrorProneOperations { get; set; } = new();
    public List<PerformanceTrend> PerformanceTrends { get; set; } = new();
    public List<OptimizationRecommendation> Recommendations { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Slow operation data
/// </summary>
public class SlowOperation
{
    public string OperationName { get; set; } = string.Empty;
    public TimeSpan AverageDuration { get; set; }
    public TimeSpan MaxDuration { get; set; }
    public int ExecutionCount { get; set; }
    public double ImpactScore { get; set; }
}

/// <summary>
/// Error prone operation data
/// </summary>
public class ErrorProneOperation
{
    public string OperationName { get; set; } = string.Empty;
    public int TotalExecutions { get; set; }
    public int ErrorCount { get; set; }
    public double ErrorRate { get; set; }
    public List<string> CommonErrors { get; set; } = new();
}

/// <summary>
/// Performance trend data
/// </summary>
public class PerformanceTrend
{
    public string MetricName { get; set; } = string.Empty;
    public List<UsageTrendPoint> TrendPoints { get; set; } = new();
    public TrendDirection Direction { get; set; }
    public double ChangePercentage { get; set; }
}

/// <summary>
/// Trend point data
/// </summary>
public class UsageTrendPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
}

/// <summary>
/// Optimization recommendation
/// </summary>
public class OptimizationRecommendation
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public OptimizationPriority Priority { get; set; }
    public double EstimatedImpact { get; set; }
    public string Implementation { get; set; } = string.Empty;
}

/// <summary>
/// Usage trends over time
/// </summary>
public class UsageTrends
{
    public Guid BusinessId { get; set; }
    public TimeSpan Period { get; set; }
    public List<UsageTrendDataPoint> UserTrends { get; set; } = new();
    public List<UsageTrendDataPoint> SessionTrends { get; set; } = new();
    public List<UsageTrendDataPoint> ActionTrends { get; set; } = new();
    public List<FeatureTrend> FeatureTrends { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Usage trend point with change tracking
/// </summary>
public class UsageTrendDataPoint
{
    public DateTime Date { get; set; }
    public double Value { get; set; }
    public double ChangeFromPrevious { get; set; }
}

/// <summary>
/// Feature trend data
/// </summary>
public class FeatureTrend
{
    public string FeatureName { get; set; } = string.Empty;
    public List<UsageTrendDataPoint> TrendPoints { get; set; } = new();
    public TrendDirection Direction { get; set; }
    public double GrowthRate { get; set; }
}

/// <summary>
/// Feature adoption metrics
/// </summary>
public class FeatureAdoptionMetrics
{
    public Guid BusinessId { get; set; }
    public string? FeatureName { get; set; }
    public List<FeatureAdoption> AdoptionData { get; set; } = new();
    public double OverallAdoptionRate { get; set; }
    public TimeSpan AverageTimeToAdoption { get; set; }
    public List<AdoptionBarrier> AdoptionBarriers { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Feature adoption data
/// </summary>
public class FeatureAdoption
{
    public string FeatureName { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; }
    public int TotalUsers { get; set; }
    public int AdoptedUsers { get; set; }
    public double AdoptionRate { get; set; }
    public TimeSpan AverageTimeToAdoption { get; set; }
    public List<AdoptionMilestone> Milestones { get; set; } = new();
}

/// <summary>
/// Adoption milestone data
/// </summary>
public class AdoptionMilestone
{
    public DateTime Date { get; set; }
    public int UserCount { get; set; }
    public double AdoptionPercentage { get; set; }
}

/// <summary>
/// Adoption barrier data
/// </summary>
public class AdoptionBarrier
{
    public string FeatureName { get; set; } = string.Empty;
    public string BarrierType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int AffectedUsers { get; set; }
    public double ImpactScore { get; set; }
}

/// <summary>
/// Top action data
/// </summary>
public class TopAction
{
    public string ActionName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
    public int UniqueUsers { get; set; }
}

/// <summary>
/// Device usage data
/// </summary>
public class DeviceUsage
{
    public string DeviceType { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public int SessionCount { get; set; }
    public double Percentage { get; set; }
    public TimeSpan AverageSessionDuration { get; set; }
}

/// <summary>
/// Hourly usage data
/// </summary>
public class HourlyUsage
{
    public int Hour { get; set; }
    public int UserCount { get; set; }
    public int ActionCount { get; set; }
    public double ActivityScore { get; set; }
}

/// <summary>
/// Trend direction enumeration
/// </summary>
public enum TrendDirection
{
    Increasing,
    Decreasing,
    Stable,
    Volatile
}

