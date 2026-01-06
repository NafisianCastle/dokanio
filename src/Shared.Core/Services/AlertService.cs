using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of alert service for centralized alerting and notifications
/// </summary>
public class AlertService : IAlertService
{
    private readonly ILogger<AlertService> _logger;
    private readonly IComprehensiveLoggingService _loggingService;
    private readonly ConcurrentDictionary<Guid, SystemAlert> _activeAlerts = new();
    private readonly ConcurrentDictionary<string, DateTime> _suppressionCache = new();
    private AlertConfiguration _configuration = new();

    public AlertService(
        ILogger<AlertService> logger,
        IComprehensiveLoggingService loggingService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
    }

    /// <summary>
    /// Triggers an error alert based on an error occurrence
    /// </summary>
    public async Task TriggerErrorAlertAsync(ErrorOccurrence errorOccurrence)
    {
        try
        {
            var severity = MapErrorSeverityToAlertSeverity(errorOccurrence.Severity);
            var alertType = $"Error_{errorOccurrence.ErrorType}";
            
            // Check if this type of alert is suppressed
            if (IsAlertSuppressed(alertType))
            {
                _logger.LogDebug("Alert suppressed: {AlertType}", alertType);
                return;
            }

            // Check for existing similar alert
            var existingAlert = _activeAlerts.Values
                .FirstOrDefault(a => a.AlertType == alertType && 
                                   a.Status == AlertStatus.Active &&
                                   a.BusinessId == errorOccurrence.BusinessId);

            if (existingAlert != null)
            {
                // Update existing alert
                existingAlert.OccurrenceCount++;
                existingAlert.LastOccurrence = DateTime.UtcNow;
                existingAlert.Message = $"Error occurred {existingAlert.OccurrenceCount} times. Latest: {errorOccurrence.Message}";
            }
            else
            {
                // Create new alert
                var alert = new SystemAlert
                {
                    AlertType = alertType,
                    Title = $"Error: {errorOccurrence.ErrorType}",
                    Message = $"Error in {errorOccurrence.Context}: {errorOccurrence.Message}",
                    Severity = severity,
                    Status = AlertStatus.Active,
                    BusinessId = errorOccurrence.BusinessId,
                    DeviceId = errorOccurrence.DeviceId,
                    UserId = errorOccurrence.UserId,
                    Metadata = new Dictionary<string, object>
                    {
                        ["ErrorId"] = errorOccurrence.Id,
                        ["ErrorType"] = errorOccurrence.ErrorType,
                        ["Context"] = errorOccurrence.Context,
                        ["StackTrace"] = errorOccurrence.StackTrace
                    }
                };

                _activeAlerts.TryAdd(alert.Id, alert);
                await DeliverAlertAsync(alert);
            }

            // Log the alert
            await _loggingService.LogErrorAsync(
                $"Alert triggered: {alertType}",
                LogCategory.System,
                errorOccurrence.DeviceId,
                null,
                errorOccurrence.UserId,
                new { AlertType = alertType, ErrorOccurrence = errorOccurrence });

            _logger.LogWarning("Error alert triggered: {AlertType} for error {ErrorId}", 
                alertType, errorOccurrence.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger error alert for error {ErrorId}", errorOccurrence.Id);
        }
    }

    /// <summary>
    /// Triggers a performance alert
    /// </summary>
    public async Task TriggerPerformanceAlertAsync(string title, string message, AlertSeverity severity, Guid? businessId = null, Guid? deviceId = null, Dictionary<string, object>? metadata = null)
    {
        try
        {
            var alertType = $"Performance_{title.Replace(" ", "_")}";
            
            if (IsAlertSuppressed(alertType))
            {
                return;
            }

            var alert = new SystemAlert
            {
                AlertType = alertType,
                Title = title,
                Message = message,
                Severity = severity,
                Status = AlertStatus.Active,
                BusinessId = businessId,
                DeviceId = deviceId,
                Metadata = metadata ?? new Dictionary<string, object>()
            };

            _activeAlerts.TryAdd(alert.Id, alert);
            await DeliverAlertAsync(alert);

            await _loggingService.LogWarningAsync(
                $"Performance alert: {title}",
                LogCategory.Performance,
                deviceId ?? Guid.NewGuid(),
                null,
                new { AlertType = alertType, Title = title, Message = message, Severity = severity.ToString() });

            _logger.LogWarning("Performance alert triggered: {Title}", title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger performance alert: {Title}", title);
        }
    }

    /// <summary>
    /// Triggers a system health alert
    /// </summary>
    public async Task TriggerSystemHealthAlertAsync(string component, string status, string message, AlertSeverity severity, Guid? businessId = null)
    {
        try
        {
            var alertType = $"SystemHealth_{component}";
            
            if (IsAlertSuppressed(alertType))
            {
                return;
            }

            var alert = new SystemAlert
            {
                AlertType = alertType,
                Title = $"System Health: {component}",
                Message = $"Component {component} status: {status}. {message}",
                Severity = severity,
                Status = AlertStatus.Active,
                BusinessId = businessId,
                Metadata = new Dictionary<string, object>
                {
                    ["Component"] = component,
                    ["Status"] = status
                }
            };

            _activeAlerts.TryAdd(alert.Id, alert);
            await DeliverAlertAsync(alert);

            await _loggingService.LogErrorAsync(
                $"System health alert: {component} - {status}",
                LogCategory.System,
                Guid.NewGuid(),
                null,
                null,
                new { AlertType = alertType, Component = component, Status = status, Message = message });

            _logger.LogWarning("System health alert triggered: {Component} - {Status}", component, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger system health alert for component {Component}", component);
        }
    }

    /// <summary>
    /// Triggers a business-specific alert
    /// </summary>
    public async Task TriggerBusinessAlertAsync(Guid businessId, string alertType, string title, string message, AlertSeverity severity, Dictionary<string, object>? metadata = null)
    {
        try
        {
            var fullAlertType = $"Business_{alertType}";
            
            if (IsAlertSuppressed(fullAlertType))
            {
                return;
            }

            var alert = new SystemAlert
            {
                AlertType = fullAlertType,
                Title = title,
                Message = message,
                Severity = severity,
                Status = AlertStatus.Active,
                BusinessId = businessId,
                Metadata = metadata ?? new Dictionary<string, object>()
            };

            _activeAlerts.TryAdd(alert.Id, alert);
            await DeliverAlertAsync(alert);

            await _loggingService.LogWarningAsync(
                $"Business alert: {title}",
                LogCategory.Business,
                Guid.NewGuid(),
                null,
                new { AlertType = fullAlertType, BusinessId = businessId, Title = title, Message = message });

            _logger.LogWarning("Business alert triggered for {BusinessId}: {Title}", businessId, title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger business alert for {BusinessId}: {AlertType}", businessId, alertType);
        }
    }

    /// <summary>
    /// Gets active alerts for a business
    /// </summary>
    public async Task<List<SystemAlert>> GetActiveAlertsAsync(Guid? businessId = null, AlertSeverity? severity = null)
    {
        try
        {
            var alerts = _activeAlerts.Values
                .Where(a => a.Status == AlertStatus.Active)
                .Where(a => !businessId.HasValue || a.BusinessId == businessId)
                .Where(a => !severity.HasValue || a.Severity >= severity.Value)
                .OrderByDescending(a => a.CreatedAt)
                .ToList();

            await Task.CompletedTask;
            return alerts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active alerts");
            return new List<SystemAlert>();
        }
    }

    /// <summary>
    /// Gets alert history for analysis
    /// </summary>
    public async Task<List<SystemAlert>> GetAlertHistoryAsync(TimeSpan period, Guid? businessId = null)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.Subtract(period);
            
            var alerts = _activeAlerts.Values
                .Where(a => a.CreatedAt >= cutoffTime)
                .Where(a => !businessId.HasValue || a.BusinessId == businessId)
                .OrderByDescending(a => a.CreatedAt)
                .ToList();

            await Task.CompletedTask;
            return alerts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alert history");
            return new List<SystemAlert>();
        }
    }

    /// <summary>
    /// Resolves an alert
    /// </summary>
    public async Task ResolveAlertAsync(Guid alertId, string resolution, Guid resolvedBy)
    {
        try
        {
            if (_activeAlerts.TryGetValue(alertId, out var alert))
            {
                alert.Status = AlertStatus.Resolved;
                alert.ResolvedAt = DateTime.UtcNow;
                alert.ResolvedBy = resolvedBy;
                alert.Resolution = resolution;

                await _loggingService.LogInfoAsync(
                    $"Alert resolved: {alert.Title}",
                    LogCategory.System,
                    Guid.NewGuid(),
                    resolvedBy,
                    new { AlertId = alertId, Resolution = resolution, ResolvedBy = resolvedBy });

                _logger.LogInformation("Alert {AlertId} resolved by {ResolvedBy}: {Resolution}", 
                    alertId, resolvedBy, resolution);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving alert {AlertId}", alertId);
        }
    }

    /// <summary>
    /// Suppresses an alert (temporarily disables similar alerts)
    /// </summary>
    public async Task SuppressAlertAsync(Guid alertId, TimeSpan suppressionDuration, Guid suppressedBy)
    {
        try
        {
            if (_activeAlerts.TryGetValue(alertId, out var alert))
            {
                var suppressionKey = alert.AlertType;
                var suppressUntil = DateTime.UtcNow.Add(suppressionDuration);
                
                alert.Status = AlertStatus.Suppressed;
                alert.SuppressedUntil = suppressUntil;
                alert.SuppressedBy = suppressedBy;
                
                _suppressionCache.TryAdd(suppressionKey, suppressUntil);

                await _loggingService.LogInfoAsync(
                    $"Alert suppressed: {alert.Title} until {suppressUntil}",
                    LogCategory.System,
                    Guid.NewGuid(),
                    suppressedBy,
                    new { AlertId = alertId, SuppressedUntil = suppressUntil, SuppressedBy = suppressedBy });

                _logger.LogInformation("Alert {AlertId} suppressed by {SuppressedBy} until {SuppressUntil}", 
                    alertId, suppressedBy, suppressUntil);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suppressing alert {AlertId}", alertId);
        }
    }

    /// <summary>
    /// Gets alert statistics for monitoring
    /// </summary>
    public async Task<AlertStatistics> GetAlertStatisticsAsync(TimeSpan period, Guid? businessId = null)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.Subtract(period);
            var alerts = _activeAlerts.Values
                .Where(a => a.CreatedAt >= cutoffTime)
                .Where(a => !businessId.HasValue || a.BusinessId == businessId)
                .ToList();

            var totalAlerts = alerts.Count;
            var activeAlerts = alerts.Count(a => a.Status == AlertStatus.Active);
            var resolvedAlerts = alerts.Count(a => a.Status == AlertStatus.Resolved);
            var suppressedAlerts = alerts.Count(a => a.Status == AlertStatus.Suppressed);

            var alertsBySeverity = alerts
                .GroupBy(a => a.Severity)
                .ToDictionary(g => g.Key, g => g.Count());

            var alertsByType = alerts
                .GroupBy(a => a.AlertType)
                .ToDictionary(g => g.Key, g => g.Count());

            var resolvedAlertsWithTime = alerts
                .Where(a => a.Status == AlertStatus.Resolved && a.ResolvedAt.HasValue)
                .ToList();

            var averageResolutionTime = resolvedAlertsWithTime.Any()
                ? TimeSpan.FromTicks((long)resolvedAlertsWithTime
                    .Average(a => (a.ResolvedAt!.Value - a.CreatedAt).Ticks))
                : TimeSpan.Zero;

            var alertRate = period.TotalHours > 0 ? totalAlerts / period.TotalHours : 0;

            var trends = CalculateAlertTrends(alerts, period);

            return new AlertStatistics
            {
                Period = period,
                StartDate = cutoffTime,
                EndDate = DateTime.UtcNow,
                TotalAlerts = totalAlerts,
                ActiveAlerts = activeAlerts,
                ResolvedAlerts = resolvedAlerts,
                SuppressedAlerts = suppressedAlerts,
                AlertsBySeverity = alertsBySeverity,
                AlertsByType = alertsByType,
                AverageResolutionTime = averageResolutionTime,
                AlertRate = alertRate,
                Trends = trends
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alert statistics");
            return new AlertStatistics { Period = period };
        }
    }

    /// <summary>
    /// Configures alert thresholds and rules
    /// </summary>
    public async Task ConfigureAlertingAsync(AlertConfiguration configuration)
    {
        try
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            await _loggingService.LogInfoAsync(
                "Alert configuration updated",
                LogCategory.System,
                Guid.NewGuid(),
                null,
                new { Configuration = configuration });

            _logger.LogInformation("Alert configuration updated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring alerting");
        }
    }

    /// <summary>
    /// Tests alert delivery mechanisms
    /// </summary>
    public async Task TestAlertDeliveryAsync(string alertType, Guid? businessId = null)
    {
        try
        {
            var testAlert = new SystemAlert
            {
                AlertType = $"Test_{alertType}",
                Title = "Test Alert",
                Message = "This is a test alert to verify delivery mechanisms are working correctly.",
                Severity = AlertSeverity.Low,
                Status = AlertStatus.Active,
                BusinessId = businessId,
                Metadata = new Dictionary<string, object>
                {
                    ["IsTest"] = true,
                    ["TestType"] = alertType
                }
            };

            await DeliverAlertAsync(testAlert);

            await _loggingService.LogInfoAsync(
                $"Test alert delivered: {alertType}",
                LogCategory.System,
                Guid.NewGuid(),
                null,
                new { AlertType = alertType, BusinessId = businessId });

            _logger.LogInformation("Test alert delivered: {AlertType}", alertType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing alert delivery for {AlertType}", alertType);
        }
    }

    // Helper methods
    private AlertSeverity MapErrorSeverityToAlertSeverity(ErrorSeverity errorSeverity)
    {
        return errorSeverity switch
        {
            ErrorSeverity.Critical => AlertSeverity.Critical,
            ErrorSeverity.High => AlertSeverity.High,
            ErrorSeverity.Medium => AlertSeverity.Medium,
            ErrorSeverity.Low => AlertSeverity.Low,
            _ => AlertSeverity.Medium
        };
    }

    private bool IsAlertSuppressed(string alertType)
    {
        if (_suppressionCache.TryGetValue(alertType, out var suppressedUntil))
        {
            if (DateTime.UtcNow < suppressedUntil)
            {
                return true;
            }
            else
            {
                // Remove expired suppression
                _suppressionCache.TryRemove(alertType, out _);
            }
        }

        return false;
    }

    private async Task DeliverAlertAsync(SystemAlert alert)
    {
        try
        {
            // In a real implementation, this would deliver alerts through configured channels
            // (email, SMS, webhooks, etc.) based on the alert configuration
            
            // For now, we'll just log the alert delivery
            _logger.LogWarning("Alert delivered: {Title} - {Message} (Severity: {Severity})", 
                alert.Title, alert.Message, alert.Severity);

            // Simulate delivery to different channels based on severity
            if (alert.Severity >= AlertSeverity.High)
            {
                // Would send to high-priority channels (SMS, phone calls, etc.)
                _logger.LogWarning("High-priority alert delivered via emergency channels: {Title}", alert.Title);
            }
            else if (alert.Severity >= AlertSeverity.Medium)
            {
                // Would send to medium-priority channels (email, Slack, etc.)
                _logger.LogInformation("Medium-priority alert delivered via standard channels: {Title}", alert.Title);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error delivering alert: {AlertId}", alert.Id);
        }
    }

    private List<AlertTrend> CalculateAlertTrends(List<SystemAlert> alerts, TimeSpan period)
    {
        var intervals = Math.Max(1, (int)(period.TotalHours / 24)); // Daily intervals for periods > 24 hours
        var intervalDuration = TimeSpan.FromHours(period.TotalHours / intervals);
        var startTime = DateTime.UtcNow.Subtract(period);

        var trends = new List<AlertTrend>();

        for (int i = 0; i < intervals; i++)
        {
            var intervalStart = startTime.Add(TimeSpan.FromTicks(intervalDuration.Ticks * i));
            var intervalEnd = intervalStart.Add(intervalDuration);

            var intervalAlerts = alerts.Where(a => 
                a.CreatedAt >= intervalStart && a.CreatedAt < intervalEnd).ToList();

            var severityBreakdown = new Dictionary<AlertSeverity, int>();
            foreach (var severity in Enum.GetValues<AlertSeverity>())
            {
                severityBreakdown[severity] = intervalAlerts.Count(a => a.Severity == severity);
            }

            trends.Add(new AlertTrend
            {
                Timestamp = intervalStart,
                AlertCount = intervalAlerts.Count,
                SeverityBreakdown = severityBreakdown
            });
        }

        return trends;
    }
}