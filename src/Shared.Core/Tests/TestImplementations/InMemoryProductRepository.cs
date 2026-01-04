using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;

namespace Shared.Core.Tests.TestImplementations;

/// <summary>
/// In-memory implementation of IProductRepository for testing purposes
/// </summary>
public class InMemoryProductRepository : IProductRepository
{
    private readonly PosDbContext _context;

    public InMemoryProductRepository(PosDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(Guid id)
    {
        return await _context.Products.FindAsync(id);
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        return await _context.Products.ToListAsync();
    }

    public async Task<IEnumerable<Product>> FindAsync(Expression<Func<Product, bool>> predicate)
    {
        return await _context.Products.Where(predicate).ToListAsync();
    }

    public async Task AddAsync(Product entity)
    {
        await _context.Products.AddAsync(entity);
    }

    public async Task UpdateAsync(Product entity)
    {
        _context.Products.Update(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product != null)
        {
            _context.Products.Remove(product);
        }
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task<Product?> GetByBarcodeAsync(string barcode)
    {
        return await _context.Products
            .FirstOrDefaultAsync(p => p.Barcode == barcode);
    }

    public async Task<IEnumerable<Product>> GetActiveByCategoryAsync(string category)
    {
        return await _context.Products
            .Where(p => p.IsActive && p.Category == category)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetExpiringMedicinesAsync(DateTime beforeDate)
    {
        return await _context.Products
            .Where(p => p.ExpiryDate.HasValue && p.ExpiryDate < beforeDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetActiveProductsAsync()
    {
        return await _context.Products
            .Where(p => p.IsActive)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetUnsyncedAsync()
    {
        return await _context.Products
            .Where(p => p.SyncStatus == SyncStatus.NotSynced)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> SearchAsync(string searchTerm)
    {
        return await _context.Products
            .Where(p => p.Name.Contains(searchTerm) || (p.Barcode != null && p.Barcode.Contains(searchTerm)))
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetProductsByShopAsync(Guid shopId)
    {
        return await _context.Products
            .Where(p => p.ShopId == shopId && p.IsActive)
            .ToListAsync();
    }
}