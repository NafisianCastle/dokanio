namespace Shared.Core.Services;

/// <summary>
/// Interface for performance monitoring service with real-time alerting
/// </summary>
public interface IPerformanceMonitoringService : IDisposable
{
    /// <summary>
    /// Starts performance monitoring with specified interval
    /// </summary>
    Task StartMonitoringAsync(TimeSpan interval);

    /// <summary>
    /// Stops performance monitoring
    /// </summary>
    Task StopMonitoringAsync();

    /// <summary>
    /// Gets current performance metrics
    /// </summary>
    Task<PerformanceMonitoringMetrics> GetCurrentMetricsAsync();

    /// <summary>
    /// Gets performance metrics history
    /// </summary>
    Task<IEnumerable<PerformanceMetric>> GetMetricsHistoryAsync(TimeSpan? period = null);

    /// <summary>
    /// Gets active performance alerts
    /// </summary>
    Task<IEnumerable<SystemPerformanceAlert>> GetActiveAlertsAsync();

    /// <summary>
    /// Records a performance metric manually
    /// </summary>
    Task RecordMetricAsync(string metricName, double value, string? unit = null);

    /// <summary>
    /// Measures operation performance and records metrics
    /// </summary>
    Task<T> MeasureOperationAsync<T>(string operationName, Func<Task<T>> operation);

    /// <summary>
    /// Updates performance thresholds
    /// </summary>
    Task UpdateThresholdsAsync(PerformanceThresholds thresholds);

    /// <summary>
    /// Resolves an active alert
    /// </summary>
    Task ResolveAlertAsync(string alertId);

    /// <summary>
    /// Gets performance summary for a specific time period
    /// </summary>
    Task<PerformanceSummary> GetPerformanceSummaryAsync(TimeSpan period);

    /// <summary>
    /// Event raised when a performance alert is triggered
    /// </summary>
    event EventHandler<SystemPerformanceAlertEventArgs> PerformanceAlertTriggered;

    /// <summary>
    /// Event raised when a metric threshold is exceeded
    /// </summary>
    event EventHandler<PerformanceMetricEventArgs> MetricThresholdExceeded;
}