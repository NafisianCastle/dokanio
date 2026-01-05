namespace Shared.Core.Enums;

/// <summary>
/// Severity levels for security threats
/// </summary>
public enum ThreatSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Status of security alerts
/// </summary>
public enum AlertStatus
{
    Active = 1,
    Acknowledged = 2,
    InProgress = 3,
    Resolved = 4,
    Dismissed = 5
}

/// <summary>
/// Compliance standards for data protection
/// </summary>
public enum ComplianceStandard
{
    GDPR = 1,      // General Data Protection Regulation
    PCI_DSS = 2,   // Payment Card Industry Data Security Standard
    HIPAA = 3,     // Health Insurance Portability and Accountability Act
    SOX = 4,       // Sarbanes-Oxley Act
    ISO27001 = 5,  // ISO/IEC 27001
    NIST = 6,      // NIST Cybersecurity Framework
    Custom = 99    // Custom compliance requirements
}

/// <summary>
/// Status of compliance checks
/// </summary>
public enum ComplianceStatus
{
    Compliant = 1,
    NonCompliant = 2,
    Warning = 3,
    NotApplicable = 4,
    Unknown = 5
}

/// <summary>
/// Types of data encryption
/// </summary>
public enum EncryptionType
{
    AtRest = 1,
    InTransit = 2,
    InMemory = 3,
    EndToEnd = 4
}

/// <summary>
/// Security event categories
/// </summary>
public enum SecurityEventCategory
{
    Authentication = 1,
    Authorization = 2,
    DataAccess = 3,
    DataModification = 4,
    SystemAccess = 5,
    NetworkAccess = 6,
    ConfigurationChange = 7,
    SecurityViolation = 8
}