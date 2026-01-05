using Shared.Core.Entities;

namespace Shared.Core.Services;

/// <summary>
/// Interface for database query optimization service
/// </summary>
public interface IDatabaseQueryOptimizationService
{
    /// <summary>
    /// Gets businesses with optimized query including proper indexing
    /// </summary>
    Task<IEnumerable<Business>> GetBusinessesOptimizedAsync(Guid ownerId);

    /// <summary>
    /// Gets shops with optimized query and pagination
    /// </summary>
    Task<IEnumerable<Shop>> GetShopsOptimizedAsync(Guid businessId, int page = 0, int pageSize = 20);

    /// <summary>
    /// Gets products with optimized query and filtering
    /// </summary>
    Task<IEnumerable<Product>> GetProductsOptimizedAsync(Guid shopId, string? category = null, bool activeOnly = true);

    /// <summary>
    /// Gets sales with optimized query and date range filtering
    /// </summary>
    Task<IEnumerable<Sale>> GetSalesOptimizedAsync(Guid shopId, DateTime? fromDate = null, DateTime? toDate = null, int page = 0, int pageSize = 50);

    /// <summary>
    /// Gets aggregated sales data with optimized query
    /// </summary>
    Task<SalesAggregateData> GetSalesAggregateOptimizedAsync(Guid shopId, DateTime fromDate, DateTime toDate);

    /// <summary>
    /// Gets inventory levels with optimized query
    /// </summary>
    Task<IEnumerable<InventoryLevel>> GetInventoryLevelsOptimizedAsync(Guid shopId);

    /// <summary>
    /// Optimizes database indexes for better query performance
    /// </summary>
    Task OptimizeDatabaseIndexesAsync();

    /// <summary>
    /// Clears query cache to free memory
    /// </summary>
    void ClearQueryCache();
}