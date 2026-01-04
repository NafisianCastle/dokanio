using Microsoft.EntityFrameworkCore;
using Shared.Core.Data;
using Shared.Core.Entities;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository implementation for UserSession entity
/// </summary>
public class UserSessionRepository : IUserSessionRepository
{
    protected readonly PosDbContext _context;

    public UserSessionRepository(PosDbContext context)
    {
        _context = context;
    }

    public async Task<UserSession?> GetByIdAsync(Guid id)
    {
        return await _context.Set<UserSession>().FindAsync(id);
    }

    public async Task<IEnumerable<UserSession>> GetAllAsync()
    {
        return await _context.Set<UserSession>().ToListAsync();
    }

    public async Task<IEnumerable<UserSession>> FindAsync(System.Linq.Expressions.Expression<Func<UserSession, bool>> predicate)
    {
        return await _context.Set<UserSession>().Where(predicate).ToListAsync();
    }

    public async Task AddAsync(UserSession entity)
    {
        await _context.Set<UserSession>().AddAsync(entity);
    }

    public async Task UpdateAsync(UserSession entity)
    {
        _context.Set<UserSession>().Update(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _context.Set<UserSession>().Remove(entity);
        }
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task<UserSession?> GetByTokenAsync(string sessionToken)
    {
        return await _context.Set<UserSession>()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.SessionToken == sessionToken);
    }

    public async Task<IEnumerable<UserSession>> GetActiveSessionsByUserIdAsync(Guid userId)
    {
        return await _context.Set<UserSession>()
            .Where(s => s.UserId == userId && s.IsActive && s.EndedAt == null)
            .OrderByDescending(s => s.LastActivityAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserSession>> GetExpiredSessionsAsync(int inactivityTimeoutMinutes)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-inactivityTimeoutMinutes);
        
        return await _context.Set<UserSession>()
            .Where(s => s.IsActive && s.EndedAt == null && s.LastActivityAt < cutoffTime)
            .ToListAsync();
    }

    public async Task<int> EndAllUserSessionsAsync(Guid userId)
    {
        var sessions = await _context.Set<UserSession>()
            .Where(s => s.UserId == userId && s.IsActive && s.EndedAt == null)
            .ToListAsync();

        var endTime = DateTime.UtcNow;
        foreach (var session in sessions)
        {
            session.IsActive = false;
            session.EndedAt = endTime;
        }

        await _context.SaveChangesAsync();
        return sessions.Count;
    }

    public async Task<int> EndExpiredSessionsAsync(int inactivityTimeoutMinutes)
    {
        var expiredSessions = await GetExpiredSessionsAsync(inactivityTimeoutMinutes);
        var sessionsList = expiredSessions.ToList();

        var endTime = DateTime.UtcNow;
        foreach (var session in sessionsList)
        {
            session.IsActive = false;
            session.EndedAt = endTime;
        }

        await _context.SaveChangesAsync();
        return sessionsList.Count;
    }
}