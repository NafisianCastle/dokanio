using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Enhanced audit service for comprehensive logging of critical operations
/// </summary>
public interface IEnhancedAuditService
{
    /// <summary>
    /// Log customer-related operations
    /// </summary>
    Task LogCustomerOperationAsync(AuditAction action, Guid? customerId, string description, 
        object? oldValues = null, object? newValues = null, Guid? userId = null);
    
    /// <summary>
    /// Log membership-related operations
    /// </summary>
    Task LogMembershipOperationAsync(AuditAction action, Guid? membershipId, string description, 
        object? oldValues = null, object? newValues = null, Guid? userId = null);
    
    /// <summary>
    /// Log sale session operations
    /// </summary>
    Task LogSaleSessionOperationAsync(AuditAction action, Guid? sessionId, string description, 
        object? oldValues = null, object? newValues = null, Guid? userId = null);
    
    /// <summary>
    /// Log sale operations with enhanced details
    /// </summary>
    Task LogSaleOperationAsync(AuditAction action, Guid? saleId, string description, 
        decimal? amount = null, object? oldValues = null, object? newValues = null, Guid? userId = null);
    
    /// <summary>
    /// Log data access operations (for sensitive data)
    /// </summary>
    Task LogDataAccessAsync(string entityType, Guid? entityId, string operation, 
        string? accessReason = null, Guid? userId = null);
    
    /// <summary>
    /// Log system configuration changes
    /// </summary>
    Task LogConfigurationChangeAsync(string configKey, object? oldValue, object? newValue, 
        string? reason = null, Guid? userId = null);
    
    /// <summary>
    /// Log authentication and authorization events
    /// </summary>
    Task LogSecurityEventAsync(string eventType, string description, bool success, 
        string? ipAddress = null, string? userAgent = null, Guid? userId = null);
    
    /// <summary>
    /// Log business rule violations
    /// </summary>
    Task LogBusinessRuleViolationAsync(string ruleName, string description, 
        object? context = null, Guid? userId = null);
    
    /// <summary>
    /// Get audit trail for specific entity
    /// </summary>
    Task<List<AuditLog>> GetAuditTrailAsync(string entityType, Guid entityId, 
        DateTime? fromDate = null, DateTime? toDate = null);
    
    /// <summary>
    /// Get audit logs by user
    /// </summary>
    Task<List<AuditLog>> GetAuditLogsByUserAsync(Guid userId, 
        DateTime? fromDate = null, DateTime? toDate = null, int maxResults = 100);
    
    /// <summary>
    /// Get security events
    /// </summary>
    Task<List<AuditLog>> GetSecurityEventsAsync(DateTime? fromDate = null, DateTime? toDate = null, 
        bool? successOnly = null, int maxResults = 100);
    
    /// <summary>
    /// Get audit statistics
    /// </summary>
    Task<AuditStatistics> GetAuditStatisticsAsync(DateTime fromDate, DateTime toDate);
}

/// <summary>
/// Audit statistics summary
/// </summary>
public class AuditStatistics
{
    public int TotalEvents { get; set; }
    public int SecurityEvents { get; set; }
    public int FailedOperations { get; set; }
    public int DataAccessEvents { get; set; }
    public Dictionary<AuditAction, int> EventsByAction { get; set; } = new();
    public Dictionary<string, int> EventsByEntityType { get; set; } = new();
    public List<string> TopUsers { get; set; } = new();
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}