using Microsoft.EntityFrameworkCore;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository implementation for AuditLog entity
/// </summary>
public class AuditLogRepository : IAuditLogRepository
{
    protected readonly PosDbContext _context;

    public AuditLogRepository(PosDbContext context)
    {
        _context = context;
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
    }

    public async Task UpdateAsync(AuditLog entity)
    {
        _context.Set<AuditLog>().Update(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _context.Set<AuditLog>().Remove(entity);
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
}