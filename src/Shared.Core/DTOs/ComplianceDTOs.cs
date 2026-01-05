using Shared.Core.Enums;

namespace Shared.Core.DTOs;

/// <summary>
/// Result of comprehensive compliance assessment
/// </summary>
public class ComplianceAssessmentResult
{
    public Guid BusinessId { get; set; }
    public DateRange AssessmentPeriod { get; set; } = new();
    public List<ComplianceStandard> AssessedStandards { get; set; } = new();
    public List<StandardAssessmentResult> StandardResults { get; set; } = new();
    public int OverallComplianceScore { get; set; }
    public List<ComplianceRequirement> CriticalGaps { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DateTime AssessmentDate { get; set; }
    public DateTime NextAssessmentDue { get; set; }
}

/// <summary>
/// Assessment result for a specific compliance standard
/// </summary>
public class StandardAssessmentResult
{
    public ComplianceStandard Standard { get; set; }
    public int ComplianceScore { get; set; }
    public List<ComplianceRequirement> PassedRequirements { get; set; } = new();
    public List<ComplianceRequirement> FailedRequirements { get; set; } = new();
    public DateTime AssessmentDate { get; set; }
}

/// <summary>
/// Individual compliance requirement
/// </summary>
public class ComplianceRequirement
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public ComplianceSeverity Severity { get; set; }
    public List<string> ImplementationGuidance { get; set; } = new();
}

/// <summary>
/// Result of requirement assessment
/// </summary>
public class RequirementAssessmentResult
{
    public bool IsMet { get; set; }
    public string Evidence { get; set; } = string.Empty;
    public int Score { get; set; }
    public List<string> Gaps { get; set; } = new();
}

/// <summary>
/// Compliance framework definition
/// </summary>
public class ComplianceFramework
{
    public ComplianceStandard Standard { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ComplianceRequirement> Requirements { get; set; } = new();
    public string Version { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
}

/// <summary>
/// Data subject rights request
/// </summary>
public class DataSubjectRightsRequest
{
    public Guid RequestId { get; set; } = Guid.NewGuid();
    public DataSubjectRightsType RequestType { get; set; }
    public Guid DataSubjectId { get; set; }
    public Guid BusinessId { get; set; }
    public Guid? RequestedBy { get; set; }
    public string RequestDetails { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// Result of data subject rights request processing
/// </summary>
public class DataSubjectRightsResult
{
    public Guid RequestId { get; set; }
    public DataSubjectRightsType RequestType { get; set; }
    public Guid DataSubjectId { get; set; }
    public Guid BusinessId { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ResponseData { get; set; }
    public DateTime ProcessedAt { get; set; }
    public Guid? ProcessedBy { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Compliance monitoring configuration
/// </summary>
public class ComplianceMonitoringConfig
{
    public Guid BusinessId { get; set; }
    public List<ComplianceStandard> RequiredStandards { get; set; } = new();
    public TimeSpan MonitoringFrequency { get; set; } = TimeSpan.FromHours(24);
    public bool EnableRealTimeAlerts { get; set; } = true;
    public bool EnableAutomatedReporting { get; set; } = true;
    public List<string> NotificationRecipients { get; set; } = new();
    public Dictionary<ComplianceStandard, ComplianceThresholds> Thresholds { get; set; } = new();
    public Guid? ConfiguredBy { get; set; }
    public DateTime ConfiguredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Compliance thresholds for alerting
/// </summary>
public class ComplianceThresholds
{
    public int MinimumComplianceScore { get; set; } = 80;
    public int CriticalViolationThreshold { get; set; } = 1;
    public int WarningThreshold { get; set; } = 5;
    public TimeSpan AlertFrequency { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// Result of compliance configuration
/// </summary>
public class ComplianceConfigurationResult
{
    public Guid BusinessId { get; set; }
    public ComplianceMonitoringConfig? Configuration { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> ValidationErrors { get; set; } = new();
    public List<string> EnabledStandards { get; set; } = new();
    public DateTime ConfiguredAt { get; set; }
}

/// <summary>
/// Enhanced compliance report with regulatory information
/// </summary>
public class EnhancedComplianceReport
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
    public string? ReportingAuthority { get; set; }
    public string? ComplianceOfficer { get; set; }
    public string? ExecutiveSummary { get; set; }
    public List<string> RegulatoryNotes { get; set; } = new();
    public Dictionary<string, object> AdditionalMetadata { get; set; } = new();
}

/// <summary>
/// Severity levels for compliance requirements
/// </summary>
public enum ComplianceSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Types of data subject rights requests
/// </summary>
public enum DataSubjectRightsType
{
    AccessRequest = 1,      // Article 15 - Right of access
    RectificationRequest = 2, // Article 16 - Right to rectification
    ErasureRequest = 3,     // Article 17 - Right to erasure
    RestrictRequest = 4,    // Article 18 - Right to restriction
    PortabilityRequest = 5, // Article 20 - Right to data portability
    ObjectionRequest = 6    // Article 21 - Right to object
}