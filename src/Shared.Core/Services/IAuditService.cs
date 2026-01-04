using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Service for audit logging of security-sensitive operations
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Logs an audit event
    /// </summary>
    /// <param name="userId">User ID performing the action</param>
    /// <param name="action">The action being performed</param>
    /// <param name="description">Description of the action</param>
    /// <param name="entityType">Type of entity being acted upon</param>
    /// <param name="entityId">ID of entity being acted upon</param>
    /// <param name="oldValues">Previous values (for updates)</param>
    /// <param name="newValues">New values (for updates)</param>
    /// <param name="ipAddress">IP address of the user</param>
    /// <param name="userAgent">User agent string</param>
    /// <returns>Created audit log entry</returns>
    Task<AuditLog> LogAsync(
        Guid? userId,
        AuditAction action,
        string description,
        string? entityType = null,
        Guid? entityId = null,
        string? oldValues = null,
        string? newValues = null,
        string? ipAddress = null,
        string? userAgent = null);
    
    /// <summary>
    /// Gets audit logs for a specific user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="from">Start date</param>
    /// <param name="to">End date</param>
    /// <returns>List of audit logs</returns>
    Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(Guid userId, DateTime? from = null, DateTime? to = null);
    
    /// <summary>
    /// Gets audit logs for a specific action
    /// </summary>
    /// <param name="action">Audit action</param>
    /// <param name="from">Start date</param>
    /// <param name="to">End date</param>
    /// <returns>List of audit logs</returns>
    Task<IEnumerable<AuditLog>> GetActionAuditLogsAsync(AuditAction action, DateTime? from = null, DateTime? to = null);
    
    /// <summary>
    /// Gets all audit logs within a date range
    /// </summary>
    /// <param name="from">Start date</param>
    /// <param name="to">End date</param>
    /// <returns>List of audit logs</returns>
    Task<IEnumerable<AuditLog>> GetAuditLogsAsync(DateTime? from = null, DateTime? to = null);
    
    /// <summary>
    /// Gets security violation logs
    /// </summary>
    /// <param name="from">Start date</param>
    /// <param name="to">End date</param>
    /// <returns>List of security violation logs</returns>
    Task<IEnumerable<AuditLog>> GetSecurityViolationsAsync(DateTime? from = null, DateTime? to = null);
}