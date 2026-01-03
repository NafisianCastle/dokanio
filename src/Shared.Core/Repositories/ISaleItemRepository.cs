using Shared.Core.Entities;

namespace Shared.Core.Repositories;

/// <summary>
/// Specialized repository interface for SaleItem entities
/// </summary>
public interface ISaleItemRepository : IRepository<SaleItem>
{
    /// <summary>
    /// Gets all sale items for a specific sale
    /// </summary>
    /// <param name="saleId">Sale identifier</param>
    /// <returns>Collection of sale items for the sale</returns>
    Task<IEnumerable<SaleItem>> GetBySaleIdAsync(Guid saleId);
    
    /// <summary>
    /// Gets all sale items for a specific product
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <returns>Collection of sale items for the product</returns>
    Task<IEnumerable<SaleItem>> GetByProductIdAsync(Guid productId);
    
    /// <summary>
    /// Gets sale items within a date range
    /// </summary>
    /// <param name="from">Start date (inclusive)</param>
    /// <param name="to">End date (inclusive)</param>
    /// <returns>Collection of sale items within the date range</returns>
    Task<IEnumerable<SaleItem>> GetByDateRangeAsync(DateTime from, DateTime to);
    
    /// <summary>
    /// Gets the total quantity sold for a specific product within a date range
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="from">Start date (inclusive)</param>
    /// <param name="to">End date (inclusive)</param>
    /// <returns>Total quantity sold</returns>
    Task<int> GetTotalQuantitySoldAsync(Guid productId, DateTime from, DateTime to);
    
    /// <summary>
    /// Gets sale items for a specific product batch
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="batchNumber">Batch number</param>
    /// <returns>Collection of sale items for the product batch</returns>
    Task<IEnumerable<SaleItem>> GetByProductBatchAsync(Guid productId, string batchNumber);
}