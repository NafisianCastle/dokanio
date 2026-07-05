using Microsoft.Extensions.DependencyInjection;
using Shared.Core.Data;
using Shared.Core.DependencyInjection;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace Shared.Core.Tests;

/// <summary>
/// Unit tests for StockValidationService and the RemoveItemFromSaleAsync method.
/// Tests cover:
/// - Product existence and availability validation (Req 2.1)
/// - Expired and inactive product prevention (Req 2.4)
/// - Stock checking with reservation system (Req 7.1, 7.2, 7.3)
/// - Batch availability with expiry validation (Req 7.4)
/// - Proper cleanup when removing items (Req 2.6)
/// </summary>
public class StockValidationServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IStockValidationService _stockValidationService;
    private readonly ISaleService _saleService;
    private readonly PosDbContext _context;
    private readonly ITestOutputHelper _output;

    private readonly Guid _deviceId;
    private readonly Guid _userId;
    private readonly Guid _shopId;
    private readonly Guid _businessId;

    public StockValidationServiceTests(ITestOutputHelper output)
    {
        _output = output;

        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        _serviceProvider = services.BuildServiceProvider();

        _stockValidationService = _serviceProvider.GetRequiredService<IStockValidationService>();
        _saleService = _serviceProvider.GetRequiredService<ISaleService>();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();

        _businessId = Guid.NewGuid();
        _shopId = Guid.NewGuid();
        _deviceId = Guid.NewGuid();
        _userId = Guid.NewGuid();

        SeedTestData().GetAwaiter().GetResult();
    }

    private async Task SeedTestData()
    {
        var business = new Business
        {
            Id = _businessId,
            Name = "Test Business",
            Type = BusinessType.GeneralRetail,
            OwnerId = _userId,
            IsActive = true
        };
        _context.Businesses.Add(business);

        var shop = new Shop
        {
            Id = _shopId,
            BusinessId = _businessId,
            Name = "Test Shop",
            DeviceId = _deviceId,
            IsActive = true
        };
        _context.Shops.Add(shop);

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
        _context.Users.Add(user);

        // Seed license
        var currentUserService = _serviceProvider.GetRequiredService<ICurrentUserService>();
        var licenseDeviceId = currentUserService.GetDeviceId();
        var license = new License
        {
            Id = Guid.NewGuid(),
            LicenseKey = "TEST-LICENSE-KEY-001",
            Type = LicenseType.Professional,
            Status = LicenseStatus.Active,
            DeviceId = licenseDeviceId,
            CustomerName = "Test Customer",
            CustomerEmail = "test@example.com",
            IssueDate = DateTime.UtcNow.AddDays(-30),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            ActivationDate = DateTime.UtcNow.AddDays(-30)
        };
        _context.Licenses.Add(license);

        await _context.SaveChangesAsync();
    }

    // =========================================================================
    // Helper: Create a product with stock
    // =========================================================================

    private async Task<(Product product, Stock stock)> CreateProductWithStockAsync(
        string name, int stockQty, bool isActive = true, DateTime? expiryDate = null, bool isWeightBased = false)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            ShopId = _shopId,
            Name = name,
            UnitPrice = 10.00m,
            IsActive = isActive,
            ExpiryDate = expiryDate,
            IsWeightBased = isWeightBased,
            RatePerKilogram = isWeightBased ? 5.00m : null,
            DeviceId = _deviceId,
            SyncStatus = SyncStatus.NotSynced
        };
        _context.Products.Add(product);

        var stock = new Stock
        {
            Id = Guid.NewGuid(),
            ShopId = _shopId,
            ProductId = product.Id,
            Quantity = stockQty,
            DeviceId = _deviceId,
            SyncStatus = SyncStatus.NotSynced
        };
        _context.Stock.Add(stock);

        await _context.SaveChangesAsync();
        return (product, stock);
    }

    // =========================================================================
    // Product Validation Tests (Req 2.1, 2.4)
    // =========================================================================

    [Fact]
    public async Task ValidateProductForSaleAsync_ActiveProductNoExpiry_ShouldReturnValid()
    {
        var (product, _) = await CreateProductWithStockAsync("Active Product", 10);

        var result = await _stockValidationService.ValidateProductForSaleAsync(product.Id);

        Assert.True(result.IsValid);
        Assert.True(result.ProductExists);
        Assert.True(result.IsActive);
        Assert.False(result.IsExpired);
        Assert.Null(result.InvalidReason);

        _output.WriteLine($"Active product validated: {product.Name}");
    }

    [Fact]
    public async Task ValidateProductForSaleAsync_InactiveProduct_ShouldReturnInvalid()
    {
        var (product, _) = await CreateProductWithStockAsync("Inactive Product", 10, isActive: false);

        var result = await _stockValidationService.ValidateProductForSaleAsync(product.Id);

        Assert.False(result.IsValid);
        Assert.True(result.ProductExists);
        Assert.False(result.IsActive);
        Assert.False(result.IsExpired);
        Assert.NotNull(result.InvalidReason);
        Assert.Contains("inactive", result.InvalidReason, StringComparison.OrdinalIgnoreCase);

        _output.WriteLine($"Inactive product correctly rejected: {result.InvalidReason}");
    }

    [Fact]
    public async Task ValidateProductForSaleAsync_ExpiredProduct_ShouldReturnInvalid()
    {
        var expiredDate = DateTime.UtcNow.AddDays(-1); // Yesterday
        var (product, _) = await CreateProductWithStockAsync("Expired Product", 10, expiryDate: expiredDate);

        var result = await _stockValidationService.ValidateProductForSaleAsync(product.Id);

        Assert.False(result.IsValid);
        Assert.True(result.ProductExists);
        Assert.True(result.IsActive);
        Assert.True(result.IsExpired);
        Assert.NotNull(result.InvalidReason);
        Assert.Contains("expired", result.InvalidReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expiredDate, result.ExpiryDate);

        _output.WriteLine($"Expired product correctly rejected: {result.InvalidReason}");
    }

    [Fact]
    public async Task ValidateProductForSaleAsync_ProductWithFutureExpiry_ShouldReturnValid()
    {
        var futureExpiry = DateTime.UtcNow.AddDays(30);
        var (product, _) = await CreateProductWithStockAsync("Valid Medicine", 10, expiryDate: futureExpiry);

        var result = await _stockValidationService.ValidateProductForSaleAsync(product.Id);

        Assert.True(result.IsValid);
        Assert.False(result.IsExpired);
        Assert.Equal(futureExpiry, result.ExpiryDate);

        _output.WriteLine($"Product with future expiry validated: {product.Name}");
    }

    [Fact]
    public async Task ValidateProductForSaleAsync_NonExistentProduct_ShouldReturnInvalid()
    {
        var result = await _stockValidationService.ValidateProductForSaleAsync(Guid.NewGuid());

        Assert.False(result.IsValid);
        Assert.False(result.ProductExists);
        Assert.NotNull(result.InvalidReason);

        _output.WriteLine($"Non-existent product correctly rejected: {result.InvalidReason}");
    }

    [Fact]
    public async Task ValidateProductForSaleAsync_EmptyGuid_ShouldReturnInvalid()
    {
        var result = await _stockValidationService.ValidateProductForSaleAsync(Guid.Empty);

        Assert.False(result.IsValid);
        Assert.False(result.ProductExists);
    }

    // =========================================================================
    // Stock Availability Tests (Req 7.1, 7.2, 7.3)
    // =========================================================================

    [Fact]
    public async Task ValidateProductAvailabilityAsync_SufficientStock_ShouldReturnAvailable()
    {
        var (product, _) = await CreateProductWithStockAsync("In-Stock Product", 20);

        var result = await _stockValidationService.ValidateProductAvailabilityAsync(product.Id, 5);

        Assert.True(result.IsAvailable);
        Assert.Equal(5, result.RequestedQuantity);
        Assert.True(result.AvailableQuantity >= 5);
        Assert.Null(result.UnavailabilityReason);

        _output.WriteLine($"Stock available: requested=5, available={result.AvailableQuantity}");
    }

    [Fact]
    public async Task ValidateProductAvailabilityAsync_InsufficientStock_ShouldReturnUnavailable()
    {
        var (product, _) = await CreateProductWithStockAsync("Low Stock Product", 3);

        var result = await _stockValidationService.ValidateProductAvailabilityAsync(product.Id, 10);

        Assert.False(result.IsAvailable);
        Assert.Equal(10, result.RequestedQuantity);
        Assert.True(result.AvailableQuantity < 10);
        Assert.NotNull(result.UnavailabilityReason);
        Assert.NotEmpty(result.Alerts);

        _output.WriteLine($"Insufficient stock correctly detected: {result.UnavailabilityReason}");
    }

    [Fact]
    public async Task ValidateProductAvailabilityAsync_OutOfStock_ShouldReturnUnavailable()
    {
        var (product, _) = await CreateProductWithStockAsync("Out of Stock Product", 0);

        var result = await _stockValidationService.ValidateProductAvailabilityAsync(product.Id, 1);

        Assert.False(result.IsAvailable);
        Assert.Equal(0, result.AvailableQuantity);
        Assert.NotNull(result.UnavailabilityReason);
        Assert.Contains(result.Alerts, a => a.AlertType == StockAlertType.OutOfStock);

        _output.WriteLine($"Out of stock correctly detected: {result.UnavailabilityReason}");
    }

    [Fact]
    public async Task ValidateProductAvailabilityAsync_LowStockWarning_ShouldIncludeAlert()
    {
        var (product, _) = await CreateProductWithStockAsync("Low Stock Product", 6);

        // Request 5, leaving only 1 unit - should trigger low stock warning
        var result = await _stockValidationService.ValidateProductAvailabilityAsync(product.Id, 5);

        Assert.True(result.IsAvailable);
        Assert.Contains(result.Alerts, a => a.AlertType == StockAlertType.LowStock);

        _output.WriteLine("Low stock warning correctly generated");
    }

    [Fact]
    public async Task ValidateProductAvailabilityAsync_ZeroQuantityRequest_ShouldReturnUnavailable()
    {
        var (product, _) = await CreateProductWithStockAsync("Product", 10);

        var result = await _stockValidationService.ValidateProductAvailabilityAsync(product.Id, 0);

        Assert.False(result.IsAvailable);
        Assert.NotNull(result.UnavailabilityReason);
    }

    // =========================================================================
    // Batch Availability Tests (Req 7.4)
    // =========================================================================

    [Fact]
    public async Task ValidateBatchAvailabilityAsync_ValidBatch_ShouldReturnAvailable()
    {
        var futureExpiry = DateTime.UtcNow.AddDays(30);
        var (product, _) = await CreateProductWithStockAsync("Batch Product", 10, expiryDate: futureExpiry);

        var result = await _stockValidationService.ValidateBatchAvailabilityAsync(product.Id, "BATCH-001", 5);

        Assert.True(result.IsAvailable);
        Assert.Equal(futureExpiry, result.ExpiryDate);

        _output.WriteLine($"Valid batch availability confirmed");
    }

    [Fact]
    public async Task ValidateBatchAvailabilityAsync_ExpiredBatch_ShouldReturnUnavailable()
    {
        var expiredDate = DateTime.UtcNow.AddDays(-5);
        var (product, _) = await CreateProductWithStockAsync("Expired Batch Product", 10, expiryDate: expiredDate);

        var result = await _stockValidationService.ValidateBatchAvailabilityAsync(product.Id, "BATCH-EXP", 1);

        Assert.False(result.IsAvailable);
        Assert.NotNull(result.UnavailabilityReason);
        Assert.Contains("expired", result.UnavailabilityReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Alerts, a => a.AlertType == StockAlertType.Expired);

        _output.WriteLine($"Expired batch correctly rejected: {result.UnavailabilityReason}");
    }

    [Fact]
    public async Task ValidateBatchAvailabilityAsync_NearExpiryBatch_ShouldIncludeWarning()
    {
        var nearExpiry = DateTime.UtcNow.AddDays(3); // Within 7-day warning window
        var (product, _) = await CreateProductWithStockAsync("Near Expiry Product", 10, expiryDate: nearExpiry);

        var result = await _stockValidationService.ValidateBatchAvailabilityAsync(product.Id, "BATCH-NEAR", 1);

        Assert.True(result.IsAvailable);
        Assert.Contains(result.Alerts, a => a.AlertType == StockAlertType.NearExpiry);

        _output.WriteLine("Near-expiry warning correctly generated");
    }

    // =========================================================================
    // Stock Reservation Tests (Req 7.3)
    // =========================================================================

    [Fact]
    public async Task ReserveStockAsync_ValidReservation_ShouldReturnTrue()
    {
        var (product, _) = await CreateProductWithStockAsync("Reservable Product", 10);
        var saleId = Guid.NewGuid();

        var reserved = await _stockValidationService.ReserveStockAsync(product.Id, 3, saleId);

        Assert.True(reserved);
        _output.WriteLine($"Stock reserved successfully for sale {saleId}");
    }

    [Fact]
    public async Task ReserveStockAsync_InsufficientStock_ShouldReturnFalse()
    {
        var (product, _) = await CreateProductWithStockAsync("Limited Product", 2);
        var saleId = Guid.NewGuid();

        var reserved = await _stockValidationService.ReserveStockAsync(product.Id, 10, saleId);

        Assert.False(reserved);
        _output.WriteLine("Reservation correctly rejected due to insufficient stock");
    }

    [Fact]
    public async Task ReleaseStockReservationAsync_ExistingReservation_ShouldReturnTrue()
    {
        var (product, _) = await CreateProductWithStockAsync("Reserved Product", 10);
        var saleId = Guid.NewGuid();

        await _stockValidationService.ReserveStockAsync(product.Id, 3, saleId);
        var released = await _stockValidationService.ReleaseStockReservationAsync(saleId);

        Assert.True(released);
        _output.WriteLine($"Stock reservation released for sale {saleId}");
    }

    [Fact]
    public async Task ReleaseStockReservationAsync_EmptyGuid_ShouldReturnFalse()
    {
        var released = await _stockValidationService.ReleaseStockReservationAsync(Guid.Empty);

        Assert.False(released);
    }

    [Fact]
    public async Task GetCurrentStockLevelAsync_AfterReservation_ShouldReduceAvailableQuantity()
    {
        var (product, _) = await CreateProductWithStockAsync("Tracked Product", 10);
        var saleId = Guid.NewGuid();

        var beforeReservation = await _stockValidationService.GetCurrentStockLevelAsync(product.Id);
        await _stockValidationService.ReserveStockAsync(product.Id, 3, saleId);
        var afterReservation = await _stockValidationService.GetCurrentStockLevelAsync(product.Id);

        Assert.Equal(10, beforeReservation.PhysicalQuantity);
        Assert.True(afterReservation.AvailableQuantity <= beforeReservation.AvailableQuantity);

        _output.WriteLine($"Stock level before: {beforeReservation.AvailableQuantity}, after reservation: {afterReservation.AvailableQuantity}");
    }

    // =========================================================================
    // Stock Alerts Tests
    // =========================================================================

    [Fact]
    public async Task GetStockAlertsAsync_ExpiredItem_ShouldReturnExpiredAlert()
    {
        var expiredDate = DateTime.UtcNow.AddDays(-1);
        var (product, _) = await CreateProductWithStockAsync("Expired Item", 5, expiryDate: expiredDate);

        var saleItem = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = Guid.NewGuid(),
            ProductId = product.Id,
            Quantity = 1,
            UnitPrice = 10m,
            TotalPrice = 10m
        };

        var alerts = await _stockValidationService.GetStockAlertsAsync(new[] { saleItem });

        Assert.Contains(alerts, a => a.AlertType == StockAlertType.Expired);
        _output.WriteLine("Expired item alert correctly generated");
    }

    [Fact]
    public async Task GetStockAlertsAsync_OutOfStockItem_ShouldReturnOutOfStockAlert()
    {
        var (product, _) = await CreateProductWithStockAsync("OOS Item", 0);

        var saleItem = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = Guid.NewGuid(),
            ProductId = product.Id,
            Quantity = 1,
            UnitPrice = 10m,
            TotalPrice = 10m
        };

        var alerts = await _stockValidationService.GetStockAlertsAsync(new[] { saleItem });

        Assert.Contains(alerts, a => a.AlertType == StockAlertType.OutOfStock);
        _output.WriteLine("Out-of-stock alert correctly generated");
    }

    [Fact]
    public async Task GetStockAlertsAsync_DeletedItem_ShouldBeSkipped()
    {
        var (product, _) = await CreateProductWithStockAsync("Deleted Item", 0);

        var deletedSaleItem = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = Guid.NewGuid(),
            ProductId = product.Id,
            Quantity = 1,
            UnitPrice = 10m,
            TotalPrice = 10m,
            IsDeleted = true
        };

        var alerts = await _stockValidationService.GetStockAlertsAsync(new[] { deletedSaleItem });

        // Deleted items should be skipped
        Assert.Empty(alerts);
        _output.WriteLine("Deleted item correctly skipped in alert generation");
    }

    // =========================================================================
    // RemoveItemFromSaleAsync Tests (Req 2.6)
    // =========================================================================

    [Fact]
    public async Task RemoveItemFromSaleAsync_ValidItem_ShouldSoftDeleteAndRecalculate()
    {
        var (product, _) = await CreateProductWithStockAsync("Removable Product", 20);

        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 2, product.UnitPrice);

        var itemId = sale.Items.First().Id;
        var totalBeforeRemoval = sale.TotalAmount;

        var updatedSale = await _saleService.RemoveItemFromSaleAsync(sale.Id, itemId);

        Assert.NotNull(updatedSale);
        Assert.Equal(0, updatedSale.TotalAmount);
        Assert.True(updatedSale.TotalAmount < totalBeforeRemoval);

        _output.WriteLine($"Item removed. Total before: {totalBeforeRemoval}, after: {updatedSale.TotalAmount}");
    }

    [Fact]
    public async Task RemoveItemFromSaleAsync_LastItem_ShouldTransitionSaleBackToDraft()
    {
        var (product, _) = await CreateProductWithStockAsync("Last Item Product", 20);

        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 1, product.UnitPrice);

        Assert.Equal(SaleStatus.Active, sale.Status);

        var itemId = sale.Items.First().Id;
        var updatedSale = await _saleService.RemoveItemFromSaleAsync(sale.Id, itemId);

        Assert.Equal(SaleStatus.Draft, updatedSale.Status);
        Assert.Equal(0, updatedSale.TotalAmount);

        _output.WriteLine("Sale correctly transitioned back to Draft after last item removed");
    }

    [Fact]
    public async Task RemoveItemFromSaleAsync_OneOfMultipleItems_ShouldRecalculateCorrectly()
    {
        var (product1, _) = await CreateProductWithStockAsync("Product A", 20);
        var (product2, _) = await CreateProductWithStockAsync("Product B", 20);

        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product1.Id, 2, 10.00m);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product2.Id, 3, 5.00m);

        // Total should be 2*10 + 3*5 = 35
        Assert.Equal(35.00m, sale.TotalAmount);

        var item1Id = sale.Items.First(i => i.ProductId == product1.Id).Id;
        var updatedSale = await _saleService.RemoveItemFromSaleAsync(sale.Id, item1Id);

        // After removing product1 (2*10=20), total should be 3*5=15
        Assert.Equal(15.00m, updatedSale.TotalAmount);
        Assert.Equal(SaleStatus.Active, updatedSale.Status);

        _output.WriteLine($"Recalculated correctly after removal: {updatedSale.TotalAmount}");
    }

    [Fact]
    public async Task RemoveItemFromSaleAsync_CompletedSale_ShouldThrowInvalidOperationException()
    {
        var (product, _) = await CreateProductWithStockAsync("Completed Sale Product", 20);

        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 1, product.UnitPrice);
        var itemId = sale.Items.First().Id;

        await _saleService.CompleteSaleAsync(sale.Id, PaymentMethod.Cash);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _saleService.RemoveItemFromSaleAsync(sale.Id, itemId));

        _output.WriteLine("Correctly rejected item removal from completed sale");
    }

    [Fact]
    public async Task RemoveItemFromSaleAsync_CancelledSale_ShouldThrowInvalidOperationException()
    {
        var (product, _) = await CreateProductWithStockAsync("Cancelled Sale Product", 20);

        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        sale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 1, product.UnitPrice);
        var itemId = sale.Items.First().Id;

        await _saleService.CancelSaleAsync(sale.Id, "Test cancellation");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _saleService.RemoveItemFromSaleAsync(sale.Id, itemId));

        _output.WriteLine("Correctly rejected item removal from cancelled sale");
    }

    [Fact]
    public async Task RemoveItemFromSaleAsync_NonExistentItem_ShouldThrowArgumentException()
    {
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _saleService.RemoveItemFromSaleAsync(sale.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task RemoveItemFromSaleAsync_EmptySaleId_ShouldThrowArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _saleService.RemoveItemFromSaleAsync(Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public async Task RemoveItemFromSaleAsync_EmptyItemId_ShouldThrowArgumentException()
    {
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _saleService.RemoveItemFromSaleAsync(sale.Id, Guid.Empty));
    }

    // =========================================================================
    // Integration: AddItemToSaleAsync validates product (Req 2.1, 2.4)
    // =========================================================================

    [Fact]
    public async Task AddItemToSaleAsync_InactiveProduct_ShouldThrowInvalidOperationException()
    {
        var (product, _) = await CreateProductWithStockAsync("Inactive Product", 10, isActive: false);
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _saleService.AddItemToSaleAsync(sale.Id, product.Id, 1, product.UnitPrice));

        _output.WriteLine("Correctly rejected adding inactive product to sale");
    }

    [Fact]
    public async Task AddItemToSaleAsync_ExpiredProduct_ShouldThrowInvalidOperationException()
    {
        var expiredDate = DateTime.UtcNow.AddDays(-1);
        var (product, _) = await CreateProductWithStockAsync("Expired Product", 10, expiryDate: expiredDate);
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _saleService.AddItemToSaleAsync(sale.Id, product.Id, 1, product.UnitPrice));

        _output.WriteLine("Correctly rejected adding expired product to sale");
    }

    [Fact]
    public async Task AddItemToSaleAsync_InsufficientStock_ShouldThrowInvalidOperationException()
    {
        var (product, _) = await CreateProductWithStockAsync("Low Stock Product", 2);
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _saleService.AddItemToSaleAsync(sale.Id, product.Id, 10, product.UnitPrice));

        _output.WriteLine("Correctly rejected adding product with insufficient stock");
    }

    [Fact]
    public async Task AddItemToSaleAsync_ValidProduct_ShouldSucceed()
    {
        var (product, _) = await CreateProductWithStockAsync("Valid Product", 20);
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);

        var updatedSale = await _saleService.AddItemToSaleAsync(sale.Id, product.Id, 3, product.UnitPrice);

        Assert.NotNull(updatedSale);
        Assert.Single(updatedSale.Items);
        Assert.Equal(30.00m, updatedSale.TotalAmount); // 3 * 10.00

        _output.WriteLine($"Valid product added successfully. Total: {updatedSale.TotalAmount}");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
