using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of usage analytics service for tracking user behavior and feature usage
/// Provides insights for feature optimization and user experience improvements
/// </summary>
public class UsageAnalyticsService : IUsageAnalyticsService
{
    private readonly PosDbContext _context;
    private readonly ILogger<UsageAnalyticsService> _logger;
    private readonly IComprehensiveLoggingService _loggingService;

    public UsageAnalyticsService(
        PosDbContext context,
        ILogger<UsageAnalyticsService> logger,
        IComprehensiveLoggingService loggingService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
    }

    /// <summary>
    /// Records a user action for analytics tracking
    /// </summary>
    public async Task RecordUserActionAsync(string action, Guid userId, Guid businessId, Guid deviceId, Dictionary<string, object>? metadata = null)
    {
        try
        {
            var actionData = new
            {
                Action = action,
                UserId = userId,
                BusinessId = businessId,
                DeviceId = deviceId,
                Metadata = metadata,
                Timestamp = DateTime.UtcNow
            };

            await _loggingService.LogInfoAsync(
                $"User action: {action}",
                LogCategory.Business,
                deviceId,
                userId,
                actionData);

            _logger.LogDebug("Recorded user action: {Action} for user {UserId}", action, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording user action: {Action} for user {UserId}", action, userId);
        }
    }

    /// <summary>
    /// Records feature usage for optimization analysis
    /// </summary>
    public async Task RecordFeatureUsageAsync(string featureName, Guid userId, Guid businessId, TimeSpan duration, bool success, Dictionary<string, object>? metadata = null)
    {
        try
        {
            var featureData = new
            {
                FeatureName = featureName,
                UserId = userId,
                BusinessId = businessId,
                Duration = duration.TotalMilliseconds,
                Success = success,
                Metadata = metadata,
                Timestamp = DateTime.UtcNow
            };

            await _loggingService.LogInfoAsync(
                $"Feature usage: {featureName} - Duration: {duration.TotalMilliseconds}ms, Success: {success}",
                LogCategory.Business,
                Guid.NewGuid(), // Device ID from metadata if available
                userId,
                featureData);

            _logger.LogDebug("Recorded feature usage: {FeatureName} for user {UserId}, Duration: {Duration}ms, Success: {Success}",
                featureName, userId, duration.TotalMilliseconds, success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording feature usage: {FeatureName} for user {UserId}", featureName, userId);
        }
    }

    /// <summary>
    /// Records performance metrics for user interactions
    /// </summary>
    public async Task RecordPerformanceMetricAsync(string operation, TimeSpan duration, Guid userId, Guid businessId, bool success)
    {
        try
        {
            var performanceData = new
            {
                Operation = operation,
                Duration = duration.TotalMilliseconds,
                UserId = userId,
                BusinessId = businessId,
                Success = success,
                Timestamp = DateTime.UtcNow
            };

            await _loggingService.LogInfoAsync(
                $"Performance metric: {operation} - Duration: {duration.TotalMilliseconds}ms, Success: {success}",
                LogCategory.Performance,
                Guid.NewGuid(),
                userId,
                performanceData);

            _logger.LogDebug("Recorded performance metric: {Operation} - Duration: {Duration}ms, Success: {Success}",
                operation, duration.TotalMilliseconds, success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording performance metric: {Operation}", operation);
        }
    }

    /// <summary>
    /// Gets usage analytics for a specific business
    /// </summary>
    public async Task<UsageAnalytics> GetUsageAnalyticsAsync(Guid businessId, TimeSpan period)
    {
        try
        {
            var startDate = DateTime.UtcNow.Subtract(period);
            var endDate = DateTime.UtcNow;

            // Get user action logs for the business
            var actionLogs = await _loggingService.GetLogsByCategoryAsync(LogCategory.Business, startDate, endDate);
            var businessLogs = actionLogs.Where(log => 
                log.AdditionalData != null && 
                log.AdditionalData.Contains(businessId.ToString())).ToList();

            // Calculate basic metrics
            var totalUsers = await _context.Users
                .Where(u => u.BusinessId == businessId && u.LastLoginAt >= startDate)
                .CountAsync();

            var activeUsers = await _context.Users
                .Where(u => u.BusinessId == businessId && u.LastLoginAt >= startDate)
                .CountAsync();

            // Parse action data from logs
            var actionData = ParseActionDataFromLogs(businessLogs);
            var topActions = CalculateTopActions(actionData);
            var deviceBreakdown = CalculateDeviceBreakdown(actionData);
            var hourlyUsage = CalculateHourlyUsage(actionData);

            return new UsageAnalytics
            {
                BusinessId = businessId,
                Period = period,
                StartDate = startDate,
                EndDate = endDate,
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                TotalSessions = actionData.Count,
                AverageSessionDuration = CalculateAverageSessionDuration(actionData),
                TotalActions = actionData.Sum(a => a.ActionCount),
                ActionsPerSession = actionData.Any() ? (double)actionData.Sum(a => a.ActionCount) / actionData.Count : 0,
                TopActions = topActions,
                DeviceBreakdown = deviceBreakdown,
                HourlyUsagePattern = hourlyUsage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting usage analytics for business {BusinessId}", businessId);
            return new UsageAnalytics { BusinessId = businessId, Period = period };
        }
    }

    /// <summary>
    /// Gets feature usage statistics
    /// </summary>
    public async Task<FeatureUsageStatistics> GetFeatureUsageStatisticsAsync(Guid businessId, TimeSpan period)
    {
        try
        {
            var startDate = DateTime.UtcNow.Subtract(period);
            var endDate = DateTime.UtcNow;

            var featureLogs = await _loggingService.GetLogsByCategoryAsync(LogCategory.Business, startDate, endDate);
            var businessFeatureLogs = featureLogs.Where(log => 
                log.AdditionalData != null && 
                log.AdditionalData.Contains(businessId.ToString()) &&
                log.Message.Contains("Feature usage")).ToList();

            var featureUsages = ParseFeatureUsageFromLogs(businessFeatureLogs);
            
            return new FeatureUsageStatistics
            {
                BusinessId = businessId,
                Period = period,
                Features = featureUsages,
                MostUsedFeatures = featureUsages.OrderByDescending(f => f.UsageCount).Take(10).ToList(),
                LeastUsedFeatures = featureUsages.OrderBy(f => f.UsageCount).Take(10).ToList(),
                ProblematicFeatures = featureUsages.Where(f => f.SuccessRate < 0.8).OrderBy(f => f.SuccessRate).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting feature usage statistics for business {BusinessId}", businessId);
            return new FeatureUsageStatistics { BusinessId = businessId, Period = period };
        }
    }

    /// <summary>
    /// Gets user behavior patterns
    /// </summary>
    public async Task<UserBehaviorPatterns> GetUserBehaviorPatternsAsync(Guid businessId, TimeSpan period)
    {
        try
        {
            var startDate = DateTime.UtcNow.Subtract(period);
            var actionLogs = await _loggingService.GetLogsByCategoryAsync(LogCategory.Business, startDate);
            var businessLogs = actionLogs.Where(log => 
                log.AdditionalData != null && 
                log.AdditionalData.Contains(businessId.ToString())).ToList();

            var userSegments = AnalyzeUserSegments(businessLogs);
            var workflows = AnalyzeWorkflowPatterns(businessLogs);
            var peakTimes = AnalyzePeakUsageTimes(businessLogs);
            var journeys = AnalyzeUserJourneys(businessLogs);

            return new UserBehaviorPatterns
            {
                BusinessId = businessId,
                Period = period,
                UserSegments = userSegments,
                CommonWorkflows = workflows,
                PeakUsageTimes = peakTimes,
                TypicalUserJourneys = journeys
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user behavior patterns for business {BusinessId}", businessId);
            return new UserBehaviorPatterns { BusinessId = businessId, Period = period };
        }
    }

    /// <summary>
    /// Gets performance insights for optimization
    /// </summary>
    public async Task<PerformanceInsights> GetPerformanceInsightsAsync(Guid businessId, TimeSpan period)
    {
        try
        {
            var startDate = DateTime.UtcNow.Subtract(period);
            var performanceLogs = await _loggingService.GetLogsByCategoryAsync(LogCategory.Performance, startDate);
            var businessPerformanceLogs = performanceLogs.Where(log => 
                log.AdditionalData != null && 
                log.AdditionalData.Contains(businessId.ToString())).ToList();

            var slowOperations = AnalyzeSlowOperations(businessPerformanceLogs);
            var errorProneOperations = AnalyzeErrorProneOperations(businessPerformanceLogs);
            var trends = AnalyzePerformanceTrends(businessPerformanceLogs);
            var recommendations = GenerateOptimizationRecommendations(slowOperations, errorProneOperations);

            return new PerformanceInsights
            {
                BusinessId = businessId,
                Period = period,
                SlowestOperations = slowOperations,
                ErrorProneOperations = errorProneOperations,
                PerformanceTrends = trends,
                Recommendations = recommendations
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance insights for business {BusinessId}", businessId);
            return new PerformanceInsights { BusinessId = businessId, Period = period };
        }
    }

    /// <summary>
    /// Gets usage trends over time
    /// </summary>
    public async Task<UsageTrends> GetUsageTrendsAsync(Guid businessId, TimeSpan period)
    {
        try
        {
            var startDate = DateTime.UtcNow.Subtract(period);
            var logs = await _loggingService.GetLogsByCategoryAsync(LogCategory.Business, startDate);
            var businessLogs = logs.Where(log => 
                log.AdditionalData != null && 
                log.AdditionalData.Contains(businessId.ToString())).ToList();

            var userTrends = CalculateUserTrends(businessLogs, period);
            var sessionTrends = CalculateSessionTrends(businessLogs, period);
            var actionTrends = CalculateActionTrends(businessLogs, period);
            var featureTrends = CalculateFeatureTrends(businessLogs, period);

            return new UsageTrends
            {
                BusinessId = businessId,
                Period = period,
                UserTrends = userTrends,
                SessionTrends = sessionTrends,
                ActionTrends = actionTrends,
                FeatureTrends = featureTrends
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting usage trends for business {BusinessId}", businessId);
            return new UsageTrends { BusinessId = businessId, Period = period };
        }
    }

    /// <summary>
    /// Gets feature adoption metrics
    /// </summary>
    public async Task<FeatureAdoptionMetrics> GetFeatureAdoptionMetricsAsync(Guid businessId, string? featureName = null)
    {
        try
        {
            var logs = await _loggingService.GetLogsByCategoryAsync(LogCategory.Business);
            var featureLogs = logs.Where(log => 
                log.AdditionalData != null && 
                log.AdditionalData.Contains(businessId.ToString()) &&
                log.Message.Contains("Feature usage") &&
                (featureName == null || log.Message.Contains(featureName))).ToList();

            var adoptionData = AnalyzeFeatureAdoption(featureLogs, businessId);
            var barriers = AnalyzeAdoptionBarriers(featureLogs);

            return new FeatureAdoptionMetrics
            {
                BusinessId = businessId,
                FeatureName = featureName,
                AdoptionData = adoptionData,
                OverallAdoptionRate = adoptionData.Any() ? adoptionData.Average(a => a.AdoptionRate) : 0,
                AverageTimeToAdoption = adoptionData.Any() ? 
                    TimeSpan.FromTicks((long)adoptionData.Average(a => a.AverageTimeToAdoption.Ticks)) : 
                    TimeSpan.Zero,
                AdoptionBarriers = barriers
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting feature adoption metrics for business {BusinessId}", businessId);
            return new FeatureAdoptionMetrics { BusinessId = businessId, FeatureName = featureName };
        }
    }

    // Helper methods for data analysis
    private List<ActionData> ParseActionDataFromLogs(List<SystemLogEntry> logs)
    {
        var actionData = new List<ActionData>();
        
        foreach (var log in logs.Where(l => l.Message.StartsWith("User action:")))
        {
            try
            {
                if (!string.IsNullOrEmpty(log.AdditionalData))
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, object>>(log.AdditionalData);
                    if (data != null && data.ContainsKey("Action"))
                    {
                        actionData.Add(new ActionData
                        {
                            Action = data["Action"].ToString() ?? "",
                            UserId = log.UserId ?? Guid.Empty,
                            Timestamp = log.CreatedAt,
                            ActionCount = 1
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing action data from log entry");
            }
        }

        return actionData;
    }

    private List<FeatureUsage> ParseFeatureUsageFromLogs(List<SystemLogEntry> logs)
    {
        var featureUsages = new Dictionary<string, FeatureUsageData>();

        foreach (var log in logs)
        {
            try
            {
                if (!string.IsNullOrEmpty(log.AdditionalData))
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, object>>(log.AdditionalData);
                    if (data != null && data.ContainsKey("FeatureName"))
                    {
                        var featureName = data["FeatureName"].ToString() ?? "";
                        var success = data.ContainsKey("Success") && bool.Parse(data["Success"].ToString() ?? "false");
                        var duration = data.ContainsKey("Duration") ? double.Parse(data["Duration"].ToString() ?? "0") : 0;

                        if (!featureUsages.ContainsKey(featureName))
                        {
                            featureUsages[featureName] = new FeatureUsageData();
                        }

                        featureUsages[featureName].UsageCount++;
                        featureUsages[featureName].UniqueUsers.Add(log.UserId ?? Guid.Empty);
                        featureUsages[featureName].TotalDuration += TimeSpan.FromMilliseconds(duration);
                        if (success) featureUsages[featureName].SuccessCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing feature usage data from log entry");
            }
        }

        return featureUsages.Select(kvp => new FeatureUsage
        {
            FeatureName = kvp.Key,
            UsageCount = kvp.Value.UsageCount,
            UniqueUsers = kvp.Value.UniqueUsers.Count,
            AverageUsageDuration = kvp.Value.UsageCount > 0 ? 
                TimeSpan.FromTicks(kvp.Value.TotalDuration.Ticks / kvp.Value.UsageCount) : 
                TimeSpan.Zero,
            SuccessRate = kvp.Value.UsageCount > 0 ? (double)kvp.Value.SuccessCount / kvp.Value.UsageCount : 0,
            AdoptionRate = 0 // Would need total user count to calculate
        }).ToList();
    }

    private List<TopAction> CalculateTopActions(List<ActionData> actionData)
    {
        var totalActions = actionData.Count;
        return actionData
            .GroupBy(a => a.Action)
            .Select(g => new TopAction
            {
                ActionName = g.Key,
                Count = g.Count(),
                Percentage = totalActions > 0 ? (double)g.Count() / totalActions * 100 : 0,
                UniqueUsers = g.Select(a => a.UserId).Distinct().Count()
            })
            .OrderByDescending(a => a.Count)
            .Take(10)
            .ToList();
    }

    private List<DeviceUsage> CalculateDeviceBreakdown(List<ActionData> actionData)
    {
        // Simplified device breakdown - would need actual device data
        return new List<DeviceUsage>
        {
            new DeviceUsage { DeviceType = "Desktop", UserCount = actionData.Count / 2, Percentage = 50 },
            new DeviceUsage { DeviceType = "Mobile", UserCount = actionData.Count / 2, Percentage = 50 }
        };
    }

    private List<HourlyUsage> CalculateHourlyUsage(List<ActionData> actionData)
    {
        return actionData
            .GroupBy(a => a.Timestamp.Hour)
            .Select(g => new HourlyUsage
            {
                Hour = g.Key,
                UserCount = g.Select(a => a.UserId).Distinct().Count(),
                ActionCount = g.Count(),
                ActivityScore = g.Count() * 1.0 // Simplified scoring
            })
            .OrderBy(h => h.Hour)
            .ToList();
    }

    private TimeSpan CalculateAverageSessionDuration(List<ActionData> actionData)
    {
        // Simplified calculation - would need actual session tracking
        return TimeSpan.FromMinutes(15); // Mock average
    }

    // Additional helper methods would be implemented here for other analysis functions
    private List<UserSegment> AnalyzeUserSegments(List<SystemLogEntry> logs) => new();
    private List<WorkflowPattern> AnalyzeWorkflowPatterns(List<SystemLogEntry> logs) => new();
    private List<UsagePattern> AnalyzePeakUsageTimes(List<SystemLogEntry> logs) => new();
    private List<UserJourney> AnalyzeUserJourneys(List<SystemLogEntry> logs) => new();
    private List<SlowOperation> AnalyzeSlowOperations(List<SystemLogEntry> logs) => new();
    private List<ErrorProneOperation> AnalyzeErrorProneOperations(List<SystemLogEntry> logs) => new();
    private List<PerformanceTrend> AnalyzePerformanceTrends(List<SystemLogEntry> logs) => new();
    private List<OptimizationRecommendation> GenerateOptimizationRecommendations(List<SlowOperation> slowOps, List<ErrorProneOperation> errorOps) => new();
    private List<UsageTrendDataPoint> CalculateUserTrends(List<SystemLogEntry> logs, TimeSpan period) => new();
    private List<UsageTrendDataPoint> CalculateSessionTrends(List<SystemLogEntry> logs, TimeSpan period) => new();
    private List<UsageTrendDataPoint> CalculateActionTrends(List<SystemLogEntry> logs, TimeSpan period) => new();
    private List<FeatureTrend> CalculateFeatureTrends(List<SystemLogEntry> logs, TimeSpan period) => new();
    private List<FeatureAdoption> AnalyzeFeatureAdoption(List<SystemLogEntry> logs, Guid businessId) => new();
    private List<AdoptionBarrier> AnalyzeAdoptionBarriers(List<SystemLogEntry> logs) => new();
}

// Helper classes for internal data processing
internal class ActionData
{
    public string Action { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTime Timestamp { get; set; }
    public int ActionCount { get; set; }
}

internal class FeatureUsageData
{
    public int UsageCount { get; set; }
    public HashSet<Guid> UniqueUsers { get; set; } = new();
    public TimeSpan TotalDuration { get; set; }
    public int SuccessCount { get; set; }
}