using Microsoft.Extensions.Logging;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// Enhanced audit service implementation for comprehensive logging of critical operations
/// </summary>
public class EnhancedAuditService : IEnhancedAuditService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EnhancedAuditService> _logger;

    public EnhancedAuditService(
        IAuditLogRepository auditLogRepository,
        ICurrentUserService currentUserService,
        ILogger<EnhancedAuditService> logger)
    {
        _auditLogRepository = auditLogRepository;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Log customer-related operations
    /// </summary>
    public async Task LogCustomerOperationAsync(AuditAction action, Guid? customerId, string description, 
        object? oldValues = null, object? newValues = null, Guid? userId = null)
    {
        await LogAuditEventAsync(action, "Customer", customerId, description, oldValues, newValues, userId);
    }

    /// <summary>
    /// Log membership-related operations
    /// </summary>
    public async Task LogMembershipOperationAsync(AuditAction action, Guid? membershipId, string description, 
        object? oldValues = null, object? newValues = null, Guid? userId = null)
    {
        await LogAuditEventAsync(action, "CustomerMembership", membershipId, description, oldValues, newValues, userId);
    }

    /// <summary>
    /// Log sale session operations
    /// </summary>
    public async Task LogSaleSessionOperationAsync(AuditAction action, Guid? sessionId, string description, 
        object? oldValues = null, object? newValues = null, Guid? userId = null)
    {
        await LogAuditEventAsync(action, "SaleSession", sessionId, description, oldValues, newValues, userId);
    }

    /// <summary>
    /// Log sale operations with enhanced details
    /// </summary>
    public async Task LogSaleOperationAsync(AuditAction action, Guid? saleId, string description, 
        decimal? amount = null, object? oldValues = null, object? newValues = null, Guid? userId = null)
    {
        var enhancedDescription = amount.HasValue 
            ? $"{description} (Amount: {amount:C})" 
            : description;
            
        await LogAuditEventAsync(action, "Sale", saleId, enhancedDescription, oldValues, newValues, userId);
    }

    /// <summary>
    /// Log data access operations (for sensitive data)
    /// </summary>
    public async Task LogDataAccessAsync(string entityType, Guid? entityId, string operation, 
        string? accessReason = null, Guid? userId = null)
    {
        var description = $"Data access: {operation}";
        if (!string.IsNullOrEmpty(accessReason))
        {
            description += $" - Reason: {accessReason}";
        }

        await LogAuditEventAsync(AuditAction.Read, entityType, entityId, description, null, null, userId);
    }

    /// <summary>
    /// Log system configuration changes
    /// </summary>
    public async Task LogConfigurationChangeAsync(string configKey, object? oldValue, object? newValue, 
        string? reason = null, Guid? userId = null)
    {
        var description = $"Configuration changed: {configKey}";
        if (!string.IsNullOrEmpty(reason))
        {
            description += $" - Reason: {reason}";
        }

        var oldValues = oldValue != null ? new { Key = configKey, Value = oldValue } : null;
        var newValues = newValue != null ? new { Key = configKey, Value = newValue } : null;

        await LogAuditEventAsync(AuditAction.Update, "Configuration", null, description, oldValues, newValues, userId);
    }

    /// <summary>
    /// Log authentication and authorization events
    /// </summary>
    public async Task LogSecurityEventAsync(string eventType, string description, bool success, 
        string? ipAddress = null, string? userAgent = null, Guid? userId = null)
    {
        try
        {
            var currentUser = _currentUserService.CurrentUser;
            var effectiveUserId = userId ?? currentUser?.Id;
            var username = currentUser?.Username ?? "Anonymous";

            var auditLog = new AuditLog
            {
                UserId = effectiveUserId,
                Username = username,
                Action = success ? AuditAction.Login : AuditAction.LoginFailed,
                Description = $"Security Event: {eventType} - {description}",
                EntityType = "Security",
                EntityId = effectiveUserId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow,
                DeviceId = Guid.NewGuid(), // Should be set from context
                SyncStatus = SyncStatus.NotSynced
            };

            await _auditLogRepository.AddAsync(auditLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging security event: {EventType}", eventType);
            throw;
        }
    }

    /// <summary>
    /// Log business rule violations
    /// </summary>
    public async Task LogBusinessRuleViolationAsync(string ruleName, string description, 
        object? context = null, Guid? userId = null)
    {
        var enhancedDescription = $"Business rule violation: {ruleName} - {description}";
        var contextValues = context != null ? new { Rule = ruleName, Context = context } : null;

        await LogAuditEventAsync(AuditAction.ValidationFailed, "BusinessRule", null, enhancedDescription, null, contextValues, userId);
    }

    /// <summary>
    /// Get audit trail for specific entity
    /// </summary>
    public async Task<List<AuditLog>> GetAuditTrailAsync(string entityType, Guid entityId, 
        DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            return await _auditLogRepository.GetByEntityAsync(entityType, entityId, fromDate, toDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit trail for {EntityType} {EntityId}", entityType, entityId);
            throw;
        }
    }

    /// <summary>
    /// Get audit logs by user
    /// </summary>
    public async Task<List<AuditLog>> GetAuditLogsByUserAsync(Guid userId, 
        DateTime? fromDate = null, DateTime? toDate = null, int maxResults = 100)
    {
        try
        {
            return await _auditLogRepository.GetByUserAsync(userId, fromDate, toDate, maxResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit logs for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Get security events
    /// </summary>
    public async Task<List<AuditLog>> GetSecurityEventsAsync(DateTime? fromDate = null, DateTime? toDate = null, 
        bool? successOnly = null, int maxResults = 100)
    {
        try
        {
            var actions = new List<AuditAction> { AuditAction.Login, AuditAction.LoginFailed, AuditAction.Logout };
            if (successOnly == true)
            {
                actions = new List<AuditAction> { AuditAction.Login, AuditAction.Logout };
            }
            else if (successOnly == false)
            {
                actions = new List<AuditAction> { AuditAction.LoginFailed };
            }

            return await _auditLogRepository.GetByActionsAsync(actions, fromDate, toDate, maxResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting security events");
            throw;
        }
    }

    /// <summary>
    /// Get audit statistics
    /// </summary>
    public async Task<AuditStatistics> GetAuditStatisticsAsync(DateTime fromDate, DateTime toDate)
    {
        try
        {
            return await _auditLogRepository.GetStatisticsAsync(fromDate, toDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit statistics");
            throw;
        }
    }

    /// <summary>
    /// Core audit logging method
    /// </summary>
    private async Task LogAuditEventAsync(AuditAction action, string entityType, Guid? entityId, 
        string description, object? oldValues = null, object? newValues = null, Guid? userId = null)
    {
        try
        {
            var currentUser = _currentUserService.CurrentUser;
            var effectiveUserId = userId ?? currentUser?.Id;
            var username = currentUser?.Username ?? "System";

            var auditLog = new AuditLog
            {
                UserId = effectiveUserId,
                Username = username,
                Action = action,
                Description = description,
                EntityType = entityType,
                EntityId = entityId,
                OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
                NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
                CreatedAt = DateTime.UtcNow,
                DeviceId = Guid.NewGuid(), // Should be set from context
                SyncStatus = SyncStatus.NotSynced
            };

            await _auditLogRepository.AddAsync(auditLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging audit event: {Action} on {EntityType}", action, entityType);
            // Don't throw - audit logging should not break business operations
        }
    }
}