using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Core.Data;
using Shared.Core.DependencyInjection;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Xunit;

namespace Shared.Core.Tests.PropertyTests;

public class CoreEntitiesPropertyTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;

    public CoreEntitiesPropertyTests()
    {
        var services = new ServiceCollection();
        
        // Use SQLite in-memory database for proper soft delete testing
        services.AddDbContext<PosDbContext>(options =>
        {
            options.UseSqlite("Data Source=:memory:", sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(30);
            });
            options.EnableSensitiveDataLogging(true);
        });
        
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        
        // Ensure database is created and configured
        _context.Database.OpenConnection(); // Keep connection open for in-memory SQLite
        _context.Database.EnsureCreated();
        
        // Enable foreign keys and WAL mode for SQLite after database creation
        _context.Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON");
    }

    [Property]
    public bool ForeignKeyConstraintEnforcement(NonEmptyString invoiceNumber)
    {
        // Feature: offline-first-pos, Property 12: For any attempt to create a relationship with a non-existent entity (e.g., sale item with invalid product ID), the operation should be rejected
        // **Validates: Requirements 8.3**
        
        // Clear the database for each test
        ClearDatabase();
        
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = $"INV-{invoiceNumber.Get}-{DateTime.Now.Ticks}",
            TotalAmount = 100.00m,
            PaymentMethod = PaymentMethod.Cash,
            DeviceId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SyncStatus = SyncStatus.NotSynced
        };
        
        // First, save the sale without items
        _context.Sales.Add(sale);
        _context.SaveChanges();
        
        // Try to create a SaleItem with an invalid ProductId (foreign key violation)
        var invalidProductId = Guid.NewGuid(); // This won't exist in the database
        var saleItem = new SaleItem
        {
            SaleId = sale.Id,
            ProductId = invalidProductId, // This should be invalid
            Quantity = 1,
            UnitPrice = 10.00m
        };
        
        _context.SaleItems.Add(saleItem);
        
        // This should throw an exception due to foreign key constraint violation
        try
        {
            _context.SaveChanges();
            return false; // If no exception is thrown, the test fails
        }
        catch (DbUpdateException)
        {
            return true; // Foreign key constraint was enforced
        }
        catch (InvalidOperationException)
        {
            return true; // Foreign key constraint was enforced
        }
    }

    [Property]
    public bool SoftDeleteBehavior(NonEmptyString productName, NonEmptyString barcode)
    {
        // Feature: offline-first-pos, Property 13: For any business entity deletion, the entity should be marked as deleted rather than physically removed from storage
        // **Validates: Requirements 8.7**
        
        // Clear the database for each test
        ClearDatabase();
        
        // Create a product to test soft delete
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = productName.Get,
            Barcode = $"{barcode.Get}-{DateTime.Now.Ticks}",
            UnitPrice = 10.00m,
            DeviceId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SyncStatus = SyncStatus.NotSynced
        };
        
        _context.Products.Add(product);
        _context.SaveChanges();
        
        // Verify the product exists and is not deleted
        var savedProduct = _context.Products.Find(product.Id);
        if (savedProduct == null || savedProduct.IsDeleted)
        {
            return false;
        }
        
        // Delete the product (should trigger soft delete)
        _context.Products.Remove(product);
        _context.SaveChanges();
        
        // The product should no longer be returned by normal queries (due to query filter)
        var deletedProduct = _context.Products.FirstOrDefault(p => p.Id == product.Id);
        if (deletedProduct != null)
        {
            return false; // Product should not be found in normal queries
        }
        
        // But it should still exist in the database when ignoring query filters
        var physicallyExistingProduct = _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefault(p => p.Id == product.Id);
            
        if (physicallyExistingProduct == null)
        {
            return false; // Product should still physically exist
        }
        
        // The product should be marked as deleted
        if (!physicallyExistingProduct.IsDeleted)
        {
            return false; // IsDeleted should be true
        }
        
        // DeletedAt should be set
        if (physicallyExistingProduct.DeletedAt == null)
        {
            return false; // DeletedAt should be set
        }
        
        return true; // Soft delete behavior is working correctly
    }

    private void ClearDatabase()
    {
        // Use IgnoreQueryFilters to remove all entities including soft-deleted ones
        _context.SaleItems.IgnoreQueryFilters().ExecuteDelete();
        _context.Sales.IgnoreQueryFilters().ExecuteDelete();
        _context.Stock.IgnoreQueryFilters().ExecuteDelete();
        _context.Products.IgnoreQueryFilters().ExecuteDelete();
    }

    public void Dispose()
    {
        _context?.Database.CloseConnection();
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}