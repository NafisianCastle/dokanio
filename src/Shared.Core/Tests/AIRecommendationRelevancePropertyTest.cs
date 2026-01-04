using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Shared.Core.Services;
using Xunit;

namespace Shared.Core.Tests;

/// <summary>
/// Property-based test for AI recommendation relevance
/// Feature: multi-business-pos, Property 8: AI Recommendation Relevance
/// </summary>
public class AIRecommendationRelevancePropertyTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;

    public AIRecommendationRelevancePropertyTest()
    {
        var services = new ServiceCollection();
        
        // Add in-memory database
        services.AddDbContext<PosDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole());
        
        // Add repositories
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IBusinessRepository, BusinessRepository>();
        services.AddScoped<IShopRepository, ShopRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IDiscountRepository, DiscountRepository>();
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        services.AddScoped<ILicenseRepository, LicenseRepository>();
        services.AddScoped<ISaleItemRepository, SaleItemRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        
        // Add services
        services.AddScoped<IBusinessManagementService, BusinessManagementService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IEncryptionService, EncryptionService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IEnhancedSalesService, EnhancedSalesService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IWeightBasedPricingService, WeightBasedPricingService>();
        services.AddScoped<IMembershipService, MembershipService>();
        services.AddScoped<IDiscountService, DiscountService>();
        services.AddScoped<IConfigurationService, ConfigurationService>();
        services.AddScoped<ILicenseService, LicenseService>();
        services.AddSingleton<ICurrentUserService, CurrentUserService>();
        
        // Add AI Analytics Engine
        services.AddScoped<IAIAnalyticsEngine, AIAnalyticsEngine>();
        
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        
        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    /// <summary>
    /// Property 8: AI Recommendation Relevance
    /// For any AI-generated recommendation, it should be based on data from the correct business context and shop configuration.
    /// Validates: Requirements 10.4, 10.6
    /// Feature: multi-business-pos, Property 8: AI Recommendation Relevance
    /// </summary>
    [Fact]
    public async Task AIRecommendationRelevance_RecommendationsShouldBeBasedOnCorrectBusinessContextAndShopConfiguration()
    {
        var aiAnalyticsEngine = _serviceProvider.GetRequiredService<IAIAnalyticsEngine>();
        
        // Test with multiple random business scenarios
        for (int iteration = 0; iteration < 3; iteration++)
        {
            try
            {
                // Setup: Create multiple businesses with different types and shops
                var testData = GenerateRandomAITestData();
                await SetupAITestDataAsync(testData);

                // Test 1: Product recommendations should be from the correct shop
                foreach (var shopData in testData.ShopData)
                {
                    var productRecommendations = await aiAnalyticsEngine.GetProductRecommendationsAsync(shopData.ShopId);
                    
                    Assert.NotNull(productRecommendations);
                    Assert.Equal(shopData.ShopId, productRecommendations.ShopId);
                    
                    // Verify all recommended products belong to the correct shop context
                    foreach (var recommendation in productRecommendations.CrossSellRecommendations)
                    {
                        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == recommendation.ProductId);
                        if (product != null)
                        {
                            Assert.Equal(shopData.ShopId, product.ShopId);
                        }
                    }
                    
                    foreach (var recommendation in productRecommendations.UpSellRecommendations)
                    {
                        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == recommendation.ProductId);
                        if (product != null)
                        {
                            Assert.Equal(shopData.ShopId, product.ShopId);
                        }
                    }
                    
                    // Verify bundle recommendations contain products from the correct shop
                    foreach (var bundle in productRecommendations.BundleRecommendations)
                    {
                        foreach (var productId in bundle.ProductIds)
                        {
                            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
                            if (product != null)
                            {
                                Assert.Equal(shopData.ShopId, product.ShopId);
                            }
                        }
                    }
                }

                // Test 2: Inventory recommendations should respect business type
                foreach (var shopData in testData.ShopData)
                {
                    var inventoryRecommendations = await aiAnalyticsEngine.GenerateInventoryRecommendationsAsync(shopData.ShopId);
                    
                    Assert.NotNull(inventoryRecommendations);
                    Assert.Equal(shopData.ShopId, inventoryRecommendations.ShopId);
                    Assert.Equal(shopData.BusinessType, inventoryRecommendations.BusinessType);
                    
                    // Verify expiry risk alerts are only generated for pharmacy businesses
                    if (shopData.BusinessType == BusinessType.Pharmacy)
                    {
                        // Pharmacy should have expiry risk analysis
                        Assert.NotNull(inventoryRecommendations.ExpiryRisks);
                    }
                    else
                    {
                        // Non-pharmacy businesses should have empty or minimal expiry risks
                        Assert.True(inventoryRecommendations.ExpiryRisks.Count == 0 || 
                                   inventoryRecommendations.ExpiryRisks.All(e => e.RiskLevel == ExpiryRiskLevel.Low),
                                   $"Non-pharmacy business type {shopData.BusinessType} should not have significant expiry risks " +
                                   $"in iteration {iteration}");
                    }
                    
                    // Verify all reorder recommendations are for products in the correct shop
                    foreach (var reorderRec in inventoryRecommendations.ReorderSuggestions)
                    {
                        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == reorderRec.ProductId);
                        if (product != null)
                        {
                            Assert.Equal(shopData.ShopId, product.ShopId);
                        }
                    }
                }

                // Test 3: Sales insights should be scoped to the correct business
                foreach (var businessData in testData.BusinessData)
                {
                    var dateRange = new DateRange
                    {
                        StartDate = DateTime.UtcNow.AddDays(-30),
                        EndDate = DateTime.UtcNow
                    };
                    
                    var salesInsights = await aiAnalyticsEngine.AnalyzeSalesTrendsAsync(businessData.BusinessId, dateRange);
                    
                    Assert.NotNull(salesInsights);
                    Assert.Equal(businessData.BusinessId, salesInsights.BusinessId);
                    
                    // Verify product insights are from the correct business context
                    foreach (var productInsight in salesInsights.TopProducts.Concat(salesInsights.LowPerformingProducts))
                    {
                        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productInsight.ProductId);
                        if (product != null)
                        {
                            var shop = await _context.Shops.FirstOrDefaultAsync(s => s.Id == product.ShopId);
                            Assert.NotNull(shop);
                            Assert.Equal(businessData.BusinessId, shop.BusinessId);
                        }
                    }
                }

                // Test 4: Price optimization should respect business boundaries
                foreach (var businessData in testData.BusinessData)
                {
                    var priceOptimizations = await aiAnalyticsEngine.AnalyzePricingOpportunitiesAsync(businessData.BusinessId);
                    
                    Assert.NotNull(priceOptimizations);
                    Assert.Equal(businessData.BusinessId, priceOptimizations.BusinessId);
                    
                    // Verify all price optimizations are for products within the business
                    foreach (var optimization in priceOptimizations.Optimizations)
                    {
                        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == optimization.ProductId);
                        if (product != null)
                        {
                            var shop = await _context.Shops.FirstOrDefaultAsync(s => s.Id == product.ShopId);
                            Assert.NotNull(shop);
                            Assert.Equal(businessData.BusinessId, shop.BusinessId);
                        }
                    }
                }
            }
            finally
            {
                CleanupTestData();
            }
        }
    }

    private AITestData GenerateRandomAITestData()
    {
        var random = new Random();
        var businessCount = random.Next(1, 3); // 1-2 businesses
        var testData = new AITestData();
        
        for (int i = 0; i < businessCount; i++)
        {
            var businessId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var businessType = (BusinessType)random.Next(0, Enum.GetValues<BusinessType>().Length);
            
            var businessData = new BusinessTestData
            {
                BusinessId = businessId,
                OwnerId = ownerId,
                BusinessType = businessType
            };
            
            testData.BusinessData.Add(businessData);
            
            // Create 1-2 shops per business
            var shopCount = random.Next(1, 3);
            for (int j = 0; j < shopCount; j++)
            {
                var shopId = Guid.NewGuid();
                var shopData = new ShopTestData
                {
                    ShopId = shopId,
                    BusinessId = businessId,
                    BusinessType = businessType,
                    ProductIds = Enumerable.Range(0, random.Next(2, 4)).Select(_ => Guid.NewGuid()).ToList()
                };
                
                testData.ShopData.Add(shopData);
            }
        }
        
        return testData;
    }

    private async Task SetupAITestDataAsync(AITestData testData)
    {
        // Create businesses and owners
        foreach (var businessData in testData.BusinessData)
        {
            var owner = new User
            {
                Id = businessData.OwnerId,
                BusinessId = businessData.BusinessId,
                Username = $"owner_{businessData.OwnerId:N}",
                FullName = $"Owner {businessData.OwnerId:N}",
                Email = $"owner_{businessData.OwnerId:N}@test.com",
                PasswordHash = "hash",
                Salt = "salt",
                Role = UserRole.BusinessOwner,
                DeviceId = Guid.NewGuid()
            };

            var business = new Business
            {
                Id = businessData.BusinessId,
                Name = $"Business {businessData.BusinessId:N}",
                Type = businessData.BusinessType,
                OwnerId = businessData.OwnerId,
                DeviceId = Guid.NewGuid()
            };

            _context.Users.Add(owner);
            _context.Businesses.Add(business);
        }

        // Create shops and products
        foreach (var shopData in testData.ShopData)
        {
            var shop = new Shop
            {
                Id = shopData.ShopId,
                BusinessId = shopData.BusinessId,
                Name = $"Shop {shopData.ShopId:N}",
                DeviceId = Guid.NewGuid()
            };

            _context.Shops.Add(shop);

            // Create products for each shop
            for (int i = 0; i < shopData.ProductIds.Count; i++)
            {
                var productId = shopData.ProductIds[i];
                var product = new Product
                {
                    Id = productId,
                    ShopId = shopData.ShopId,
                    Name = $"Product {i + 1} - {shopData.BusinessType}",
                    Barcode = $"BC{productId:N}",
                    Category = GetCategoryForBusinessType(shopData.BusinessType, i),
                    UnitPrice = 10.00m + i,
                    DeviceId = Guid.NewGuid()
                };

                // Add business type specific attributes
                if (shopData.BusinessType == BusinessType.Pharmacy)
                {
                    product.ExpiryDate = DateTime.UtcNow.AddDays(30 + i * 10); // Varying expiry dates
                    product.BatchNumber = $"BATCH{i:D3}";
                }
                else if (shopData.BusinessType == BusinessType.Grocery)
                {
                    product.IsWeightBased = i % 2 == 0; // Some weight-based products
                    product.RatePerKilogram = product.IsWeightBased ? product.UnitPrice * 10 : null;
                }

                var stock = new Stock
                {
                    Id = Guid.NewGuid(),
                    ShopId = shopData.ShopId,
                    ProductId = productId,
                    Quantity = 50 + i * 5, // Varying stock levels
                    LastUpdatedAt = DateTime.UtcNow.AddMinutes(-i * 10),
                    DeviceId = Guid.NewGuid()
                };

                _context.Products.Add(product);
                _context.Stock.Add(stock);
            }

            // Create some sample sales data for analysis
            await CreateSampleSalesDataAsync(shopData);
        }

        await _context.SaveChangesAsync();
    }

    private async Task CreateSampleSalesDataAsync(ShopTestData shopData)
    {
        var random = new Random();
        var saleCount = random.Next(2, 5);
        
        for (int i = 0; i < saleCount; i++)
        {
            var sale = new Sale
            {
                Id = Guid.NewGuid(),
                ShopId = shopData.ShopId,
                UserId = Guid.NewGuid(), // Simplified - would need proper user
                InvoiceNumber = $"INV{i:D6}",
                TotalAmount = random.Next(20, 200),
                PaymentMethod = PaymentMethod.Cash,
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30)),
                DeviceId = Guid.NewGuid()
            };

            // Add some sale items
            var itemCount = random.Next(1, 4);
            for (int j = 0; j < itemCount && j < shopData.ProductIds.Count; j++)
            {
                var saleItem = new SaleItem
                {
                    Id = Guid.NewGuid(),
                    SaleId = sale.Id,
                    ProductId = shopData.ProductIds[j],
                    Quantity = random.Next(1, 5),
                    UnitPrice = 10.00m + j,
                    TotalPrice = (10.00m + j) * random.Next(1, 5)
                };
                
                _context.SaleItems.Add(saleItem);
            }

            _context.Sales.Add(sale);
        }
    }

    private string GetCategoryForBusinessType(BusinessType businessType, int index)
    {
        return businessType switch
        {
            BusinessType.Pharmacy => (index % 3) switch
            {
                0 => "Medicine",
                1 => "Vitamins",
                _ => "Health Supplements"
            },
            BusinessType.Grocery => (index % 3) switch
            {
                0 => "Fresh Produce",
                1 => "Dairy",
                _ => "Packaged Foods"
            },
            BusinessType.SuperShop => (index % 4) switch
            {
                0 => "Electronics",
                1 => "Clothing",
                2 => "Home & Garden",
                _ => "Food & Beverages"
            },
            _ => "General"
        };
    }

    private void CleanupTestData()
    {
        try
        {
            // Remove all test data in correct order (respecting foreign keys)
            _context.SaleItems.RemoveRange(_context.SaleItems);
            _context.Sales.RemoveRange(_context.Sales);
            _context.Stock.RemoveRange(_context.Stock);
            _context.Products.RemoveRange(_context.Products);
            _context.Shops.RemoveRange(_context.Shops);
            _context.Businesses.RemoveRange(_context.Businesses);
            _context.Users.RemoveRange(_context.Users);
            _context.SaveChanges();
        }
        catch (Exception)
        {
            // Ignore cleanup errors
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }

    private class AITestData
    {
        public List<BusinessTestData> BusinessData { get; set; } = new();
        public List<ShopTestData> ShopData { get; set; } = new();
    }

    private class BusinessTestData
    {
        public Guid BusinessId { get; set; }
        public Guid OwnerId { get; set; }
        public BusinessType BusinessType { get; set; }
    }

    private class ShopTestData
    {
        public Guid ShopId { get; set; }
        public Guid BusinessId { get; set; }
        public BusinessType BusinessType { get; set; }
        public List<Guid> ProductIds { get; set; } = new();
    }
}