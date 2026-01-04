using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository interface for AuditLog entity
/// </summary>
public interface IAuditLogRepository : IRepository<AuditLog>
{
    /// <summary>
    /// Gets audit logs for a specific user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="from">Start date</param>
    /// <param name="to">End date</param>
    /// <returns>List of audit logs</returns>
    Task<IEnumerable<AuditLog>> GetByUserIdAsync(Guid userId, DateTime? from = null, DateTime? to = null);
    
    /// <summary>
    /// Gets audit logs for a specific action
    /// </summary>
    /// <param name="action">Audit action</param>
    /// <param name="from">Start date</param>
    /// <param name="to">End date</param>
    /// <returns>List of audit logs</returns>
    Task<IEnumerable<AuditLog>> GetByActionAsync(AuditAction action, DateTime? from = null, DateTime? to = null);
    
    /// <summary>
    /// Gets audit logs within a date range
    /// </summary>
    /// <param name="from">Start date</param>
    /// <param name="to">End date</param>
    /// <returns>List of audit logs</returns>
    Task<IEnumerable<AuditLog>> GetByDateRangeAsync(DateTime? from = null, DateTime? to = null);
    
    /// <summary>
    /// Gets security violation logs
    /// </summary>
    /// <param name="from">Start date</param>
    /// <param name="to">End date</param>
    /// <returns>List of security violation logs</returns>
    Task<IEnumerable<AuditLog>> GetSecurityViolationsAsync(DateTime? from = null, DateTime? to = null);
    
    /// <summary>
    /// Gets audit logs for a specific entity
    /// </summary>
    /// <param name="entityType">Entity type</param>
    /// <param name="entityId">Entity ID</param>
    /// <returns>List of audit logs</returns>
    Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityType, Guid entityId);
}