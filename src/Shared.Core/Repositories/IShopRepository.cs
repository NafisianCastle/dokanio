using Shared.Core.Entities;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository interface for Shop entity operations
/// </summary>
public interface IShopRepository : IRepository<Shop>
{
    /// <summary>
    /// Gets all shops belonging to a specific business
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <returns>Collection of shops belonging to the business</returns>
    Task<IEnumerable<Shop>> GetShopsByBusinessAsync(Guid businessId);
    
    /// <summary>
    /// Gets a shop by ID including its business information
    /// </summary>
    /// <param name="shopId">Shop ID</param>
    /// <returns>Shop with business information if found, null otherwise</returns>
    Task<Shop?> GetShopWithBusinessAsync(Guid shopId);
    
    /// <summary>
    /// Gets shops by business and user access
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="userId">User ID for access validation</param>
    /// <returns>Collection of shops the user has access to</returns>
    Task<IEnumerable<Shop>> GetShopsByBusinessAndUserAsync(Guid businessId, Guid userId);
    
    /// <summary>
    /// Checks if a shop name is unique within a business
    /// </summary>
    /// <param name="name">Shop name</param>
    /// <param name="businessId">Business ID</param>
    /// <param name="excludeShopId">Shop ID to exclude from check (for updates)</param>
    /// <returns>True if name is unique, false otherwise</returns>
    Task<bool> IsShopNameUniqueAsync(string name, Guid businessId, Guid? excludeShopId = null);
    
    /// <summary>
    /// Gets shops with their inventory counts
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <returns>Collection of shops with inventory information</returns>
    Task<IEnumerable<Shop>> GetShopsWithInventoryAsync(Guid businessId);
}