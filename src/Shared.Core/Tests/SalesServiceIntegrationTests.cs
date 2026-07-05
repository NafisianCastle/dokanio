using Microsoft.Extensions.DependencyInjection;
using Shared.Core.Data;
using Shared.Core.DependencyInjection;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Shared.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace Shared.Core.Tests;

/// <summary>
/// Comprehensive integration tests for the Sales Service implementation.
/// Tests complete sale workflows from creation to completion, cross-service integration,
/// concurrent operations, and error handling across service boundaries.
/// 
/// Requirements covered:
/// - Requirement 1: Sale Creation and Management
/// - Requirement 2: Product Addition and Item Management
/// - Requirement 3: Real-Time Calculation Engine
/// - Requirement 4: Discount and Membership Processing
/// - Requirement 5: Weight-Based Product Handling
/// - Requirement 6: Sale Completion and Payment Processing
/// - Requirement 7: Stock Validation and Inventory Integration
/// - Requirement 8: Error Handling and Data Integrity
/// - Requirement 9: Performance and Scalability
/// </summary>
public class SalesServiceIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _dbContext;
    private readonly ISaleService _saleService;
    private readonly IRealTimeCalculationEngine _calculationEngine;
    private readonly IDiscountProcessingEngine _discountEngine;
    private readonly IStockValidationService _stockValidationService;
    private readonly IPaymentProcessingService _paymentService;
    private readonly IInventoryUpdater _inventoryUpdater;
    private readonly IProductRepository _productRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IStockRepository _stockRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ITestOutputHelper _output;

    // Shared test data IDs
    private readonly Guid _businessId;
    private readonly Guid _shopId;
    private readonly Guid _userId;
    private readonly Guid _deviceId;

    public SalesServiceIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        _serviceProvider = services.BuildServiceProvider();

        _dbContext = _serviceProvider.GetRequiredService<PosDbContext>();
        _saleService = _serviceProvider.GetRequiredService<ISaleService>();
        _calculationEngine = _serviceProvider.GetRequiredService<IRealTimeCalculationEngine>();
        _discountEngine = _serviceProvider.GetRequiredService<IDiscountProcessingEngine>();
        _stockValidationService = _serviceProvider.GetRequiredService<IStockValidationService>();
        _paymentService = _serviceProvider.GetRequiredService<IPaymentProcessingService>();
        _inventoryUpdater = _serviceProvider.GetRequiredService<IInventoryUpdater>();
        _productRepository = _serviceProvider.GetRequiredService<IProductRepository>();
        _saleRepository = _serviceProvider.GetRequiredService<ISaleRepository>();
        _stockRepository = _serviceProvider.GetRequiredService<IStockRepository>();
        _customerRepository = _serviceProvider.GetRequiredService<ICustomerRepository>();

        _businessId = Guid.NewGuid();
        _shopId = Guid.NewGuid();
        _userId = Guid.NewGuid();
        _deviceId = Guid.NewGuid();

        SeedTestData().GetAwaiter().GetResult();
    }

    private async Task SeedTestData()
    {
        // Create business
        var business = new Business
        {
            Id = _businessId,
            Name = "Test Business",
            Type = BusinessType.GeneralRetail,
            OwnerId = _userId,
            IsActive = true
        };
        _dbContext.Businesses.Add(business);

        // Create shop
        var shop = new Shop
        {
            Id = _shopId,
            BusinessId = _businessId,
            Name = "Test Shop",
            Address = "123 Test Street",
            Phone = "555-0100",
            DeviceId = _deviceId,
            IsActive = true
        };
        _dbContext.Shops.Add(shop);

        // Create user
        var user = new User
        {
            Id = _userId,
            BusinessId = _businessId,
            ShopId = _shopId,
            Username = "testcashier",
            FullName = "Test Cashier",
            Email = "cashier@test.com",
            PasswordHash = "hash",
            Salt = "salt",
            Role = UserRole.Cashier,
            DeviceId = _deviceId,
            IsActive = true
        };
        _dbContext.Users.Add(user);

        // Seed license for device validation
        var currentUserService = _serviceProvider.GetRequiredService<ICurrentUserService>();
        var licenseDeviceId = currentUserService.GetDeviceId();
        var license = new License
        {
            Id = Guid.NewGuid(),
            LicenseKey = "TEST-LICENSE-INTEGRATION-001",
            Type = LicenseType.Professional,
            Status = LicenseStatus.Active,
            DeviceId = licenseDeviceId,
            CustomerName = "Test Customer",
            CustomerEmail = "test@example.com",
            IssueDate = DateTime.UtcNow.AddDays(-30),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            ActivationDate = DateTime.UtcNow.AddDays(-30)
        };
        _dbContext.Licenses.Add(license);

        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Helper to create a product with stock for testing.
    /// </summary>
    private async Task<(Product product, Stock stock)> CreateProductWithStockAsync(
        string name,
        decimal unitPrice,
        int stockQty,
        bool isActive = true,
        DateTime? expiryDate = null,
        bool isWeightBased = false,
        decimal? ratePerKg = null)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            ShopId = _shopId,
            Name = name,
            Barcode = Guid.NewGuid().ToString("N").Substring(0, 13),
            UnitPrice = unitPrice,
            IsActive = isActive,
            ExpiryDate = expiryDate,
            IsWeightBased = isWeightBased,
            RatePerKilogram = ratePerKg,
            DeviceId = _deviceId,
            SyncStatus = SyncStatus.NotSynced
        };
        _dbContext.Products.Add(product);

        var stock = new Stock
        {
            Id = Guid.NewGuid(),
            ShopId = _shopId,
            ProductId = product.Id,
            Quantity = stockQty,
            DeviceId = _deviceId,
            SyncStatus = SyncStatus.NotSynced
        };
        _dbContext.Stock.Add(stock);

        await _dbContext.SaveChangesAsync();
        return (product, stock);
    }

    /// <summary>
    /// Helper to create a customer with membership for testing.
    /// </summary>
    private async Task<Customer> CreateCustomerAsync(
        string name,
        MembershipTier tier = MembershipTier.Silver,
        bool isActive = true)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = name,
            Phone = "555-" + Guid.NewGuid().ToString("N").Substring(0, 4),
            MembershipNumber = "MEM-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
            Tier = tier,
            IsActive = isActive,
            DeviceId = _deviceId,
            SyncStatus = SyncStatus.NotSynced
        };
        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();
        return customer;
    }

    // =========================================================================
    // Complete Sale Workflow Tests (Requirements 1-6)
    // =========================================================================

    /// <summary>
    /// Tests the complete sale workflow from creation to completion.
    /// Validates: Requirements 1.1, 2.1, 3.1, 6.1, 6.2, 6.3, 6.4
    /// </summary>
    [Fact]
    public async Task CompleteSaleWorkflow_FromCreationToCompletion_ShouldSucceed()
    {
        // Arrange: Create products with stock
        var (product1, stock1) = await CreateProductWithStockAsync("Product A", 25.00m, 100);
        var (product2, stock2) = await CreateProductWithStockAsync("Product B", 15.00m, 50);

        // Act 1: Create sale
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        Assert.NotNull(sale);
        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Empty(sale.Items);
        _output.WriteLine($"Step 1: Created sale {sale.InvoiceNumber} with Draft status");

        // Act 2: Add first product
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product1.Id, 2, product1.UnitPrice);
        Assert.Equal(SaleStatus.Active, sale.Status);
        Assert.Single(sale.Items);
        Assert.Equal(50.00m, sale.TotalAmount);
        _output.WriteLine($"Step 2: Added 2x Product A. Total: {sale.TotalAmount:C}");

        // Act 3: Add second product
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product2.Id, 3, product2.UnitPrice);
        Assert.Equal(2, sale.Items.Count);
        Assert.Equal(95.00m, sale.TotalAmount); // 50 + 45
        _output.WriteLine($"Step 3: Added 3x Product B. Total: {sale.TotalAmount:C}");

        // Act 4: Complete the sale
        var completedSale = await _saleService.CompleteSaleAsync(sale.Id, PaymentMethod.Cash);
        Assert.Equal(SaleStatus.Completed, completedSale.Status);
        Assert.NotNull(completedSale.CompletedAt);
        Assert.Equal(PaymentMethod.Cash, completedSale.PaymentMethod);
        _output.WriteLine($"Step 4: Completed sale. Status: {completedSale.Status}");

        // Assert: Verify inventory was updated
        var updatedStock1 = await _stockRepository.GetByProductIdAsync(product1.Id);
        var updatedStock2 = await _stockRepository.GetByProductIdAsync(product2.Id);
        Assert.Equal(98, updatedStock1!.Quantity); // 100 - 2
        Assert.Equal(47, updatedStock2!.Quantity); // 50 - 3
        _output.WriteLine($"Step 5: Verified inventory. Product A: {updatedStock1.Quantity}, Product B: {updatedStock2.Quantity}");
    }

    /// <summary>
    /// Tests complete sale workflow with customer and membership discount.
    /// Validates: Requirements 1.4, 4.1, 4.2, 6.6
    /// </summary>
    [Fact]
    public async Task CompleteSaleWorkflow_WithMembershipCustomer_ShouldApplyDiscounts()
    {
        // Arrange: Create product and Gold member customer
        var (product, stock) = await CreateProductWithStockAsync("Premium Product", 100.00m, 50);
        var customer = await CreateCustomerAsync("Gold Member", MembershipTier.Gold);

        // Act 1: Create sale with customer
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId, customer.Id);
        Assert.Equal(customer.Id, sale.CustomerId);
        _output.WriteLine($"Step 1: Created sale for Gold member {customer.Name}");

        // Act 2: Add product
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 1, product.UnitPrice);
        Assert.Equal(100.00m, sale.TotalAmount);
        _output.WriteLine($"Step 2: Added product. Subtotal: {sale.TotalAmount:C}");

        // Act 3: Calculate full sale total (includes membership discount)
        var calculation = await _saleService.CalculateFullSaleTotalAsync(sale.Id);
        Assert.True(calculation.MembershipDiscountAmount > 0, "Gold member should receive membership discount");
        _output.WriteLine($"Step 3: Calculated totals. Membership Discount: {calculation.MembershipDiscountAmount:C}, Final: {calculation.FinalTotal:C}");

        // Act 4: Complete sale
        var completedSale = await _saleService.CompleteSaleAsync(sale.Id, PaymentMethod.Card);
        Assert.Equal(SaleStatus.Completed, completedSale.Status);
        _output.WriteLine($"Step 4: Completed sale with card payment");
    }

    /// <summary>
    /// Tests complete sale workflow with weight-based products.
    /// Validates: Requirements 5.1, 5.2, 5.3, 5.4, 5.5
    /// </summary>
    [Fact]
    public async Task CompleteSaleWorkflow_WithWeightBasedProducts_ShouldCalculateCorrectly()
    {
        // Arrange: Create weight-based product
        var (product, stock) = await CreateProductWithStockAsync(
            "Rice (per kg)", 0m, 100, isWeightBased: true, ratePerKg: 5.00m);

        // Act 1: Create sale
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        _output.WriteLine($"Step 1: Created sale {sale.InvoiceNumber}");

        // Act 2: Add weight-based item
        sale = await _saleService.AddWeightBasedItemToSaleAsync(sale.Id, product.Id, 2.5m); // 2.5 kg
        Assert.Single(sale.Items);
        var item = sale.Items.First();
        Assert.True(item.IsWeightBased);
        Assert.Equal(2.5m, item.Weight);
        Assert.Equal(5.00m, item.RatePerKilogram);
        _output.WriteLine($"Step 2: Added 2.5kg at $5/kg. Line total: {item.LineSubtotal:C}");

        // Act 3: Update weight
        sale = await _saleService.UpdateItemWeightAsync(sale.Id, item.Id, 3.0m);
        var updatedItem = sale.Items.First(i => i.Id == item.Id);
        Assert.Equal(3.0m, updatedItem.Weight);
        Assert.Equal(15.00m, updatedItem.LineSubtotal); // 3kg * $5/kg
        _output.WriteLine($"Step 3: Updated weight to 3kg. New line total: {updatedItem.LineSubtotal:C}");

        // Act 4: Complete sale
        var completedSale = await _saleService.CompleteSaleAsync(sale.Id, PaymentMethod.Cash);
        Assert.Equal(SaleStatus.Completed, completedSale.Status);
        _output.WriteLine($"Step 4: Completed sale. Final total: {completedSale.FinalTotal:C}");
    }

    // =========================================================================
    // Cross-Service Integration Tests (Requirements 3, 4, 7)
    // =========================================================================

    /// <summary>
    /// Tests integration between SaleService, CalculationEngine, and DiscountEngine.
    /// Validates: Requirements 3.1, 3.2, 4.1, 4.3
    /// </summary>
    [Fact]
    public async Task CrossServiceIntegration_CalculationAndDiscount_ShouldWorkTogether()
    {
        // Arrange: Create products and customer
        var (product1, _) = await CreateProductWithStockAsync("Item 1", 50.00m, 20);
        var (product2, _) = await CreateProductWithStockAsync("Item 2", 30.00m, 20);
        var customer = await CreateCustomerAsync("Silver Member", MembershipTier.Silver);

        // Act 1: Create sale and add items
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId, customer.Id);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product1.Id, 2, product1.UnitPrice);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product2.Id, 1, product2.UnitPrice);

        // Act 2: Calculate discounts via DiscountEngine
        var discountResult = await _discountEngine.CalculateDiscountsAsync(sale, customer);
        Assert.True(discountResult.TotalDiscountAmount > 0, "Silver member should get discount");
        _output.WriteLine($"Discount engine calculated: {discountResult.TotalDiscountAmount:C} discount");

        // Act 3: Get full calculation via CalculationEngine
        var shopConfig = new Shared.Core.DTOs.ShopConfiguration
        {
            TaxRate = 0.08m,
            Currency = "USD"
        };
        var items = sale.Items.Where(i => !i.IsDeleted).ToList();
        var calcResult = await _calculationEngine.CalculateOrderTotalsAsync(items, shopConfig, customer);

        Assert.True(calcResult.IsValid);
        Assert.Equal(130.00m, calcResult.Subtotal); // 2*50 + 1*30
        Assert.True(calcResult.TotalDiscountAmount > 0);
        Assert.True(calcResult.TotalTaxAmount > 0);
        _output.WriteLine($"Order totals: Subtotal={calcResult.Subtotal:C}, Discount={calcResult.TotalDiscountAmount:C}, Tax={calcResult.TotalTaxAmount:C}, Final={calcResult.FinalTotal:C}");
    }

    /// <summary>
    /// Tests integration between SaleService and StockValidationService.
    /// Validates: Requirements 2.2, 7.1, 7.2, 7.3
    /// </summary>
    [Fact]
    public async Task CrossServiceIntegration_StockValidation_ShouldPreventOverselling()
    {
        // Arrange: Create product with limited stock
        var (product, stock) = await CreateProductWithStockAsync("Limited Item", 10.00m, 5);

        // Act 1: Verify stock availability
        var availability = await _stockValidationService.ValidateProductAvailabilityAsync(product.Id, 3);
        Assert.True(availability.IsAvailable);
        Assert.Equal(5, availability.AvailableQuantity);
        _output.WriteLine($"Stock check: Requested 3, Available {availability.AvailableQuantity}");

        // Act 2: Create sale and add items (should succeed)
        var sale1 = await _saleService.CreateSaleAsync(_deviceId, _userId);
        sale1 = await _saleService.AddItemToSaleAsync(sale1.Id, product.Id, 3, product.UnitPrice);
        Assert.Single(sale1.Items);
        _output.WriteLine($"Sale 1: Added 3 items. Remaining available: 2");

        // Act 3: Try to add more than available in another sale (should fail)
        var sale2 = await _saleService.CreateSaleAsync(_deviceId, _userId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _saleService.AddItemToSaleAsync(sale2.Id, product.Id, 3, product.UnitPrice));
        _output.WriteLine($"Sale 2: Correctly prevented adding 3 items (only 2 available)");

        // Act 4: Add remaining stock to second sale
        sale2 = await _saleService.AddItemToSaleAsync(sale2.Id, product.Id, 2, product.UnitPrice);
        Assert.Single(sale2.Items);
        _output.WriteLine($"Sale 2: Added 2 items successfully");
    }

    /// <summary>
    /// Tests integration between SaleService, PaymentService, and InventoryUpdater.
    /// Validates: Requirements 6.1, 6.2, 6.3, 6.6
    /// </summary>
    [Fact]
    public async Task CrossServiceIntegration_PaymentAndInventory_ShouldUpdateCorrectly()
    {
        // Arrange: Create product and customer
        var (product, stock) = await CreateProductWithStockAsync("Inventory Test Product", 20.00m, 100);
        var customer = await CreateCustomerAsync("Test Customer", MembershipTier.Bronze);

        // Act 1: Create and populate sale
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId, customer.Id);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 5, product.UnitPrice);
        _output.WriteLine($"Created sale with 5 items. Subtotal: {sale.TotalAmount:C}");

        // Act 2: Validate payment method
        var paymentValidation = await _paymentService.ValidatePaymentMethodAsync(PaymentMethod.Cash, 100.00m);
        Assert.True(paymentValidation.IsValid);
        _output.WriteLine($"Payment method validated: {paymentValidation.IsValid}");

        // Act 3: Complete sale
        var completedSale = await _saleService.CompleteSaleAsync(sale.Id, PaymentMethod.Cash);
        Assert.Equal(SaleStatus.Completed, completedSale.Status);
        _output.WriteLine($"Sale completed. Status: {completedSale.Status}");

        // Act 4: Verify inventory was reduced
        var updatedStock = await _stockRepository.GetByProductIdAsync(product.Id);
        Assert.Equal(95, updatedStock!.Quantity); // 100 - 5
        _output.WriteLine($"Inventory verified: 100 -> {updatedStock.Quantity}");
    }

    // =========================================================================
    // Concurrent Operations Tests (Requirements 9.5, 9.6)
    // =========================================================================

    /// <summary>
    /// Tests concurrent sale creation and item addition.
    /// Validates: Requirements 9.5, 9.6
    /// </summary>
    [Fact]
    public async Task ConcurrentOperations_MultipleSales_ShouldNotInterfere()
    {
        // Arrange: Create products with sufficient stock
        var (product1, _) = await CreateProductWithStockAsync("Concurrent Product 1", 10.00m, 100);
        var (product2, _) = await CreateProductWithStockAsync("Concurrent Product 2", 15.00m, 100);

        // Act: Create multiple sales concurrently
        var saleTasks = Enumerable.Range(0, 5).Select(async i =>
        {
            var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
            sale = await _saleService.AddItemToSaleAsync(sale.Id, product1.Id, 2, product1.UnitPrice);
            sale = await _saleService.AddItemToSaleAsync(sale.Id, product2.Id, 1, product2.UnitPrice);
            return sale;
        });

        var sales = await Task.WhenAll(saleTasks);

        // Assert: All sales should be created successfully with correct totals
        Assert.Equal(5, sales.Length);
        Assert.All(sales, sale =>
        {
            Assert.Equal(35.00m, sale.TotalAmount); // 2*10 + 1*15
            Assert.Equal(2, sale.Items.Count);
        });
        _output.WriteLine($"Created {sales.Length} concurrent sales successfully");

        // Verify all invoice numbers are unique
        var invoiceNumbers = sales.Select(s => s.InvoiceNumber).ToList();
        Assert.Equal(5, invoiceNumbers.Distinct().Count());
        _output.WriteLine("All invoice numbers are unique");
    }

    /// <summary>
    /// Tests concurrent stock access from multiple sales.
    /// Validates: Requirements 7.3, 9.5
    /// </summary>
    [Fact]
    public async Task ConcurrentOperations_StockAccess_ShouldBeThreadSafe()
    {
        // Arrange: Create product with limited stock
        var (product, _) = await CreateProductWithStockAsync("Limited Stock Product", 10.00m, 20);

        // Act: Try to create 10 sales, each trying to add 3 items (total 30, but only 20 available)
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            try
            {
                var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
                sale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 3, product.UnitPrice);
                return (Success: true, Sale: sale);
            }
            catch (InvalidOperationException)
            {
                // Expected when stock runs out
                return (Success: false, Sale: (Sale?)null);
            }
        });

        var results = await Task.WhenAll(tasks);

        // Assert: Only some sales should succeed (20 items / 3 per sale = 6 max)
        var successfulSales = results.Count(r => r.Success);
        Assert.True(successfulSales >= 6 && successfulSales <= 7,
            $"Expected 6-7 successful sales, got {successfulSales}");
        _output.WriteLine($"{successfulSales} sales succeeded out of 10 attempts (stock: 20, requested per sale: 3)");
    }

    // =========================================================================
    // Error Handling Tests (Requirements 8.1, 8.3, 8.4)
    // =========================================================================

    /// <summary>
    /// Tests error handling when adding expired products.
    /// Validates: Requirements 2.4, 8.1
    /// </summary>
    [Fact]
    public async Task ErrorHandling_ExpiredProduct_ShouldProvideClearError()
    {
        // Arrange: Create expired product
        var (product, _) = await CreateProductWithStockAsync(
            "Expired Medicine", 15.00m, 10, expiryDate: DateTime.UtcNow.AddDays(-1));

        // Act & Assert: Should throw with clear message
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _saleService.AddItemToSaleAsync(sale.Id, product.Id, 1, product.UnitPrice));

        Assert.Contains("expired", exception.Message, StringComparison.OrdinalIgnoreCase);
        _output.WriteLine($"Correctly rejected expired product: {exception.Message}");
    }

    /// <summary>
    /// Tests error handling when adding inactive products.
    /// Validates: Requirements 2.4, 8.1
    /// </summary>
    [Fact]
    public async Task ErrorHandling_InactiveProduct_ShouldProvideClearError()
    {
        // Arrange: Create inactive product
        var (product, _) = await CreateProductWithStockAsync(
            "Inactive Product", 10.00m, 10, isActive: false);

        // Act & Assert: Should throw with clear message
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _saleService.AddItemToSaleAsync(sale.Id, product.Id, 1, product.UnitPrice));

        Assert.Contains("inactive", exception.Message, StringComparison.OrdinalIgnoreCase);
        _output.WriteLine($"Correctly rejected inactive product: {exception.Message}");
    }

    /// <summary>
    /// Tests error handling for insufficient stock.
    /// Validates: Requirements 2.2, 7.2, 8.1
    /// </summary>
    [Fact]
    public async Task ErrorHandling_InsufficientStock_ShouldProvideAvailableQuantity()
    {
        // Arrange: Create product with low stock
        var (product, _) = await CreateProductWithStockAsync("Low Stock Item", 10.00m, 3);

        // Act & Assert: Should throw with available quantity info
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _saleService.AddItemToSaleAsync(sale.Id, product.Id, 10, product.UnitPrice));

        Assert.Contains("3", exception.Message); // Should mention available quantity
        _output.WriteLine($"Correctly rejected with stock info: {exception.Message}");
    }

    /// <summary>
    /// Tests that sale state is preserved when payment fails.
    /// Validates: Requirements 6.5, 8.4
    /// </summary>
    [Fact]
    public async Task ErrorHandling_PaymentFailure_ShouldPreserveSaleState()
    {
        // Arrange: Create product and sale
        var (product, _) = await CreateProductWithStockAsync("Test Product", 50.00m, 10);
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 2, product.UnitPrice);
        var saleId = sale.Id;

        // Act: Process payment with insufficient amount
        var paymentResult = await _paymentService.ProcessPaymentAsync(sale, PaymentMethod.Cash, 10.00m);

        // Assert: Payment should fail but sale state preserved
        Assert.False(paymentResult.IsSuccess);
        Assert.True(paymentResult.SaleStatePreserved);
        Assert.True(paymentResult.FinalTotal > 0);

        // Verify sale is still accessible
        var retrievedSale = await _saleService.GetSaleByIdAsync(saleId);
        Assert.NotNull(retrievedSale);
        Assert.Equal(SaleStatus.Active, retrievedSale.Status);
        Assert.Equal(2, retrievedSale.Items.Count);

        _output.WriteLine($"Payment failed as expected. Sale state preserved: {paymentResult.SaleStatePreserved}");
    }

    /// <summary>
    /// Tests transaction rollback on inventory update failure.
    /// Validates: Requirements 8.4
    /// </summary>
    [Fact]
    public async Task ErrorHandling_InventoryUpdateFailure_ShouldRollback()
    {
        // Arrange: Create product
        var (product, _) = await CreateProductWithStockAsync("Rollback Test Product", 10.00m, 100);

        // Act: Create and complete sale
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 5, product.UnitPrice);
        var completedSale = await _saleService.CompleteSaleAsync(sale.Id, PaymentMethod.Cash);

        // Assert: Verify inventory was reduced
        var stockAfter = await _stockRepository.GetByProductIdAsync(product.Id);
        Assert.Equal(95, stockAfter!.Quantity);

        // Act: Rollback inventory
        var rollbackResult = await _inventoryUpdater.RollbackInventoryUpdateAsync(sale.Id);
        Assert.True(rollbackResult.IsSuccess);

        // Assert: Stock should be restored
        var stockAfterRollback = await _stockRepository.GetByProductIdAsync(product.Id);
        Assert.Equal(100, stockAfterRollback!.Quantity);

        _output.WriteLine($"Inventory rollback succeeded. Stock: 95 -> {stockAfterRollback.Quantity}");
    }

    // =========================================================================
    // Data Consistency Tests
    // =========================================================================

    /// <summary>
    /// Tests that sale totals remain consistent after multiple item modifications.
    /// Validates: Requirements 1.5, 3.1
    /// </summary>
    [Fact]
    public async Task DataConsistency_MultipleItemModifications_ShouldMaintainCorrectTotals()
    {
        // Arrange: Create products
        var (product1, _) = await CreateProductWithStockAsync("Product 1", 10.00m, 50);
        var (product2, _) = await CreateProductWithStockAsync("Product 2", 20.00m, 50);
        var (product3, _) = await CreateProductWithStockAsync("Product 3", 30.00m, 50);

        // Act: Create sale and add items
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product1.Id, 2, product1.UnitPrice);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product2.Id, 1, product2.UnitPrice);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product3.Id, 1, product3.UnitPrice);

        // Assert: Total should be 2*10 + 1*20 + 1*30 = 70
        Assert.Equal(70.00m, sale.TotalAmount);
        _output.WriteLine($"Initial total: {sale.TotalAmount:C}");

        // Act: Remove one item
        var itemToRemove = sale.Items.First(i => i.ProductId == product2.Id);
        sale = await _saleService.RemoveItemFromSaleAsync(sale.Id, itemToRemove.Id);

        // Assert: Total should be 2*10 + 1*30 = 50
        Assert.Equal(50.00m, sale.TotalAmount);
        Assert.Equal(2, sale.Items.Count(i => !i.IsDeleted));
        _output.WriteLine($"After removing Product 2: {sale.TotalAmount:C}");

        // Act: Complete sale and verify final state
        var completedSale = await _saleService.CompleteSaleAsync(sale.Id, PaymentMethod.Cash);
        Assert.Equal(50.00m, completedSale.TotalAmount);
        Assert.Equal(SaleStatus.Completed, completedSale.Status);
        _output.WriteLine($"Final total after completion: {completedSale.TotalAmount:C}");
    }

    /// <summary>
    /// Tests that cancelled sales do not affect inventory.
    /// Validates: Requirements 1.6, 6.3
    /// </summary>
    [Fact]
    public async Task DataConsistency_CancelledSale_ShouldNotAffectInventory()
    {
        // Arrange: Create product
        var (product, stock) = await CreateProductWithStockAsync("Cancellation Test Product", 15.00m, 50);
        var initialStock = stock.Quantity;

        // Act: Create sale, add items, then cancel
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 5, product.UnitPrice);
        var cancelledSale = await _saleService.CancelSaleAsync(sale.Id, "Customer changed mind");

        // Assert: Sale should be cancelled
        Assert.Equal(SaleStatus.Cancelled, cancelledSale.Status);
        Assert.NotNull(cancelledSale.CancelledAt);
        Assert.Equal("Customer changed mind", cancelledSale.CancellationReason);

        // Assert: Stock should be unchanged
        var stockAfter = await _stockRepository.GetByProductIdAsync(product.Id);
        Assert.Equal(initialStock, stockAfter!.Quantity);

        _output.WriteLine($"Sale cancelled. Stock unchanged: {stockAfter.Quantity}");
    }

    /// <summary>
    /// Tests that completed sales cannot be modified.
    /// Validates: Requirements 1.5, 8.4
    /// </summary>
    [Fact]
    public async Task DataConsistency_CompletedSale_ShouldBeImmutable()
    {
        // Arrange: Create and complete sale
        var (product, _) = await CreateProductWithStockAsync("Immutable Test Product", 10.00m, 50);
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 2, product.UnitPrice);
        var completedSale = await _saleService.CompleteSaleAsync(sale.Id, PaymentMethod.Cash);

        // Act & Assert: Should not be able to add items
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _saleService.AddItemToSaleAsync(completedSale.Id, product.Id, 1, product.UnitPrice));
        _output.WriteLine("Correctly prevented adding items to completed sale");

        // Act & Assert: Should not be able to remove items
        var itemId = completedSale.Items.First().Id;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _saleService.RemoveItemFromSaleAsync(completedSale.Id, itemId));
        _output.WriteLine("Correctly prevented removing items from completed sale");

        // Act & Assert: Should not be able to cancel
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _saleService.CancelSaleAsync(completedSale.Id, "Too late"));
        _output.WriteLine("Correctly prevented cancelling completed sale");
    }

    // =========================================================================
    // Performance Tests (Requirements 9.1, 9.2)
    // =========================================================================

    /// <summary>
    /// Tests that add/remove item operations complete within 200ms.
    /// Validates: Requirements 9.1
    /// </summary>
    [Fact]
    public async Task Performance_AddItemOperation_ShouldCompleteWithin200ms()
    {
        // Arrange: Create product
        var (product, _) = await CreateProductWithStockAsync("Performance Test Product", 10.00m, 1000);
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);

        // Act: Measure time for add operation
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 1, product.UnitPrice);
        stopwatch.Stop();

        // Assert: Should complete within 200ms
        Assert.True(stopwatch.ElapsedMilliseconds < 200,
            $"Add item took {stopwatch.ElapsedMilliseconds}ms, expected < 200ms");
        _output.WriteLine($"Add item operation completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Tests that calculation completes within 100ms for sales with up to 50 items.
    /// Validates: Requirements 9.2
    /// </summary>
    [Fact]
    public async Task Performance_CalculationWith50Items_ShouldCompleteWithin100ms()
    {
        // Arrange: Create products and sale with 50 items
        var products = new List<Product>();
        for (int i = 0; i < 50; i++)
        {
            var (product, _) = await CreateProductWithStockAsync($"Product {i}", 10.00m + i, 100);
            products.Add(product);
        }

        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        foreach (var product in products)
        {
            sale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 1, product.UnitPrice);
        }

        // Act: Measure calculation time
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var calculation = await _saleService.CalculateFullSaleTotalAsync(sale.Id);
        stopwatch.Stop();

        // Assert: Should complete within 100ms
        Assert.True(stopwatch.ElapsedMilliseconds < 100,
            $"Calculation took {stopwatch.ElapsedMilliseconds}ms, expected < 100ms");
        Assert.True(calculation.FinalTotal > 0);
        _output.WriteLine($"Calculation for 50 items completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    // =========================================================================
    // Audit Trail Tests (Requirements 10.1, 10.2, 10.3)
    // =========================================================================

    /// <summary>
    /// Tests that sale creation is logged with timestamps.
    /// Validates: Requirements 10.1, 10.2
    /// </summary>
    [Fact]
    public async Task AuditTrail_SaleCreation_ShouldBeLogged()
    {
        // Arrange & Act: Create sale
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);

        // Assert: Verify timestamps are set
        Assert.NotEqual(default, sale.CreatedAt);
        Assert.True(sale.CreatedAt <= DateTime.UtcNow);
        Assert.Equal(_userId, sale.UserId);
        Assert.Equal(_deviceId, sale.DeviceId);

        _output.WriteLine($"Sale created at: {sale.CreatedAt:O}");
        _output.WriteLine($"User ID: {sale.UserId}");
        _output.WriteLine($"Device ID: {sale.DeviceId}");
    }

    /// <summary>
    /// Tests that sale completion is logged with timestamps.
    /// Validates: Requirements 10.1, 10.5
    /// </summary>
    [Fact]
    public async Task AuditTrail_SaleCompletion_ShouldBeLogged()
    {
        // Arrange: Create and populate sale
        var (product, _) = await CreateProductWithStockAsync("Audit Test Product", 10.00m, 50);
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 2, product.UnitPrice);

        // Act: Complete sale
        var completedSale = await _saleService.CompleteSaleAsync(sale.Id, PaymentMethod.Cash);

        // Assert: Verify completion timestamp
        Assert.NotNull(completedSale.CompletedAt);
        Assert.True(completedSale.CompletedAt <= DateTime.UtcNow);
        Assert.Equal(PaymentMethod.Cash, completedSale.PaymentMethod);
        Assert.Equal(SaleStatus.Completed, completedSale.Status);

        _output.WriteLine($"Sale completed at: {completedSale.CompletedAt:O}");
        _output.WriteLine($"Payment method: {completedSale.PaymentMethod}");
    }

    /// <summary>
    /// Tests that sale cancellation is logged with reason.
    /// Validates: Requirements 10.1, 10.3
    /// </summary>
    [Fact]
    public async Task AuditTrail_SaleCancellation_ShouldBeLoggedWithReason()
    {
        // Arrange: Create and populate sale
        var (product, _) = await CreateProductWithStockAsync("Cancellation Audit Product", 10.00m, 50);
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 1, product.UnitPrice);

        // Act: Cancel sale
        var reason = "Customer requested cancellation";
        var cancelledSale = await _saleService.CancelSaleAsync(sale.Id, reason);

        // Assert: Verify cancellation details
        Assert.NotNull(cancelledSale.CancelledAt);
        Assert.Equal(reason, cancelledSale.CancellationReason);
        Assert.Equal(SaleStatus.Cancelled, cancelledSale.Status);

        _output.WriteLine($"Sale cancelled at: {cancelledSale.CancelledAt:O}");
        _output.WriteLine($"Reason: {cancelledSale.CancellationReason}");
    }

    // =========================================================================
    // Receipt Generation Tests (Requirement 6.4)
    // =========================================================================

    /// <summary>
    /// Tests receipt generation with complete transaction data.
    /// Validates: Requirements 6.4
    /// </summary>
    [Fact]
    public async Task ReceiptGeneration_CompletedSale_ShouldContainAllData()
    {
        // Arrange: Create and complete sale
        var (product1, _) = await CreateProductWithStockAsync("Receipt Product 1", 25.00m, 50);
        var (product2, _) = await CreateProductWithStockAsync("Receipt Product 2", 15.00m, 50);
        var customer = await CreateCustomerAsync("Receipt Customer", MembershipTier.Silver);

        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId, customer.Id);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product1.Id, 2, product1.UnitPrice);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product2.Id, 1, product2.UnitPrice);

        var completedSale = await _saleService.CompleteSaleAsync(sale.Id, PaymentMethod.Cash);

        // Act: Generate payment result and receipt
        var paymentResult = new PaymentResult
        {
            IsSuccess = true,
            TransactionId = Guid.NewGuid(),
            SaleId = completedSale.Id,
            PaymentMethod = PaymentMethod.Cash,
            FinalTotal = completedSale.FinalTotal,
            AmountPaid = completedSale.TotalAmount,
            ChangeAmount = 0,
            Subtotal = completedSale.Subtotal,
            TotalDiscount = completedSale.TotalDiscount,
            MembershipDiscountAmount = completedSale.MembershipDiscountAmount,
            TotalTax = completedSale.TotalTax,
            ProcessedAt = DateTime.UtcNow,
            InvoiceNumber = completedSale.InvoiceNumber
        };

        var receipt = await _paymentService.GenerateReceiptAsync(completedSale, paymentResult);

        // Assert: Verify receipt contains all required data
        Assert.Equal(completedSale.InvoiceNumber, receipt.InvoiceNumber);
        Assert.Equal(completedSale.Id, receipt.SaleId);
        Assert.Equal(customer.Name, receipt.CustomerName);
        Assert.Equal(2, receipt.LineItems.Count);
        Assert.True(receipt.IsComplete);

        _output.WriteLine($"Receipt generated for invoice: {receipt.InvoiceNumber}");
        _output.WriteLine($"Customer: {receipt.CustomerName}");
        _output.WriteLine($"Line items: {receipt.LineItems.Count}");
        _output.WriteLine($"Final total: {receipt.FinalTotal:C}");
    }

    // =========================================================================
    // Multi-Item Sale Tests
    // =========================================================================

    /// <summary>
    /// Tests sale with mixed product types (regular and weight-based).
    /// Validates: Requirements 2.3, 5.1
    /// </summary>
    [Fact]
    public async Task MixedProductSale_RegularAndWeightBased_ShouldCalculateCorrectly()
    {
        // Arrange: Create regular and weight-based products
        var (regularProduct, _) = await CreateProductWithStockAsync("Regular Item", 20.00m, 50);
        var (weightProduct, _) = await CreateProductWithStockAsync(
            "Weight Item", 0m, 50, isWeightBased: true, ratePerKg: 8.00m);

        // Act: Create sale with mixed items
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, regularProduct.Id, 3, regularProduct.UnitPrice);
        sale = await _saleService.AddWeightBasedItemToSaleAsync(sale.Id, weightProduct.Id, 2.5m);

        // Assert: Verify both item types
        Assert.Equal(2, sale.Items.Count);
        var regularItem = sale.Items.First(i => !i.IsWeightBased);
        var weightItem = sale.Items.First(i => i.IsWeightBased);

        Assert.Equal(60.00m, regularItem.LineSubtotal); // 3 * 20
        Assert.Equal(20.00m, weightItem.LineSubtotal); // 2.5kg * 8

        _output.WriteLine($"Regular item: {regularItem.LineSubtotal:C}");
        _output.WriteLine($"Weight item: {weightItem.LineSubtotal:C}");
        _output.WriteLine($"Total: {sale.TotalAmount:C}");
    }

    /// <summary>
    /// Tests sale with many items to verify scalability.
    /// Validates: Requirements 9.3, 9.6
    /// </summary>
    [Fact]
    public async Task LargeSale_With100Items_ShouldProcessCorrectly()
    {
        // Arrange: Create 100 products
        var products = new List<(Product product, Stock stock)>();
        for (int i = 0; i < 100; i++)
        {
            var result = await CreateProductWithStockAsync($"Bulk Product {i}", 1.00m + (i * 0.10m), 100);
            products.Add(result);
        }

        // Act: Create sale and add all products
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        decimal expectedTotal = 0;

        foreach (var (product, _) in products)
        {
            sale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 1, product.UnitPrice);
            expectedTotal += product.UnitPrice;
        }

        // Assert: Verify all items added correctly
        Assert.Equal(100, sale.Items.Count);
        Assert.Equal(expectedTotal, sale.TotalAmount);

        // Act: Complete the sale
        var completedSale = await _saleService.CompleteSaleAsync(sale.Id, PaymentMethod.Cash);
        Assert.Equal(SaleStatus.Completed, completedSale.Status);

        _output.WriteLine($"Successfully processed sale with 100 items");
        _output.WriteLine($"Total: {completedSale.TotalAmount:C}");
    }

    // =========================================================================
    // Edge Cases and Boundary Tests
    // =========================================================================

    /// <summary>
    /// Tests sale with zero total (all items removed).
    /// </summary>
    [Fact]
    public async Task EdgeCase_AllItemsRemoved_ShouldTransitionToDraft()
    {
        // Arrange: Create product and sale
        var (product, _) = await CreateProductWithStockAsync("Removable Product", 10.00m, 50);
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 2, product.UnitPrice);

        Assert.Equal(SaleStatus.Active, sale.Status);

        // Act: Remove all items
        var itemId = sale.Items.First().Id;
        sale = await _saleService.RemoveItemFromSaleAsync(sale.Id, itemId);

        // Assert: Should transition back to Draft
        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Equal(0, sale.TotalAmount);
        Assert.Empty(sale.Items.Where(i => !i.IsDeleted));

        _output.WriteLine("Sale transitioned to Draft after all items removed");
    }

    /// <summary>
    /// Tests sale with minimum quantity (1 item).
    /// </summary>
    [Fact]
    public async Task EdgeCase_SingleItemSale_ShouldProcessCorrectly()
    {
        // Arrange: Create product
        var (product, _) = await CreateProductWithStockAsync("Single Item", 5.00m, 10);

        // Act: Create and complete minimal sale
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 1, product.UnitPrice);
        var completedSale = await _saleService.CompleteSaleAsync(sale.Id, PaymentMethod.Cash);

        // Assert
        Assert.Single(completedSale.Items);
        Assert.Equal(5.00m, completedSale.TotalAmount);
        Assert.Equal(SaleStatus.Completed, completedSale.Status);

        _output.WriteLine($"Single item sale completed: {completedSale.TotalAmount:C}");
    }

    /// <summary>
    /// Tests that duplicate invoice numbers are handled correctly.
    /// Validates: Requirements 1.1
    /// </summary>
    [Fact]
    public async Task EdgeCase_DuplicateInvoiceNumber_ShouldGenerateNewNumber()
    {
        // Arrange: Create first sale
        var invoiceNumber = _saleService.GenerateInvoiceNumber();
        var sale1 = await _saleService.CreateSaleAsync(invoiceNumber, _deviceId);
        Assert.Equal(invoiceNumber, sale1.InvoiceNumber);

        // Act: Try to create second sale with same invoice number
        var sale2 = await _saleService.CreateSaleAsync(invoiceNumber, _deviceId);

        // Assert: Should have different invoice number
        Assert.NotEqual(invoiceNumber, sale2.InvoiceNumber);
        Assert.StartsWith("INV-", sale2.InvoiceNumber);

        _output.WriteLine($"Original: {invoiceNumber}");
        _output.WriteLine($"New: {sale2.InvoiceNumber}");
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _serviceProvider?.Dispose();
    }
}
