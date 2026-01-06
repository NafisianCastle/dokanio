using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Service for managing alerts and notifications throughout the system
/// Provides centralized alerting for errors, performance issues, and system events
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// Triggers an error alert based on an error occurrence
    /// </summary>
    /// <param name="errorOccurrence">The error occurrence to alert on</param>
    Task TriggerErrorAlertAsync(ErrorOccurrence errorOccurrence);

    /// <summary>
    /// Triggers a performance alert
    /// </summary>
    /// <param name="title">Alert title</param>
    /// <param name="message">Alert message</param>
    /// <param name="severity">Alert severity</param>
    /// <param name="businessId">Business context (optional)</param>
    /// <param name="deviceId">Device context (optional)</param>
    /// <param name="metadata">Additional alert metadata</param>
    Task TriggerPerformanceAlertAsync(string title, string message, AlertSeverity severity, Guid? businessId = null, Guid? deviceId = null, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Triggers a system health alert
    /// </summary>
    /// <param name="component">System component</param>
    /// <param name="status">Health status</param>
    /// <param name="message">Alert message</param>
    /// <param name="severity">Alert severity</param>
    /// <param name="businessId">Business context (optional)</param>
    Task TriggerSystemHealthAlertAsync(string component, string status, string message, AlertSeverity severity, Guid? businessId = null);

    /// <summary>
    /// Triggers a business-specific alert
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="alertType">Type of alert</param>
    /// <param name="title">Alert title</param>
    /// <param name="message">Alert message</param>
    /// <param name="severity">Alert severity</param>
    /// <param name="metadata">Additional alert metadata</param>
    Task TriggerBusinessAlertAsync(Guid businessId, string alertType, string title, string message, AlertSeverity severity, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Gets active alerts for a business
    /// </summary>
    /// <param name="businessId">Business ID (optional - if null, returns system-wide alerts)</param>
    /// <param name="severity">Minimum severity level (optional)</param>
    /// <returns>Active alerts</returns>
    Task<List<SystemAlert>> GetActiveAlertsAsync(Guid? businessId = null, AlertSeverity? severity = null);

    /// <summary>
    /// Gets alert history for analysis
    /// </summary>
    /// <param name="period">Time period to analyze</param>
    /// <param name="businessId">Business filter (optional)</param>
    /// <returns>Alert history</returns>
    Task<List<SystemAlert>> GetAlertHistoryAsync(TimeSpan period, Guid? businessId = null);

    /// <summary>
    /// Resolves an alert
    /// </summary>
    /// <param name="alertId">Alert ID</param>
    /// <param name="resolution">Resolution description</param>
    /// <param name="resolvedBy">User who resolved the alert</param>
    Task ResolveAlertAsync(Guid alertId, string resolution, Guid resolvedBy);

    /// <summary>
    /// Suppresses an alert (temporarily disables similar alerts)
    /// </summary>
    /// <param name="alertId">Alert ID</param>
    /// <param name="suppressionDuration">How long to suppress similar alerts</param>
    /// <param name="suppressedBy">User who suppressed the alert</param>
    Task SuppressAlertAsync(Guid alertId, TimeSpan suppressionDuration, Guid suppressedBy);

    /// <summary>
    /// Gets alert statistics for monitoring
    /// </summary>
    /// <param name="period">Time period to analyze</param>
    /// <param name="businessId">Business filter (optional)</param>
    /// <returns>Alert statistics</returns>
    Task<AlertStatistics> GetAlertStatisticsAsync(TimeSpan period, Guid? businessId = null);

    /// <summary>
    /// Configures alert thresholds and rules
    /// </summary>
    /// <param name="configuration">Alert configuration</param>
    Task ConfigureAlertingAsync(AlertConfiguration configuration);

    /// <summary>
    /// Tests alert delivery mechanisms
    /// </summary>
    /// <param name="alertType">Type of alert to test</param>
    /// <param name="businessId">Business context for testing</param>
    Task TestAlertDeliveryAsync(string alertType, Guid? businessId = null);
}

/// <summary>
/// System alert data structure
/// </summary>
public class SystemAlert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AlertType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public AlertStatus Status { get; set; }
    public Guid? BusinessId { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }
    public string? Resolution { get; set; }
    public DateTime? SuppressedUntil { get; set; }
    public Guid? SuppressedBy { get; set; }
    public int OccurrenceCount { get; set; } = 1;
    public DateTime LastOccurrence { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Alert statistics data structure
/// </summary>
public class AlertStatistics
{
    public TimeSpan Period { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalAlerts { get; set; }
    public int ActiveAlerts { get; set; }
    public int ResolvedAlerts { get; set; }
    public int SuppressedAlerts { get; set; }
    public Dictionary<AlertSeverity, int> AlertsBySeverity { get; set; } = new();
    public Dictionary<string, int> AlertsByType { get; set; } = new();
    public TimeSpan AverageResolutionTime { get; set; }
    public double AlertRate { get; set; }
    public List<AlertTrend> Trends { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Alert trend data
/// </summary>
public class AlertTrend
{
    public DateTime Timestamp { get; set; }
    public int AlertCount { get; set; }
    public Dictionary<AlertSeverity, int> SeverityBreakdown { get; set; } = new();
}

/// <summary>
/// Alert configuration data structure
/// </summary>
public class AlertConfiguration
{
    public Guid? BusinessId { get; set; }
    public Dictionary<string, AlertRule> Rules { get; set; } = new();
    public List<AlertChannel> Channels { get; set; } = new();
    public AlertThresholds Thresholds { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Alert rule configuration
/// </summary>
public class AlertRule
{
    public string RuleType { get; set; } = string.Empty;
    public AlertSeverity MinimumSeverity { get; set; } = AlertSeverity.Medium;
    public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxOccurrences { get; set; } = 10;
    public bool IsEnabled { get; set; } = true;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Alert delivery channel configuration
/// </summary>
public class AlertChannel
{
    public string ChannelType { get; set; } = string.Empty; // Email, SMS, Webhook, etc.
    public string Destination { get; set; } = string.Empty;
    public AlertSeverity MinimumSeverity { get; set; } = AlertSeverity.High;
    public bool IsEnabled { get; set; } = true;
    public Dictionary<string, object> Configuration { get; set; } = new();
}

/// <summary>
/// Alert thresholds configuration
/// </summary>
public class AlertThresholds
{
    public int ErrorRateThreshold { get; set; } = 10; // Errors per hour
    public double PerformanceThreshold { get; set; } = 1000; // Response time in ms
    public double MemoryThreshold { get; set; } = 80; // Memory usage percentage
    public double CpuThreshold { get; set; } = 80; // CPU usage percentage
    public double DiskThreshold { get; set; } = 90; // Disk usage percentage
    public int ConcurrentUserThreshold { get; set; } = 100;
}

