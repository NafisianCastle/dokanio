using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository implementation for Business entity operations
/// </summary>
public class BusinessRepository : Repository<Business>, IBusinessRepository
{
    public BusinessRepository(PosDbContext context, ILogger<BusinessRepository> logger) : base(context, logger)
    {
    }

    /// <summary>
    /// Gets all businesses owned by a specific user
    /// </summary>
    /// <param name="ownerId">Owner user ID</param>
    /// <returns>Collection of businesses owned by the user</returns>
    public async Task<IEnumerable<Business>> GetBusinessesByOwnerAsync(Guid ownerId)
    {
        return await _context.Set<Business>()
            .Where(b => b.OwnerId == ownerId && !b.IsDeleted)
            .Include(b => b.Shops.Where(s => !s.IsDeleted))
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a business by ID including its shops
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <returns>Business with shops if found, null otherwise</returns>
    public async Task<Business?> GetBusinessWithShopsAsync(Guid businessId)
    {
        return await _context.Set<Business>()
            .Where(b => b.Id == businessId && !b.IsDeleted)
            .Include(b => b.Shops.Where(s => !s.IsDeleted))
            .Include(b => b.Owner)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Gets businesses by type
    /// </summary>
    /// <param name="businessType">Business type to filter by</param>
    /// <returns>Collection of businesses of the specified type</returns>
    public async Task<IEnumerable<Business>> GetBusinessesByTypeAsync(BusinessType businessType)
    {
        return await _context.Set<Business>()
            .Where(b => b.Type == businessType && !b.IsDeleted)
            .Include(b => b.Owner)
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Checks if a business name is unique for an owner
    /// </summary>
    /// <param name="name">Business name</param>
    /// <param name="ownerId">Owner ID</param>
    /// <param name="excludeBusinessId">Business ID to exclude from check (for updates)</param>
    /// <returns>True if name is unique, false otherwise</returns>
    public async Task<bool> IsBusinessNameUniqueAsync(string name, Guid ownerId, Guid? excludeBusinessId = null)
    {
        var query = _context.Set<Business>()
            .Where(b => b.Name.ToLower() == name.ToLower() && 
                       b.OwnerId == ownerId && 
                       !b.IsDeleted);

        if (excludeBusinessId.HasValue)
        {
            query = query.Where(b => b.Id != excludeBusinessId.Value);
        }

        return !await query.AnyAsync();
    }
}