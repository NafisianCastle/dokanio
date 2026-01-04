using Shared.Core.Entities;

namespace Shared.Core.Services;

public interface IProductService
{
    Task<Product?> GetProductByBarcodeAsync(string barcode);
    Task<Product?> GetProductByIdAsync(Guid productId);
    Task<IEnumerable<Product>> GetAllActiveProductsAsync();
    Task<IEnumerable<Product>> SearchProductsAsync(string searchTerm);
    Task<bool> ValidateMedicineExpiryAsync(Guid productId);
    Task<IEnumerable<Product>> GetExpiringMedicinesAsync(int daysAhead = 30);
    Task<bool> IsProductActiveAsync(Guid productId);
    Task<Product> CreateProductAsync(string name, string? barcode, string? category, decimal unitPrice, Guid deviceId, string? batchNumber = null, DateTime? expiryDate = null);
}