using Shared.Core.DTOs;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Interface for security monitoring and threat detection service
/// </summary>
public interface ISecurityMonitoringService
{
    /// <summary>
    /// Monitors security events in real-time and detects potential threats
    /// </summary>
    /// <param name="businessId">Business ID to monitor</param>
    /// <param name="monitoringWindow">Time window for monitoring</param>
    /// <returns>Threat detection results</returns>
    Task<ThreatDetectionResult> MonitorSecurityEventsAsync(Guid businessId, TimeSpan monitoringWindow);

    /// <summary>
    /// Analyzes authentication patterns for suspicious activity
    /// </summary>
    /// <param name="businessId">Business ID to analyze</param>
    /// <param name="analysisWindow">Time window for analysis</param>
    /// <returns>Authentication analysis results</returns>
    Task<AuthenticationAnalysisResult> AnalyzeAuthenticationPatternsAsync(Guid businessId, TimeSpan analysisWindow);

    /// <summary>
    /// Generates security compliance report for data protection regulations
    /// </summary>
    /// <param name="businessId">Business ID for compliance check</param>
    /// <param name="standard">Compliance standard to check against</param>
    /// <param name="fromDate">Start date for compliance period</param>
    /// <param name="toDate">End date for compliance period</param>
    /// <returns>Compliance report</returns>
    Task<ComplianceReport> GenerateComplianceReportAsync(Guid businessId, ComplianceStandard standard, DateTime? fromDate = null, DateTime? toDate = null);

    /// <summary>
    /// Configures security monitoring rules and thresholds
    /// </summary>
    /// <param name="businessId">Business ID to configure</param>
    /// <param name="config">Security monitoring configuration</param>
    /// <returns>Configuration result</returns>
    Task<SecurityConfigurationResult> ConfigureSecurityMonitoringAsync(Guid businessId, SecurityMonitoringConfig config);
}