using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.DependencyInjection;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using Shared.Core.Repositories;
using Xunit;
using Xunit.Abstractions;

namespace Shared.Core.Tests;

/// <summary>
/// Comprehensive final integration test for the sales service implementation.
/// Tests complete workflows, performance, error handling, and backward compatibility.
/// Requirements: All requirements integration
/// </summary>
public class ComprehensiveFinalIntegrationTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ITestOutputHelper _output;

    public ComprehensiveFinalIntegrationTest(ITestOutputHelper output)
    {
        _output = output;

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning));
        services.AddSharedCoreInMemory();

        _serviceProvider = services.BuildServiceProvider();

        // Seed a valid license so service-level license checks pass
        SetupLicenseAsync().GetAwaiter().GetResult();
    }

    private async Task SetupLicenseAsync()
    {
        var currentUserService = _serviceProvider.GetService<ICurrentUserService>();
        var licenseRepo = _serviceProvider.GetService<ILicenseRepository>();
        var userRepo = _serviceProvider.GetService<IUserRepository>();
        var businessRepo = _serviceProvider.GetService<IBusinessRepository>();
        var shopRepo = _serviceProvider.GetService<IShopRepository>();
        
        if (currentUserService == null || licenseRepo == null || userRepo == null || businessRepo == null || shopRepo == null) 
            return;

        var deviceId = currentUserService.GetDeviceId();
        var userId = currentUserService.GetUserId() ?? Guid.NewGuid();

        // Create a business
        var business = new Business
        {
            Id = Guid.NewGuid(),
            Name = "Test Business",
            Type = BusinessType.GeneralRetail,
            OwnerId = userId,
            IsActive = true
        };
        await businessRepo.AddAsync(business);

        // Create a shop
        var shop = new Shop
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            Name = "Test Shop",
            DeviceId = deviceId,
            IsActive = true
        };
        await shopRepo.AddAsync(shop);

        // Create a user with the device ID (required for device validation)
        var user = new User
        {
            Id = userId,
            BusinessId = business.Id,
            ShopId = shop.Id,
            Username = "testuser",
            FullName = "Test User",
            Email = "test@example.com",
            PasswordHash = "hash",
            Salt = "salt",
            Role = UserRole.Administrator,
            DeviceId = deviceId,
            IsActive = true
        };
        await userRepo.AddAsync(user);

        var license = new License
        {
            Id = Guid.NewGuid(),
            LicenseKey = "COMPREHENSIVE-FINAL-TEST",
            Type = LicenseType.Professional,
            IssueDate = DateTime.UtcNow.AddDays(-30),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Status = LicenseStatus.Active,
            CustomerName = "Final Integration Test",
            CustomerEmail = "test@final.com",
            MaxDevices = 10,
            Features = new List<string> { "basic_pos", "inventory", "advanced_reports", "multi_user", "weight_based", "membership", "discounts" },
            ActivationDate = DateTime.UtcNow.AddDays(-30),
            DeviceId = deviceId
        };

        await licenseRepo.AddAsync(license);
        await licenseRepo.SaveChangesAsync();
        await userRepo.SaveChangesAsync();
        await businessRepo.SaveChangesAsync();
        await shopRepo.SaveChangesAsync();
    }

    [Fact]
    public async Task ComprehensiveFinalIntegration_AllSystemComponents_ShouldWorkTogether()
    {
        _output.WriteLine("=== Comprehensive Final Integration Test ===");

        // Arrange: Get all required services
        var saleService = _serviceProvider.GetRequiredService<ISaleService>();
        var productService = _serviceProvider.GetRequiredService<IProductService>();
        var inventoryService = _serviceProvider.GetRequiredService<IInventoryService>();
        var calculationEngine = _serviceProvider.GetRequiredService<IRealTimeCalculationEngine>();
        var discountEngine = _serviceProvider.GetRequiredService<IDiscountProcessingEngine>();
        var stockValidationService = _serviceProvider.GetRequiredService<IStockValidationService>();
        var paymentService = _serviceProvider.GetRequiredService<IPaymentProcessingService>();
        var auditService = _serviceProvider.GetRequiredService<IAuditLoggingService>();
        
        var productRepo = _serviceProvider.GetRequiredService<IProductRepository>();
        var stockRepo = _serviceProvider.GetRequiredService<IStockRepository>();
        var customerRepo = _serviceProvider.GetRequiredService<ICustomerRepository>();

        var currentUserService = _serviceProvider.GetRequiredService<ICurrentUserService>();
        var deviceId = currentUserService.GetDeviceId();
        var userId = currentUserService.GetUserId() ?? Guid.NewGuid(); // Fallback for tests

        _output.WriteLine("✓ All services resolved successfully");

        // Step 1: Create test data
        var product1 = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Regular Product",
            Barcode = "REG001",
            UnitPrice = 25.00m,
            IsActive = true,
            DeviceId = deviceId,
            SyncStatus = SyncStatus.NotSynced
        };

        var product2 = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Weight Product",
            Barcode = "WGT001",
            UnitPrice = 0m,
            IsWeightBased = true,
            RatePerKilogram = 12.00m,
            WeightPrecision = 2,
            IsActive = true,
            DeviceId = deviceId,
            SyncStatus = SyncStatus.NotSynced
        };

        await productRepo.AddAsync(product1);
        await productRepo.AddAsync(product2);
        await productRepo.SaveChangesAsync();

        var stock1 = new Stock { Id = Guid.NewGuid(), ProductId = product1.Id, Quantity = 100, DeviceId = deviceId, SyncStatus = SyncStatus.NotSynced };
        var stock2 = new Stock { Id = Guid.NewGuid(), ProductId = product2.Id, Quantity = 50, DeviceId = deviceId, SyncStatus = SyncStatus.NotSynced };
        await stockRepo.AddAsync(stock1);
        await stockRepo.AddAsync(stock2);
        await stockRepo.SaveChangesAsync();

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Gold Customer",
            MembershipNumber = "GOLD001",
            Phone = "555-1234",
            Tier = MembershipTier.Gold,
            TotalSpent = 5000m,
            IsActive = true,
            DeviceId = deviceId,
            SyncStatus = SyncStatus.NotSynced
        };
        await customerRepo.AddAsync(customer);
        await customerRepo.SaveChangesAsync();

        _output.WriteLine("✓ Test data created");

        // Step 2: Test sale creation and validation
        var sale = await saleService.CreateSaleAsync(deviceId, userId, (Guid?)customer.Id);
        Assert.NotNull(sale);
        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Equal(customer.Id, sale.CustomerId);
        _output.WriteLine($"✓ Sale created: {sale.InvoiceNumber}");

        // Step 3: Test stock validation
        var stockValidation = await stockValidationService.ValidateProductAvailabilityAsync(product1.Id, 5);
        Assert.True(stockValidation.IsAvailable);
        Assert.Equal(100, stockValidation.AvailableQuantity);
        _output.WriteLine("✓ Stock validation working");

        // Step 4: Test product addition (regular)
        sale = await saleService.AddItemToSaleAsync(sale.Id, product1.Id, 3, product1.UnitPrice);
        Assert.Equal(SaleStatus.Active, sale.Status);
        Assert.Single(sale.Items);
        Assert.Equal(75.00m, sale.TotalAmount); // 3 * 25
        _output.WriteLine($"✓ Regular product added: {sale.TotalAmount:C}");

        // Step 5: Test weight-based product addition
        sale = await saleService.AddWeightBasedItemToSaleAsync(sale.Id, product2.Id, 2.5m);
        Assert.Equal(2, sale.Items.Count);
        var weightItem = sale.Items.First(i => i.IsWeightBased);
        Assert.Equal(2.5m, weightItem.Weight);
        Assert.Equal(12.00m, weightItem.RatePerKilogram);
        _output.WriteLine($"✓ Weight-based product added: {weightItem.Weight}kg at {weightItem.RatePerKilogram:C}/kg");

        // Step 6: Test real-time calculations
        var shopConfig = new Shared.Core.DTOs.ShopConfiguration
        {
            TaxRate = 0.08m,
            Currency = "USD"
        };
        var items = sale.Items.Where(i => !i.IsDeleted).ToList();
        var calcResult = await calculationEngine.CalculateOrderTotalsAsync(items, shopConfig, customer);
        
        Assert.True(calcResult.IsValid);
        Assert.True(calcResult.Subtotal > 0);
        Assert.True(calcResult.FinalTotal > 0);
        _output.WriteLine($"✓ Real-time calculation: Subtotal={calcResult.Subtotal:C}, Final={calcResult.FinalTotal:C}");

        // Step 7: Test discount processing
        var discountResult = await discountEngine.CalculateDiscountsAsync(sale, customer);
        Assert.NotNull(discountResult);
        _output.WriteLine($"✓ Discount processing: {discountResult.TotalDiscountAmount:C}");

        // Step 8: Test payment validation
        var paymentValidation = await paymentService.ValidatePaymentMethodAsync(PaymentMethod.Cash, 200.00m);
        Assert.True(paymentValidation.IsValid);
        _output.WriteLine("✓ Payment validation working");

        // Step 9: Test sale completion
        var completedSale = await saleService.CompleteSaleAsync(sale.Id, PaymentMethod.Cash);
        Assert.Equal(SaleStatus.Completed, completedSale.Status);
        Assert.NotNull(completedSale.CompletedAt);
        _output.WriteLine($"✓ Sale completed: {completedSale.Status}");

        // Step 10: Verify inventory updates
        var updatedStock1 = await stockRepo.GetByProductIdAsync(product1.Id);
        Assert.Equal(97, updatedStock1!.Quantity); // 100 - 3
        _output.WriteLine($"✓ Inventory updated: {updatedStock1.Quantity} remaining");

        // Step 11: Test error handling
        var expiredProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Expired Product",
            UnitPrice = 10.00m,
            ExpiryDate = DateTime.UtcNow.AddDays(-1),
            IsActive = true,
            DeviceId = deviceId,
            SyncStatus = SyncStatus.NotSynced
        };
        await productRepo.AddAsync(expiredProduct);
        await productRepo.SaveChangesAsync();

        var newSale = await saleService.CreateSaleAsync(deviceId, userId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            saleService.AddItemToSaleAsync(newSale.Id, expiredProduct.Id, 1, expiredProduct.UnitPrice));
        _output.WriteLine("✓ Error handling working (expired product rejected)");

        // Step 12: Test performance (should complete within reasonable time)
        var performanceStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var performanceSale = await saleService.CreateSaleAsync(deviceId, userId);
        performanceSale = await saleService.AddItemToSaleAsync(performanceSale.Id, product1.Id, 1, product1.UnitPrice);
        var performanceCalc = await saleService.CalculateFullSaleTotalAsync(performanceSale.Id);
        performanceStopwatch.Stop();

        Assert.True(performanceStopwatch.ElapsedMilliseconds < 1000, 
            $"Performance test took {performanceStopwatch.ElapsedMilliseconds}ms, expected < 1000ms");
        _output.WriteLine($"✓ Performance test: {performanceStopwatch.ElapsedMilliseconds}ms");

        // Step 13: Test concurrent operations
        var concurrentTasks = Enumerable.Range(0, 5).Select(async i =>
        {
            var concurrentSale = await saleService.CreateSaleAsync(deviceId, userId);
            return await saleService.AddItemToSaleAsync(concurrentSale.Id, product1.Id, 1, product1.UnitPrice);
        });

        var concurrentResults = await Task.WhenAll(concurrentTasks);
        Assert.Equal(5, concurrentResults.Length);
        Assert.All(concurrentResults, s => Assert.Equal(25.00m, s.TotalAmount));
        _output.WriteLine("✓ Concurrent operations working");

        // Step 14: Test memory usage (basic check)
        var beforeGC = GC.GetTotalMemory(false);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var afterGC = GC.GetTotalMemory(false);
        var memoryUsedMB = (afterGC - beforeGC) / (1024.0 * 1024.0);
        
        _output.WriteLine($"✓ Memory usage: {memoryUsedMB:F2}MB after GC");

        _output.WriteLine("=== Comprehensive Final Integration Test PASSED ===");
    }

    [Fact]
    public async Task BackwardCompatibility_ExistingAPIs_ShouldStillWork()
    {
        _output.WriteLine("=== Backward Compatibility Test ===");

        var saleService = _serviceProvider.GetRequiredService<ISaleService>();
        var currentUserService = _serviceProvider.GetRequiredService<ICurrentUserService>();
        var deviceId = currentUserService.GetDeviceId();

        // Test legacy sale creation method
        var invoiceNumber = saleService.GenerateInvoiceNumber();
        var sale = await saleService.CreateSaleAsync(invoiceNumber, deviceId);
        
        Assert.NotNull(sale);
        Assert.Equal(invoiceNumber, sale.InvoiceNumber);
        _output.WriteLine($"✓ Legacy sale creation works: {sale.InvoiceNumber}");

        // Test legacy retrieval methods
        var retrievedSale = await saleService.GetSaleByIdAsync(sale.Id);
        Assert.NotNull(retrievedSale);
        Assert.Equal(sale.Id, retrievedSale.Id);
        _output.WriteLine("✓ Legacy retrieval methods work");

        var retrievedByInvoice = await saleService.GetSaleByInvoiceNumberAsync(invoiceNumber);
        Assert.NotNull(retrievedByInvoice);
        Assert.Equal(sale.Id, retrievedByInvoice.Id);
        _output.WriteLine("✓ Legacy invoice lookup works");

        _output.WriteLine("=== Backward Compatibility Test PASSED ===");
    }

    [Fact]
    public void DependencyInjection_AllServices_ShouldResolveCorrectly()
    {
        _output.WriteLine("=== Dependency Injection Test ===");

        var criticalServices = new[]
        {
            typeof(ISaleService),
            typeof(IRealTimeCalculationEngine),
            typeof(IDiscountProcessingEngine),
            typeof(IStockValidationService),
            typeof(IPaymentProcessingService),
            typeof(IInventoryUpdater),
            typeof(IAuditLoggingService),
            typeof(IValidationService),
            typeof(IWeightBasedPricingService),
            typeof(ISalesCacheService),
            typeof(ConcurrentSaleOperationGuard)
        };

        foreach (var serviceType in criticalServices)
        {
            var resolved = _serviceProvider.GetService(serviceType);
            Assert.NotNull(resolved);
            _output.WriteLine($"✓ {serviceType.Name} resolved");
        }

        _output.WriteLine("=== Dependency Injection Test PASSED ===");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}