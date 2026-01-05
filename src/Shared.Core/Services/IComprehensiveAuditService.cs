using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Interface for comprehensive audit logging service with enhanced security features
/// </summary>
public interface IComprehensiveAuditService
{
    /// <summary>
    /// Logs a comprehensive audit event with enhanced security context
    /// </summary>
    /// <param name="userId">User performing the action</param>
    /// <param name="action">The audit action</param>
    /// <param name="description">Description of the action</param>
    /// <param name="entityType">Type of entity being acted upon</param>
    /// <param name="entityId">ID of entity being acted upon</param>
    /// <param name="oldValues">Previous values (for updates)</param>
    /// <param name="newValues">New values (for updates)</param>
    /// <param name="ipAddress">IP address of the user</param>
    /// <param name="userAgent">User agent string</param>
    /// <param name="businessId">Business context</param>
    /// <param name="additionalContext">Additional context information</param>
    /// <returns>Created audit log entry</returns>
    Task<AuditLog> LogSecurityEventAsync(
        Guid? userId,
        AuditAction action,
        string description,
        string? entityType = null,
        Guid? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        string? ipAddress = null,
        string? userAgent = null,
        Guid? businessId = null,
        Dictionary<string, object>? additionalContext = null);

    /// <summary>
    /// Logs business-critical operations with enhanced tracking
    /// </summary>
    /// <param name="userId">User performing the operation</param>
    /// <param name="operationName">Name of the critical operation</param>
    /// <param name="description">Description of the operation</param>
    /// <param name="businessId">Business context</param>
    /// <param name="operationData">Data related to the operation</param>
    /// <param name="ipAddress">IP address of the user</param>
    /// <param name="userAgent">User agent string</param>
    /// <returns>Created audit log entry</returns>
    Task<AuditLog> LogBusinessCriticalOperationAsync(
        Guid userId,
        string operationName,
        string description,
        Guid businessId,
        object? operationData = null,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Logs data access events for compliance tracking
    /// </summary>
    /// <param name="userId">User accessing the data</param>
    /// <param name="dataType">Type of data being accessed</param>
    /// <param name="entityId">ID of the entity being accessed</param>
    /// <param name="accessType">Type of access (read, write, etc.)</param>
    /// <param name="businessId">Business context</param>
    /// <param name="reason">Reason for data access</param>
    /// <param name="ipAddress">IP address of the user</param>
    /// <returns>Created audit log entry</returns>
    Task<AuditLog> LogDataAccessAsync(
        Guid userId,
        string dataType,
        Guid? entityId,
        DataAccessType accessType,
        Guid businessId,
        string? reason = null,
        string? ipAddress = null);

    /// <summary>
    /// Logs security violations with detailed context
    /// </summary>
    /// <param name="userId">User involved in the violation (if applicable)</param>
    /// <param name="violationType">Type of security violation</param>
    /// <param name="description">Description of the violation</param>
    /// <param name="severity">Severity of the violation</param>
    /// <param name="businessId">Business context</param>
    /// <param name="ipAddress">IP address involved</param>
    /// <param name="evidence">Evidence of the violation</param>
    /// <returns>Created audit log entry</returns>
    Task<AuditLog> LogSecurityViolationAsync(
        Guid? userId,
        string violationType,
        string description,
        ThreatSeverity severity,
        Guid? businessId = null,
        string? ipAddress = null,
        Dictionary<string, object>? evidence = null);

    /// <summary>
    /// Generates comprehensive audit report for compliance
    /// </summary>
    /// <param name="businessId">Business ID for the report</param>
    /// <param name="fromDate">Start date for the report</param>
    /// <param name="toDate">End date for the report</param>
    /// <param name="specificActions">Specific actions to include (optional)</param>
    /// <param name="includeSystemEvents">Whether to include system events</param>
    /// <returns>Comprehensive audit report</returns>
    Task<AuditReport> GenerateAuditReportAsync(
        Guid businessId,
        DateTime fromDate,
        DateTime toDate,
        List<AuditAction>? specificActions = null,
        bool includeSystemEvents = true);

    /// <summary>
    /// Archives old audit logs based on retention policy
    /// </summary>
    /// <param name="retentionPeriod">How long to retain logs</param>
    /// <param name="deleteAfterArchive">Whether to delete logs after archiving</param>
    /// <returns>Archive operation result</returns>
    Task<AuditArchiveResult> ArchiveOldLogsAsync(TimeSpan retentionPeriod, bool deleteAfterArchive = false);
}