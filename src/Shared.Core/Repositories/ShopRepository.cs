using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository implementation for Shop entity operations
/// </summary>
public class ShopRepository : Repository<Shop>, IShopRepository
{
    public ShopRepository(PosDbContext context, ILogger<ShopRepository> logger) : base(context, logger)
    {
    }

    /// <summary>
    /// Gets all shops belonging to a specific business
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <returns>Collection of shops belonging to the business</returns>
    public async Task<IEnumerable<Shop>> GetShopsByBusinessAsync(Guid businessId)
    {
        return await _context.Set<Shop>()
            .Where(s => s.BusinessId == businessId && !s.IsDeleted)
            .Include(s => s.Business)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a shop by ID including its business information
    /// </summary>
    /// <param name="shopId">Shop ID</param>
    /// <returns>Shop with business information if found, null otherwise</returns>
    public async Task<Shop?> GetShopWithBusinessAsync(Guid shopId)
    {
        return await _context.Set<Shop>()
            .Where(s => s.Id == shopId && !s.IsDeleted)
            .Include(s => s.Business)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Gets shops by business and user access
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="userId">User ID for access validation</param>
    /// <returns>Collection of shops the user has access to</returns>
    public async Task<IEnumerable<Shop>> GetShopsByBusinessAndUserAsync(Guid businessId, Guid userId)
    {
        // Get user to check their role and shop assignment
        var user = await _context.Set<User>()
            .Where(u => u.Id == userId && !u.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null)
            return Enumerable.Empty<Shop>();

        var query = _context.Set<Shop>()
            .Where(s => s.BusinessId == businessId && !s.IsDeleted);

        // Business owners and shop managers can see all shops in their business
        if (user.Role == UserRole.BusinessOwner)
        {
            // Business owners can see all shops in any of their businesses
            query = query.Where(s => s.Business.OwnerId == userId);
        }
        else if (user.Role == UserRole.ShopManager && user.ShopId.HasValue)
        {
            // Shop managers can only see their assigned shop
            query = query.Where(s => s.Id == user.ShopId.Value);
        }
        else if (user.Role == UserRole.Cashier || user.Role == UserRole.InventoryStaff)
        {
            // Cashiers and inventory staff can only see their assigned shop
            if (user.ShopId.HasValue)
            {
                query = query.Where(s => s.Id == user.ShopId.Value);
            }
            else
            {
                return Enumerable.Empty<Shop>();
            }
        }

        return await query
            .Include(s => s.Business)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Checks if a shop name is unique within a business
    /// </summary>
    /// <param name="name">Shop name</param>
    /// <param name="businessId">Business ID</param>
    /// <param name="excludeShopId">Shop ID to exclude from check (for updates)</param>
    /// <returns>True if name is unique, false otherwise</returns>
    public async Task<bool> IsShopNameUniqueAsync(string name, Guid businessId, Guid? excludeShopId = null)
    {
        var query = _context.Set<Shop>()
            .Where(s => s.Name.ToLower() == name.ToLower() && 
                       s.BusinessId == businessId && 
                       !s.IsDeleted);

        if (excludeShopId.HasValue)
        {
            query = query.Where(s => s.Id != excludeShopId.Value);
        }

        return !await query.AnyAsync();
    }

    /// <summary>
    /// Gets shops with their inventory counts
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <returns>Collection of shops with inventory information</returns>
    public async Task<IEnumerable<Shop>> GetShopsWithInventoryAsync(Guid businessId)
    {
        return await _context.Set<Shop>()
            .Where(s => s.BusinessId == businessId && !s.IsDeleted)
            .Include(s => s.Business)
            .Include(s => s.Inventory.Where(i => !i.IsDeleted))
            .OrderBy(s => s.Name)
            .ToListAsync();
    }
}