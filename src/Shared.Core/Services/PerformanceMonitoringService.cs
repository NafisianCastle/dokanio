using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Shared.Core.Services;

/// <summary>
/// Enhanced performance monitoring service with real-time alerting
/// Monitors system performance and triggers alerts when thresholds are exceeded
/// </summary>
public class PerformanceMonitoringService : IPerformanceMonitoringService, IDisposable
{
    private readonly ILogger<PerformanceMonitoringService> _logger;
    private readonly ISystemMonitoringService _systemMonitoringService;
    private readonly Timer _monitoringTimer;
    private readonly ConcurrentQueue<PerformanceMetric> _metricsHistory = new();
    private readonly ConcurrentDictionary<string, SystemPerformanceAlert> _activeAlerts = new();
    private readonly object _lockObject = new();

    // Performance thresholds
    private readonly PerformanceThresholds _thresholds = new()
    {
        MaxResponseTimeMs = 1000,
        MaxMemoryUsageMB = 512,
        MaxCpuUsagePercent = 80,
        MaxDiskUsagePercent = 90,
        MaxErrorRatePercent = 5,
        MinCacheHitRatePercent = 70
    };

    private bool _isMonitoring = false;
    private DateTime _lastMetricsCollection = DateTime.UtcNow;

    public event EventHandler<SystemPerformanceAlertEventArgs>? PerformanceAlertTriggered;
    public event EventHandler<PerformanceMetricEventArgs>? MetricThresholdExceeded;

    public PerformanceMonitoringService(
        ILogger<PerformanceMonitoringService> logger,
        ISystemMonitoringService systemMonitoringService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemMonitoringService = systemMonitoringService ?? throw new ArgumentNullException(nameof(systemMonitoringService));
        
        // Start monitoring timer (every 30 seconds)
        _monitoringTimer = new Timer(CollectMetrics, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Starts performance monitoring with specified interval
    /// </summary>
    public async Task StartMonitoringAsync(TimeSpan interval)
    {
        _isMonitoring = true;
        _monitoringTimer.Change(interval, interval);
        
        _logger.LogInformation("Performance monitoring started with interval: {Interval}", interval);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops performance monitoring
    /// </summary>
    public async Task StopMonitoringAsync()
    {
        _isMonitoring = false;
        _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
        
        _logger.LogInformation("Performance monitoring stopped");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets current performance metrics
    /// </summary>
    public async Task<PerformanceMonitoringMetrics> GetCurrentMetricsAsync()
    {
        var systemMetrics = await _systemMonitoringService.GetSystemMetricsAsync();
        var process = Process.GetCurrentProcess();
        
        var metrics = new PerformanceMonitoringMetrics
        {
            Timestamp = DateTime.UtcNow,
            ResponseTimeMs = systemMetrics.AverageResponseTime,
            MemoryUsageMB = process.WorkingSet64 / (1024 * 1024),
            CpuUsagePercent = systemMetrics.CpuUsagePercent,
            DiskUsagePercent = systemMetrics.DiskUsagePercent,
            ActiveConnections = systemMetrics.ActiveConnections,
            ErrorCount = systemMetrics.ErrorCount,
            TotalRequests = systemMetrics.TotalRequests,
            CacheHitRate = CalculateCacheHitRate(),
            DatabaseResponseTimeMs = await MeasureDatabaseResponseTimeAsync()
        };

        // Check thresholds and trigger alerts if necessary
        await CheckThresholdsAsync(metrics);

        return metrics;
    }

    /// <summary>
    /// Gets performance metrics history
    /// </summary>
    public async Task<IEnumerable<PerformanceMetric>> GetMetricsHistoryAsync(TimeSpan? period = null)
    {
        var cutoffTime = period.HasValue 
            ? DateTime.UtcNow.Subtract(period.Value)
            : DateTime.UtcNow.AddHours(-24); // Default to last 24 hours

        var historicalMetrics = _metricsHistory
            .Where(m => m.Timestamp >= cutoffTime)
            .OrderByDescending(m => m.Timestamp)
            .ToList();

        _logger.LogDebug("Retrieved {Count} performance metrics from history", historicalMetrics.Count);
        
        await Task.CompletedTask;
        return historicalMetrics;
    }

    /// <summary>
    /// Gets active performance alerts
    /// </summary>
    public async Task<IEnumerable<SystemPerformanceAlert>> GetActiveAlertsAsync()
    {
        var activeAlerts = _activeAlerts.Values
            .Where(a => a.Status == AlertStatus.Active)
            .OrderByDescending(a => a.CreatedAt)
            .ToList();

        await Task.CompletedTask;
        return activeAlerts;
    }

    /// <summary>
    /// Records a performance metric manually
    /// </summary>
    public async Task RecordMetricAsync(string metricName, double value, string? unit = null)
    {
        var metric = new PerformanceMetric
        {
            Name = metricName,
            Value = value,
            Unit = unit ?? "count",
            Timestamp = DateTime.UtcNow
        };

        _metricsHistory.Enqueue(metric);
        
        // Keep only recent metrics (last 1000 entries)
        while (_metricsHistory.Count > 1000)
        {
            _metricsHistory.TryDequeue(out _);
        }

        _logger.LogDebug("Recorded performance metric: {MetricName} = {Value} {Unit}", 
            metricName, value, unit);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Measures operation performance and records metrics
    /// </summary>
    public async Task<T> MeasureOperationAsync<T>(string operationName, Func<Task<T>> operation)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await operation();
            stopwatch.Stop();
            
            await RecordMetricAsync($"{operationName}_duration", stopwatch.ElapsedMilliseconds, "ms");
            await RecordMetricAsync($"{operationName}_success", 1);
            
            // Check if operation exceeded threshold
            if (stopwatch.ElapsedMilliseconds > _thresholds.MaxResponseTimeMs)
            {
                await TriggerPerformanceAlertAsync(
                    $"Slow Operation: {operationName}",
                    $"Operation took {stopwatch.ElapsedMilliseconds}ms, threshold is {_thresholds.MaxResponseTimeMs}ms",
                    AlertSeverity.Medium);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            await RecordMetricAsync($"{operationName}_duration", stopwatch.ElapsedMilliseconds, "ms");
            await RecordMetricAsync($"{operationName}_error", 1);
            
            await TriggerPerformanceAlertAsync(
                $"Operation Failed: {operationName}",
                $"Operation failed with error: {ex.Message}",
                AlertSeverity.High);
            
            throw;
        }
    }

    /// <summary>
    /// Updates performance thresholds
    /// </summary>
    public async Task UpdateThresholdsAsync(PerformanceThresholds thresholds)
    {
        lock (_lockObject)
        {
            _thresholds.MaxResponseTimeMs = thresholds.MaxResponseTimeMs;
            _thresholds.MaxMemoryUsageMB = thresholds.MaxMemoryUsageMB;
            _thresholds.MaxCpuUsagePercent = thresholds.MaxCpuUsagePercent;
            _thresholds.MaxDiskUsagePercent = thresholds.MaxDiskUsagePercent;
            _thresholds.MaxErrorRatePercent = thresholds.MaxErrorRatePercent;
            _thresholds.MinCacheHitRatePercent = thresholds.MinCacheHitRatePercent;
        }

        _logger.LogInformation("Performance thresholds updated");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Resolves an active alert
    /// </summary>
    public async Task ResolveAlertAsync(string alertId)
    {
        if (_activeAlerts.TryGetValue(alertId, out var alert))
        {
            alert.Status = AlertStatus.Resolved;
            alert.ResolvedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Performance alert resolved: {AlertId} - {Title}", alertId, alert.Title);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets performance summary for a specific time period
    /// </summary>
    public async Task<PerformanceSummary> GetPerformanceSummaryAsync(TimeSpan period)
    {
        var metrics = await GetMetricsHistoryAsync(period);
        var metricsList = metrics.ToList();

        if (!metricsList.Any())
        {
            return new PerformanceSummary
            {
                Period = period,
                StartTime = DateTime.UtcNow.Subtract(period),
                EndTime = DateTime.UtcNow
            };
        }

        var responseTimeMetrics = metricsList.Where(m => m.Name.EndsWith("_duration")).ToList();
        var errorMetrics = metricsList.Where(m => m.Name.EndsWith("_error")).ToList();
        var successMetrics = metricsList.Where(m => m.Name.EndsWith("_success")).ToList();

        return new PerformanceSummary
        {
            Period = period,
            StartTime = DateTime.UtcNow.Subtract(period),
            EndTime = DateTime.UtcNow,
            AverageResponseTime = responseTimeMetrics.Any() ? responseTimeMetrics.Average(m => m.Value) : 0,
            MaxResponseTime = responseTimeMetrics.Any() ? responseTimeMetrics.Max(m => m.Value) : 0,
            MinResponseTime = responseTimeMetrics.Any() ? responseTimeMetrics.Min(m => m.Value) : 0,
            TotalRequests = successMetrics.Sum(m => m.Value) + errorMetrics.Sum(m => m.Value),
            TotalErrors = errorMetrics.Sum(m => m.Value),
            ErrorRate = CalculateErrorRate(successMetrics.Sum(m => m.Value), errorMetrics.Sum(m => m.Value)),
            AlertsTriggered = _activeAlerts.Values.Count(a => a.CreatedAt >= DateTime.UtcNow.Subtract(period))
        };
    }

    private void CollectMetrics(object? state)
    {
        if (!_isMonitoring) return;

        try
        {
            _ = Task.Run(async () =>
            {
                var metrics = await GetCurrentMetricsAsync();
                _lastMetricsCollection = DateTime.UtcNow;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting performance metrics");
        }
    }

    private async Task CheckThresholdsAsync(PerformanceMonitoringMetrics metrics)
    {
        var alerts = new List<(string title, string message, AlertSeverity severity)>();

        if (metrics.ResponseTimeMs > _thresholds.MaxResponseTimeMs)
        {
            alerts.Add(("High Response Time", 
                $"Response time {metrics.ResponseTimeMs}ms exceeds threshold {_thresholds.MaxResponseTimeMs}ms", 
                AlertSeverity.Medium));
        }

        if (metrics.MemoryUsageMB > _thresholds.MaxMemoryUsageMB)
        {
            alerts.Add(("High Memory Usage", 
                $"Memory usage {metrics.MemoryUsageMB}MB exceeds threshold {_thresholds.MaxMemoryUsageMB}MB", 
                AlertSeverity.Medium));
        }

        if (metrics.CpuUsagePercent > _thresholds.MaxCpuUsagePercent)
        {
            alerts.Add(("High CPU Usage", 
                $"CPU usage {metrics.CpuUsagePercent:F1}% exceeds threshold {_thresholds.MaxCpuUsagePercent}%", 
                AlertSeverity.Medium));
        }

        if (metrics.DiskUsagePercent > _thresholds.MaxDiskUsagePercent)
        {
            alerts.Add(("High Disk Usage", 
                $"Disk usage {metrics.DiskUsagePercent:F1}% exceeds threshold {_thresholds.MaxDiskUsagePercent}%", 
                AlertSeverity.Critical));
        }

        if (metrics.CacheHitRate < _thresholds.MinCacheHitRatePercent)
        {
            alerts.Add(("Low Cache Hit Rate", 
                $"Cache hit rate {metrics.CacheHitRate:F1}% below threshold {_thresholds.MinCacheHitRatePercent}%", 
                AlertSeverity.Medium));
        }

        foreach (var (title, message, severity) in alerts)
        {
            await TriggerPerformanceAlertAsync(title, message, severity);
        }
    }

    private async Task TriggerPerformanceAlertAsync(string title, string message, AlertSeverity severity)
    {
        var alertId = $"{title}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        
        // Check if similar alert already exists
        var existingAlert = _activeAlerts.Values
            .FirstOrDefault(a => a.Title == title && a.Status == AlertStatus.Active);

        if (existingAlert != null)
        {
            existingAlert.LastOccurrence = DateTime.UtcNow;
            existingAlert.OccurrenceCount++;
            return;
        }

        var alert = new SystemPerformanceAlert
        {
            Id = alertId,
            Title = title,
            Message = message,
            Severity = severity,
            Status = AlertStatus.Active,
            CreatedAt = DateTime.UtcNow,
            LastOccurrence = DateTime.UtcNow,
            OccurrenceCount = 1
        };

        _activeAlerts.TryAdd(alertId, alert);

        // Trigger events
        PerformanceAlertTriggered?.Invoke(this, new SystemPerformanceAlertEventArgs { Alert = alert });

        _logger.LogWarning("Performance alert triggered: {Title} - {Message}", title, message);
        await Task.CompletedTask;
    }

    private double CalculateCacheHitRate()
    {
        // This would typically come from the caching service
        // For now, return a mock value
        return 85.0;
    }

    private async Task<double> MeasureDatabaseResponseTimeAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Simple database ping
            await _systemMonitoringService.GetSystemHealthAsync();
            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }
        catch
        {
            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }
    }

    private double CalculateErrorRate(double successCount, double errorCount)
    {
        var totalRequests = successCount + errorCount;
        return totalRequests > 0 ? (errorCount / totalRequests) * 100 : 0;
    }

    public void Dispose()
    {
        _monitoringTimer?.Dispose();
        _activeAlerts.Clear();
        _metricsHistory.Clear();
    }
}

/// <summary>
/// Performance monitoring metrics data structure
/// </summary>
public class PerformanceMonitoringMetrics
{
    public DateTime Timestamp { get; set; }
    public double ResponseTimeMs { get; set; }
    public long MemoryUsageMB { get; set; }
    public double CpuUsagePercent { get; set; }
    public double DiskUsagePercent { get; set; }
    public int ActiveConnections { get; set; }
    public int ErrorCount { get; set; }
    public int TotalRequests { get; set; }
    public double CacheHitRate { get; set; }
    public double DatabaseResponseTimeMs { get; set; }
}

/// <summary>
/// Individual performance metric
/// </summary>
public class PerformanceMetric
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Performance thresholds configuration
/// </summary>
public class PerformanceThresholds
{
    public double MaxResponseTimeMs { get; set; } = 1000;
    public long MaxMemoryUsageMB { get; set; } = 512;
    public double MaxCpuUsagePercent { get; set; } = 80;
    public double MaxDiskUsagePercent { get; set; } = 90;
    public double MaxErrorRatePercent { get; set; } = 5;
    public double MinCacheHitRatePercent { get; set; } = 70;
}

/// <summary>
/// Performance summary for a time period
/// </summary>
public class PerformanceSummary
{
    public TimeSpan Period { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double AverageResponseTime { get; set; }
    public double MaxResponseTime { get; set; }
    public double MinResponseTime { get; set; }
    public double TotalRequests { get; set; }
    public double TotalErrors { get; set; }
    public double ErrorRate { get; set; }
    public int AlertsTriggered { get; set; }
}

/// <summary>
/// Alert status
/// </summary>
public enum AlertStatus
{
    Active,
    Resolved,
    Suppressed
}

/// <summary>
/// System performance alert data structure
/// </summary>
public class SystemPerformanceAlert
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public AlertStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime LastOccurrence { get; set; }
    public int OccurrenceCount { get; set; }
}

/// <summary>
/// Event arguments for system performance alerts
/// </summary>
public class SystemPerformanceAlertEventArgs : EventArgs
{
    public SystemPerformanceAlert Alert { get; set; } = new();
}

/// <summary>
/// Event arguments for metric threshold events
/// </summary>
public class PerformanceMetricEventArgs : EventArgs
{
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public double Threshold { get; set; }
}