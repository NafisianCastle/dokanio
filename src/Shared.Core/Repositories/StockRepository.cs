using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Repositories;

/// <summary>
/// Stock repository implementation with offline-first storage priority
/// </summary>
public class StockRepository : Repository<Stock>, IStockRepository
{
    public StockRepository(PosDbContext context, ILogger<StockRepository> logger) 
        : base(context, logger)
    {
    }

    /// <summary>
    /// Gets stock information for a specific product from Local_Storage
    /// </summary>
    public async Task<Stock?> GetByProductIdAsync(Guid productId)
    {
        try
        {
            _logger.LogDebug("Getting stock for product {ProductId} from Local_Storage", productId);
            
            // Local-first: Query Local_Storage only
            var stock = await _dbSet
                .Include(s => s.Product)
                .FirstOrDefaultAsync(s => s.ProductId == productId);
            
            if (stock != null)
            {
                _logger.LogDebug("Found stock for product {ProductId}: {Quantity} units in Local_Storage", productId, stock.Quantity);
            }
            else
            {
                _logger.LogDebug("No stock found for product {ProductId} in Local_Storage", productId);
            }
            
            return stock;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stock for product {ProductId} from Local_Storage", productId);
            throw;
        }
    }

    /// <summary>
    /// Gets all products with low stock (below threshold) from Local_Storage
    /// </summary>
    public async Task<IEnumerable<Stock>> GetLowStockAsync(int threshold = 10)
    {
        try
        {
            _logger.LogDebug("Getting low stock items (threshold: {Threshold}) from Local_Storage", threshold);
            
            // Local-first: Query Local_Storage only
            var lowStockItems = await _dbSet
                .Include(s => s.Product)
                .Where(s => s.Quantity <= threshold && s.Product.IsActive)
                .ToListAsync();
            
            _logger.LogDebug("Found {Count} low stock items in Local_Storage", lowStockItems.Count);
            
            return lowStockItems;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting low stock items from Local_Storage");
            throw;
        }
    }

    /// <summary>
    /// Gets all stock records that need to be synced to the server from Local_Storage
    /// </summary>
    public async Task<IEnumerable<Stock>> GetUnsyncedAsync()
    {
        try
        {
            _logger.LogDebug("Getting unsynced stock records from Local_Storage");
            
            // Local-first: Query Local_Storage only
            var unsyncedStock = await _dbSet
                .Include(s => s.Product)
                .Where(s => s.SyncStatus == SyncStatus.NotSynced || s.SyncStatus == SyncStatus.SyncFailed)
                .ToListAsync();
            
            _logger.LogDebug("Found {Count} unsynced stock records in Local_Storage", unsyncedStock.Count);
            
            return unsyncedStock;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unsynced stock records from Local_Storage");
            throw;
        }
    }

    /// <summary>
    /// Updates stock quantity for a specific product by a change amount in Local_Storage
    /// Local-first: Updates Local_Storage before any network operations
    /// </summary>
    public async Task UpdateStockQuantityAsync(Guid productId, int quantityChange, Guid deviceId)
    {
        try
        {
            _logger.LogDebug("Updating stock quantity for product {ProductId} by {QuantityChange} from device {DeviceId} in Local_Storage", 
                productId, quantityChange, deviceId);
            
            // Local-first: Update in Local_Storage immediately
            var stock = await _dbSet.FirstOrDefaultAsync(s => s.ProductId == productId);
            
            if (stock != null)
            {
                var oldQuantity = stock.Quantity;
                stock.Quantity += quantityChange;
                stock.LastUpdatedAt = DateTime.UtcNow;
                stock.DeviceId = deviceId; // Update the device that made the change
                stock.SyncStatus = SyncStatus.NotSynced; // Mark for sync
                
                _logger.LogDebug("Updated stock for product {ProductId} from {OldQuantity} to {NewQuantity} (change: {QuantityChange}) in Local_Storage", 
                    productId, oldQuantity, stock.Quantity, quantityChange);
            }
            else
            {
                _logger.LogWarning("Stock record for product {ProductId} not found in Local_Storage", productId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating stock quantity for product {ProductId} in Local_Storage", productId);
            throw;
        }
    }

    /// <summary>
    /// Gets current stock quantity for a product from Local_Storage
    /// </summary>
    public async Task<int> GetStockQuantityAsync(Guid productId)
    {
        try
        {
            _logger.LogDebug("Getting stock quantity for product {ProductId} from Local_Storage", productId);
            
            // Local-first: Query Local_Storage only
            var stock = await _dbSet.FirstOrDefaultAsync(s => s.ProductId == productId);
            
            var quantity = stock?.Quantity ?? 0;
            _logger.LogDebug("Stock quantity for product {ProductId}: {Quantity} in Local_Storage", productId, quantity);
            
            return quantity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stock quantity for product {ProductId} from Local_Storage", productId);
            throw;
        }
    }

    /// <summary>
    /// Gets all stock entries for products in a specific category from Local_Storage
    /// </summary>
    public async Task<IEnumerable<Stock>> GetStockByCategoryAsync(string category)
    {
        try
        {
            _logger.LogDebug("Getting stock for products in category {Category} from Local_Storage", category);
            
            // Local-first: Query Local_Storage only
            var categoryStock = await _dbSet
                .Include(s => s.Product)
                .Where(s => s.Product.Category == category && s.Product.IsActive)
                .ToListAsync();
            
            _logger.LogDebug("Found {Count} stock entries for category {Category} in Local_Storage", categoryStock.Count, category);
            
            return categoryStock;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stock for category {Category} from Local_Storage", category);
            throw;
        }
    }

    /// <summary>
    /// Checks if a product has sufficient stock for a sale from Local_Storage
    /// </summary>
    public async Task<bool> HasSufficientStockAsync(Guid productId, int requiredQuantity)
    {
        try
        {
            _logger.LogDebug("Checking if product {ProductId} has sufficient stock ({RequiredQuantity}) in Local_Storage", 
                productId, requiredQuantity);
            
            // Local-first: Query Local_Storage only
            var currentQuantity = await GetStockQuantityAsync(productId);
            var hasSufficientStock = currentQuantity >= requiredQuantity;
            
            _logger.LogDebug("Product {ProductId} has {CurrentQuantity} units, required {RequiredQuantity}, sufficient: {HasSufficientStock}", 
                productId, currentQuantity, requiredQuantity, hasSufficientStock);
            
            return hasSufficientStock;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking sufficient stock for product {ProductId} in Local_Storage", productId);
            throw;
        }
    }

    /// <summary>
    /// Adjusts stock quantity by a delta amount (positive for increase, negative for decrease)
    /// Local-first: Updates Local_Storage before any network operations
    /// </summary>
    public async Task AdjustStockQuantityAsync(Guid productId, int quantityDelta)
    {
        try
        {
            _logger.LogDebug("Adjusting stock quantity for product {ProductId} by {QuantityDelta} in Local_Storage", productId, quantityDelta);
            
            // Local-first: Update in Local_Storage immediately
            var stock = await _dbSet.FirstOrDefaultAsync(s => s.ProductId == productId);
            
            if (stock != null)
            {
                var oldQuantity = stock.Quantity;
                stock.Quantity += quantityDelta;
                stock.LastUpdatedAt = DateTime.UtcNow;
                stock.SyncStatus = SyncStatus.NotSynced; // Mark for sync
                
                _logger.LogDebug("Adjusted stock for product {ProductId} from {OldQuantity} to {NewQuantity} (delta: {QuantityDelta}) in Local_Storage", 
                    productId, oldQuantity, stock.Quantity, quantityDelta);
            }
            else
            {
                _logger.LogWarning("Stock record for product {ProductId} not found for adjustment in Local_Storage", productId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adjusting stock quantity for product {ProductId} in Local_Storage", productId);
            throw;
        }
    }

    /// <summary>
    /// Gets all stock records for a specific device from Local_Storage
    /// </summary>
    public async Task<IEnumerable<Stock>> GetStockByDeviceAsync(Guid deviceId)
    {
        try
        {
            _logger.LogDebug("Getting stock records for device {DeviceId} from Local_Storage", deviceId);
            
            // Local-first: Query Local_Storage only
            var deviceStock = await _dbSet
                .Include(s => s.Product)
                .Where(s => s.DeviceId == deviceId)
                .ToListAsync();
            
            _logger.LogDebug("Found {Count} stock records for device {DeviceId} in Local_Storage", deviceStock.Count, deviceId);
            
            return deviceStock;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stock records for device {DeviceId} from Local_Storage", deviceId);
            throw;
        }
    }

    /// <summary>
    /// Gets all stock entries for a specific shop from Local_Storage
    /// </summary>
    public async Task<IEnumerable<Stock>> GetStockByShopAsync(Guid shopId)
    {
        try
        {
            _logger.LogDebug("Getting stock for shop {ShopId} from Local_Storage", shopId);
            
            // Local-first: Query Local_Storage only
            var shopStock = await _dbSet
                .Include(s => s.Product)
                .Where(s => s.ShopId == shopId)
                .ToListAsync();
            
            _logger.LogDebug("Found {Count} stock entries for shop {ShopId} in Local_Storage", shopStock.Count, shopId);
            
            return shopStock;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stock for shop {ShopId} from Local_Storage", shopId);
            throw;
        }
    }
}