using Shared.Core.Entities;

namespace Shared.Core.Repositories;

/// <summary>
/// Specialized repository interface for Sale entities
/// </summary>
public interface ISaleRepository : IRepository<Sale>
{
    /// <summary>
    /// Gets all sales that haven't been synced to the server
    /// </summary>
    /// <returns>Collection of unsynced sales</returns>
    Task<IEnumerable<Sale>> GetUnsyncedAsync();
    
    /// <summary>
    /// Gets the total sales amount for a specific date
    /// </summary>
    /// <param name="date">Date to calculate sales for</param>
    /// <returns>Total sales amount for the date</returns>
    Task<decimal> GetDailySalesAsync(DateTime date);
    
    /// <summary>
    /// Gets the count of sales for a specific date
    /// </summary>
    /// <param name="date">Date to count sales for</param>
    /// <returns>Number of sales for the date</returns>
    Task<int> GetDailySalesCountAsync(DateTime date);
    
    /// <summary>
    /// Gets all sales within a date range
    /// </summary>
    /// <param name="from">Start date (inclusive)</param>
    /// <param name="to">End date (inclusive)</param>
    /// <returns>Collection of sales within the date range</returns>
    Task<IEnumerable<Sale>> GetSalesByDateRangeAsync(DateTime from, DateTime to);
    
    /// <summary>
    /// Gets sales for a specific device
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <returns>Collection of sales for the device</returns>
    Task<IEnumerable<Sale>> GetSalesByDeviceAsync(Guid deviceId);
    
    /// <summary>
    /// Gets the most recent sale
    /// </summary>
    /// <returns>Most recent sale if any exists, null otherwise</returns>
    Task<Sale?> GetLatestSaleAsync();
    
    /// <summary>
    /// Gets sales by invoice number
    /// </summary>
    /// <param name="invoiceNumber">Invoice number</param>
    /// <returns>Sale with the specified invoice number, null if not found</returns>
    Task<Sale?> GetByInvoiceNumberAsync(string invoiceNumber);
    
    /// <summary>
    /// Gets all sales within a date range for a specific shop
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="from">Start date (inclusive)</param>
    /// <param name="to">End date (inclusive)</param>
    /// <returns>Collection of sales within the date range for the shop</returns>
    Task<IEnumerable<Sale>> GetSalesByShopAndDateRangeAsync(Guid shopId, DateTime from, DateTime to);
    
    /// <summary>
    /// Gets all sales for a specific shop
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Collection of sales for the shop</returns>
    Task<IEnumerable<Sale>> GetSalesByShopAsync(Guid shopId);
}