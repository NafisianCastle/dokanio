using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// Comprehensive audit logging service with enhanced security features
/// </summary>
public class ComprehensiveAuditService : IComprehensiveAuditService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<ComprehensiveAuditService> _logger;
    private readonly Dictionary<AuditAction, SecurityEventCategory> _actionCategoryMap;

    public ComprehensiveAuditService(
        IAuditLogRepository auditLogRepository,
        IUserRepository userRepository,
        IEncryptionService encryptionService,
        ILogger<ComprehensiveAuditService> logger)
    {
        _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _actionCategoryMap = InitializeActionCategoryMap();
    }

    /// <summary>
    /// Logs a comprehensive audit event with enhanced security context
    /// </summary>
    public async Task<AuditLog> LogSecurityEventAsync(
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
        Dictionary<string, object>? additionalContext = null)
    {
        try
        {
            string? username = null;
            if (userId.HasValue)
            {
                var user = await _userRepository.GetByIdAsync(userId.Value);
                username = user?.Username;
            }

            // Serialize and encrypt sensitive data
            var oldValuesJson = oldValues != null ? JsonSerializer.Serialize(oldValues) : null;
            var newValuesJson = newValues != null ? JsonSerializer.Serialize(newValues) : null;
            var contextJson = additionalContext != null ? JsonSerializer.Serialize(additionalContext) : null;

            // Encrypt sensitive values if they contain PII or sensitive data
            if (ShouldEncryptAuditData(action, entityType))
            {
                if (!string.IsNullOrEmpty(oldValuesJson))
                    oldValuesJson = _encryptionService.Encrypt(oldValuesJson);
                if (!string.IsNullOrEmpty(newValuesJson))
                    newValuesJson = _encryptionService.Encrypt(newValuesJson);
                if (!string.IsNullOrEmpty(contextJson))
                    contextJson = _encryptionService.Encrypt(contextJson);
            }

            var auditLog = new AuditLog
            {
                UserId = userId,
                Username = username,
                Action = action,
                Description = description,
                EntityType = entityType,
                EntityId = entityId,
                OldValues = oldValuesJson,
                NewValues = newValuesJson,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow,
                DeviceId = Guid.NewGuid(), // In real implementation, this would come from context
                SyncStatus = SyncStatus.NotSynced
            };

            // Add business context if available
            if (businessId.HasValue)
            {
                auditLog.Description += $" [BusinessId: {businessId.Value}]";
            }

            // Add additional context
            if (!string.IsNullOrEmpty(contextJson))
            {
                auditLog.Description += $" [Context: {contextJson}]";
            }

            await _auditLogRepository.AddAsync(auditLog);
            await _auditLogRepository.SaveChangesAsync();

            // Log to system logger for immediate visibility
            var logLevel = GetLogLevelForAction(action);
            _logger.Log(logLevel, "Audit Event: {Action} by {User} - {Description}", 
                action, username ?? "System", description);

            return auditLog;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit event: {Action} - {Description}", action, description);
            throw;
        }
    }

    /// <summary>
    /// Logs business-critical operations with enhanced tracking
    /// </summary>
    public async Task<AuditLog> LogBusinessCriticalOperationAsync(
        Guid userId,
        string operationName,
        string description,
        Guid businessId,
        object? operationData = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        var additionalContext = new Dictionary<string, object>
        {
            ["OperationName"] = operationName,
            ["BusinessId"] = businessId,
            ["Timestamp"] = DateTime.UtcNow,
            ["IsCritical"] = true
        };

        if (operationData != null)
        {
            additionalContext["OperationData"] = operationData;
        }

        return await LogSecurityEventAsync(
            userId,
            AuditAction.SystemConfiguration, // Use appropriate action based on operation
            $"Critical Operation: {operationName} - {description}",
            "BusinessOperation",
            businessId,
            null,
            operationData,
            ipAddress,
            userAgent,
            businessId,
            additionalContext);
    }

    /// <summary>
    /// Logs data access events for compliance tracking
    /// </summary>
    public async Task<AuditLog> LogDataAccessAsync(
        Guid userId,
        string dataType,
        Guid? entityId,
        DataAccessType accessType,
        Guid businessId,
        string? reason = null,
        string? ipAddress = null)
    {
        var additionalContext = new Dictionary<string, object>
        {
            ["DataType"] = dataType,
            ["AccessType"] = accessType.ToString(),
            ["BusinessId"] = businessId,
            ["AccessReason"] = reason ?? "Not specified"
        };

        var description = $"Data Access: {accessType} on {dataType}";
        if (!string.IsNullOrEmpty(reason))
        {
            description += $" - Reason: {reason}";
        }

        return await LogSecurityEventAsync(
            userId,
            AuditAction.DataAccess,
            description,
            dataType,
            entityId,
            null,
            null,
            ipAddress,
            null,
            businessId,
            additionalContext);
    }

    /// <summary>
    /// Logs security violations with detailed context
    /// </summary>
    public async Task<AuditLog> LogSecurityViolationAsync(
        Guid? userId,
        string violationType,
        string description,
        ThreatSeverity severity,
        Guid? businessId = null,
        string? ipAddress = null,
        Dictionary<string, object>? evidence = null)
    {
        var additionalContext = new Dictionary<string, object>
        {
            ["ViolationType"] = violationType,
            ["Severity"] = severity.ToString(),
            ["DetectedAt"] = DateTime.UtcNow
        };

        if (evidence != null)
        {
            additionalContext["Evidence"] = evidence;
        }

        if (businessId.HasValue)
        {
            additionalContext["BusinessId"] = businessId.Value;
        }

        var enhancedDescription = $"SECURITY VIOLATION [{severity}]: {violationType} - {description}";

        return await LogSecurityEventAsync(
            userId,
            AuditAction.SecurityViolation,
            enhancedDescription,
            "SecurityViolation",
            null,
            null,
            evidence,
            ipAddress,
            null,
            businessId,
            additionalContext);
    }

    /// <summary>
    /// Generates comprehensive audit report for compliance
    /// </summary>
    public async Task<AuditReport> GenerateAuditReportAsync(
        Guid businessId,
        DateTime fromDate,
        DateTime toDate,
        List<AuditAction>? specificActions = null,
        bool includeSystemEvents = true)
    {
        try
        {
            _logger.LogInformation("Generating audit report for business {BusinessId} from {From} to {To}",
                businessId, fromDate, toDate);

            var allLogs = await _auditLogRepository.GetByDateRangeAsync(fromDate, toDate);
            
            // Filter for business-related logs
            var businessLogs = allLogs.Where(log => 
                log.Description.Contains(businessId.ToString()) ||
                IsBusinessRelatedEntity(log.EntityType)).ToList();

            // Filter by specific actions if provided
            if (specificActions != null && specificActions.Any())
            {
                businessLogs = businessLogs.Where(log => specificActions.Contains(log.Action)).ToList();
            }

            // Filter out system events if not requested
            if (!includeSystemEvents)
            {
                businessLogs = businessLogs.Where(log => log.UserId.HasValue).ToList();
            }

            // Categorize events
            var eventsByCategory = businessLogs
                .GroupBy(log => _actionCategoryMap.GetValueOrDefault(log.Action, SecurityEventCategory.SystemAccess))
                .ToDictionary(g => g.Key, g => g.ToList());

            // Calculate statistics
            var userActivity = businessLogs
                .Where(log => log.UserId.HasValue)
                .GroupBy(log => log.UserId.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            var securityViolations = businessLogs
                .Where(log => log.Action == AuditAction.SecurityViolation)
                .ToList();

            var criticalEvents = businessLogs
                .Where(log => IsCriticalEvent(log.Action))
                .ToList();

            var report = new AuditReport
            {
                BusinessId = businessId,
                ReportPeriod = new DateRange { StartDate = fromDate, EndDate = toDate },
                TotalEvents = businessLogs.Count,
                EventsByCategory = eventsByCategory.ToDictionary(
                    kvp => kvp.Key.ToString(), 
                    kvp => kvp.Value.Count),
                UserActivitySummary = userActivity,
                SecurityViolations = securityViolations.Count,
                CriticalEvents = criticalEvents.Count,
                MostActiveUsers = userActivity
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(10)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                RecentSecurityEvents = securityViolations
                    .OrderByDescending(log => log.CreatedAt)
                    .Take(20)
                    .ToList(),
                ComplianceMetrics = CalculateComplianceMetrics(businessLogs),
                GeneratedAt = DateTime.UtcNow,
                GeneratedBy = "System" // In real implementation, this would be the requesting user
            };

            _logger.LogInformation("Audit report generated for business {BusinessId}. Total events: {EventCount}",
                businessId, businessLogs.Count);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating audit report for business {BusinessId}", businessId);
            throw;
        }
    }

    /// <summary>
    /// Archives old audit logs based on retention policy
    /// </summary>
    public async Task<AuditArchiveResult> ArchiveOldLogsAsync(TimeSpan retentionPeriod, bool deleteAfterArchive = false)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.Subtract(retentionPeriod);
            _logger.LogInformation("Starting audit log archival for logs older than {CutoffDate}", cutoffDate);

            var oldLogs = await _auditLogRepository.GetByDateRangeAsync(DateTime.MinValue, cutoffDate);
            var logsToArchive = oldLogs.ToList();

            if (!logsToArchive.Any())
            {
                return new AuditArchiveResult
                {
                    Success = true,
                    ArchivedCount = 0,
                    DeletedCount = 0,
                    Message = "No logs found for archival"
                };
            }

            // In a real implementation, this would archive to long-term storage
            // For now, we'll just mark them as archived
            var archiveCount = logsToArchive.Count;
            var deleteCount = 0;

            if (deleteAfterArchive)
            {
                // Delete the old logs after archiving
                foreach (var log in logsToArchive)
                {
                    await _auditLogRepository.DeleteAsync(log.Id);
                }
                await _auditLogRepository.SaveChangesAsync();
                deleteCount = archiveCount;
            }

            var result = new AuditArchiveResult
            {
                Success = true,
                ArchivedCount = archiveCount,
                DeletedCount = deleteCount,
                ArchiveDate = DateTime.UtcNow,
                RetentionPeriod = retentionPeriod,
                Message = $"Successfully archived {archiveCount} audit logs"
            };

            _logger.LogInformation("Audit log archival completed. Archived: {ArchivedCount}, Deleted: {DeletedCount}",
                archiveCount, deleteCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during audit log archival");
            return new AuditArchiveResult
            {
                Success = false,
                Message = $"Archival failed: {ex.Message}"
            };
        }
    }

    private Dictionary<AuditAction, SecurityEventCategory> InitializeActionCategoryMap()
    {
        return new Dictionary<AuditAction, SecurityEventCategory>
        {
            [AuditAction.Login] = SecurityEventCategory.Authentication,
            [AuditAction.Logout] = SecurityEventCategory.Authentication,
            [AuditAction.PasswordChange] = SecurityEventCategory.Authentication,
            [AuditAction.AccountLocked] = SecurityEventCategory.Authentication,
            [AuditAction.AccountUnlocked] = SecurityEventCategory.Authentication,
            [AuditAction.ChangeUserRole] = SecurityEventCategory.Authorization,
            [AuditAction.DataAccess] = SecurityEventCategory.DataAccess,
            [AuditAction.DataExport] = SecurityEventCategory.DataAccess,
            [AuditAction.DataImport] = SecurityEventCategory.DataAccess,
            [AuditAction.CreateProduct] = SecurityEventCategory.DataModification,
            [AuditAction.UpdateProduct] = SecurityEventCategory.DataModification,
            [AuditAction.DeleteProduct] = SecurityEventCategory.DataModification,
            [AuditAction.CreateSale] = SecurityEventCategory.DataModification,
            [AuditAction.RefundSale] = SecurityEventCategory.DataModification,
            [AuditAction.UpdateInventory] = SecurityEventCategory.DataModification,
            [AuditAction.SystemConfiguration] = SecurityEventCategory.ConfigurationChange,
            [AuditAction.SystemMaintenance] = SecurityEventCategory.SystemAccess,
            [AuditAction.SecurityViolation] = SecurityEventCategory.SecurityViolation,
            [AuditAction.SecurityAlert] = SecurityEventCategory.SecurityViolation,
            [AuditAction.ThreatDetection] = SecurityEventCategory.SecurityViolation,
            [AuditAction.DataEncryption] = SecurityEventCategory.DataAccess,
            [AuditAction.DataDecryption] = SecurityEventCategory.DataAccess
        };
    }

    private bool ShouldEncryptAuditData(AuditAction action, string? entityType)
    {
        // Encrypt audit data for sensitive operations
        var sensitiveActions = new[]
        {
            AuditAction.DataEncryption,
            AuditAction.DataDecryption,
            AuditAction.PasswordChange,
            AuditAction.SecurityViolation
        };

        var sensitiveEntities = new[]
        {
            nameof(User),
            "Customer",
            "Payment",
            "CreditCard"
        };

        return sensitiveActions.Contains(action) || 
               (entityType != null && sensitiveEntities.Contains(entityType));
    }

    private Microsoft.Extensions.Logging.LogLevel GetLogLevelForAction(AuditAction action)
    {
        return action switch
        {
            AuditAction.SecurityViolation => Microsoft.Extensions.Logging.LogLevel.Warning,
            AuditAction.SecurityAlert => Microsoft.Extensions.Logging.LogLevel.Warning,
            AuditAction.ThreatDetection => Microsoft.Extensions.Logging.LogLevel.Warning,
            AuditAction.AccountLocked => Microsoft.Extensions.Logging.LogLevel.Warning,
            AuditAction.Login => Microsoft.Extensions.Logging.LogLevel.Information,
            AuditAction.Logout => Microsoft.Extensions.Logging.LogLevel.Information,
            _ => Microsoft.Extensions.Logging.LogLevel.Debug
        };
    }

    private bool IsBusinessRelatedEntity(string? entityType)
    {
        if (string.IsNullOrEmpty(entityType)) return false;

        var businessEntities = new[]
        {
            nameof(Business),
            nameof(Shop),
            nameof(Product),
            nameof(Sale),
            nameof(User),
            "Stock",
            "Customer"
        };

        return businessEntities.Contains(entityType);
    }

    private bool IsCriticalEvent(AuditAction action)
    {
        var criticalActions = new[]
        {
            AuditAction.SecurityViolation,
            AuditAction.SecurityAlert,
            AuditAction.ThreatDetection,
            AuditAction.ChangeUserRole,
            AuditAction.SystemConfiguration,
            AuditAction.DataExport,
            AuditAction.AccountLocked
        };

        return criticalActions.Contains(action);
    }

    private ComplianceMetrics CalculateComplianceMetrics(List<AuditLog> logs)
    {
        var totalEvents = logs.Count;
        var securityEvents = logs.Count(log => 
            log.Action == AuditAction.SecurityViolation ||
            log.Action == AuditAction.SecurityAlert ||
            log.Action == AuditAction.ThreatDetection);

        var encryptionEvents = logs.Count(log => 
            log.Action == AuditAction.DataEncryption ||
            log.Action == AuditAction.DataDecryption);

        var authEvents = logs.Count(log => 
            log.Action == AuditAction.Login ||
            log.Action == AuditAction.Logout ||
            log.Action == AuditAction.PasswordChange);

        return new ComplianceMetrics
        {
            TotalAuditEvents = totalEvents,
            SecurityEvents = securityEvents,
            EncryptionEvents = encryptionEvents,
            AuthenticationEvents = authEvents,
            DataAccessEvents = logs.Count(log => log.Action == AuditAction.DataAccess),
            ConfigurationChanges = logs.Count(log => log.Action == AuditAction.SystemConfiguration),
            ComplianceScore = totalEvents > 0 ? (int)((double)(totalEvents - securityEvents) / totalEvents * 100) : 100
        };
    }
}

/// <summary>
/// Data access types for audit logging
/// </summary>
public enum DataAccessType
{
    Read,
    Create,
    Update,
    Delete,
    Export,
    Import
}