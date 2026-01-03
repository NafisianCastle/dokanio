using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Repositories;

/// <summary>
/// Sale repository implementation with offline-first storage priority
/// </summary>
public class SaleRepository : Repository<Sale>, ISaleRepository
{
    public SaleRepository(PosDbContext context, ILogger<SaleRepository> logger) 
        : base(context, logger)
    {
    }

    /// <summary>
    /// Gets all sales that haven't been synced to the server from Local_Storage
    /// </summary>
    public async Task<IEnumerable<Sale>> GetUnsyncedAsync()
    {
        try
        {
            _logger.LogDebug("Getting unsynced sales from Local_Storage");
            
            // Local-first: Query Local_Storage only
            var unsyncedSales = await _dbSet
                .Include(s => s.Items)
                .Where(s => s.SyncStatus == SyncStatus.NotSynced || s.SyncStatus == SyncStatus.SyncFailed)
                .ToListAsync();
            
            _logger.LogDebug("Found {Count} unsynced sales in Local_Storage", unsyncedSales.Count);
            
            return unsyncedSales;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unsynced sales from Local_Storage");
            throw;
        }
    }

    /// <summary>
    /// Gets the total sales amount for a specific date from Local_Storage
    /// </summary>
    public async Task<decimal> GetDailySalesAsync(DateTime date)
    {
        try
        {
            _logger.LogDebug("Getting daily sales total for date {Date} from Local_Storage", date.Date);
            
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);
            
            // Local-first: Query Local_Storage only
            var dailyTotal = await _dbSet
                .Where(s => s.CreatedAt >= startOfDay && s.CreatedAt <= endOfDay)
                .SumAsync(s => s.TotalAmount);
            
            _logger.LogDebug("Daily sales total for {Date}: {Total} from Local_Storage", date.Date, dailyTotal);
            
            return dailyTotal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting daily sales for date {Date} from Local_Storage", date.Date);
            throw;
        }
    }

    /// <summary>
    /// Gets all sales within a date range from Local_Storage
    /// </summary>
    public async Task<IEnumerable<Sale>> GetSalesByDateRangeAsync(DateTime from, DateTime to)
    {
        try
        {
            _logger.LogDebug("Getting sales from {From} to {To} from Local_Storage", from, to);
            
            var startDate = from.Date;
            var endDate = to.Date.AddDays(1).AddTicks(-1);
            
            // Local-first: Query Local_Storage only
            var salesInRange = await _dbSet
                .Include(s => s.Items)
                .Where(s => s.CreatedAt >= startDate && s.CreatedAt <= endDate)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
            
            _logger.LogDebug("Found {Count} sales from {From} to {To} in Local_Storage", salesInRange.Count, from, to);
            
            return salesInRange;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sales from {From} to {To} from Local_Storage", from, to);
            throw;
        }
    }

    /// <summary>
    /// Gets sales for a specific device from Local_Storage
    /// </summary>
    public async Task<IEnumerable<Sale>> GetSalesByDeviceAsync(Guid deviceId)
    {
        try
        {
            _logger.LogDebug("Getting sales for device {DeviceId} from Local_Storage", deviceId);
            
            // Local-first: Query Local_Storage only
            var deviceSales = await _dbSet
                .Include(s => s.Items)
                .Where(s => s.DeviceId == deviceId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
            
            _logger.LogDebug("Found {Count} sales for device {DeviceId} in Local_Storage", deviceSales.Count, deviceId);
            
            return deviceSales;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sales for device {DeviceId} from Local_Storage", deviceId);
            throw;
        }
    }

    /// <summary>
    /// Gets the most recent sale from Local_Storage
    /// </summary>
    public async Task<Sale?> GetLatestSaleAsync()
    {
        try
        {
            _logger.LogDebug("Getting latest sale from Local_Storage");
            
            // Local-first: Query Local_Storage only
            var latestSale = await _dbSet
                .Include(s => s.Items)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();
            
            if (latestSale != null)
            {
                _logger.LogDebug("Found latest sale {InvoiceNumber} from {CreatedAt} in Local_Storage", latestSale.InvoiceNumber, latestSale.CreatedAt);
            }
            else
            {
                _logger.LogDebug("No sales found in Local_Storage");
            }
            
            return latestSale;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest sale from Local_Storage");
            throw;
        }
    }

    /// <summary>
    /// Gets sales by invoice number from Local_Storage
    /// </summary>
    public async Task<Sale?> GetByInvoiceNumberAsync(string invoiceNumber)
    {
        try
        {
            _logger.LogDebug("Getting sale by invoice number {InvoiceNumber} from Local_Storage", invoiceNumber);
            
            if (string.IsNullOrWhiteSpace(invoiceNumber))
            {
                _logger.LogWarning("Invoice number is null or empty");
                return null;
            }
            
            // Local-first: Query Local_Storage only
            var sale = await _dbSet
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.InvoiceNumber == invoiceNumber);
            
            if (sale != null)
            {
                _logger.LogDebug("Found sale with invoice number {InvoiceNumber} in Local_Storage", invoiceNumber);
            }
            else
            {
                _logger.LogDebug("Sale with invoice number {InvoiceNumber} not found in Local_Storage", invoiceNumber);
            }
            
            return sale;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sale by invoice number {InvoiceNumber} from Local_Storage", invoiceNumber);
            throw;
        }
    }

    /// <summary>
    /// Gets sales count for a specific date from Local_Storage
    /// </summary>
    public async Task<int> GetDailySalesCountAsync(DateTime date)
    {
        try
        {
            _logger.LogDebug("Getting daily sales count for date {Date} from Local_Storage", date.Date);
            
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);
            
            // Local-first: Query Local_Storage only
            var dailyCount = await _dbSet
                .Where(s => s.CreatedAt >= startOfDay && s.CreatedAt <= endOfDay)
                .CountAsync();
            
            _logger.LogDebug("Daily sales count for {Date}: {Count} from Local_Storage", date.Date, dailyCount);
            
            return dailyCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting daily sales count for date {Date} from Local_Storage", date.Date);
            throw;
        }
    }
}