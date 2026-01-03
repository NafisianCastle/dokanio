using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Core.Data;
using Shared.Core.DependencyInjection;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Xunit;

namespace Shared.Core.Tests.PropertyTests;

public class RepositoryPropertyTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;
    private readonly IProductRepository _productRepository;

    public RepositoryPropertyTests()
    {
        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        _productRepository = _serviceProvider.GetRequiredService<IProductRepository>();
        _context.Database.EnsureCreated();
    }

    [Property]
    public bool BarcodeProductLookup(NonEmptyString productName, PositiveInt price)
    {
        // Feature: offline-first-pos, Property 1: For any valid barcode that exists in Local_Storage, scanning or looking up that barcode should return the correct product information
        // **Validates: Requirements 1.1, 3.3**
        
        // Clear the database for each test
        ClearDatabase();
        
        // Generate a unique barcode for this test - ensure it's unique across test runs
        var barcode = $"TEST{Guid.NewGuid().ToString("N")[..8]}{price.Get}";
        var deviceId = Guid.NewGuid();
        
        // Create a product with the barcode
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = productName.Get,
            Barcode = barcode,
            Category = "Test Category",
            UnitPrice = Math.Max(0.01m, price.Get), // Ensure positive price
            IsActive = true,
            DeviceId = deviceId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncStatus = SyncStatus.NotSynced
        };
        
        try
        {
            // Add the product using the repository
            var addTask = _productRepository.AddAsync(product);
            addTask.Wait();
            
            var saveTask = _productRepository.SaveChangesAsync();
            saveTask.Wait();
            
            // Now test the barcode lookup using the repository interface
            var lookupTask = _productRepository.GetByBarcodeAsync(barcode);
            lookupTask.Wait();
            var foundProduct = lookupTask.Result;
            
            // Verify that the product was found and has the correct information
            return foundProduct != null &&
                   foundProduct.Barcode == barcode &&
                   foundProduct.Name == productName.Get &&
                   foundProduct.UnitPrice == Math.Max(0.01m, price.Get) &&
                   foundProduct.Id == product.Id;
        }
        catch
        {
            // If there's any exception during the test, return false
            return false;
        }
    }

    private void ClearDatabase()
    {
        _context.SaleItems.RemoveRange(_context.SaleItems);
        _context.Sales.RemoveRange(_context.Sales);
        _context.Stock.RemoveRange(_context.Stock);
        _context.Products.RemoveRange(_context.Products);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}