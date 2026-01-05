using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.DTOs;

/// <summary>
/// Result of threat detection monitoring
/// </summary>
public class ThreatDetectionResult
{
    public Guid BusinessId { get; set; }
    public TimeSpan MonitoringWindow { get; set; }
    public List<DetectedThreat> DetectedThreats { get; set; } = new();
    public List<SecurityAlert> SecurityAlerts { get; set; } = new();
    public int TotalEventsAnalyzed { get; set; }
    public int HighSeverityThreats { get; set; }
    public int MediumSeverityThreats { get; set; }
    public int LowSeverityThreats { get; set; }
    public DateTime MonitoringTimestamp { get; set; }
}

/// <summary>
/// Detected security threat
/// </summary>
public class DetectedThreat
{
    public string ThreatType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ThreatSeverity Severity { get; set; }
    public Guid BusinessId { get; set; }
    public DateTime DetectedAt { get; set; }
    public int EventCount { get; set; }
    public TimeSpan TimeWindow { get; set; }
    public List<Guid> AffectedUsers { get; set; } = new();
    public List<string> Evidence { get; set; } = new();
}

/// <summary>
/// Security alert generated from detected threats
/// </summary>
public class SecurityAlert
{
    public Guid AlertId { get; set; }
    public Guid BusinessId { get; set; }
    public string ThreatType { get; set; } = string.Empty;
    public ThreatSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> RecommendedActions { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public AlertStatus Status { get; set; }
    public List<Guid> AffectedUsers { get; set; } = new();
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
}

/// <summary>
/// Result of authentication pattern analysis
/// </summary>
public class AuthenticationAnalysisResult
{
    public Guid BusinessId { get; set; }
    public TimeSpan AnalysisWindow { get; set; }
    public int TotalLoginAttempts { get; set; }
    public int SuccessfulLogins { get; set; }
    public int FailedLogins { get; set; }
    public List<SuspiciousAuthPattern> SuspiciousPatterns { get; set; } = new();
    public bool UnusualActivityDetected { get; set; }
    public DateTime AnalysisTimestamp { get; set; }
}

/// <summary>
/// Suspicious authentication pattern
/// </summary>
public class SuspiciousAuthPattern
{
    public string PatternType { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public int EventCount { get; set; }
    public DateTime FirstOccurrence { get; set; }
    public DateTime LastOccurrence { get; set; }
    public ThreatSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Compliance report for data protection regulations
/// </summary>
public class ComplianceReport
{
    public Guid BusinessId { get; set; }
    public ComplianceStandard Standard { get; set; }
    public DateRange ReportPeriod { get; set; } = new();
    public List<ComplianceCheck> ComplianceChecks { get; set; } = new();
    public int OverallComplianceScore { get; set; }
    public int PassedChecks { get; set; }
    public int FailedChecks { get; set; }
    public int WarningChecks { get; set; }
    public List<string> RecommendedActions { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Individual compliance check result
/// </summary>
public class ComplianceCheck
{
    public string CheckName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ComplianceStatus Status { get; set; }
    public string Evidence { get; set; } = string.Empty;
    public string Requirement { get; set; } = string.Empty;
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Security monitoring configuration
/// </summary>
public class SecurityMonitoringConfig
{
    public Guid BusinessId { get; set; }
    public List<ThreatDetectionRule> ThreatDetectionRules { get; set; } = new();
    public TimeSpan DefaultMonitoringWindow { get; set; } = TimeSpan.FromHours(24);
    public bool EnableRealTimeAlerts { get; set; } = true;
    public bool EnableComplianceReporting { get; set; } = true;
    public List<ComplianceStandard> RequiredStandards { get; set; } = new();
}

/// <summary>
/// Threat detection rule configuration
/// </summary>
public class ThreatDetectionRule
{
    public string PatternName { get; set; } = string.Empty;
    public int Threshold { get; set; }
    public TimeSpan TimeWindow { get; set; }
    public ThreatSeverity Severity { get; set; }
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Result of security configuration
/// </summary>
public class SecurityConfigurationResult
{
    public Guid BusinessId { get; set; }
    public SecurityMonitoringConfig? Configuration { get; set; }
    public DateTime ConfiguredAt { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> ValidationErrors { get; set; } = new();
    public List<string> EnabledFeatures { get; set; } = new();
}

/// <summary>
/// Threat pattern definition
/// </summary>
public class ThreatPattern
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Threshold { get; set; }
    public TimeSpan TimeWindow { get; set; }
    public ThreatSeverity Severity { get; set; }
    public AuditAction[] Actions { get; set; } = Array.Empty<AuditAction>();
}

/// <summary>
/// Security metrics for a business
/// </summary>
public class SecurityMetrics
{
    public Guid BusinessId { get; set; }
    public int TotalThreatsDetected { get; set; }
    public int HighSeverityThreats { get; set; }
    public int MediumSeverityThreats { get; set; }
    public int LowSeverityThreats { get; set; }
    public int ActiveAlerts { get; set; }
    public DateTime LastMonitoringRun { get; set; }
    public double SecurityScore { get; set; }
}