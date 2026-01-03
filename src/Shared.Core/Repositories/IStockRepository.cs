using Shared.Core.Entities;

namespace Shared.Core.Repositories;

/// <summary>
/// Specialized repository interface for Stock entities
/// </summary>
public interface IStockRepository : IRepository<Stock>
{
    /// <summary>
    /// Gets stock information for a specific product
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <returns>Stock information for the product, null if not found</returns>
    Task<Stock?> GetByProductIdAsync(Guid productId);
    
    /// <summary>
    /// Gets all products with low stock (below specified threshold)
    /// </summary>
    /// <param name="threshold">Stock quantity threshold</param>
    /// <returns>Collection of stock entries below the threshold</returns>
    Task<IEnumerable<Stock>> GetLowStockAsync(int threshold = 10);
    
    /// <summary>
    /// Gets all stock entries that need to be synced to the server
    /// </summary>
    /// <returns>Collection of unsynced stock entries</returns>
    Task<IEnumerable<Stock>> GetUnsyncedAsync();
    
    /// <summary>
    /// Updates stock quantity for a specific product
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="quantityChange">Quantity change (positive for increase, negative for decrease)</param>
    /// <param name="deviceId">Device making the change</param>
    Task UpdateStockQuantityAsync(Guid productId, int quantityChange, Guid deviceId);
    
    /// <summary>
    /// Gets current stock quantity for a product
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <returns>Current stock quantity, 0 if product not found</returns>
    Task<int> GetStockQuantityAsync(Guid productId);
    
    /// <summary>
    /// Gets all stock entries for products in a specific category
    /// </summary>
    /// <param name="category">Product category</param>
    /// <returns>Collection of stock entries for products in the category</returns>
    Task<IEnumerable<Stock>> GetStockByCategoryAsync(string category);
    
    /// <summary>
    /// Checks if a product has sufficient stock for a sale
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="requiredQuantity">Required quantity</param>
    /// <returns>True if sufficient stock is available, false otherwise</returns>
    Task<bool> HasSufficientStockAsync(Guid productId, int requiredQuantity);
}