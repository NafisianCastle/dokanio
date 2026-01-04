using Shared.Core.Entities;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository interface for Business entity operations
/// </summary>
public interface IBusinessRepository : IRepository<Business>
{
    /// <summary>
    /// Gets all businesses owned by a specific user
    /// </summary>
    /// <param name="ownerId">Owner user ID</param>
    /// <returns>Collection of businesses owned by the user</returns>
    Task<IEnumerable<Business>> GetBusinessesByOwnerAsync(Guid ownerId);
    
    /// <summary>
    /// Gets a business by ID including its shops
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <returns>Business with shops if found, null otherwise</returns>
    Task<Business?> GetBusinessWithShopsAsync(Guid businessId);
    
    /// <summary>
    /// Gets businesses by type
    /// </summary>
    /// <param name="businessType">Business type to filter by</param>
    /// <returns>Collection of businesses of the specified type</returns>
    Task<IEnumerable<Business>> GetBusinessesByTypeAsync(Enums.BusinessType businessType);
    
    /// <summary>
    /// Checks if a business name is unique for an owner
    /// </summary>
    /// <param name="name">Business name</param>
    /// <param name="ownerId">Owner ID</param>
    /// <param name="excludeBusinessId">Business ID to exclude from check (for updates)</param>
    /// <returns>True if name is unique, false otherwise</returns>
    Task<bool> IsBusinessNameUniqueAsync(string name, Guid ownerId, Guid? excludeBusinessId = null);
}