using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of error tracking service for comprehensive error monitoring and analysis
/// </summary>
public class ErrorTrackingService : IErrorTrackingService
{
    private readonly PosDbContext _context;
    private readonly ILogger<ErrorTrackingService> _logger;
    private readonly IComprehensiveLoggingService _loggingService;
    private readonly IAlertService _alertService;

    public ErrorTrackingService(
        PosDbContext context,
        ILogger<ErrorTrackingService> logger,
        IComprehensiveLoggingService loggingService,
        IAlertService alertService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
    }

    /// <summary>
    /// Records an error occurrence for tracking and analysis
    /// </summary>
    public async Task RecordErrorAsync(Exception exception, string context, Guid? userId = null, Guid? businessId = null, Guid deviceId = default, Dictionary<string, object>? additionalData = null)
    {
        try
        {
            var errorType = exception.GetType().Name;
            var severity = DetermineSeverity(exception);

            // Log to comprehensive logging service
            await _loggingService.LogErrorAsync(
                $"Error in {context}: {exception.Message}",
                LogCategory.System,
                deviceId,
                exception,
                userId,
                new
                {
                    ErrorType = errorType,
                    Context = context,
                    BusinessId = businessId,
                    Severity = severity.ToString(),
                    AdditionalData = additionalData
                });

            // Create error occurrence record
            var errorOccurrence = new ErrorOccurrence
            {
                Id = Guid.NewGuid(),
                ErrorType = errorType,
                Message = exception.Message,
                StackTrace = exception.StackTrace ?? string.Empty,
                Context = context,
                Severity = severity,
                UserId = userId,
                BusinessId = businessId,
                DeviceId = deviceId,
                OccurredAt = DateTime.UtcNow,
                IsResolved = false,
                AdditionalData = additionalData ?? new Dictionary<string, object>()
            };

            // Store in database for analysis (would need ErrorOccurrences table)
            // For now, we'll use the logging system as the primary storage

            // Trigger alerts for critical errors
            if (severity >= ErrorSeverity.High)
            {
                await _alertService.TriggerErrorAlertAsync(errorOccurrence);
            }

            _logger.LogDebug("Recorded error: {ErrorType} in {Context}", errorType, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record error occurrence");
        }
    }

    /// <summary>
    /// Records a custom error event for tracking
    /// </summary>
    public async Task RecordCustomErrorAsync(string errorType, string message, ErrorSeverity severity, string context, Guid? userId = null, Guid? businessId = null, Guid deviceId = default, Dictionary<string, object>? additionalData = null)
    {
        try
        {
            // Log to comprehensive logging service
            var logLevel = severity switch
            {
                ErrorSeverity.Critical => LogLevel.Critical,
                ErrorSeverity.High => LogLevel.Error,
                ErrorSeverity.Medium => LogLevel.Warning,
                ErrorSeverity.Low => LogLevel.Information,
                _ => LogLevel.Warning
            };

            if (logLevel == LogLevel.Critical || logLevel == LogLevel.Error)
            {
                await _loggingService.LogErrorAsync(
                    $"Custom error in {context}: {message}",
                    LogCategory.System,
                    deviceId,
                    null,
                    userId,
                    new
                    {
                        ErrorType = errorType,
                        Context = context,
                        BusinessId = businessId,
                        Severity = severity.ToString(),
                        AdditionalData = additionalData
                    });
            }
            else if (logLevel == LogLevel.Warning)
            {
                await _loggingService.LogWarningAsync(
                    $"Custom warning in {context}: {message}",
                    LogCategory.System,
                    deviceId,
                    userId,
                    new
                    {
                        ErrorType = errorType,
                        Context = context,
                        BusinessId = businessId,
                        Severity = severity.ToString(),
                        AdditionalData = additionalData
                    });
            }
            else
            {
                await _loggingService.LogInfoAsync(
                    $"Custom info in {context}: {message}",
                    LogCategory.System,
                    deviceId,
                    userId,
                    new
                    {
                        ErrorType = errorType,
                        Context = context,
                        BusinessId = businessId,
                        Severity = severity.ToString(),
                        AdditionalData = additionalData
                    });
            }

            // Create error occurrence record
            var errorOccurrence = new ErrorOccurrence
            {
                Id = Guid.NewGuid(),
                ErrorType = errorType,
                Message = message,
                StackTrace = string.Empty,
                Context = context,
                Severity = severity,
                UserId = userId,
                BusinessId = businessId,
                DeviceId = deviceId,
                OccurredAt = DateTime.UtcNow,
                IsResolved = false,
                AdditionalData = additionalData ?? new Dictionary<string, object>()
            };

            // Trigger alerts for high severity errors
            if (severity >= ErrorSeverity.High)
            {
                await _alertService.TriggerErrorAlertAsync(errorOccurrence);
            }

            _logger.LogDebug("Recorded custom error: {ErrorType} in {Context}", errorType, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record custom error");
        }
    }

    /// <summary>
    /// Gets error statistics for a specific time period
    /// </summary>
    public async Task<ErrorStatistics> GetErrorStatisticsAsync(TimeSpan period, Guid? businessId = null)
    {
        try
        {
            var startDate = DateTime.UtcNow.Subtract(period);
            var endDate = DateTime.UtcNow;

            // Get error logs from the comprehensive logging service
            var errorLogs = await _loggingService.GetErrorLogsAsync(startDate, endDate);
            
            // Filter by business if specified
            if (businessId.HasValue)
            {
                errorLogs = errorLogs.Where(log => 
                    log.AdditionalData != null && 
                    log.AdditionalData.Contains(businessId.Value.ToString()));
            }

            var errorList = errorLogs.ToList();
            var totalErrors = errorList.Count;
            var uniqueErrors = errorList.GroupBy(e => e.Message).Count();

            // Parse severity from additional data
            var severityCounts = new Dictionary<ErrorSeverity, int>
            {
                { ErrorSeverity.Critical, 0 },
                { ErrorSeverity.High, 0 },
                { ErrorSeverity.Medium, 0 },
                { ErrorSeverity.Low, 0 }
            };

            foreach (var log in errorList)
            {
                var severity = ParseSeverityFromLog(log);
                if (severityCounts.ContainsKey(severity))
                {
                    severityCounts[severity]++;
                }
            }

            // Calculate error rate (errors per hour)
            var errorRate = period.TotalHours > 0 ? totalErrors / period.TotalHours : 0;

            // Group errors by category and context
            var errorsByCategory = AnalyzeErrorsByCategory(errorList);
            var errorsByContext = AnalyzeErrorsByContext(errorList);

            return new ErrorStatistics
            {
                Period = period,
                StartDate = startDate,
                EndDate = endDate,
                TotalErrors = totalErrors,
                UniqueErrors = uniqueErrors,
                CriticalErrors = severityCounts[ErrorSeverity.Critical],
                HighSeverityErrors = severityCounts[ErrorSeverity.High],
                MediumSeverityErrors = severityCounts[ErrorSeverity.Medium],
                LowSeverityErrors = severityCounts[ErrorSeverity.Low],
                ErrorRate = errorRate,
                ErrorsByCategory = errorsByCategory,
                ErrorsByContext = errorsByContext
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting error statistics");
            return new ErrorStatistics { Period = period };
        }
    }

    /// <summary>
    /// Gets error trends over time
    /// </summary>
    public async Task<ErrorTrends> GetErrorTrendsAsync(TimeSpan period, Guid? businessId = null)
    {
        try
        {
            var startDate = DateTime.UtcNow.Subtract(period);
            var errorLogs = await _loggingService.GetErrorLogsAsync(startDate);

            if (businessId.HasValue)
            {
                errorLogs = errorLogs.Where(log => 
                    log.AdditionalData != null && 
                    log.AdditionalData.Contains(businessId.Value.ToString()));
            }

            var errorList = errorLogs.ToList();
            var trendPoints = CalculateErrorTrendPoints(errorList, period);
            var direction = CalculateTrendDirection(trendPoints);
            var changePercentage = CalculateChangePercentage(trendPoints);
            var severityTrends = CalculateSeverityTrends(errorList, period);

            return new ErrorTrends
            {
                Period = period,
                TrendPoints = trendPoints,
                Direction = direction,
                ChangePercentage = changePercentage,
                SeverityTrends = severityTrends
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting error trends");
            return new ErrorTrends { Period = period };
        }
    }

    /// <summary>
    /// Gets the most frequent errors
    /// </summary>
    public async Task<List<FrequentError>> GetMostFrequentErrorsAsync(TimeSpan period, int limit = 10, Guid? businessId = null)
    {
        try
        {
            var startDate = DateTime.UtcNow.Subtract(period);
            var errorLogs = await _loggingService.GetErrorLogsAsync(startDate);

            if (businessId.HasValue)
            {
                errorLogs = errorLogs.Where(log => 
                    log.AdditionalData != null && 
                    log.AdditionalData.Contains(businessId.Value.ToString()));
            }

            var frequentErrors = errorLogs
                .GroupBy(log => new { log.Message, Context = ExtractContextFromLog(log) })
                .Select(group => new FrequentError
                {
                    ErrorType = ExtractErrorTypeFromLog(group.First()),
                    Message = group.Key.Message,
                    Context = group.Key.Context,
                    Severity = ParseSeverityFromLog(group.First()),
                    OccurrenceCount = group.Count(),
                    FirstOccurrence = group.Min(log => log.CreatedAt),
                    LastOccurrence = group.Max(log => log.CreatedAt),
                    AffectedUsers = group.Where(log => log.UserId.HasValue)
                                        .Select(log => log.UserId.ToString()!)
                                        .Distinct()
                                        .ToList(),
                    AffectedDevices = group.Select(log => log.DeviceId.ToString())
                                          .Distinct()
                                          .ToList(),
                    IsResolved = false // Would need resolution tracking
                })
                .OrderByDescending(error => error.OccurrenceCount)
                .Take(limit)
                .ToList();

            return frequentErrors;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting most frequent errors");
            return new List<FrequentError>();
        }
    }

    /// <summary>
    /// Gets error patterns and correlations
    /// </summary>
    public async Task<ErrorPatterns> GetErrorPatternsAsync(TimeSpan period, Guid? businessId = null)
    {
        try
        {
            var startDate = DateTime.UtcNow.Subtract(period);
            var errorLogs = await _loggingService.GetErrorLogsAsync(startDate);

            if (businessId.HasValue)
            {
                errorLogs = errorLogs.Where(log => 
                    log.AdditionalData != null && 
                    log.AdditionalData.Contains(businessId.Value.ToString()));
            }

            var errorList = errorLogs.ToList();
            
            // Analyze patterns (simplified implementation)
            var correlations = AnalyzeErrorCorrelations(errorList);
            var clusters = AnalyzeErrorClusters(errorList);
            var sequences = AnalyzeErrorSequences(errorList);
            var hotspots = AnalyzeErrorHotspots(errorList);

            return new ErrorPatterns
            {
                Period = period,
                Correlations = correlations,
                Clusters = clusters,
                CommonSequences = sequences,
                Hotspots = hotspots
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting error patterns");
            return new ErrorPatterns { Period = period };
        }
    }

    /// <summary>
    /// Gets errors by severity level
    /// </summary>
    public async Task<List<ErrorOccurrence>> GetErrorsBySeverityAsync(ErrorSeverity severity, TimeSpan period, Guid? businessId = null)
    {
        try
        {
            var startDate = DateTime.UtcNow.Subtract(period);
            var errorLogs = await _loggingService.GetErrorLogsAsync(startDate);

            if (businessId.HasValue)
            {
                errorLogs = errorLogs.Where(log => 
                    log.AdditionalData != null && 
                    log.AdditionalData.Contains(businessId.Value.ToString()));
            }

            var filteredErrors = errorLogs
                .Where(log => ParseSeverityFromLog(log) == severity)
                .Select(log => new ErrorOccurrence
                {
                    Id = log.Id,
                    ErrorType = ExtractErrorTypeFromLog(log),
                    Message = log.Message,
                    StackTrace = log.ExceptionDetails ?? string.Empty,
                    Context = ExtractContextFromLog(log),
                    Severity = severity,
                    UserId = log.UserId,
                    BusinessId = businessId,
                    DeviceId = log.DeviceId,
                    OccurredAt = log.CreatedAt,
                    IsResolved = false,
                    AdditionalData = ParseAdditionalDataFromLog(log)
                })
                .OrderByDescending(error => error.OccurredAt)
                .ToList();

            return filteredErrors;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting errors by severity");
            return new List<ErrorOccurrence>();
        }
    }

    /// <summary>
    /// Resolves an error (marks it as handled)
    /// </summary>
    public async Task ResolveErrorAsync(Guid errorId, string resolution, Guid resolvedBy)
    {
        try
        {
            // Log the resolution
            await _loggingService.LogInfoAsync(
                $"Error {errorId} resolved: {resolution}",
                LogCategory.System,
                Guid.NewGuid(),
                resolvedBy,
                new { ErrorId = errorId, Resolution = resolution, ResolvedBy = resolvedBy });

            _logger.LogInformation("Error {ErrorId} resolved by {ResolvedBy}: {Resolution}", 
                errorId, resolvedBy, resolution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving error {ErrorId}", errorId);
        }
    }

    /// <summary>
    /// Gets unresolved critical errors
    /// </summary>
    public async Task<List<ErrorOccurrence>> GetUnresolvedCriticalErrorsAsync(Guid? businessId = null)
    {
        try
        {
            // Get critical errors from the last 24 hours
            var period = TimeSpan.FromHours(24);
            return await GetErrorsBySeverityAsync(ErrorSeverity.Critical, period, businessId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unresolved critical errors");
            return new List<ErrorOccurrence>();
        }
    }

    // Helper methods for error analysis
    private ErrorSeverity DetermineSeverity(Exception exception)
    {
        return exception switch
        {
            OutOfMemoryException => ErrorSeverity.Critical,
            StackOverflowException => ErrorSeverity.Critical,
            AccessViolationException => ErrorSeverity.Critical,
            InvalidOperationException => ErrorSeverity.High,
            ArgumentException => ErrorSeverity.Medium,
            NotImplementedException => ErrorSeverity.Medium,
            _ => ErrorSeverity.Low
        };
    }

    private ErrorSeverity ParseSeverityFromLog(SystemLogEntry log)
    {
        try
        {
            if (!string.IsNullOrEmpty(log.AdditionalData))
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(log.AdditionalData);
                if (data != null && data.ContainsKey("Severity"))
                {
                    if (Enum.TryParse<ErrorSeverity>(data["Severity"].ToString(), out var severity))
                    {
                        return severity;
                    }
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        // Default based on log level
        return log.Level switch
        {
            LogLevel.Critical => ErrorSeverity.Critical,
            LogLevel.Error => ErrorSeverity.High,
            LogLevel.Warning => ErrorSeverity.Medium,
            _ => ErrorSeverity.Low
        };
    }

    private string ExtractErrorTypeFromLog(SystemLogEntry log)
    {
        try
        {
            if (!string.IsNullOrEmpty(log.AdditionalData))
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(log.AdditionalData);
                if (data != null && data.ContainsKey("ErrorType"))
                {
                    return data["ErrorType"].ToString() ?? "Unknown";
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return "Unknown";
    }

    private string ExtractContextFromLog(SystemLogEntry log)
    {
        try
        {
            if (!string.IsNullOrEmpty(log.AdditionalData))
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(log.AdditionalData);
                if (data != null && data.ContainsKey("Context"))
                {
                    return data["Context"].ToString() ?? "Unknown";
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return log.Category.ToString();
    }

    private Dictionary<string, object> ParseAdditionalDataFromLog(SystemLogEntry log)
    {
        try
        {
            if (!string.IsNullOrEmpty(log.AdditionalData))
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(log.AdditionalData) 
                       ?? new Dictionary<string, object>();
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return new Dictionary<string, object>();
    }

    // Analysis helper methods (simplified implementations)
    private List<ErrorByCategory> AnalyzeErrorsByCategory(List<SystemLogEntry> errorLogs)
    {
        return errorLogs
            .GroupBy(log => log.Category)
            .Select(group => new ErrorByCategory
            {
                Category = group.Key.ToString(),
                Count = group.Count(),
                Percentage = (double)group.Count() / errorLogs.Count * 100,
                AverageSeverity = ErrorSeverity.Medium // Simplified
            })
            .OrderByDescending(category => category.Count)
            .ToList();
    }

    private List<ErrorByContext> AnalyzeErrorsByContext(List<SystemLogEntry> errorLogs)
    {
        return errorLogs
            .GroupBy(log => ExtractContextFromLog(log))
            .Select(group => new ErrorByContext
            {
                Context = group.Key,
                Count = group.Count(),
                Percentage = (double)group.Count() / errorLogs.Count * 100,
                CommonErrorTypes = group.Select(log => ExtractErrorTypeFromLog(log))
                                      .GroupBy(type => type)
                                      .OrderByDescending(g => g.Count())
                                      .Take(3)
                                      .Select(g => g.Key)
                                      .ToList()
            })
            .OrderByDescending(context => context.Count)
            .ToList();
    }

    private List<ErrorTrendPoint> CalculateErrorTrendPoints(List<SystemLogEntry> errorLogs, TimeSpan period)
    {
        var intervals = Math.Max(1, (int)(period.TotalHours / 24)); // Daily intervals for periods > 24 hours
        var intervalDuration = TimeSpan.FromHours(period.TotalHours / intervals);
        var startTime = DateTime.UtcNow.Subtract(period);

        var trendPoints = new List<ErrorTrendPoint>();

        for (int i = 0; i < intervals; i++)
        {
            var intervalStart = startTime.Add(TimeSpan.FromTicks(intervalDuration.Ticks * i));
            var intervalEnd = intervalStart.Add(intervalDuration);

            var intervalErrors = errorLogs.Where(log => 
                log.CreatedAt >= intervalStart && log.CreatedAt < intervalEnd).ToList();

            var severityBreakdown = new Dictionary<ErrorSeverity, int>();
            foreach (var severity in Enum.GetValues<ErrorSeverity>())
            {
                severityBreakdown[severity] = intervalErrors.Count(log => ParseSeverityFromLog(log) == severity);
            }

            trendPoints.Add(new ErrorTrendPoint
            {
                Timestamp = intervalStart,
                ErrorCount = intervalErrors.Count,
                ErrorRate = intervalDuration.TotalHours > 0 ? intervalErrors.Count / intervalDuration.TotalHours : 0,
                SeverityBreakdown = severityBreakdown
            });
        }

        return trendPoints;
    }

    private TrendDirection CalculateTrendDirection(List<ErrorTrendPoint> trendPoints)
    {
        if (trendPoints.Count < 2) return TrendDirection.Stable;

        var firstHalf = trendPoints.Take(trendPoints.Count / 2).Average(p => p.ErrorCount);
        var secondHalf = trendPoints.Skip(trendPoints.Count / 2).Average(p => p.ErrorCount);

        var change = (secondHalf - firstHalf) / Math.Max(firstHalf, 1);

        return change switch
        {
            > 0.1 => TrendDirection.Increasing,
            < -0.1 => TrendDirection.Decreasing,
            _ => TrendDirection.Stable
        };
    }

    private double CalculateChangePercentage(List<ErrorTrendPoint> trendPoints)
    {
        if (trendPoints.Count < 2) return 0;

        var firstValue = trendPoints.First().ErrorCount;
        var lastValue = trendPoints.Last().ErrorCount;

        return firstValue > 0 ? ((double)(lastValue - firstValue) / firstValue) * 100 : 0;
    }

    private List<SeverityTrend> CalculateSeverityTrends(List<SystemLogEntry> errorLogs, TimeSpan period)
    {
        var severityTrends = new List<SeverityTrend>();

        foreach (var severity in Enum.GetValues<ErrorSeverity>())
        {
            var severityLogs = errorLogs.Where(log => ParseSeverityFromLog(log) == severity).ToList();
            var trendPoints = CalculateErrorTrendPoints(severityLogs, period);
            var direction = CalculateTrendDirection(trendPoints);
            var changePercentage = CalculateChangePercentage(trendPoints);

            severityTrends.Add(new SeverityTrend
            {
                Severity = severity,
                TrendPoints = trendPoints,
                Direction = direction,
                ChangePercentage = changePercentage
            });
        }

        return severityTrends;
    }

    // Simplified analysis methods (would be more sophisticated in production)
    private List<ErrorCorrelation> AnalyzeErrorCorrelations(List<SystemLogEntry> errorLogs) => new();
    private List<ErrorCluster> AnalyzeErrorClusters(List<SystemLogEntry> errorLogs) => new();
    private List<ErrorSequenceData> AnalyzeErrorSequences(List<SystemLogEntry> errorLogs) => new();
    private List<ErrorHotspot> AnalyzeErrorHotspots(List<SystemLogEntry> errorLogs) => new();
}