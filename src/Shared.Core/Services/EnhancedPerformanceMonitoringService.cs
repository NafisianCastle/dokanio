using Microsoft.Extensions.Logging;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of enhanced performance monitoring service
/// </summary>
public class EnhancedPerformanceMonitoringService : IEnhancedPerformanceMonitoringService
{
    private readonly IPerformanceMonitoringService _baseService;
    private readonly IComprehensiveLoggingService _loggingService;
    private readonly IAlertService _alertService;
    private readonly ILogger<EnhancedPerformanceMonitoringService> _logger;

    public EnhancedPerformanceMonitoringService(
        IPerformanceMonitoringService baseService,
        IComprehensiveLoggingService loggingService,
        IAlertService alertService,
        ILogger<EnhancedPerformanceMonitoringService> logger)
    {
        _baseService = baseService ?? throw new ArgumentNullException(nameof(baseService));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Implement IDisposable
    public void Dispose()
    {
        _baseService?.Dispose();
        GC.SuppressFinalize(this);
    }

    // Implement base interface methods
    public Task StartMonitoringAsync(TimeSpan interval)
        => _baseService.StartMonitoringAsync(interval);

    public Task StopMonitoringAsync()
        => _baseService.StopMonitoringAsync();

    public Task<PerformanceMonitoringMetrics> GetCurrentMetricsAsync()
        => _baseService.GetCurrentMetricsAsync();

    public Task<IEnumerable<PerformanceMetric>> GetMetricsHistoryAsync(TimeSpan? period = null)
        => _baseService.GetMetricsHistoryAsync(period);

    public Task<IEnumerable<SystemPerformanceAlert>> GetActiveAlertsAsync()
        => _baseService.GetActiveAlertsAsync();

    public Task RecordMetricAsync(string metricName, double value, string? unit = null)
        => _baseService.RecordMetricAsync(metricName, value, unit);

    public Task<T> MeasureOperationAsync<T>(string operationName, Func<Task<T>> operation)
        => _baseService.MeasureOperationAsync(operationName, operation);

    public Task UpdateThresholdsAsync(PerformanceThresholds thresholds)
        => _baseService.UpdateThresholdsAsync(thresholds);

    public Task ResolveAlertAsync(string alertId)
        => _baseService.ResolveAlertAsync(alertId);

    public Task<PerformanceSummary> GetPerformanceSummaryAsync(TimeSpan period)
        => _baseService.GetPerformanceSummaryAsync(period);

    // Delegate events
    public event EventHandler<SystemPerformanceAlertEventArgs>? PerformanceAlertTriggered
    {
        add => _baseService.PerformanceAlertTriggered += value;
        remove => _baseService.PerformanceAlertTriggered -= value;
    }

    public event EventHandler<PerformanceMetricEventArgs>? MetricThresholdExceeded
    {
        add => _baseService.MetricThresholdExceeded += value;
        remove => _baseService.MetricThresholdExceeded -= value;
    }

    // Enhanced methods implementation
    public async Task RecordBusinessMetricAsync(Guid businessId, string metricName, double value, string? unit = null, Dictionary<string, object>? metadata = null)
    {
        try
        {
            await _baseService.RecordMetricAsync($"business_{metricName}", value, unit);

            await _loggingService.LogInfoAsync(
                $"Business metric recorded: {metricName} = {value} {unit ?? ""}",
                LogCategory.Performance,
                Guid.NewGuid(),
                null,
                new { BusinessId = businessId, MetricName = metricName, Value = value, Unit = unit });

            _logger.LogDebug("Recorded business metric {MetricName} for business {BusinessId}", metricName, businessId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording business metric {MetricName} for business {BusinessId}", metricName, businessId);
        }
    }

    public async Task RecordUserInteractionAsync(Guid userId, string action, TimeSpan duration, bool success, Guid deviceId, Dictionary<string, object>? metadata = null)
    {
        try
        {
            await _baseService.RecordMetricAsync($"user_interaction_{action}", duration.TotalMilliseconds, "ms");

            await _loggingService.LogInfoAsync(
                $"User interaction recorded: {action} took {duration.TotalMilliseconds}ms (Success: {success})",
                LogCategory.Performance,
                deviceId,
                userId,
                new { Action = action, Duration = duration.TotalMilliseconds, Success = success });

            _logger.LogDebug("Recorded user interaction {Action} for user {UserId}", action, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording user interaction {Action} for user {UserId}", action, userId);
        }
    }

    public async Task RecordDatabaseMetricAsync(string queryType, TimeSpan duration, int recordsAffected, bool success, Guid? businessId = null)
    {
        try
        {
            await _baseService.RecordMetricAsync($"db_{queryType}", duration.TotalMilliseconds, "ms");

            await _loggingService.LogInfoAsync(
                $"Database metric recorded: {queryType} took {duration.TotalMilliseconds}ms, affected {recordsAffected} records (Success: {success})",
                LogCategory.Performance,
                Guid.NewGuid(),
                null,
                new { QueryType = queryType, Duration = duration.TotalMilliseconds, RecordsAffected = recordsAffected, Success = success, BusinessId = businessId });

            _logger.LogDebug("Recorded database metric {QueryType}", queryType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording database metric {QueryType}", queryType);
        }
    }

    public async Task RecordApiMetricAsync(string endpoint, string method, int statusCode, TimeSpan duration, long requestSize, long responseSize, Guid? userId = null, Guid? businessId = null)
    {
        try
        {
            await _baseService.RecordMetricAsync($"api_{method}_{endpoint}", duration.TotalMilliseconds, "ms");

            await _loggingService.LogInfoAsync(
                $"API metric recorded: {method} {endpoint} returned {statusCode} in {duration.TotalMilliseconds}ms",
                LogCategory.Performance,
                Guid.NewGuid(),
                userId,
                new { Endpoint = endpoint, Method = method, StatusCode = statusCode, Duration = duration.TotalMilliseconds, RequestSize = requestSize, ResponseSize = responseSize, BusinessId = businessId });

            _logger.LogDebug("Recorded API metric {Method} {Endpoint}", method, endpoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording API metric {Method} {Endpoint}", method, endpoint);
        }
    }

    // Simplified implementations for complex methods (would be more sophisticated in production)
    public async Task<BusinessPerformanceReport> GetBusinessPerformanceReportAsync(Guid businessId, TimeSpan period)
    {
        try
        {
            var summary = await _baseService.GetPerformanceSummaryAsync(period);
            
            return new BusinessPerformanceReport
            {
                BusinessId = businessId,
                Period = period,
                StartDate = DateTime.UtcNow.Subtract(period),
                EndDate = DateTime.UtcNow,
                Overview = new PerformanceOverview
                {
                    AverageResponseTime = summary.AverageResponseTime,
                    ThroughputPerSecond = summary.RequestsPerSecond,
                    ErrorRate = summary.ErrorRate,
                    TotalRequests = summary.TotalRequests,
                    UptimePercentage = 99.9 // Simplified
                },
                UserMetrics = new List<UserPerformanceMetric>(),
                ApiMetrics = new List<ApiPerformanceMetric>(),
                DatabaseMetrics = new List<DatabasePerformanceMetric>(),
                Issues = new List<PerformanceIssue>(),
                Recommendations = new List<PerformanceRecommendation>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating business performance report for {BusinessId}", businessId);
            return new BusinessPerformanceReport { BusinessId = businessId, Period = period };
        }
    }

    public async Task<PerformanceDashboard> GetPerformanceDashboardAsync(TimeSpan period)
    {
        try
        {
            var summary = await _baseService.GetPerformanceSummaryAsync(period);
            var currentMetrics = await _baseService.GetCurrentMetricsAsync();
            
            return new PerformanceDashboard
            {
                Period = period,
                SystemOverview = new SystemPerformanceOverview
                {
                    SystemCpuUsage = currentMetrics.CpuUsage,
                    SystemMemoryUsage = currentMetrics.MemoryUsage,
                    OverallResponseTime = summary.AverageResponseTime,
                    OverallThroughput = summary.RequestsPerSecond,
                    OverallErrorRate = summary.ErrorRate
                },
                BusinessSummaries = new List<BusinessPerformanceSummary>(),
                ActiveAlerts = new List<PerformanceAlert>(),
                Trends = new List<PerformanceTrendData>(),
                TopIssues = new List<TopPerformanceIssue>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating performance dashboard");
            return new PerformanceDashboard { Period = period };
        }
    }

    public async Task<PerformanceComparison> GetPerformanceComparisonAsync(TimeSpan currentPeriod, TimeSpan comparisonPeriod, Guid? businessId = null)
    {
        try
        {
            var currentSummary = await _baseService.GetPerformanceSummaryAsync(currentPeriod);
            var comparisonSummary = await _baseService.GetPerformanceSummaryAsync(comparisonPeriod);
            
            return new PerformanceComparison
            {
                CurrentPeriod = currentPeriod,
                ComparisonPeriod = comparisonPeriod,
                ResponseTime = new PerformanceMetricComparison
                {
                    CurrentValue = currentSummary.AverageResponseTime,
                    ComparisonValue = comparisonSummary.AverageResponseTime,
                    ChangePercentage = CalculateChangePercentage(currentSummary.AverageResponseTime, comparisonSummary.AverageResponseTime),
                    Direction = DetermineChangeDirection(currentSummary.AverageResponseTime, comparisonSummary.AverageResponseTime)
                },
                Throughput = new PerformanceMetricComparison
                {
                    CurrentValue = currentSummary.RequestsPerSecond,
                    ComparisonValue = comparisonSummary.RequestsPerSecond,
                    ChangePercentage = CalculateChangePercentage(currentSummary.RequestsPerSecond, comparisonSummary.RequestsPerSecond),
                    Direction = DetermineChangeDirection(currentSummary.RequestsPerSecond, comparisonSummary.RequestsPerSecond, true)
                },
                ErrorRate = new PerformanceMetricComparison
                {
                    CurrentValue = currentSummary.ErrorRate,
                    ComparisonValue = comparisonSummary.ErrorRate,
                    ChangePercentage = CalculateChangePercentage(currentSummary.ErrorRate, comparisonSummary.ErrorRate),
                    Direction = DetermineChangeDirection(currentSummary.ErrorRate, comparisonSummary.ErrorRate)
                },
                Highlights = new List<PerformanceChangeHighlight>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating performance comparison");
            return new PerformanceComparison { CurrentPeriod = currentPeriod, ComparisonPeriod = comparisonPeriod };
        }
    }

    public async Task<PerformanceAnalysis> GetPerformanceAnalysisAsync(Guid? businessId, TimeSpan period)
    {
        try
        {
            return new PerformanceAnalysis
            {
                Period = period,
                Bottlenecks = new List<PerformanceBottleneck>(),
                Recommendations = new List<PerformanceRecommendation>(),
                OptimizationOpportunities = new List<PerformanceOptimization>(),
                OverallScore = new PerformanceScore
                {
                    OverallScore = 85.0,
                    ResponseTimeScore = 80.0,
                    ThroughputScore = 90.0,
                    ReliabilityScore = 85.0,
                    EfficiencyScore = 85.0,
                    Grade = PerformanceGrade.B
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating performance analysis");
            return new PerformanceAnalysis { Period = period };
        }
    }

    public async Task ConfigurePerformanceMonitoringAsync(PerformanceMonitoringConfiguration configuration)
    {
        try
        {
            await _baseService.UpdateThresholdsAsync(configuration.Thresholds);
            
            await _loggingService.LogInfoAsync(
                "Performance monitoring configuration updated",
                LogCategory.System,
                Guid.NewGuid(),
                null,
                new { Configuration = "Updated" });

            _logger.LogInformation("Performance monitoring configuration updated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring performance monitoring");
        }
    }

    public async Task<RealTimePerformanceMetrics> GetRealTimeMetricsAsync(Guid? businessId = null)
    {
        try
        {
            var currentMetrics = await _baseService.GetCurrentMetricsAsync();
            
            return new RealTimePerformanceMetrics
            {
                CurrentCpuUsage = currentMetrics.CpuUsage,
                CurrentMemoryUsage = currentMetrics.MemoryUsage,
                CurrentResponseTime = currentMetrics.AverageResponseTime,
                CurrentThroughput = currentMetrics.RequestsPerSecond,
                CurrentErrorRate = currentMetrics.ErrorRate,
                ActiveConnections = 0, // Would be implemented with real connection tracking
                QueuedRequests = 0, // Would be implemented with real queue monitoring
                ActiveAlerts = new List<RealTimeAlert>(),
                CustomMetrics = new Dictionary<string, double>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting real-time metrics");
            return new RealTimePerformanceMetrics();
        }
    }

    public async Task<PerformanceDataExport> ExportPerformanceDataAsync(TimeSpan period, string format = "JSON", Guid? businessId = null)
    {
        try
        {
            return new PerformanceDataExport
            {
                Format = format,
                Period = period,
                RecordCount = 1000, // Simplified
                FileSizeBytes = 50000, // Simplified
                DownloadUrl = "https://example.com/export/performance-data.json",
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                Metadata = new Dictionary<string, object>
                {
                    ["BusinessId"] = businessId?.ToString() ?? "All",
                    ["GeneratedAt"] = DateTime.UtcNow,
                    ["Format"] = format
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting performance data");
            return new PerformanceDataExport { Format = format, Period = period };
        }
    }

    // Helper methods
    private double CalculateChangePercentage(double current, double previous)
    {
        if (previous == 0) return current > 0 ? 100 : 0;
        return ((current - previous) / previous) * 100;
    }

    private PerformanceChangeDirection DetermineChangeDirection(double current, double previous, bool higherIsBetter = false)
    {
        var change = current - previous;
        if (Math.Abs(change) < 0.01) return PerformanceChangeDirection.Stable;
        
        if (higherIsBetter)
            return change > 0 ? PerformanceChangeDirection.Improved : PerformanceChangeDirection.Degraded;
        else
            return change < 0 ? PerformanceChangeDirection.Improved : PerformanceChangeDirection.Degraded;
    }
}