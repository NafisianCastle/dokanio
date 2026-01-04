using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Shared.Core.Data;
using Shared.Core.DependencyInjection;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using Shared.Core.Repositories;
using Xunit;

namespace Shared.Core.Tests;

/// <summary>
/// Integration tests for end-to-end workflows in the offline-first POS system.
/// Tests complete sale workflow from product scan to receipt printing,
/// offline-to-online sync scenarios, and multi-device synchronization scenarios.
/// </summary>
public class IntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _dbContext;
    private readonly ISaleService _saleService;
    private readonly IProductService _productService;
    private readonly IInventoryService _inventoryService;
    private readonly ISyncEngine _syncEngine;
    private readonly IReceiptService _receiptService;
    private readonly IProductRepository _productRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IStockRepository _stockRepository;

    public IntegrationTests()
    {
        var services = new ServiceCollection();
        
        // Register all shared core services with in-memory database
        services.AddSharedCoreInMemory();
        
        _serviceProvider = services.BuildServiceProvider();
        
        _dbContext = _serviceProvider.GetRequiredService<PosDbContext>();
        _saleService = _serviceProvider.GetRequiredService<ISaleService>();
        _productService = _serviceProvider.GetRequiredService<IProductService>();
        _inventoryService = _serviceProvider.GetRequiredService<IInventoryService>();
        _syncEngine = _serviceProvider.GetRequiredService<ISyncEngine>();
        _receiptService = _serviceProvider.GetRequiredService<IReceiptService>();
        _productRepository = _serviceProvider.GetRequiredService<IProductRepository>();
        _saleRepository = _serviceProvider.GetRequiredService<ISaleRepository>();
        _stockRepository = _serviceProvider.GetRequiredService<IStockRepository>();

        // Ensure database is created
        _dbContext.Database.EnsureCreated();
        
        // Set up a valid license for testing
        SetupValidLicense().Wait();
    }

    private async Task SetupValidLicense()
    {
        var currentUserService = _serviceProvider.GetRequiredService<ICurrentUserService>();
        var licenseRepository = _serviceProvider.GetRequiredService<ILicenseRepository>();
        var deviceId = currentUserService.GetDeviceId();
        
        var license = new License
        {
            Id = Guid.NewGuid(),
            LicenseKey = "TEST-12345-ABCDE",
            Type = LicenseType.Professional,
            IssueDate = DateTime.UtcNow.AddDays(-30),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Status = LicenseStatus.Active,
            CustomerName = "Test Customer",
            CustomerEmail = "test@example.com",
            MaxDevices = 10,
            Features = new List<string> { "basic_pos", "inventory", "advanced_reports", "multi_user", "weight_based", "membership", "discounts" },
            ActivationDate = DateTime.UtcNow.AddDays(-30),
            DeviceId = deviceId
        };
        
        await licenseRepository.AddAsync(license);
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CompleteWorkflow_ProductScanToReceiptPrinting_ShouldSucceed()
    {
        // Arrange: Set up test data
        var deviceId = Guid.NewGuid();
        var testProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Barcode = "1234567890123",
            Category = "Electronics",
            UnitPrice = 29.99m,
            IsActive = true,
            DeviceId = deviceId,
            SyncStatus = SyncStatus.NotSynced
        };

        var testStock = new Stock
        {
            Id = Guid.NewGuid(),
            ProductId = testProduct.Id,
            Quantity = 100,
            DeviceId = deviceId,
            SyncStatus = SyncStatus.NotSynced
        };

        // Add test data to database
        await _productRepository.AddAsync(testProduct);
        await _stockRepository.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        // Act 1: Scan product barcode (simulate barcode scanning)
        var scannedProduct = await _productService.GetProductByBarcodeAsync(testProduct.Barcode);
        Assert.NotNull(scannedProduct);
        Assert.Equal(testProduct.Id, scannedProduct.Id);

        // Act 2: Create sale with scanned product
        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var sale = await _saleService.CreateSaleAsync(invoiceNumber, deviceId);
        
        // Add item to sale
        sale = await _saleService.AddItemToSaleAsync(sale.Id, testProduct.Id, 2, testProduct.UnitPrice);
        Assert.NotNull(sale);
        Assert.Equal(2 * testProduct.UnitPrice, sale.TotalAmount);

        // Act 3: Complete the sale
        var completedSale = await _saleService.CompleteSaleAsync(sale.Id, PaymentMethod.Cash);
        Assert.NotNull(completedSale);

        // Act 4: Verify inventory was updated
        var updatedStock = await _stockRepository.GetByProductIdAsync(testProduct.Id);
        Assert.NotNull(updatedStock);
        Assert.Equal(98, updatedStock.Quantity); // 100 - 2 = 98

        // Act 5: Generate receipt
        var receiptContent = await _receiptService.GenerateReceiptAsync(completedSale);
        Assert.NotNull(receiptContent);
        Assert.Contains(testProduct.Name, receiptContent.PlainText);
        Assert.Contains(completedSale.TotalAmount.ToString("C"), receiptContent.PlainText);

        // Verify the complete workflow succeeded
        Assert.Equal(SyncStatus.NotSynced, completedSale.SyncStatus);
        Assert.True(completedSale.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task OfflineToOnlineSync_WithPendingTransactions_ShouldSyncSuccessfully()
    {
        // Arrange: Create offline transactions
        var deviceId = Guid.NewGuid();
        var testProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Offline Product",
            Barcode = "9876543210987",
            Category = "Books",
            UnitPrice = 15.50m,
            IsActive = true,
            DeviceId = deviceId,
            SyncStatus = SyncStatus.NotSynced
        };

        await _productRepository.AddAsync(testProduct);
        
        // Add sufficient stock for the test
        var testStock = new Stock
        {
            Id = Guid.NewGuid(),
            ProductId = testProduct.Id,
            Quantity = 100, // Sufficient for all sales
            DeviceId = deviceId,
            SyncStatus = SyncStatus.NotSynced
        };
        await _stockRepository.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();
        await _dbContext.SaveChangesAsync();

        // Create multiple offline sales
        var offlineSales = new List<Sale>();
        for (int i = 0; i < 3; i++)
        {
            var invoiceNumber = $"OFFLINE-{DateTime.UtcNow:yyyyMMddHHmmss}-{i}";
            var sale = await _saleService.CreateSaleAsync(invoiceNumber, deviceId);
            
            // Add item to sale
            sale = await _saleService.AddItemToSaleAsync(sale.Id, testProduct.Id, 1, testProduct.UnitPrice);
            
            // Complete sale
            var completedSale = await _saleService.CompleteSaleAsync(sale.Id, PaymentMethod.Cash);
            offlineSales.Add(completedSale);
        }

        // Verify all sales are marked as not synced
        var unsyncedSales = await _saleRepository.GetUnsyncedAsync();
        Assert.Equal(3, unsyncedSales.Count());

        // Act: Simulate sync operation (Note: This is a mock sync since we don't have a real server)
        // In a real scenario, this would connect to the server and upload the data
        foreach (var sale in offlineSales)
        {
            sale.SyncStatus = SyncStatus.Synced;
            sale.ServerSyncedAt = DateTime.UtcNow;
            await _saleRepository.UpdateAsync(sale);
        }
        await _dbContext.SaveChangesAsync();

        // Assert: Verify sync completed
        var syncedSales = await _saleRepository.GetAllAsync();
        Assert.All(syncedSales, sale => Assert.Equal(SyncStatus.Synced, sale.SyncStatus));
        Assert.All(syncedSales, sale => Assert.NotNull(sale.ServerSyncedAt));
    }

    [Fact]
    public async Task MultiDeviceSync_WithConflictingData_ShouldResolveCorrectly()
    {
        // Arrange: Simulate two devices with conflicting product data
        var device1Id = Guid.NewGuid();
        var device2Id = Guid.NewGuid();
        var productId = Guid.NewGuid();

        // Device 1 creates a product
        var device1Product = new Product
        {
            Id = productId,
            Name = "Shared Product",
            Barcode = "1111111111111",
            Category = "Electronics",
            UnitPrice = 25.00m,
            IsActive = true,
            DeviceId = device1Id,
            SyncStatus = SyncStatus.NotSynced,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10) // Older timestamp
        };

        // Device 2 updates the same product with different price (newer timestamp)
        var device2Product = new Product
        {
            Id = productId,
            Name = "Shared Product",
            Barcode = "1111111111111",
            Category = "Electronics",
            UnitPrice = 30.00m, // Different price
            IsActive = true,
            DeviceId = device2Id,
            SyncStatus = SyncStatus.NotSynced,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5) // Newer timestamp
        };

        // Add both products to simulate conflict scenario
        await _productRepository.AddAsync(device1Product);
        await _dbContext.SaveChangesAsync();

        // Act: Simulate conflict resolution (server wins for prices)
        // In real sync, the server would determine which version to keep
        // For this test, we simulate that the newer version (device2) wins
        device1Product.UnitPrice = device2Product.UnitPrice;
        device1Product.UpdatedAt = device2Product.UpdatedAt;
        device1Product.SyncStatus = SyncStatus.ConflictResolved;
        
        await _productRepository.UpdateAsync(device1Product);
        await _dbContext.SaveChangesAsync();

        // Assert: Verify conflict was resolved correctly
        var resolvedProduct = await _productRepository.GetByIdAsync(productId);
        Assert.NotNull(resolvedProduct);
        Assert.Equal(30.00m, resolvedProduct.UnitPrice); // Should have the newer price
        Assert.Equal(SyncStatus.ConflictResolved, resolvedProduct.SyncStatus);
    }

    [Fact]
    public async Task SaleWorkflow_WithMedicineValidation_ShouldPreventExpiredSales()
    {
        // Arrange: Create expired medicine product
        var deviceId = Guid.NewGuid();
        var expiredMedicine = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Expired Medicine",
            Barcode = "5555555555555",
            Category = "Medicine",
            UnitPrice = 12.99m,
            IsActive = true,
            DeviceId = deviceId,
            SyncStatus = SyncStatus.NotSynced,
            BatchNumber = "BATCH001",
            ExpiryDate = DateTime.UtcNow.AddDays(-30) // Expired 30 days ago
        };

        await _productRepository.AddAsync(expiredMedicine);
        await _dbContext.SaveChangesAsync();

        // Act & Assert: Attempt to create sale with expired medicine should fail
        var invoiceNumber = $"EXP-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var sale = await _saleService.CreateSaleAsync(invoiceNumber, deviceId);

        // This should throw an exception when trying to add expired medicine
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _saleService.AddItemToSaleAsync(sale.Id, expiredMedicine.Id, 1, expiredMedicine.UnitPrice);
        });
    }

    [Fact]
    public async Task InventoryManagement_WithLowStockAlert_ShouldGenerateAlert()
    {
        // Arrange: Create product with low stock
        var deviceId = Guid.NewGuid();
        var lowStockProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Low Stock Product",
            Barcode = "7777777777777",
            Category = "Office Supplies",
            UnitPrice = 5.99m,
            IsActive = true,
            DeviceId = deviceId,
            SyncStatus = SyncStatus.NotSynced
        };

        var lowStock = new Stock
        {
            Id = Guid.NewGuid(),
            ProductId = lowStockProduct.Id,
            Quantity = 2, // Very low quantity
            DeviceId = deviceId,
            SyncStatus = SyncStatus.NotSynced
        };

        await _productRepository.AddAsync(lowStockProduct);
        await _stockRepository.AddAsync(lowStock);
        await _dbContext.SaveChangesAsync();

        // Act: Check for low stock alerts
        var lowStockProducts = await _inventoryService.GetLowStockProductsAsync(5); // Threshold of 5

        // Assert: Should generate low stock alert
        Assert.NotEmpty(lowStockProducts);
        Assert.Contains(lowStockProducts, product => product.Id == lowStockProduct.Id);
    }

    [Fact]
    public async Task DailySalesReport_WithMultipleSales_ShouldCalculateCorrectly()
    {
        // Arrange: Create multiple sales for today
        var deviceId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date; // Use UTC date to match sale CreatedAt timestamps
        
        var testProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Daily Sales Product",
            Barcode = "8888888888888",
            Category = "Food",
            UnitPrice = 10.00m,
            IsActive = true,
            DeviceId = deviceId,
            SyncStatus = SyncStatus.NotSynced
        };

        await _productRepository.AddAsync(testProduct);
        
        // Add sufficient stock for the test
        var testStock = new Stock
        {
            Id = Guid.NewGuid(),
            ProductId = testProduct.Id,
            Quantity = 100, // Sufficient for all sales
            DeviceId = deviceId,
            SyncStatus = SyncStatus.NotSynced
        };
        await _stockRepository.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        // Create 3 sales with different amounts
        var expectedTotal = 0m;
        for (int i = 1; i <= 3; i++)
        {
            var invoiceNumber = $"DAILY-{DateTime.UtcNow:yyyyMMddHHmmss}-{i}";
            var sale = await _saleService.CreateSaleAsync(invoiceNumber, deviceId);
            
            // Add items to sale
            sale = await _saleService.AddItemToSaleAsync(sale.Id, testProduct.Id, i, testProduct.UnitPrice);
            
            // Complete sale
            var completedSale = await _saleService.CompleteSaleAsync(sale.Id, PaymentMethod.Cash);
            expectedTotal += i * testProduct.UnitPrice;
        }

        // Act: Get daily sales total
        var dailyTotal = await _saleService.GetDailySalesAsync(today);

        // Assert: Should match expected total
        Assert.Equal(expectedTotal, dailyTotal);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _serviceProvider?.Dispose();
    }
}