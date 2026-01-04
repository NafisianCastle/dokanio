using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository implementation for SaleItem entities
/// </summary>
public class SaleItemRepository : Repository<SaleItem>, ISaleItemRepository
{
    public SaleItemRepository(PosDbContext context, ILogger<Repository<SaleItem>> logger) : base(context, logger)
    {
    }

    /// <summary>
    /// Gets all sale items for a specific sale
    /// </summary>
    /// <param name="saleId">Sale identifier</param>
    /// <returns>Collection of sale items for the sale</returns>
    public async Task<IEnumerable<SaleItem>> GetBySaleIdAsync(Guid saleId)
    {
        return await _context.SaleItems
            .Where(si => si.SaleId == saleId)
            .Include(si => si.Product)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all sale items for a specific product
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <returns>Collection of sale items for the product</returns>
    public async Task<IEnumerable<SaleItem>> GetByProductIdAsync(Guid productId)
    {
        return await _context.SaleItems
            .Where(si => si.ProductId == productId)
            .Include(si => si.Product)
            .ToListAsync();
    }

    /// <summary>
    /// Gets sale items within a date range
    /// </summary>
    /// <param name="from">Start date (inclusive)</param>
    /// <param name="to">End date (inclusive)</param>
    /// <returns>Collection of sale items within the date range</returns>
    public async Task<IEnumerable<SaleItem>> GetByDateRangeAsync(DateTime from, DateTime to)
    {
        return await _context.SaleItems
            .Include(si => si.Sale)
            .Include(si => si.Product)
            .Where(si => si.Sale.CreatedAt >= from && si.Sale.CreatedAt <= to)
            .ToListAsync();
    }

    /// <summary>
    /// Gets the total quantity sold for a specific product within a date range
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="from">Start date (inclusive)</param>
    /// <param name="to">End date (inclusive)</param>
    /// <returns>Total quantity sold</returns>
    public async Task<int> GetTotalQuantitySoldAsync(Guid productId, DateTime from, DateTime to)
    {
        return await _context.SaleItems
            .Include(si => si.Sale)
            .Where(si => si.ProductId == productId && 
                        si.Sale.CreatedAt >= from && 
                        si.Sale.CreatedAt <= to)
            .SumAsync(si => si.Quantity);
    }

    /// <summary>
    /// Gets sale items for a specific product batch
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="batchNumber">Batch number</param>
    /// <returns>Collection of sale items for the product batch</returns>
    public async Task<IEnumerable<SaleItem>> GetByProductBatchAsync(Guid productId, string batchNumber)
    {
        return await _context.SaleItems
            .Where(si => si.ProductId == productId && si.BatchNumber == batchNumber)
            .Include(si => si.Product)
            .ToListAsync();
    }
}