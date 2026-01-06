using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository implementation for sale session operations
/// </summary>
public class SaleSessionRepository : Repository<SaleSession>, ISaleSessionRepository
{
    public SaleSessionRepository(PosDbContext context, ILogger<Repository<SaleSession>> logger) : base(context, logger)
    {
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SaleSession>> GetActiveSessionsAsync(Guid userId, Guid deviceId)
    {
        return await _context.Set<SaleSession>()
            .Where(s => s.UserId == userId && 
                       s.DeviceId == deviceId && 
                       s.IsActive && 
                       s.State == SessionState.Active)
            .Include(s => s.Customer)
            .Include(s => s.Sale)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SaleSession>> GetActiveSessionsByShopAsync(Guid shopId)
    {
        return await _context.Set<SaleSession>()
            .Where(s => s.ShopId == shopId && 
                       s.IsActive && 
                       s.State == SessionState.Active)
            .Include(s => s.User)
            .Include(s => s.Customer)
            .Include(s => s.Sale)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<SaleSession?> GetSessionByTabNameAsync(string tabName, Guid userId, Guid deviceId)
    {
        return await _context.Set<SaleSession>()
            .Where(s => s.TabName == tabName && 
                       s.UserId == userId && 
                       s.DeviceId == deviceId && 
                       s.IsActive)
            .Include(s => s.Customer)
            .Include(s => s.Sale)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<int> DeactivateAllSessionsAsync(Guid userId, Guid deviceId)
    {
        var sessions = await _context.Set<SaleSession>()
            .Where(s => s.UserId == userId && 
                       s.DeviceId == deviceId && 
                       s.IsActive)
            .ToListAsync();

        foreach (var session in sessions)
        {
            session.IsActive = false;
            session.State = SessionState.Cancelled;
            session.LastModified = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return sessions.Count;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SaleSession>> GetExpiredSessionsAsync(DateTime expiryThreshold)
    {
        return await _context.Set<SaleSession>()
            .Where(s => s.IsActive && 
                       s.State == SessionState.Active && 
                       s.LastModified < expiryThreshold)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<bool> UpdateLastModifiedAsync(Guid sessionId)
    {
        var session = await _context.Set<SaleSession>()
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
            return false;

        session.LastModified = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SaleSession>> GetSessionsByStateAsync(SessionState state, Guid? userId = null, Guid? shopId = null)
    {
        var query = _context.Set<SaleSession>()
            .Where(s => s.State == state);

        if (userId.HasValue)
        {
            query = query.Where(s => s.UserId == userId.Value);
        }

        if (shopId.HasValue)
        {
            query = query.Where(s => s.ShopId == shopId.Value);
        }

        return await query
            .Include(s => s.User)
            .Include(s => s.Customer)
            .Include(s => s.Sale)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();
    }
}