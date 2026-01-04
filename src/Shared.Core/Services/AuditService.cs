using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of audit logging service
/// </summary>
public class AuditService : IAuditService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUserRepository _userRepository;

    public AuditService(IAuditLogRepository auditLogRepository, IUserRepository userRepository)
    {
        _auditLogRepository = auditLogRepository;
        _userRepository = userRepository;
    }

    public async Task<AuditLog> LogAsync(
        Guid? userId,
        AuditAction action,
        string description,
        string? entityType = null,
        Guid? entityId = null,
        string? oldValues = null,
        string? newValues = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        string? username = null;
        if (userId.HasValue)
        {
            var user = await _userRepository.GetByIdAsync(userId.Value);
            username = user?.Username;
        }

        var auditLog = new AuditLog
        {
            UserId = userId,
            Username = username,
            Action = action,
            Description = description,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow
        };

        await _auditLogRepository.AddAsync(auditLog);
        await _auditLogRepository.SaveChangesAsync();

        return auditLog;
    }

    public async Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(Guid userId, DateTime? from = null, DateTime? to = null)
    {
        return await _auditLogRepository.GetByUserIdAsync(userId, from, to);
    }

    public async Task<IEnumerable<AuditLog>> GetActionAuditLogsAsync(AuditAction action, DateTime? from = null, DateTime? to = null)
    {
        return await _auditLogRepository.GetByActionAsync(action, from, to);
    }

    public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync(DateTime? from = null, DateTime? to = null)
    {
        return await _auditLogRepository.GetByDateRangeAsync(from, to);
    }

    public async Task<IEnumerable<AuditLog>> GetSecurityViolationsAsync(DateTime? from = null, DateTime? to = null)
    {
        return await _auditLogRepository.GetSecurityViolationsAsync(from, to);
    }
}