using Shared.Core.Entities;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository interface for customer preference operations
/// </summary>
public interface ICustomerPreferenceRepository : IRepository<CustomerPreference>
{
    /// <summary>
    /// Get preferences by customer ID
    /// </summary>
    Task<List<CustomerPreference>> GetByCustomerIdAsync(Guid customerId);
    
    /// <summary>
    /// Get preference by customer ID and key
    /// </summary>
    Task<CustomerPreference?> GetByCustomerIdAndKeyAsync(Guid customerId, string key);
    
    /// <summary>
    /// Get preferences by category
    /// </summary>
    Task<List<CustomerPreference>> GetByCategoryAsync(string category);
    
    /// <summary>
    /// Get preferences by customer ID and category
    /// </summary>
    Task<List<CustomerPreference>> GetByCustomerIdAndCategoryAsync(Guid customerId, string category);
    
    /// <summary>
    /// Set or update preference value
    /// </summary>
    Task<bool> SetPreferenceAsync(Guid customerId, string key, string value, string category = "");
    
    /// <summary>
    /// Remove preference by key
    /// </summary>
    Task<bool> RemovePreferenceAsync(Guid customerId, string key);
    
    /// <summary>
    /// Get customer preferences as dictionary
    /// </summary>
    Task<Dictionary<string, string>> GetPreferencesDictionaryAsync(Guid customerId, string? category = null);
}