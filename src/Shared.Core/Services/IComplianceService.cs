using Shared.Core.DTOs;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Interface for compliance service managing data protection regulations
/// </summary>
public interface IComplianceService
{
    /// <summary>
    /// Performs comprehensive compliance assessment for a business
    /// </summary>
    /// <param name="businessId">Business ID to assess</param>
    /// <param name="standards">Compliance standards to assess against</param>
    /// <param name="fromDate">Start date for assessment period</param>
    /// <param name="toDate">End date for assessment period</param>
    /// <returns>Compliance assessment result</returns>
    Task<ComplianceAssessmentResult> PerformComplianceAssessmentAsync(
        Guid businessId, 
        List<ComplianceStandard> standards, 
        DateTime? fromDate = null, 
        DateTime? toDate = null);

    /// <summary>
    /// Handles data subject rights requests (GDPR Article 15-22)
    /// </summary>
    /// <param name="request">Data subject rights request</param>
    /// <returns>Processing result</returns>
    Task<DataSubjectRightsResult> HandleDataSubjectRightsRequestAsync(DataSubjectRightsRequest request);

    /// <summary>
    /// Generates compliance reports for regulatory authorities
    /// </summary>
    /// <param name="businessId">Business ID for the report</param>
    /// <param name="standard">Compliance standard</param>
    /// <param name="fromDate">Start date for report period</param>
    /// <param name="toDate">End date for report period</param>
    /// <param name="reportingAuthority">Authority requesting the report</param>
    /// <returns>Regulatory compliance report</returns>
    Task<EnhancedComplianceReport> GenerateRegulatoryReportAsync(
        Guid businessId, 
        ComplianceStandard standard, 
        DateTime fromDate, 
        DateTime toDate,
        string reportingAuthority);

    /// <summary>
    /// Configures compliance monitoring and alerting
    /// </summary>
    /// <param name="businessId">Business ID to configure</param>
    /// <param name="config">Compliance monitoring configuration</param>
    /// <returns>Configuration result</returns>
    Task<ComplianceConfigurationResult> ConfigureComplianceMonitoringAsync(
        Guid businessId, 
        ComplianceMonitoringConfig config);
}