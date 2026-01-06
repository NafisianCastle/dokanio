using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;

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

    // Enhanced methods for comprehensive audit service
    
    /// <summary>
    /// Gets audit logs for a specific entity with date range
    /// </summary>
    Task<List<AuditLog>> GetByEntityAsync(string entityType, Guid entityId, DateTime? fromDate, DateTime? toDate);
    
    /// <summary>
    /// Gets audit logs by user with pagination
    /// </summary>
    Task<List<AuditLog>> GetByUserAsync(Guid userId, DateTime? fromDate, DateTime? toDate, int maxResults);
    
    /// <summary>
    /// Gets audit logs by multiple actions
    /// </summary>
    Task<List<AuditLog>> GetByActionsAsync(List<AuditAction> actions, DateTime? fromDate, DateTime? toDate, int maxResults);
    
    /// <summary>
    /// Gets audit statistics for a date range
    /// </summary>
    Task<AuditStatistics> GetStatisticsAsync(DateTime fromDate, DateTime toDate);
}