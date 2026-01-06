using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository implementation for AuditLog entity
/// </summary>
public class AuditLogRepository : IAuditLogRepository
{
    protected readonly PosDbContext _context;
    protected readonly ILogger<AuditLogRepository> _logger;

    public AuditLogRepository(PosDbContext context, ILogger<AuditLogRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AuditLog?> GetByIdAsync(Guid id)
    {
        return await _context.Set<AuditLog>().FindAsync(id);
    }

    public async Task<IEnumerable<AuditLog>> GetAllAsync()
    {
        return await _context.Set<AuditLog>().ToListAsync();
    }

    public async Task<IEnumerable<AuditLog>> FindAsync(System.Linq.Expressions.Expression<Func<AuditLog, bool>> predicate)
    {
        return await _context.Set<AuditLog>().Where(predicate).ToListAsync();
    }

    public async Task AddAsync(AuditLog entity)
    {
        await _context.Set<AuditLog>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(AuditLog entity)
    {
        _context.Set<AuditLog>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _context.Set<AuditLog>().Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetByUserIdAsync(Guid userId, DateTime? from = null, DateTime? to = null)
    {
        var query = _context.Set<AuditLog>()
            .Where(a => a.UserId == userId);

        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetByActionAsync(AuditAction action, DateTime? from = null, DateTime? to = null)
    {
        var query = _context.Set<AuditLog>()
            .Where(a => a.Action == action);

        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetByDateRangeAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = _context.Set<AuditLog>().AsQueryable();

        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetSecurityViolationsAsync(DateTime? from = null, DateTime? to = null)
    {
        return await GetByActionAsync(AuditAction.SecurityViolation, from, to);
    }

    public async Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityType, Guid entityId)
    {
        return await _context.Set<AuditLog>()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    // Enhanced methods for comprehensive audit service

    public async Task<List<AuditLog>> GetByEntityAsync(string entityType, Guid entityId, DateTime? fromDate, DateTime? toDate)
    {
        try
        {
            var query = _context.AuditLogs
                .Where(a => a.EntityType == entityType && a.EntityId == entityId);

            if (fromDate.HasValue)
                query = query.Where(a => a.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(a => a.CreatedAt <= toDate.Value);

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit logs for entity {EntityType} {EntityId}", entityType, entityId);
            throw;
        }
    }

    public async Task<List<AuditLog>> GetByUserAsync(Guid userId, DateTime? fromDate, DateTime? toDate, int maxResults)
    {
        try
        {
            var query = _context.AuditLogs
                .Where(a => a.UserId == userId);

            if (fromDate.HasValue)
                query = query.Where(a => a.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(a => a.CreatedAt <= toDate.Value);

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(maxResults)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit logs for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<AuditLog>> GetByActionsAsync(List<AuditAction> actions, DateTime? fromDate, DateTime? toDate, int maxResults)
    {
        try
        {
            var query = _context.AuditLogs
                .Where(a => actions.Contains(a.Action));

            if (fromDate.HasValue)
                query = query.Where(a => a.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(a => a.CreatedAt <= toDate.Value);

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(maxResults)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit logs by actions");
            throw;
        }
    }

    public async Task<AuditStatistics> GetStatisticsAsync(DateTime fromDate, DateTime toDate)
    {
        try
        {
            var logs = await _context.AuditLogs
                .Where(a => a.CreatedAt >= fromDate && a.CreatedAt <= toDate)
                .ToListAsync();

            var statistics = new AuditStatistics
            {
                FromDate = fromDate,
                ToDate = toDate,
                TotalEvents = logs.Count,
                SecurityEvents = logs.Count(l => l.EntityType == "Security"),
                FailedOperations = logs.Count(l => l.Action.ToString().Contains("Failed")),
                DataAccessEvents = logs.Count(l => l.Action == AuditAction.Read),
                EventsByAction = logs.GroupBy(l => l.Action).ToDictionary(g => g.Key, g => g.Count()),
                EventsByEntityType = logs.Where(l => !string.IsNullOrEmpty(l.EntityType))
                                       .GroupBy(l => l.EntityType!)
                                       .ToDictionary(g => g.Key, g => g.Count()),
                TopUsers = logs.Where(l => !string.IsNullOrEmpty(l.Username))
                              .GroupBy(l => l.Username!)
                              .OrderByDescending(g => g.Count())
                              .Take(10)
                              .Select(g => g.Key)
                              .ToList()
            };

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit statistics");
            throw;
        }
    }
}