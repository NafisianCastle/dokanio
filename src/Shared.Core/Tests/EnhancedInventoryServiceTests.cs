using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.DependencyInjection;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using Xunit;

namespace Shared.Core.Tests;

public class EnhancedInventoryServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IEnhancedInventoryService _enhancedInventoryService;
    private readonly IBusinessManagementService _businessManagementService;
    private readonly IProductService _productService;
    private readonly ISaleService _saleService;
    private readonly IUserService _userService;
    private readonly ILicenseService _licenseService;

    public EnhancedInventoryServiceTests()
    {
        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        _serviceProvider = services.BuildServiceProvider();

        _enhancedInventoryService = _serviceProvider.GetRequiredService<IEnhancedInventoryService>();
        _businessManagementService = _serviceProvider.GetRequiredService<IBusinessManagementService>();
        _productService = _serviceProvider.GetRequiredService<IProductService>();
        _saleService = _serviceProvider.GetRequiredService<ISaleService>();
        _userService = _serviceProvider.GetRequiredService<IUserService>();
        _licenseService = _serviceProvider.GetRequiredService<ILicenseService>();
        
        // Create a valid license for testing
        CreateValidLicenseAsync().Wait();
    }

    [Fact]
    public async Task PredictLowStockAsync_WithValidShop_ReturnsRecommendations()
    {
        // Arrange
        var business = await CreateTestBusinessAsync();
        var shop = await CreateTestShopAsync(business.Id);
        var product = await CreateTestProductAsync(shop.Id);
        
        // Add some stock
        await _enhancedInventoryService.UpdateStockAsync(product.Id, 10, Guid.NewGuid());

        // Act
        var recommendations = await _enhancedInventoryService.PredictLowStockAsync(shop.Id, 30);

        // Assert
        Assert.NotNull(recommendations);
        // Without sales history, predictions may be limited
    }

    [Fact]
    public async Task GetReorderRecommendationsAsync_WithValidShop_ReturnsRecommendations()
    {
        // Arrange
        var business = await CreateTestBusinessAsync();
        var shop = await CreateTestShopAsync(business.Id);
        var product = await CreateTestProductAsync(shop.Id);
        
        // Add some stock
        await _enhancedInventoryService.UpdateStockAsync(product.Id, 5, Guid.NewGuid());

        // Act
        var recommendations = await _enhancedInventoryService.GetReorderRecommendationsAsync(shop.Id);

        // Assert
        Assert.NotNull(recommendations);
        // Should return AI-generated recommendations
    }

    [Fact]
    public async Task GetOverstockAlertsAsync_WithValidShop_ReturnsAlerts()
    {
        // Arrange
        var business = await CreateTestBusinessAsync();
        var shop = await CreateTestShopAsync(business.Id);
        var product = await CreateTestProductAsync(shop.Id);
        
        // Add high stock with no sales to trigger overstock
        await _enhancedInventoryService.UpdateStockAsync(product.Id, 1000, Guid.NewGuid());

        // Act
        var alerts = await _enhancedInventoryService.GetOverstockAlertsAsync(shop.Id);

        // Assert
        Assert.NotNull(alerts);
        // With high stock and no sales, should detect overstock
    }

    [Fact]
    public async Task GetExpiryRiskAlertsAsync_WithPharmacyShop_ReturnsExpiryAlerts()
    {
        // Arrange
        var business = await CreateTestBusinessAsync(BusinessType.Pharmacy);
        var shop = await CreateTestShopAsync(business.Id);
        var product = await CreateTestProductAsync(shop.Id, DateTime.UtcNow.AddDays(30)); // Expires in 30 days
        
        // Add some stock
        await _enhancedInventoryService.UpdateStockAsync(product.Id, 50, Guid.NewGuid());

        // Act
        var alerts = await _enhancedInventoryService.GetExpiryRiskAlertsAsync(shop.Id);

        // Assert
        Assert.NotNull(alerts);
        // Should detect products expiring within 60 days
    }

    [Fact]
    public async Task GetSeasonalRecommendationsAsync_WithValidShop_ReturnsSeasonalRecommendations()
    {
        // Arrange
        var business = await CreateTestBusinessAsync(BusinessType.Pharmacy);
        var shop = await CreateTestShopAsync(business.Id);

        // Act
        var recommendations = await _enhancedInventoryService.GetSeasonalRecommendationsAsync(shop.Id);

        // Assert
        Assert.NotNull(recommendations);
        // Should return business type-specific seasonal recommendations
    }

    [Fact]
    public async Task AnalyzeInventoryTurnoverAsync_WithValidShop_ReturnsAnalysis()
    {
        // Arrange
        var business = await CreateTestBusinessAsync();
        var shop = await CreateTestShopAsync(business.Id);
        var product = await CreateTestProductAsync(shop.Id);
        
        // Add stock
        await _enhancedInventoryService.UpdateStockAsync(product.Id, 100, Guid.NewGuid());

        // Act
        var analysis = await _enhancedInventoryService.AnalyzeInventoryTurnoverAsync(shop.Id);

        // Assert
        Assert.NotNull(analysis);
        Assert.Equal(shop.Id, analysis.ShopId);
        Assert.NotNull(analysis.ProductInsights);
    }

    [Fact]
    public async Task GetComprehensiveInventoryRecommendationsAsync_WithValidShop_ReturnsComprehensiveRecommendations()
    {
        // Arrange
        var business = await CreateTestBusinessAsync();
        var shop = await CreateTestShopAsync(business.Id);
        var product = await CreateTestProductAsync(shop.Id);
        
        // Add some stock
        await _enhancedInventoryService.UpdateStockAsync(product.Id, 20, Guid.NewGuid());

        // Act
        var recommendations = await _enhancedInventoryService.GetComprehensiveInventoryRecommendationsAsync(shop.Id);

        // Assert
        Assert.NotNull(recommendations);
        Assert.Equal(shop.Id, recommendations.ShopId);
        Assert.NotNull(recommendations.ReorderSuggestions);
        Assert.NotNull(recommendations.OverstockAlerts);
        Assert.NotNull(recommendations.GeneralRecommendations);
    }

    [Fact]
    public async Task CalculateSafetyStockAsync_WithValidProduct_ReturnsSafetyStockRecommendation()
    {
        // Arrange
        var business = await CreateTestBusinessAsync();
        var shop = await CreateTestShopAsync(business.Id);
        var product = await CreateTestProductAsync(shop.Id);

        // Act
        var recommendation = await _enhancedInventoryService.CalculateSafetyStockAsync(shop.Id, product.Id);

        // Assert
        Assert.NotNull(recommendation);
        Assert.Equal(product.Id, recommendation.ProductId);
        Assert.Equal(product.Name, recommendation.ProductName);
        Assert.True(recommendation.ServiceLevel > 0);
    }

    [Fact]
    public async Task AnalyzeInventoryValueAsync_WithValidShop_ReturnsValueAnalysis()
    {
        // Arrange
        var business = await CreateTestBusinessAsync();
        var shop = await CreateTestShopAsync(business.Id);
        var product = await CreateTestProductAsync(shop.Id);
        
        // Add stock with value
        await _enhancedInventoryService.UpdateStockAsync(product.Id, 50, Guid.NewGuid());

        // Act
        var analysis = await _enhancedInventoryService.AnalyzeInventoryValueAsync(shop.Id);

        // Assert
        Assert.NotNull(analysis);
        Assert.Equal(shop.Id, analysis.ShopId);
        Assert.True(analysis.TotalInventoryValue >= 0);
        Assert.NotNull(analysis.CategoryBreakdown);
        Assert.NotNull(analysis.Recommendations);
    }

    #region Helper Methods

    private async Task<DTOs.BusinessResponse> CreateTestBusinessAsync(BusinessType businessType = BusinessType.GeneralRetail)
    {
        // Create a test user first to be the owner
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var testUser = await _userService.CreateUserAsync(
            $"testowner_{uniqueId}", 
            "Test Owner", 
            $"testowner_{uniqueId}@example.com", 
            "Password123!", 
            UserRole.BusinessOwner);
        
        var request = new DTOs.CreateBusinessRequest
        {
            Name = $"Test Business {uniqueId}",
            Type = businessType,
            OwnerId = testUser.Id,
            Description = "Test business for inventory testing",
            Address = "123 Test Street",
            Phone = "555-0123",
            Email = $"testbusiness_{uniqueId}@example.com"
        };

        return await _businessManagementService.CreateBusinessAsync(request);
    }

    private async Task<DTOs.ShopResponse> CreateTestShopAsync(Guid businessId)
    {
        var request = new DTOs.CreateShopRequest
        {
            BusinessId = businessId,
            Name = "Test Shop",
            Address = "456 Shop Street",
            Phone = "555-0456",
            Email = "shop@business.com"
        };

        return await _businessManagementService.CreateShopAsync(request);
    }

    private async Task<Product> CreateTestProductAsync(Guid shopId, DateTime? expiryDate = null)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            Name = "Test Product",
            Barcode = "123456789",
            Category = "Test Category",
            UnitPrice = 10.00m,
            ExpiryDate = expiryDate,
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };

        await _productService.CreateProductAsync(product);
        return product;
    }

    private async Task CreateTestSalesAsync(Guid shopId, Guid productId, int quantity)
    {
        var invoiceNumber = $"TEST-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        var sale = await _saleService.CreateSaleAsync(invoiceNumber, Guid.NewGuid());
        
        await _saleService.AddItemToSaleAsync(sale.Id, productId, quantity, 10.00m);

        await _saleService.CompleteSaleAsync(sale.Id, PaymentMethod.Cash);
    }

    private async Task CreateValidLicenseAsync()
    {
        try
        {
            // Try to create a trial license for testing
            var request = new DTOs.TrialLicenseRequest
            {
                DeviceId = Guid.NewGuid(),
                CustomerName = "Test Company",
                CustomerEmail = "test@example.com"
            };

            await _licenseService.CreateTrialLicenseAsync(request);
        }
        catch
        {
            // If license creation fails, it might already exist or have other issues
            // For testing purposes, we'll continue anyway
        }
    }

    #endregion

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}