using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;

namespace Shared.Core.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;

    public ProductService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Product?> GetProductByBarcodeAsync(string barcode)
    {
        return await _productRepository.GetByBarcodeAsync(barcode);
    }

    public async Task<Product?> GetProductByIdAsync(Guid productId)
    {
        return await _productRepository.GetByIdAsync(productId);
    }

    public async Task<bool> ValidateMedicineExpiryAsync(Guid productId)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null)
        {
            return false;
        }

        // If the product has an expiry date, check if it's not expired
        if (product.ExpiryDate.HasValue)
        {
            return product.ExpiryDate.Value >= DateTime.UtcNow;
        }

        // If no expiry date, it's valid (not a medicine or doesn't expire)
        return true;
    }

    public async Task<IEnumerable<Product>> GetExpiringMedicinesAsync(int daysAhead = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(daysAhead);
        var allProducts = await _productRepository.GetAllAsync();

        return allProducts.Where(p => 
            p.IsActive && 
            p.ExpiryDate.HasValue && 
            p.ExpiryDate.Value <= cutoffDate &&
            p.ExpiryDate.Value >= DateTime.UtcNow // Not already expired
        );
    }

    public async Task<bool> IsProductActiveAsync(Guid productId)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        return product?.IsActive ?? false;
    }

    public async Task<Product> CreateProductAsync(string name, string? barcode, string? category, decimal unitPrice, Guid deviceId, string? batchNumber = null, DateTime? expiryDate = null)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Barcode = barcode,
            Category = category,
            UnitPrice = unitPrice,
            IsActive = true,
            DeviceId = deviceId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncStatus = SyncStatus.NotSynced,
            BatchNumber = batchNumber,
            ExpiryDate = expiryDate
        };

        await _productRepository.AddAsync(product);
        await _productRepository.SaveChangesAsync();

        return product;
    }

    public async Task<IEnumerable<Product>> GetAllActiveProductsAsync()
    {
        var allProducts = await _productRepository.GetAllAsync();
        return allProducts.Where(p => p.IsActive);
    }

    public async Task<IEnumerable<Product>> SearchProductsAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllActiveProductsAsync();
        }

        var allProducts = await _productRepository.GetAllAsync();
        var searchTermLower = searchTerm.ToLowerInvariant();

        return allProducts.Where(p => 
            p.IsActive && (
                p.Name.ToLowerInvariant().Contains(searchTermLower) ||
                (p.Barcode != null && p.Barcode.Contains(searchTerm)) ||
                (p.Category != null && p.Category.ToLowerInvariant().Contains(searchTermLower))
            )
        );
    }
}