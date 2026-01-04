using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Repositories;

/// <summary>
/// Product repository implementation with offline-first storage priority
/// </summary>
public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(PosDbContext context, ILogger<ProductRepository> logger) 
        : base(context, logger)
    {
    }

    /// <summary>
    /// Gets a product by its barcode from Local_Storage
    /// Supports offline operation continuity
    /// </summary>
    public async Task<Product?> GetByBarcodeAsync(string barcode)
    {
        try
        {
            _logger.LogDebug("Getting product by barcode {Barcode} from Local_Storage", barcode);
            
            if (string.IsNullOrWhiteSpace(barcode))
            {
                _logger.LogWarning("Barcode is null or empty");
                return null;
            }
            
            // Local-first: Query Local_Storage only for offline operation continuity
            var product = await _dbSet
                .FirstOrDefaultAsync(p => p.Barcode == barcode);
            
            if (product != null)
            {
                _logger.LogDebug("Found product {ProductName} with barcode {Barcode} in Local_Storage", product.Name, barcode);
            }
            else
            {
                _logger.LogDebug("Product with barcode {Barcode} not found in Local_Storage", barcode);
            }
            
            return product;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product by barcode {Barcode} from Local_Storage", barcode);
            throw;
        }
    }

    /// <summary>
    /// Gets all active products in a specific category from Local_Storage
    /// </summary>
    public async Task<IEnumerable<Product>> GetActiveByCategoryAsync(string category)
    {
        try
        {
            _logger.LogDebug("Getting active products by category {Category} from Local_Storage", category);
            
            // Local-first: Query Local_Storage only
            var products = await _dbSet
                .Where(p => p.IsActive && p.Category == category)
                .ToListAsync();
            
            _logger.LogDebug("Found {Count} active products in category {Category} in Local_Storage", products.Count, category);
            
            return products;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active products by category {Category} from Local_Storage", category);
            throw;
        }
    }

    /// <summary>
    /// Gets all medicine products expiring before the specified date from Local_Storage
    /// </summary>
    public async Task<IEnumerable<Product>> GetExpiringMedicinesAsync(DateTime beforeDate)
    {
        try
        {
            _logger.LogDebug("Getting expiring medicines before {BeforeDate} from Local_Storage", beforeDate);
            
            // Local-first: Query Local_Storage only
            var expiringMedicines = await _dbSet
                .Where(p => p.IsActive && 
                           p.ExpiryDate.HasValue && 
                           p.ExpiryDate.Value < beforeDate)
                .ToListAsync();
            
            _logger.LogDebug("Found {Count} expiring medicines before {BeforeDate} in Local_Storage", expiringMedicines.Count, beforeDate);
            
            return expiringMedicines;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expiring medicines before {BeforeDate} from Local_Storage", beforeDate);
            throw;
        }
    }

    /// <summary>
    /// Gets all active products from Local_Storage
    /// </summary>
    public async Task<IEnumerable<Product>> GetActiveProductsAsync()
    {
        try
        {
            _logger.LogDebug("Getting all active products from Local_Storage");
            
            // Local-first: Query Local_Storage only
            var activeProducts = await _dbSet
                .Where(p => p.IsActive)
                .ToListAsync();
            
            _logger.LogDebug("Found {Count} active products in Local_Storage", activeProducts.Count);
            
            return activeProducts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active products from Local_Storage");
            throw;
        }
    }

    /// <summary>
    /// Gets products that need to be synced to the server from Local_Storage
    /// </summary>
    public async Task<IEnumerable<Product>> GetUnsyncedAsync()
    {
        try
        {
            _logger.LogDebug("Getting unsynced products from Local_Storage");
            
            // Local-first: Query Local_Storage only
            var unsyncedProducts = await _dbSet
                .Where(p => p.SyncStatus == SyncStatus.NotSynced || p.SyncStatus == SyncStatus.SyncFailed)
                .ToListAsync();
            
            _logger.LogDebug("Found {Count} unsynced products in Local_Storage", unsyncedProducts.Count);
            
            return unsyncedProducts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unsynced products from Local_Storage");
            throw;
        }
    }

    /// <summary>
    /// Searches products by name or barcode from Local_Storage
    /// </summary>
    public async Task<IEnumerable<Product>> SearchAsync(string searchTerm)
    {
        try
        {
            _logger.LogDebug("Searching products by term {SearchTerm} from Local_Storage", searchTerm);
            
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return Enumerable.Empty<Product>();
            }
            
            var lowerSearchTerm = searchTerm.ToLower();
            
            // Local-first: Query Local_Storage only
            var matchingProducts = await _dbSet
                .Where(p => p.IsActive && 
                           (p.Name.ToLower().Contains(lowerSearchTerm) || 
                            p.Barcode != null && p.Barcode.ToLower().Contains(lowerSearchTerm)))
                .ToListAsync();
            
            _logger.LogDebug("Found {Count} products matching search term {SearchTerm} in Local_Storage", matchingProducts.Count, searchTerm);
            
            return matchingProducts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching products by term {SearchTerm} from Local_Storage", searchTerm);
            throw;
        }
    }
}