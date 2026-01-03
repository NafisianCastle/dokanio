using Shared.Core.Entities;

namespace Shared.Core.Repositories;

/// <summary>
/// Specialized repository interface for Product entities
/// </summary>
public interface IProductRepository : IRepository<Product>
{
    /// <summary>
    /// Gets a product by its barcode
    /// </summary>
    /// <param name="barcode">Product barcode</param>
    /// <returns>Product if found, null otherwise</returns>
    Task<Product?> GetByBarcodeAsync(string barcode);
    
    /// <summary>
    /// Gets all active products in a specific category
    /// </summary>
    /// <param name="category">Product category</param>
    /// <returns>Collection of active products in the category</returns>
    Task<IEnumerable<Product>> GetActiveByCategoryAsync(string category);
    
    /// <summary>
    /// Gets all medicine products expiring before the specified date
    /// </summary>
    /// <param name="beforeDate">Expiry date threshold</param>
    /// <returns>Collection of expiring medicine products</returns>
    Task<IEnumerable<Product>> GetExpiringMedicinesAsync(DateTime beforeDate);
    
    /// <summary>
    /// Gets all active products
    /// </summary>
    /// <returns>Collection of active products</returns>
    Task<IEnumerable<Product>> GetActiveProductsAsync();
    
    /// <summary>
    /// Gets products that need to be synced to the server
    /// </summary>
    /// <returns>Collection of unsynced products</returns>
    Task<IEnumerable<Product>> GetUnsyncedAsync();
    
    /// <summary>
    /// Searches products by name or barcode
    /// </summary>
    /// <param name="searchTerm">Search term</param>
    /// <returns>Collection of matching products</returns>
    Task<IEnumerable<Product>> SearchAsync(string searchTerm);
}