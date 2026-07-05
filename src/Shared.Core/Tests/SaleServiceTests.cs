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
/// Unit tests for the enhanced SaleService implementation.
/// Tests cover: unique invoice number generation, device/user validation,
/// sale state management, and sale lifecycle transitions.
/// </summary>
public class SaleServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ISaleService _saleService;
    private readonly PosDbContext _context;
    private readonly ITestOutputHelper _output;

    // Shared test data
    private readonly Guid _deviceId;
    private readonly Guid _userId;
    private readonly Guid _shopId;
    private readonly Guid _businessId;

    public SaleServiceTests(ITestOutputHelper output)
    {
        _output = output;

        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        _serviceProvider = services.BuildServiceProvider();

        _saleService = _serviceProvider.GetRequiredService<ISaleService>();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();

        // Seed required test data
        _businessId = Guid.NewGuid();
        _shopId = Guid.NewGuid();
        _deviceId = Guid.NewGuid();
        _userId = Guid.NewGuid();

        SeedTestData().GetAwaiter().GetResult();
    }

    private async Task SeedTestData()
    {
        // Create a business
        var business = new Business
        {
            Id = _businessId,
            Name = "Test Business",
            Type = BusinessType.GeneralRetail,
            OwnerId = _userId,
            IsActive = true
        };
        _context.Businesses.Add(business);

        // Create a shop
        var shop = new Shop
        {
            Id = _shopId,
            BusinessId = _businessId,
            Name = "Test Shop",
            DeviceId = _deviceId,
            IsActive = true
        };
        _context.Shops.Add(shop);

        // Create a user with the device ID (required for device validation)
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

        // Seed an active license for the device (required by LicenseService)
        // The LicenseService uses ICurrentUserService.GetDeviceId() to look up the license
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
    // Invoice Number Generation Tests
    // =========================================================================

    [Fact]
    public void GenerateInvoiceNumber_ShouldReturnNonEmptyString()
    {
        var invoiceNumber = _saleService.GenerateInvoiceNumber();

        Assert.NotNull(invoiceNumber);
        Assert.NotEmpty(invoiceNumber);
        _output.WriteLine($"Generated invoice number: {invoiceNumber}");
    }

    [Fact]
    public void GenerateInvoiceNumber_ShouldFollowExpectedFormat()
    {
        var invoiceNumber = _saleService.GenerateInvoiceNumber();

        // Format: INV-YYYYMMDD-HHMMSS-XXXX
        Assert.StartsWith("INV-", invoiceNumber);
        var parts = invoiceNumber.Split('-');
        Assert.Equal(4, parts.Length);
        Assert.Equal("INV", parts[0]);
        Assert.Equal(8, parts[1].Length); // YYYYMMDD
        Assert.Equal(6, parts[2].Length); // HHMMSS
        Assert.Equal(4, parts[3].Length); // Random suffix

        _output.WriteLine($"Invoice format validated: {invoiceNumber}");
    }

    [Fact]
    public void GenerateInvoiceNumber_CalledMultipleTimes_ShouldProduceUniqueNumbers()
    {
        // Generate 100 invoice numbers and verify uniqueness
        var invoiceNumbers = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            var number = _saleService.GenerateInvoiceNumber();
            invoiceNumbers.Add(number);
        }

        // Due to the random suffix, we expect high uniqueness
        // (some collisions possible within the same second, but rare)
        Assert.True(invoiceNumbers.Count >= 90,
            $"Expected at least 90 unique invoice numbers out of 100, got {invoiceNumbers.Count}");

        _output.WriteLine($"Generated {invoiceNumbers.Count} unique invoice numbers out of 100 attempts");
    }

    // =========================================================================
    // Device Validation Tests
    // =========================================================================

    [Fact]
    public async Task ValidateDeviceAsync_WithRegisteredDevice_ShouldReturnTrue()
    {
        var isValid = await _saleService.ValidateDeviceAsync(_deviceId);

        Assert.True(isValid);
        _output.WriteLine($"Device {_deviceId} validated successfully");
    }

    [Fact]
    public async Task ValidateDeviceAsync_WithUnknownDevice_ShouldReturnFalse()
    {
        var unknownDeviceId = Guid.NewGuid();

        var isValid = await _saleService.ValidateDeviceAsync(unknownDeviceId);

        Assert.False(isValid);
        _output.WriteLine($"Unknown device {unknownDeviceId} correctly rejected");
    }

    [Fact]
    public async Task ValidateDeviceAsync_WithEmptyGuid_ShouldReturnFalse()
    {
        var isValid = await _saleService.ValidateDeviceAsync(Guid.Empty);

        Assert.False(isValid);
    }

    // =========================================================================
    // User Permission Validation Tests
    // =========================================================================

    [Fact]
    public async Task ValidateUserPermissionsAsync_WithCashierUser_ShouldReturnTrue()
    {
        var hasPermission = await _saleService.ValidateUserPermissionsAsync(_userId);

        Assert.True(hasPermission);
        _output.WriteLine($"User {_userId} (Cashier) has CreateSale permission");
    }

    [Fact]
    public async Task ValidateUserPermissionsAsync_WithUnknownUser_ShouldReturnFalse()
    {
        var unknownUserId = Guid.NewGuid();

        var hasPermission = await _saleService.ValidateUserPermissionsAsync(unknownUserId);

        Assert.False(hasPermission);
        _output.WriteLine($"Unknown user {unknownUserId} correctly rejected");
    }

    [Fact]
    public async Task ValidateUserPermissionsAsync_WithEmptyGuid_ShouldReturnFalse()
    {
        var hasPermission = await _saleService.ValidateUserPermissionsAsync(Guid.Empty);

        Assert.False(hasPermission);
    }

    [Fact]
    public async Task ValidateUserPermissionsAsync_WithInactiveUser_ShouldReturnFalse()
    {
        // Create an inactive user
        var inactiveUserId = Guid.NewGuid();
        var inactiveUser = new User
        {
            Id = inactiveUserId,
            BusinessId = _businessId,
            Username = "inactiveuser",
            FullName = "Inactive User",
            Email = "inactive@test.com",
            PasswordHash = "hash",
            Salt = "salt",
            Role = UserRole.Cashier,
            DeviceId = _deviceId,
            IsActive = false // Inactive
        };
        _context.Users.Add(inactiveUser);
        await _context.SaveChangesAsync();

        var hasPermission = await _saleService.ValidateUserPermissionsAsync(inactiveUserId);

        Assert.False(hasPermission);
        _output.WriteLine($"Inactive user {inactiveUserId} correctly rejected");
    }

    // =========================================================================
    // Sale Creation Tests
    // =========================================================================

    [Fact]
    public async Task CreateSaleAsync_WithInvoiceAndDevice_ShouldCreateSaleWithDraftStatus()
    {
        var invoiceNumber = _saleService.GenerateInvoiceNumber();

        var sale = await _saleService.CreateSaleAsync(invoiceNumber, _deviceId);

        Assert.NotNull(sale);
        Assert.Equal(invoiceNumber, sale.InvoiceNumber);
        Assert.Equal(_deviceId, sale.DeviceId);
        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Equal(0, sale.TotalAmount);
        Assert.Empty(sale.Items);
        Assert.NotEqual(Guid.Empty, sale.Id);

        _output.WriteLine($"Created sale {sale.InvoiceNumber} with status {sale.Status}");
    }

    [Fact]
    public async Task CreateSaleAsync_WithDeviceAndUser_ShouldValidateAndCreateSale()
    {
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);

        Assert.NotNull(sale);
        Assert.Equal(_deviceId, sale.DeviceId);
        Assert.Equal(_userId, sale.UserId);
        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.StartsWith("INV-", sale.InvoiceNumber);
        Assert.Equal(0, sale.TotalAmount);

        _output.WriteLine($"Created sale {sale.InvoiceNumber} for user {_userId}");
    }

    [Fact]
    public async Task CreateSaleAsync_WithUnknownDevice_ShouldThrowInvalidOperationException()
    {
        var unknownDeviceId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _saleService.CreateSaleAsync(unknownDeviceId, _userId));

        _output.WriteLine("Correctly rejected sale creation with unknown device");
    }

    [Fact]
    public async Task CreateSaleAsync_WithUnauthorizedUser_ShouldThrowUnauthorizedAccessException()
    {
        var unknownUserId = Guid.NewGuid();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _saleService.CreateSaleAsync(_deviceId, unknownUserId));

        _output.WriteLine("Correctly rejected sale creation with unauthorized user");
    }

    [Fact]
    public async Task CreateSaleAsync_WithEmptyInvoiceNumber_ShouldThrowArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _saleService.CreateSaleAsync(string.Empty, _deviceId));
    }

    [Fact]
    public async Task CreateSaleAsync_WithDuplicateInvoiceNumber_ShouldGenerateNewInvoiceNumber()
    {
        var invoiceNumber = _saleService.GenerateInvoiceNumber();

        // Create first sale with this invoice number
        var firstSale = await _saleService.CreateSaleAsync(invoiceNumber, _deviceId);
        Assert.Equal(invoiceNumber, firstSale.InvoiceNumber);

        // Attempt to create second sale with same invoice number - should auto-generate new one
        var secondSale = await _saleService.CreateSaleAsync(invoiceNumber, _deviceId);
        Assert.NotNull(secondSale);
        Assert.NotEqual(invoiceNumber, secondSale.InvoiceNumber);

        _output.WriteLine($"Duplicate invoice handled: original={invoiceNumber}, new={secondSale.InvoiceNumber}");
    }

    // =========================================================================
    // Sale State Management Tests
    // =========================================================================

    [Fact]
    public async Task CreateSaleAsync_NewSale_ShouldHaveDraftStatus()
    {
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);

        Assert.Equal(SaleStatus.Draft, sale.Status);
        _output.WriteLine($"New sale has Draft status as expected");
    }

    [Fact]
    public async Task CancelSaleAsync_DraftSale_ShouldTransitionToCancelled()
    {
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        Assert.Equal(SaleStatus.Draft, sale.Status);

        var cancelledSale = await _saleService.CancelSaleAsync(sale.Id, "Customer changed mind");

        Assert.Equal(SaleStatus.Cancelled, cancelledSale.Status);
        Assert.NotNull(cancelledSale.CancelledAt);
        Assert.Equal("Customer changed mind", cancelledSale.CancellationReason);

        _output.WriteLine($"Sale {sale.Id} cancelled successfully");
    }

    [Fact]
    public async Task CancelSaleAsync_AlreadyCancelledSale_ShouldThrowInvalidOperationException()
    {
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);
        await _saleService.CancelSaleAsync(sale.Id, "First cancellation");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _saleService.CancelSaleAsync(sale.Id, "Second cancellation"));
    }

    [Fact]
    public async Task CancelSaleAsync_WithEmptyReason_ShouldThrowArgumentException()
    {
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _saleService.CancelSaleAsync(sale.Id, string.Empty));
    }

    [Fact]
    public async Task GetSaleByIdAsync_ExistingSale_ShouldReturnSale()
    {
        var createdSale = await _saleService.CreateSaleAsync(_deviceId, _userId);

        var retrievedSale = await _saleService.GetSaleByIdAsync(createdSale.Id);

        Assert.NotNull(retrievedSale);
        Assert.Equal(createdSale.Id, retrievedSale.Id);
        Assert.Equal(createdSale.InvoiceNumber, retrievedSale.InvoiceNumber);
    }

    [Fact]
    public async Task GetSaleByIdAsync_NonExistentSale_ShouldReturnNull()
    {
        var result = await _saleService.GetSaleByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSaleByInvoiceNumberAsync_ExistingSale_ShouldReturnSale()
    {
        var invoiceNumber = _saleService.GenerateInvoiceNumber();
        var createdSale = await _saleService.CreateSaleAsync(invoiceNumber, _deviceId);

        var retrievedSale = await _saleService.GetSaleByInvoiceNumberAsync(invoiceNumber);

        Assert.NotNull(retrievedSale);
        Assert.Equal(invoiceNumber, retrievedSale.InvoiceNumber);
    }

    [Fact]
    public async Task GetSaleByInvoiceNumberAsync_NonExistentInvoice_ShouldReturnNull()
    {
        var result = await _saleService.GetSaleByInvoiceNumberAsync("NONEXISTENT-INVOICE");

        Assert.Null(result);
    }

    // =========================================================================
    // Sale Lifecycle Tests
    // =========================================================================

    [Fact]
    public async Task CreateSaleAsync_ShouldInitializeWithEmptyItemList()
    {
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);

        Assert.NotNull(sale.Items);
        Assert.Empty(sale.Items);
        Assert.Equal(0, sale.TotalAmount);

        _output.WriteLine("Sale initialized with empty item list as required by Requirement 1.1");
    }

    [Fact]
    public async Task CreateSaleAsync_ShouldSetCreatedAtTimestamp()
    {
        var beforeCreation = DateTime.UtcNow.AddSeconds(-1);

        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);

        var afterCreation = DateTime.UtcNow.AddSeconds(1);

        Assert.True(sale.CreatedAt >= beforeCreation && sale.CreatedAt <= afterCreation,
            $"CreatedAt {sale.CreatedAt} should be between {beforeCreation} and {afterCreation}");
    }

    [Fact]
    public async Task CreateSaleAsync_ShouldSetSyncStatusToNotSynced()
    {
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);

        Assert.Equal(SyncStatus.NotSynced, sale.SyncStatus);
    }

    [Fact]
    public async Task CreateSaleAsync_WithDeviceAndUser_ShouldSetShopId()
    {
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);

        // ShopId should be resolved from the device ID
        Assert.Equal(_shopId, sale.ShopId);
        _output.WriteLine($"Sale {sale.Id} has ShopId {sale.ShopId} resolved from device {_deviceId}");
    }

    [Fact]
    public async Task CreateSaleAsync_WithInvoiceAndDevice_ShouldSetShopId()
    {
        var invoiceNumber = _saleService.GenerateInvoiceNumber();

        var sale = await _saleService.CreateSaleAsync(invoiceNumber, _deviceId);

        // ShopId should be resolved from the device ID
        Assert.Equal(_shopId, sale.ShopId);
        _output.WriteLine($"Sale {sale.Id} has ShopId {sale.ShopId} resolved from device {_deviceId}");
    }

    [Fact]
    public async Task CreateSaleAsync_WithDeviceAndUser_ShouldSetUserId()
    {
        var sale = await _saleService.CreateSaleAsync(_deviceId, _userId);

        Assert.Equal(_userId, sale.UserId);
        _output.WriteLine($"Sale {sale.Id} has UserId {sale.UserId}");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
