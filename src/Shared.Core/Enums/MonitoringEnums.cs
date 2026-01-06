namespace Shared.Core.Enums;

/// <summary>
/// Alert severity enumeration
/// </summary>
public enum AlertSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Alert status enumeration
/// </summary>
public enum AlertStatus
{
    Active,
    Resolved,
    Suppressed,
    Acknowledged
}

/// <summary>
/// Error severity enumeration
/// </summary>
public enum ErrorSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Trend direction enumeration
/// </summary>
public enum TrendDirection
{
    Increasing,
    Decreasing,
    Stable,
    Volatile
}

/// <summary>
/// Issue severity enumeration
/// </summary>
public enum IssueSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Recommendation priority enumeration
/// </summary>
public enum RecommendationPriority
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Insight type enumeration
/// </summary>
public enum InsightType
{
    Performance,
    Usage,
    Error,
    Security,
    Business,
    Optimization
}

/// <summary>
/// Insight severity enumeration
/// </summary>
public enum InsightSeverity
{
    Information,
    Warning,
    Critical
}

/// <summary>
/// Optimization priority enumeration
/// </summary>
public enum OptimizationPriority
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Performance grade enumeration
/// </summary>
public enum PerformanceGrade
{
    A,
    B,
    C,
    D,
    F
}