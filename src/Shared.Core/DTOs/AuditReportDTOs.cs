using Shared.Core.Entities;

namespace Shared.Core.DTOs;

/// <summary>
/// Comprehensive audit report
/// </summary>
public class AuditReport
{
    public Guid BusinessId { get; set; }
    public DateRange ReportPeriod { get; set; } = new();
    public int TotalEvents { get; set; }
    public Dictionary<string, int> EventsByCategory { get; set; } = new();
    public Dictionary<Guid, int> UserActivitySummary { get; set; } = new();
    public int SecurityViolations { get; set; }
    public int CriticalEvents { get; set; }
    public Dictionary<Guid, int> MostActiveUsers { get; set; } = new();
    public List<AuditLog> RecentSecurityEvents { get; set; } = new();
    public ComplianceMetrics ComplianceMetrics { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public string GeneratedBy { get; set; } = string.Empty;
}

/// <summary>
/// Compliance metrics for audit reporting
/// </summary>
public class ComplianceMetrics
{
    public int TotalAuditEvents { get; set; }
    public int SecurityEvents { get; set; }
    public int EncryptionEvents { get; set; }
    public int AuthenticationEvents { get; set; }
    public int DataAccessEvents { get; set; }
    public int ConfigurationChanges { get; set; }
    public int ComplianceScore { get; set; }
}

/// <summary>
/// Result of audit log archival operation
/// </summary>
public class AuditArchiveResult
{
    public bool Success { get; set; }
    public int ArchivedCount { get; set; }
    public int DeletedCount { get; set; }
    public DateTime ArchiveDate { get; set; }
    public TimeSpan RetentionPeriod { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
}