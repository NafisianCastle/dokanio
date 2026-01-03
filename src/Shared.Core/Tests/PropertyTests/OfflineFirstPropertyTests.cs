using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Xunit;

namespace Shared.Core.Tests.PropertyTests;

public class OfflineFirstPropertyTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;
    private readonly IProductRepository _productRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IStockRepository _stockRepository;

    public OfflineFirstPropertyTests()
    {
        var services = new ServiceCollection();
        
        // Use SQLite in-memory database for testing
        services.AddDbContext<PosDbContext>(options =>
        {
            options.UseSqlite("Data Source=:memory:", sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(30);
            });
            options.EnableSensitiveDataLogging(true);
        });
        
        // Add logging
        services.AddLogging();
        
        // Add repositories
        services.AddScoped<IRepository<Product>, Repository<Product>>();
        services.AddScoped<IRepository<Sale>, Repository<Sale>>();
        services.AddScoped<IRepository<Stock>, Repository<Stock>>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        _productRepository = _serviceProvider.GetRequiredService<IProductRepository>();
        _saleRepository = _serviceProvider.GetRequiredService<ISaleRepository>();
        _stockRepository = _serviceProvider.GetRequiredService<IStockRepository>();
        
        // Ensure database is created and configured
        _context.Database.OpenConnection(); // Keep connection open for in-memory SQLite
        _context.Database.EnsureCreated();
        
        // Enable foreign keys for SQLite
        _context.Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON");
    }

    [Property]
    public bool LocalFirstStorage(NonEmptyString productName, PositiveInt price)
    {
        // Feature: offline-first-pos, Property 5: For any transaction operation, the data should be persisted to Local_Storage before any attempt to communicate with the server
        // **Validates: Requirements 2.1**
        
        // Clear the database for each test
        ClearDatabase();
        
        var deviceId = Guid.NewGuid();
        var barcode = $"TEST{Guid.NewGuid().ToString("N")[..8]}";
        
        // Create a product to test local-first storage
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = productName.Get,
            Barcode = barcode,
            UnitPrice = Math.Max(0.01m, price.Get),
            IsActive = true,
            DeviceId = deviceId,
            CreatedAt = DateTime.UtcNow,
            SyncStatus = SyncStatus.NotSynced // This indicates it hasn't been synced to server yet
        };
        
        try
        {
            // Add the product using repository (this should persist to Local_Storage immediately)
            var addTask = _productRepository.AddAsync(product);
            addTask.Wait();
            
            var saveTask = _productRepository.SaveChangesAsync();
            saveTask.Wait();
            
            // Verify that the data is immediately available in Local_Storage
            // This simulates the local-first behavior where data is persisted locally before any server communication
            var retrievedProduct = _context.Products.FirstOrDefault(p => p.Id == product.Id);
            
            if (retrievedProduct == null)
            {
                return false; // Product should be in Local_Storage
            }
            
            // Verify that the product is marked as not synced (indicating local-first storage)
            if (retrievedProduct.SyncStatus != SyncStatus.NotSynced)
            {
                return false; // Should be marked as not synced initially
            }
            
            // Verify that ServerSyncedAt is null (indicating no server communication yet)
            if (retrievedProduct.ServerSyncedAt != null)
            {
                return false; // Should not have server sync timestamp yet
            }
            
            // Verify all data is correctly stored locally
            return retrievedProduct.Name == productName.Get &&
                   retrievedProduct.Barcode == barcode &&
                   retrievedProduct.UnitPrice == Math.Max(0.01m, price.Get) &&
                   retrievedProduct.DeviceId == deviceId;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool OfflineOperationContinuity(NonEmptyString productName, PositiveInt quantity)
    {
        // Feature: offline-first-pos, Property 6: For any core POS operation (product lookup, sale creation, inventory check), the operation should succeed when using only Local_Storage, regardless of network connectivity
        // **Validates: Requirements 2.2**
        
        // Clear the database for each test
        ClearDatabase();
        
        var deviceId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var barcode = $"OFFLINE{Guid.NewGuid().ToString("N")[..8]}";
        var stockQuantity = Math.Max(1, quantity.Get);
        
        try
        {
            // Set up test data in Local_Storage (simulating offline scenario)
            var product = new Product
            {
                Id = productId,
                Name = productName.Get,
                Barcode = barcode,
                UnitPrice = 15.00m,
                IsActive = true,
                DeviceId = deviceId,
                CreatedAt = DateTime.UtcNow,
                SyncStatus = SyncStatus.NotSynced
            };
            
            var stock = new Stock
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                Quantity = stockQuantity,
                LastUpdatedAt = DateTime.UtcNow,
                DeviceId = deviceId,
                SyncStatus = SyncStatus.NotSynced
            };
            
            _context.Products.Add(product);
            _context.Stock.Add(stock);
            _context.SaveChanges();
            
            // Test 1: Product lookup should work offline (using only Local_Storage)
            var lookupTask = _productRepository.GetByBarcodeAsync(barcode);
            lookupTask.Wait();
            var foundProduct = lookupTask.Result;
            
            if (foundProduct == null || foundProduct.Name != productName.Get)
            {
                return false; // Product lookup should work offline
            }
            
            // Test 2: Inventory check should work offline (using only Local_Storage)
            var stockTask = _stockRepository.GetByProductIdAsync(productId);
            stockTask.Wait();
            var foundStock = stockTask.Result;
            
            if (foundStock == null || foundStock.Quantity != stockQuantity)
            {
                return false; // Inventory check should work offline
            }
            
            // Test 3: Sale creation should work offline (using only Local_Storage)
            var sale = new Sale
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"OFFLINE-{DateTime.Now.Ticks}",
                TotalAmount = 15.00m,
                PaymentMethod = PaymentMethod.Cash,
                DeviceId = deviceId,
                CreatedAt = DateTime.UtcNow,
                SyncStatus = SyncStatus.NotSynced
            };
            
            var saleItem = new SaleItem
            {
                Id = Guid.NewGuid(),
                SaleId = sale.Id,
                ProductId = productId,
                Quantity = 1,
                UnitPrice = 15.00m
            };
            
            var addSaleTask = _saleRepository.AddAsync(sale);
            addSaleTask.Wait();
            
            _context.SaleItems.Add(saleItem);
            
            var saveSaleTask = _saleRepository.SaveChangesAsync();
            saveSaleTask.Wait();
            
            // Verify sale was created successfully offline
            var createdSale = _context.Sales.FirstOrDefault(s => s.Id == sale.Id);
            if (createdSale == null)
            {
                return false; // Sale creation should work offline
            }
            
            // All core POS operations should work using only Local_Storage
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool SalePersistence(NonEmptyString invoiceNumber, PositiveInt totalAmount)
    {
        // Feature: offline-first-pos, Property 7: For any completed sale, the transaction should be immediately stored in Local_Storage and retrievable after completion
        // **Validates: Requirements 1.3**
        
        // Clear the database for each test
        ClearDatabase();
        
        var deviceId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var amount = Math.Max(0.01m, totalAmount.Get);
        var uniqueInvoiceNumber = $"{invoiceNumber.Get}-{DateTime.Now.Ticks}";
        
        try
        {
            // Create a product first
            var product = new Product
            {
                Id = productId,
                Name = "Test Product",
                Barcode = $"PERSIST{Guid.NewGuid().ToString("N")[..8]}",
                UnitPrice = amount,
                IsActive = true,
                DeviceId = deviceId,
                CreatedAt = DateTime.UtcNow,
                SyncStatus = SyncStatus.NotSynced
            };
            
            _context.Products.Add(product);
            _context.SaveChanges();
            
            // Create and complete a sale
            var sale = new Sale
            {
                Id = saleId,
                InvoiceNumber = uniqueInvoiceNumber,
                TotalAmount = amount,
                PaymentMethod = PaymentMethod.Cash,
                DeviceId = deviceId,
                CreatedAt = DateTime.UtcNow,
                SyncStatus = SyncStatus.NotSynced
            };
            
            var saleItem = new SaleItem
            {
                Id = Guid.NewGuid(),
                SaleId = saleId,
                ProductId = productId,
                Quantity = 1,
                UnitPrice = amount
            };
            
            // Add and save the sale (simulating completion)
            var addTask = _saleRepository.AddAsync(sale);
            addTask.Wait();
            
            _context.SaleItems.Add(saleItem);
            
            var saveTask = _saleRepository.SaveChangesAsync();
            saveTask.Wait();
            
            // Immediately verify that the sale is stored and retrievable from Local_Storage
            var retrievedSale = _context.Sales
                .Include(s => s.Items)
                .FirstOrDefault(s => s.Id == saleId);
            
            if (retrievedSale == null)
            {
                return false; // Sale should be immediately stored
            }
            
            // Verify all sale data is correctly persisted
            if (retrievedSale.InvoiceNumber != uniqueInvoiceNumber ||
                retrievedSale.TotalAmount != amount ||
                retrievedSale.DeviceId != deviceId ||
                retrievedSale.Items.Count != 1)
            {
                return false; // All sale data should be correctly persisted
            }
            
            // Test retrieval by invoice number
            var lookupTask = _saleRepository.GetByInvoiceNumberAsync(uniqueInvoiceNumber);
            lookupTask.Wait();
            var foundSale = lookupTask.Result;
            
            if (foundSale == null || foundSale.Id != saleId)
            {
                return false; // Sale should be retrievable by invoice number
            }
            
            return true; // Sale persistence is working correctly
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool TransactionDurability(NonEmptyString productName, PositiveInt price)
    {
        // Feature: offline-first-pos, Property 14: For any completed transaction, the data should survive system failures and be recoverable after restart
        // **Validates: Requirements 11.2**
        
        // Clear the database for each test
        ClearDatabase();
        
        var deviceId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var barcode = $"DURABLE{Guid.NewGuid().ToString("N")[..8]}";
        var unitPrice = Math.Max(0.01m, price.Get);
        
        try
        {
            // Create a transaction (product creation)
            var product = new Product
            {
                Id = productId,
                Name = productName.Get,
                Barcode = barcode,
                UnitPrice = unitPrice,
                IsActive = true,
                DeviceId = deviceId,
                CreatedAt = DateTime.UtcNow,
                SyncStatus = SyncStatus.NotSynced
            };
            
            // Add and save the transaction
            var addTask = _productRepository.AddAsync(product);
            addTask.Wait();
            
            var saveTask = _productRepository.SaveChangesAsync();
            saveTask.Wait();
            
            // Verify that the transaction data is immediately available in the same context
            // This simulates durability by checking that the data persists in Local_Storage
            var persistedProduct = _context.Products.FirstOrDefault(p => p.Id == productId);
            
            if (persistedProduct == null)
            {
                return false; // Transaction should be durable in Local_Storage
            }
            
            // Verify all transaction data is intact and durable
            if (persistedProduct.Name != productName.Get ||
                persistedProduct.Barcode != barcode ||
                persistedProduct.UnitPrice != unitPrice ||
                persistedProduct.DeviceId != deviceId)
            {
                return false; // All transaction data should be durable
            }
            
            // Check that transaction logs were created for durability
            var transactionLogs = _context.TransactionLogs
                .Where(log => log.EntityId == productId && log.EntityType == "Product")
                .ToList();
            
            // Transaction logging should have recorded this operation for durability
            var hasTransactionLog = transactionLogs.Any(log => log.Operation == "INSERT");
            
            return hasTransactionLog; // Transaction durability includes logging for recovery
        }
        catch
        {
            return false;
        }
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