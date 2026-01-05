using Shared.Core.DTOs;

namespace Shared.Core.Services;

/// <summary>
/// Interface for model performance monitoring and retraining automation
/// </summary>
public interface IModelPerformanceMonitoringService
{
    /// <summary>
    /// Configures performance monitoring for a business
    /// </summary>
    /// <param name="businessId">The business to configure monitoring for</param>
    /// <param name="config">Auto-retraining configuration</param>
    /// <returns>Configuration result</returns>
    Task<MonitoringConfigurationResult> ConfigureAsync(Guid businessId, AutoRetrainingConfig config);

    /// <summary>
    /// Evaluates current performance of a model
    /// </summary>
    /// <param name="modelId">Model to evaluate</param>
    /// <returns>Current performance metrics</returns>
    Task<ModelPerformanceMetrics> EvaluateModelPerformanceAsync(string modelId);

    /// <summary>
    /// Checks for performance degradation compared to baseline
    /// </summary>
    /// <param name="modelId">Model to check</param>
    /// <param name="currentMetrics">Current performance metrics</param>
    /// <returns>Performance alert if degradation detected</returns>
    Task<PerformanceAlert?> CheckPerformanceDegradationAsync(string modelId, ModelPerformanceMetrics currentMetrics);

    /// <summary>
    /// Gets models that need retraining based on performance criteria
    /// </summary>
    /// <param name="businessId">Business to check models for</param>
    /// <returns>List of models needing retraining</returns>
    Task<List<ModelStatus>> GetModelsNeedingRetrainingAsync(Guid businessId);

    /// <summary>
    /// Gets historical performance metrics for a model
    /// </summary>
    /// <param name="modelId">Model to get metrics for</param>
    /// <returns>Historical performance metrics</returns>
    Task<ModelPerformanceMetrics> GetModelMetricsAsync(string modelId);

    /// <summary>
    /// Checks if a model needs retraining based on configured criteria
    /// </summary>
    /// <param name="modelId">Model to check</param>
    /// <returns>True if model needs retraining</returns>
    Task<bool> CheckIfModelNeedsRetrainingAsync(string modelId);

    /// <summary>
    /// Monitors data drift in model inputs
    /// </summary>
    /// <param name="modelId">Model to monitor</param>
    /// <param name="recentData">Recent input data</param>
    /// <returns>Data drift analysis result</returns>
    Task<DataDriftAnalysis> MonitorDataDriftAsync(string modelId, List<Dictionary<string, object>> recentData);

    /// <summary>
    /// Tracks model prediction accuracy over time
    /// </summary>
    /// <param name="modelId">Model to track</param>
    /// <param name="predictions">Recent predictions with actual outcomes</param>
    /// <returns>Accuracy tracking result</returns>
    Task<AccuracyTrackingResult> TrackPredictionAccuracyAsync(string modelId, List<PredictionOutcome> predictions);

    /// <summary>
    /// Generates a comprehensive model health report
    /// </summary>
    /// <param name="businessId">Business to generate report for</param>
    /// <returns>Model health report</returns>
    Task<ModelHealthReport> GenerateModelHealthReportAsync(Guid businessId);

    /// <summary>
    /// Sets up automated monitoring schedules
    /// </summary>
    /// <param name="businessId">Business to set up monitoring for</param>
    /// <param name="scheduleConfig">Monitoring schedule configuration</param>
    /// <returns>Schedule setup result</returns>
    Task<MonitoringScheduleResult> SetupAutomatedMonitoringAsync(Guid businessId, MonitoringScheduleConfig scheduleConfig);
}

/// <summary>
/// Monitoring configuration result
/// </summary>
public class MonitoringConfigurationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid BusinessId { get; set; }
    public DateTime ConfiguredAt { get; set; } = DateTime.UtcNow;
    public AutoRetrainingConfig Configuration { get; set; } = new();
    public List<string> EnabledMonitoringFeatures { get; set; } = new();
}

/// <summary>
/// Data drift analysis result
/// </summary>
public class DataDriftAnalysis
{
    public string ModelId { get; set; } = string.Empty;
    public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
    public bool DriftDetected { get; set; }
    public double OverallDriftScore { get; set; }
    public Dictionary<string, FeatureDriftMetrics> FeatureDriftScores { get; set; } = new();
    public List<string> DriftedFeatures { get; set; } = new();
    public DriftSeverity Severity { get; set; }
    public List<string> RecommendedActions { get; set; } = new();
}

/// <summary>
/// Feature drift metrics
/// </summary>
public class FeatureDriftMetrics
{
    public string FeatureName { get; set; } = string.Empty;
    public double DriftScore { get; set; }
    public DriftType DriftType { get; set; }
    public double StatisticalSignificance { get; set; }
    public Dictionary<string, object> DistributionMetrics { get; set; } = new();
}

/// <summary>
/// Prediction outcome for accuracy tracking
/// </summary>
public class PredictionOutcome
{
    public string PredictionId { get; set; } = string.Empty;
    public Dictionary<string, object> InputData { get; set; } = new();
    public Dictionary<string, object> PredictedValues { get; set; } = new();
    public Dictionary<string, object> ActualValues { get; set; } = new();
    public DateTime PredictionTime { get; set; }
    public DateTime OutcomeTime { get; set; }
    public double ConfidenceScore { get; set; }
}

/// <summary>
/// Accuracy tracking result
/// </summary>
public class AccuracyTrackingResult
{
    public string ModelId { get; set; } = string.Empty;
    public DateTime TrackingPeriodStart { get; set; }
    public DateTime TrackingPeriodEnd { get; set; }
    public int TotalPredictions { get; set; }
    public double CurrentAccuracy { get; set; }
    public double BaselineAccuracy { get; set; }
    public double AccuracyChange { get; set; }
    public bool AccuracyDegraded { get; set; }
    public List<AccuracyTrend> AccuracyTrends { get; set; } = new();
    public Dictionary<string, double> MetricBreakdown { get; set; } = new();
}

/// <summary>
/// Accuracy trend data point
/// </summary>
public class AccuracyTrend
{
    public DateTime Date { get; set; }
    public double Accuracy { get; set; }
    public int PredictionCount { get; set; }
    public double ConfidenceInterval { get; set; }
}

/// <summary>
/// Model health report
/// </summary>
public class ModelHealthReport
{
    public Guid BusinessId { get; set; }
    public DateTime ReportGeneratedAt { get; set; } = DateTime.UtcNow;
    public List<ModelHealthStatus> ModelHealthStatuses { get; set; } = new();
    public ModelHealthSummary OverallHealth { get; set; } = new();
    public List<PerformanceAlert> CriticalAlerts { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
    public Dictionary<string, object> HealthMetrics { get; set; } = new();
}

/// <summary>
/// Individual model health status
/// </summary>
public class ModelHealthStatus
{
    public string ModelId { get; set; } = string.Empty;
    public MLModelType ModelType { get; set; }
    public ModelHealthLevel HealthLevel { get; set; }
    public ModelPerformanceMetrics CurrentMetrics { get; set; } = new();
    public ModelPerformanceMetrics BaselineMetrics { get; set; } = new();
    public List<HealthIssue> Issues { get; set; } = new();
    public DateTime LastHealthCheck { get; set; }
    public int DaysSinceLastRetraining { get; set; }
    public bool RetrainingRecommended { get; set; }
}

/// <summary>
/// Model health summary
/// </summary>
public class ModelHealthSummary
{
    public int TotalModels { get; set; }
    public int HealthyModels { get; set; }
    public int ModelsWithWarnings { get; set; }
    public int CriticalModels { get; set; }
    public int ModelsNeedingRetraining { get; set; }
    public double OverallHealthScore { get; set; }
    public ModelHealthLevel OverallHealthLevel { get; set; }
}

/// <summary>
/// Health issue
/// </summary>
public class HealthIssue
{
    public HealthIssueType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public HealthIssueSeverity Severity { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public string RecommendedAction { get; set; } = string.Empty;
    public Dictionary<string, object> IssueMetadata { get; set; } = new();
}

/// <summary>
/// Monitoring schedule configuration
/// </summary>
public class MonitoringScheduleConfig
{
    public bool EnableDailyHealthChecks { get; set; } = true;
    public bool EnableWeeklyPerformanceReports { get; set; } = true;
    public bool EnableRealTimeAlerting { get; set; } = true;
    public int PerformanceCheckIntervalHours { get; set; } = 6;
    public int DataDriftCheckIntervalHours { get; set; } = 24;
    public int AccuracyTrackingIntervalHours { get; set; } = 12;
    public List<string> AlertRecipients { get; set; } = new();
    public Dictionary<string, object> CustomSchedules { get; set; } = new();
}

/// <summary>
/// Monitoring schedule setup result
/// </summary>
public class MonitoringScheduleResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid BusinessId { get; set; }
    public List<string> ScheduledJobs { get; set; } = new();
    public DateTime NextScheduledCheck { get; set; }
    public MonitoringScheduleConfig Configuration { get; set; } = new();
}

/// <summary>
/// Enums for monitoring
/// </summary>
public enum DriftSeverity
{
    None,
    Low,
    Medium,
    High,
    Critical
}

public enum DriftType
{
    Statistical,
    Distributional,
    Conceptual,
    Temporal
}

public enum ModelHealthLevel
{
    Healthy,
    Warning,
    Critical,
    Offline
}

public enum HealthIssueType
{
    PerformanceDegradation,
    DataDrift,
    AccuracyDrop,
    HighLatency,
    HighErrorRate,
    InsufficientData,
    ModelStaleness
}

public enum HealthIssueSeverity
{
    Low,
    Medium,
    High,
    Critical
}