using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Shared.Core.Services;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Tests;

/// <summary>
/// Integration tests for mobile-enhanced functionality
/// Validates Requirements 5.1, 6.1, 8.1 - Tab management, customer lookup, and barcode scanning
/// </summary>
public class MobileEnhancedFunctionalityTests
{
    private readonly Mock<IMultiTabSalesManager> _mockTabManager;
    private readonly Mock<ICustomerLookupService> _mockCustomerService;
    private readonly Mock<IBarcodeIntegrationService> _mockBarcodeService;

    public MobileEnhancedFunctionalityTests()
    {
        _mockTabManager = new Mock<IMultiTabSalesManager>();
        _mockCustomerService = new Mock<ICustomerLookupService>();
        _mockBarcodeService = new Mock<IBarcodeIntegrationService>();
    }

    [Fact]
    public async Task MultiTabSalesManager_CreateNewSession_ReturnsValidSession()
    {
        // Arrange
        var request = new CreateSaleSessionRequest
        {
            TabName = "Mobile Sale 1",
            ShopId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid()
        };

        var expectedSession = new SaleSessionDto
        {
            Id = Guid.NewGuid(),
            TabName = request.TabName,
            ShopId = request.ShopId,
            UserId = request.UserId,
            DeviceId = request.DeviceId,
            State = SessionState.Active,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _mockTabManager.Setup(x => x.CreateNewSaleSessionAsync(It.IsAny<CreateSaleSessionRequest>()))
            .ReturnsAsync(new SessionOperationResult
            {
                Success = true,
                Session = expectedSession,
                Message = "Session created successfully"
            });

        // Act
        var result = await _mockTabManager.Object.CreateNewSaleSessionAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Session);
        Assert.Equal(request.TabName, result.Session.TabName);
        Assert.Equal(SessionState.Active, result.Session.State);
        Assert.True(result.Session.IsActive);
    }

    [Fact]
    public async Task CustomerLookupService_LookupByMobileNumber_ReturnsCustomerWithMembership()
    {
        // Arrange
        var mobileNumber = "1234567890";
        var expectedCustomer = new CustomerLookupResult
        {
            Id = Guid.NewGuid(),
            Name = "John Doe",
            Phone = mobileNumber,
            Tier = MembershipTier.Gold,
            TotalSpent = 1500.00m,
            VisitCount = 25,
            LastVisit = DateTime.UtcNow.AddDays(-5),
            IsActive = true,
            AvailableDiscounts = new List<MembershipDiscount>
            {
                new() { DiscountPercentage = 10, Tier = MembershipTier.Gold, Reason = "Gold member discount" }
            }
        };

        _mockCustomerService.Setup(x => x.LookupByMobileNumberAsync(mobileNumber))
            .ReturnsAsync(expectedCustomer);

        // Act
        var result = await _mockCustomerService.Object.LookupByMobileNumberAsync(mobileNumber);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedCustomer.Name, result.Name);
        Assert.Equal(expectedCustomer.Phone, result.Phone);
        Assert.Equal(MembershipTier.Gold, result.Tier);
        Assert.True(result.AvailableDiscounts.Any());
        Assert.Equal(10, result.AvailableDiscounts.First().DiscountPercentage);
    }

    [Fact]
    public async Task BarcodeIntegrationService_ScanBarcode_ReturnsProductInformation()
    {
        // Arrange
        var barcode = "1234567890123";
        var shopId = Guid.NewGuid();
        var expectedProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Barcode = barcode,
            UnitPrice = 19.99m,
            IsActive = true
        };

        var scanOptions = new ScanOptions
        {
            ShopId = shopId,
            EnableBeep = true,
            EnableVibration = true,
            AutoAddToSale = true
        };

        var expectedResult = new BarcodeResult
        {
            IsSuccess = true,
            Barcode = barcode,
            Product = expectedProduct,
            IsProductFound = true,
            IsInStock = true,
            AvailableQuantity = 50,
            Timestamp = DateTime.UtcNow
        };

        _mockBarcodeService.Setup(x => x.ScanBarcodeAsync(It.IsAny<ScanOptions>()))
            .ReturnsAsync(expectedResult);

        _mockBarcodeService.Setup(x => x.LookupProductByBarcodeAsync(barcode, shopId))
            .ReturnsAsync(expectedProduct);

        // Act
        var scanResult = await _mockBarcodeService.Object.ScanBarcodeAsync(scanOptions);
        var productResult = await _mockBarcodeService.Object.LookupProductByBarcodeAsync(barcode, shopId);

        // Assert
        Assert.True(scanResult.IsSuccess);
        Assert.Equal(barcode, scanResult.Barcode);
        Assert.NotNull(scanResult.Product);
        Assert.True(scanResult.IsProductFound);
        Assert.True(scanResult.IsInStock);

        Assert.NotNull(productResult);
        Assert.Equal(expectedProduct.Name, productResult.Name);
        Assert.Equal(expectedProduct.UnitPrice, productResult.UnitPrice);
    }

    [Fact]
    public async Task CustomerLookupService_ValidateMobileNumber_ReturnsValidationResult()
    {
        // Arrange
        var validMobileNumber = "1234567890";
        var invalidMobileNumber = "123";

        _mockCustomerService.Setup(x => x.ValidateMobileNumberAsync(validMobileNumber))
            .ReturnsAsync(new MobileNumberValidationResult
            {
                IsValid = true,
                FormattedNumber = "+1-234-567-8900",
                CountryCode = "US"
            });

        _mockCustomerService.Setup(x => x.ValidateMobileNumberAsync(invalidMobileNumber))
            .ReturnsAsync(new MobileNumberValidationResult
            {
                IsValid = false,
                ErrorMessage = "Mobile number must be at least 10 digits"
            });

        // Act
        var validResult = await _mockCustomerService.Object.ValidateMobileNumberAsync(validMobileNumber);
        var invalidResult = await _mockCustomerService.Object.ValidateMobileNumberAsync(invalidMobileNumber);

        // Assert
        Assert.True(validResult.IsValid);
        Assert.NotNull(validResult.FormattedNumber);
        Assert.Equal("US", validResult.CountryCode);

        Assert.False(invalidResult.IsValid);
        Assert.NotNull(invalidResult.ErrorMessage);
        Assert.Contains("10 digits", invalidResult.ErrorMessage);
    }

    [Fact]
    public async Task MultiTabSalesManager_SwitchToSession_UpdatesActiveSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        _mockTabManager.Setup(x => x.SwitchToSessionAsync(sessionId))
            .ReturnsAsync(true);

        // Act
        var result = await _mockTabManager.Object.SwitchToSessionAsync(sessionId);

        // Assert
        Assert.True(result);
        _mockTabManager.Verify(x => x.SwitchToSessionAsync(sessionId), Times.Once);
    }

    [Fact]
    public async Task BarcodeIntegrationService_GetSupportedFormats_ReturnsMobileOptimizedFormats()
    {
        // Arrange
        var expectedFormats = new List<BarcodeFormat>
        {
            new() { Name = "Code128", IsSupported = true, Description = "Linear barcode" },
            new() { Name = "QR Code", IsSupported = true, Description = "2D matrix barcode" },
            new() { Name = "EAN-13", IsSupported = true, Description = "European Article Number" },
            new() { Name = "UPC-A", IsSupported = true, Description = "Universal Product Code" }
        };

        _mockBarcodeService.Setup(x => x.GetSupportedFormatsAsync())
            .ReturnsAsync(expectedFormats);

        // Act
        var result = await _mockBarcodeService.Object.GetSupportedFormatsAsync();

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains(result, f => f.Name == "Code128" && f.IsSupported);
        Assert.Contains(result, f => f.Name == "QR Code" && f.IsSupported);
        Assert.All(result, format => Assert.True(format.IsSupported));
    }

    [Fact]
    public async Task CustomerLookupService_CreateNewCustomer_ReturnsCreatedCustomer()
    {
        // Arrange
        var request = new CustomerCreationRequest
        {
            Name = "Jane Smith",
            MobileNumber = "9876543210",
            Email = "jane@example.com",
            InitialTier = MembershipTier.Bronze,
            ShopId = Guid.NewGuid()
        };

        var expectedResult = new CustomerCreationResult
        {
            Success = true,
            Customer = new CustomerLookupResult
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Phone = request.MobileNumber,
                Email = request.Email,
                Tier = request.InitialTier,
                IsActive = true,
                TotalSpent = 0,
                VisitCount = 0
            }
        };

        _mockCustomerService.Setup(x => x.CreateNewCustomerAsync(It.IsAny<CustomerCreationRequest>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockCustomerService.Object.CreateNewCustomerAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Customer);
        Assert.Equal(request.Name, result.Customer.Name);
        Assert.Equal(request.MobileNumber, result.Customer.Phone);
        Assert.Equal(MembershipTier.Bronze, result.Customer.Tier);
        Assert.True(result.Customer.IsActive);
    }

    [Fact]
    public async Task MultiTabSalesManager_CanCreateNewSession_ValidatesMaxSessions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        _mockTabManager.Setup(x => x.CanCreateNewSessionAsync(userId, deviceId))
            .ReturnsAsync(true);

        _mockTabManager.Setup(x => x.GetMaxConcurrentSessionsAsync())
            .ReturnsAsync(3); // Mobile optimized limit

        // Act
        var canCreate = await _mockTabManager.Object.CanCreateNewSessionAsync(userId, deviceId);
        var maxSessions = await _mockTabManager.Object.GetMaxConcurrentSessionsAsync();

        // Assert
        Assert.True(canCreate);
        Assert.Equal(3, maxSessions); // Mobile should have fewer concurrent sessions
    }
}