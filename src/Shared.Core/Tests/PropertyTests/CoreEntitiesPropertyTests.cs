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
        services.AddSharedCoreInMemory();
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        _context.Database.EnsureCreated();
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