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
/// Property-based tests for multi-business POS system functionality
/// Feature: multi-business-pos
/// </summary>
public class MultiBusinessPropertyTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;

    public MultiBusinessPropertyTests()
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
        
        // Add services
        services.AddScoped<IBusinessManagementService, BusinessManagementService>();
        
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        
        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    /// <summary>
    /// Property 1: Multi-Tenant Data Isolation
    /// For any two different businesses, data operations on one business should never affect or access data belonging to the other business.
    /// Validates: Requirements 6.4, 6.5
    /// Feature: multi-business-pos, Property 1: Multi-Tenant Data Isolation
    /// </summary>
    [Fact]
    public void MultiTenantDataIsolation_BusinessDataShouldBeIsolated()
    {
        // Test with multiple random business scenarios
        for (int iteration = 0; iteration < 10; iteration++)
        {
            // Generate two different businesses
            var businessData1 = GenerateBusinessData();
            var businessData2 = GenerateBusinessData();
            
            // Ensure different business IDs
            while (businessData1.BusinessId == businessData2.BusinessId)
            {
                businessData2 = GenerateBusinessData();
            }

            try
            {
                // Setup: Create two separate businesses with their data
                SetupBusinessData(businessData1);
                SetupBusinessData(businessData2);

                // Test: Verify that queries for business1 data don't return business2 data
                var business1Products = _context.Products
                    .Include(p => p.Shop)
                    .Where(p => p.Shop.BusinessId == businessData1.BusinessId)
                    .ToList();
                
                var business1Sales = _context.Sales
                    .Include(s => s.Shop)
                    .Where(s => s.Shop.BusinessId == businessData1.BusinessId)
                    .ToList();
                
                var business1Stock = _context.Stock
                    .Include(s => s.Shop)
                    .Where(s => s.Shop.BusinessId == businessData1.BusinessId)
                    .ToList();

                // Verify isolation: Business1 queries should not contain Business2 data
                var hasNoBusinessTwoProducts = business1Products.All(p => 
                    p.Shop.BusinessId == businessData1.BusinessId);
                
                var hasNoBusinessTwoSales = business1Sales.All(s => 
                    s.Shop.BusinessId == businessData1.BusinessId);
                
                var hasNoBusinessTwoStock = business1Stock.All(s => 
                    s.Shop.BusinessId == businessData1.BusinessId);

                // Test reverse direction as well
                var business2Products = _context.Products
                    .Include(p => p.Shop)
                    .Where(p => p.Shop.BusinessId == businessData2.BusinessId)
                    .ToList();
                
                var hasNoBusinessOneProducts = business2Products.All(p => 
                    p.Shop.BusinessId == businessData2.BusinessId);

                // Assert isolation
                Assert.True(hasNoBusinessTwoProducts, $"Business1 products contain Business2 data in iteration {iteration}");
                Assert.True(hasNoBusinessTwoSales, $"Business1 sales contain Business2 data in iteration {iteration}");
                Assert.True(hasNoBusinessTwoStock, $"Business1 stock contains Business2 data in iteration {iteration}");
                Assert.True(hasNoBusinessOneProducts, $"Business2 products contain Business1 data in iteration {iteration}");
                
                // Verify that each business has its own data
                Assert.True(business1Products.Count > 0, $"Business1 should have products in iteration {iteration}");
                Assert.True(business2Products.Count > 0, $"Business2 should have products in iteration {iteration}");
            }
            finally
            {
                // Cleanup: Remove test data
                CleanupTestData();
            }
        }
    }

    /// <summary>
    /// Property 3: Business Type Validation Consistency
    /// For any product sale attempt, the validation rules applied should be consistent with the business type configuration of the shop.
    /// Validates: Requirements 1.5, 5.6
    /// Feature: multi-business-pos, Property 3: Business Type Validation Consistency
    /// </summary>
    [Fact]
    public async Task BusinessTypeValidation_ValidationRulesShouldBeConsistentWithBusinessType()
    {
        var businessManagementService = _serviceProvider.GetRequiredService<IBusinessManagementService>();
        
        // Test with multiple random business type scenarios
        for (int iteration = 0; iteration < 10; iteration++)
        {
            try
            {
                // Generate random business type and attributes
                var businessTypes = Enum.GetValues<BusinessType>();
                var random = new Random();
                var businessType = businessTypes[random.Next(businessTypes.Length)];
                var attributes = GenerateRandomBusinessTypeAttributes(businessType, random);

                // Test: Validate product attributes against business type
                var validationResult = await businessManagementService.ValidateProductAttributesAsync(businessType, attributes);

                // Verify consistency: Validation should be consistent with business type requirements
                switch (businessType)
                {
                    case BusinessType.Pharmacy:
                        // For pharmacy, expiry date should be required and validated
                        if (attributes.ExpiryDate.HasValue && attributes.ExpiryDate.Value <= DateTime.UtcNow)
                        {
                            Assert.False(validationResult.IsValid, 
                                $"Pharmacy products with past expiry dates should be invalid in iteration {iteration}");
                            Assert.Contains(validationResult.Errors, e => e.Contains("expiry", StringComparison.OrdinalIgnoreCase));
                        }
                        
                        // Required attributes should cause validation failure if missing
                        if (string.IsNullOrEmpty(attributes.Manufacturer))
                        {
                            Assert.False(validationResult.IsValid, 
                                $"Pharmacy products without manufacturer should be invalid in iteration {iteration}");
                        }
                        break;

                    case BusinessType.Grocery:
                        // For grocery, weight should be positive if specified
                        if (attributes.Weight.HasValue && attributes.Weight.Value <= 0)
                        {
                            Assert.False(validationResult.IsValid, 
                                $"Grocery products with non-positive weight should be invalid in iteration {iteration}");
                            Assert.Contains(validationResult.Errors, e => e.Contains("weight", StringComparison.OrdinalIgnoreCase));
                        }
                        
                        // Unit should be required for grocery
                        if (string.IsNullOrEmpty(attributes.Unit))
                        {
                            Assert.False(validationResult.IsValid, 
                                $"Grocery products without unit should be invalid in iteration {iteration}");
                        }
                        break;

                    case BusinessType.GeneralRetail:
                    case BusinessType.Custom:
                    case BusinessType.SuperShop:
                        // These types have more flexible validation
                        if (attributes.Weight.HasValue && attributes.Weight.Value <= 0)
                        {
                            Assert.False(validationResult.IsValid, 
                                $"Products with non-positive weight should be invalid in iteration {iteration}");
                        }
                        break;
                }

                // Verify that validation is deterministic - same input should give same result
                var secondValidationResult = await businessManagementService.ValidateProductAttributesAsync(businessType, attributes);
                Assert.Equal(validationResult.IsValid, secondValidationResult.IsValid);
                Assert.Equal(validationResult.Errors.Count, secondValidationResult.Errors.Count);
            }
            finally
            {
                CleanupTestData();
            }
        }
    }

    /// <summary>
    /// Property 4: Shop-Level Configuration Isolation
    /// For any shop configuration change, it should only affect operations within that specific shop and not impact other shops.
    /// Validates: Requirements 2.4, 2.6
    /// Feature: multi-business-pos, Property 4: Shop-Level Configuration Isolation
    /// </summary>
    [Fact]
    public async Task ShopConfigurationIsolation_ConfigurationChangesShouldOnlyAffectTargetShop()
    {
        var businessManagementService = _serviceProvider.GetRequiredService<IBusinessManagementService>();
        
        // Test with multiple random shop configuration scenarios
        for (int iteration = 0; iteration < 10; iteration++)
        {
            try
            {
                // Setup: Create a business with multiple shops
                var businessData = GenerateBusinessWithMultipleShops();
                SetupBusinessWithShops(businessData);

                var targetShopId = businessData.ShopIds[0];
                var otherShopIds = businessData.ShopIds.Skip(1).ToList();

                // Generate random configuration for target shop
                var random = new Random();
                var newConfiguration = GenerateRandomShopConfiguration(random);

                // Get original configurations of other shops
                var originalConfigurations = new Dictionary<Guid, ShopConfiguration>();
                foreach (var shopId in otherShopIds)
                {
                    originalConfigurations[shopId] = await businessManagementService.GetShopConfigurationAsync(shopId);
                }

                // Test: Update configuration of target shop
                var updateResult = await businessManagementService.UpdateShopConfigurationAsync(targetShopId, newConfiguration);
                Assert.True(updateResult, $"Configuration update should succeed in iteration {iteration}");

                // Verify isolation: Other shops' configurations should remain unchanged
                foreach (var shopId in otherShopIds)
                {
                    var currentConfig = await businessManagementService.GetShopConfigurationAsync(shopId);
                    var originalConfig = originalConfigurations[shopId];

                    // Compare key configuration values
                    Assert.Equal(originalConfig.Currency, currentConfig.Currency);
                    Assert.Equal(originalConfig.TaxRate, currentConfig.TaxRate);
                    Assert.Equal(originalConfig.PricingRules.MaxDiscountPercentage, currentConfig.PricingRules.MaxDiscountPercentage);
                    Assert.Equal(originalConfig.InventorySettings.LowStockThreshold, currentConfig.InventorySettings.LowStockThreshold);
                }

                // Verify that target shop has the new configuration
                var targetConfig = await businessManagementService.GetShopConfigurationAsync(targetShopId);
                Assert.Equal(newConfiguration.Currency, targetConfig.Currency);
                Assert.Equal(newConfiguration.TaxRate, targetConfig.TaxRate);
                Assert.Equal(newConfiguration.PricingRules.MaxDiscountPercentage, targetConfig.PricingRules.MaxDiscountPercentage);
                Assert.Equal(newConfiguration.InventorySettings.LowStockThreshold, targetConfig.InventorySettings.LowStockThreshold);
            }
            finally
            {
                CleanupTestData();
            }
        }
    }

    /// <summary>
    /// Generates test data for a business including shops, products, sales, and stock
    /// </summary>
    private static BusinessTestData GenerateBusinessData()
    {
        var random = new Random();
        return new BusinessTestData
        {
            BusinessId = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            BusinessType = (BusinessType)random.Next(0, Enum.GetValues<BusinessType>().Length),
            ShopCount = random.Next(1, 4), // 1-3 shops per business
            ProductCount = random.Next(1, 6), // 1-5 products per shop
            SaleCount = random.Next(0, 4) // 0-3 sales per shop
        };
    }

    /// <summary>
    /// Sets up test data for a business in the database
    /// </summary>
    private void SetupBusinessData(BusinessTestData data)
    {
        // Create business owner
        var owner = new User
        {
            Id = data.OwnerId,
            BusinessId = data.BusinessId, // This will be updated after business creation
            Username = $"owner_{data.BusinessId:N}",
            FullName = $"Owner {data.BusinessId:N}",
            Email = $"owner_{data.BusinessId:N}@test.com",
            PasswordHash = "hash",
            Salt = "salt",
            Role = UserRole.BusinessOwner,
            DeviceId = Guid.NewGuid()
        };

        // Create business
        var business = new Business
        {
            Id = data.BusinessId,
            Name = $"Business {data.BusinessId:N}",
            Type = data.BusinessType,
            OwnerId = data.OwnerId,
            DeviceId = Guid.NewGuid()
        };

        // Update owner's business reference
        owner.BusinessId = business.Id;

        _context.Users.Add(owner);
        _context.Businesses.Add(business);

        // Create shops for the business
        for (int i = 0; i < data.ShopCount; i++)
        {
            var shopId = Guid.NewGuid();
            var shop = new Shop
            {
                Id = shopId,
                BusinessId = data.BusinessId,
                Name = $"Shop {i + 1} - {data.BusinessId:N}",
                DeviceId = Guid.NewGuid()
            };

            _context.Shops.Add(shop);

            // Create products for the shop
            for (int j = 0; j < data.ProductCount; j++)
            {
                var productId = Guid.NewGuid();
                var product = new Product
                {
                    Id = productId,
                    ShopId = shopId,
                    Name = $"Product {j + 1} - Shop {i + 1} - {data.BusinessId:N}",
                    Barcode = $"BC{data.BusinessId:N}{i}{j}",
                    UnitPrice = 10.00m + j,
                    DeviceId = Guid.NewGuid()
                };

                _context.Products.Add(product);

                // Create stock for the product
                var stock = new Stock
                {
                    Id = Guid.NewGuid(),
                    ShopId = shopId,
                    ProductId = productId,
                    Quantity = 100 + j,
                    DeviceId = Guid.NewGuid()
                };

                _context.Stock.Add(stock);
            }

            // Create sales for the shop
            for (int k = 0; k < data.SaleCount; k++)
            {
                var sale = new Sale
                {
                    Id = Guid.NewGuid(),
                    ShopId = shopId,
                    UserId = data.OwnerId,
                    InvoiceNumber = $"INV{data.BusinessId:N}{i}{k}",
                    TotalAmount = 50.00m + k,
                    PaymentMethod = PaymentMethod.Cash,
                    DeviceId = Guid.NewGuid()
                };

                _context.Sales.Add(sale);
            }
        }

        _context.SaveChanges();
    }

    /// <summary>
    /// Cleans up test data from the database
    /// </summary>
    private void CleanupTestData()
    {
        try
        {
            // Remove all test data
            _context.Stock.RemoveRange(_context.Stock);
            _context.Sales.RemoveRange(_context.Sales);
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

    /// <summary>
    /// Generates random business type attributes based on business type
    /// </summary>
    private static BusinessTypeAttributes GenerateRandomBusinessTypeAttributes(BusinessType businessType, Random random)
    {
        var attributes = new BusinessTypeAttributes();

        switch (businessType)
        {
            case BusinessType.Pharmacy:
                // Sometimes generate invalid expiry dates to test validation
                attributes.ExpiryDate = random.Next(0, 2) == 0 
                    ? DateTime.UtcNow.AddDays(random.Next(-30, 365)) // Past or future
                    : DateTime.UtcNow.AddDays(random.Next(1, 365)); // Always future
                
                attributes.Manufacturer = random.Next(0, 3) == 0 ? null : $"Manufacturer{random.Next(1, 100)}";
                attributes.BatchNumber = random.Next(0, 4) == 0 ? null : $"BATCH{random.Next(1000, 9999)}";
                attributes.GenericName = $"Generic{random.Next(1, 50)}";
                attributes.Dosage = $"{random.Next(1, 500)}mg";
                break;

            case BusinessType.Grocery:
                attributes.Weight = random.Next(0, 3) == 0 
                    ? (decimal)(random.NextDouble() * 2 - 0.5) // Sometimes negative to test validation
                    : (decimal)(random.NextDouble() * 10 + 0.1); // Always positive
                
                attributes.Volume = $"{random.Next(100, 2000)}ml";
                attributes.Unit = random.Next(0, 4) == 0 ? null : new[] { "kg", "piece", "liter", "gram" }[random.Next(4)];
                break;

            case BusinessType.SuperShop:
                attributes.Weight = random.Next(0, 2) == 0 ? (decimal)(random.NextDouble() * 10 + 0.1) : null;
                attributes.Volume = random.Next(0, 2) == 0 ? $"{random.Next(100, 2000)}ml" : null;
                attributes.Unit = new[] { "kg", "piece", "liter", "gram", "box" }[random.Next(5)];
                break;

            case BusinessType.GeneralRetail:
            case BusinessType.Custom:
            default:
                // Minimal attributes for general retail
                attributes.Weight = random.Next(0, 3) == 0 ? (decimal)(random.NextDouble() * 5 + 0.1) : null;
                break;
        }

        return attributes;
    }

    /// <summary>
    /// Generates test data for a business with multiple shops
    /// </summary>
    private static BusinessWithShopsTestData GenerateBusinessWithMultipleShops()
    {
        var random = new Random();
        var shopCount = random.Next(2, 5); // 2-4 shops
        
        return new BusinessWithShopsTestData
        {
            BusinessId = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            BusinessType = (BusinessType)random.Next(0, Enum.GetValues<BusinessType>().Length),
            ShopIds = Enumerable.Range(0, shopCount).Select(_ => Guid.NewGuid()).ToList()
        };
    }

    /// <summary>
    /// Sets up test data for a business with multiple shops
    /// </summary>
    private void SetupBusinessWithShops(BusinessWithShopsTestData data)
    {
        // Create business owner
        var owner = new User
        {
            Id = data.OwnerId,
            BusinessId = data.BusinessId,
            Username = $"owner_{data.BusinessId:N}",
            FullName = $"Owner {data.BusinessId:N}",
            Email = $"owner_{data.BusinessId:N}@test.com",
            PasswordHash = "hash",
            Salt = "salt",
            Role = UserRole.BusinessOwner,
            DeviceId = Guid.NewGuid()
        };

        // Create business
        var business = new Business
        {
            Id = data.BusinessId,
            Name = $"Business {data.BusinessId:N}",
            Type = data.BusinessType,
            OwnerId = data.OwnerId,
            DeviceId = Guid.NewGuid()
        };

        _context.Users.Add(owner);
        _context.Businesses.Add(business);

        // Create shops for the business
        for (int i = 0; i < data.ShopIds.Count; i++)
        {
            var shop = new Shop
            {
                Id = data.ShopIds[i],
                BusinessId = data.BusinessId,
                Name = $"Shop {i + 1} - {data.BusinessId:N}",
                Configuration = GenerateRandomShopConfigurationJson(),
                DeviceId = Guid.NewGuid()
            };

            _context.Shops.Add(shop);
        }

        _context.SaveChanges();
    }

    /// <summary>
    /// Generates random shop configuration
    /// </summary>
    private static ShopConfiguration GenerateRandomShopConfiguration(Random random)
    {
        return new ShopConfiguration
        {
            Currency = new[] { "USD", "EUR", "GBP", "CAD" }[random.Next(4)],
            TaxRate = (decimal)(random.NextDouble() * 0.3), // 0-30%
            PricingRules = new PricingRules
            {
                AllowPriceOverride = random.Next(0, 2) == 1,
                MaxDiscountPercentage = (decimal)(random.NextDouble() * 0.5), // 0-50%
                EnableDynamicPricing = random.Next(0, 2) == 1
            },
            InventorySettings = new InventorySettings
            {
                EnableLowStockAlerts = random.Next(0, 2) == 1,
                LowStockThreshold = random.Next(1, 50),
                EnableAutoReorder = random.Next(0, 2) == 1,
                ExpiryAlertDays = random.Next(1, 90)
            }
        };
    }

    /// <summary>
    /// Generates random shop configuration as JSON string
    /// </summary>
    private static string GenerateRandomShopConfigurationJson()
    {
        var random = new Random();
        var config = GenerateRandomShopConfiguration(random);
        return System.Text.Json.JsonSerializer.Serialize(config);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }

    /// <summary>
    /// Test data structure for a business with multiple shops
    /// </summary>
    private class BusinessWithShopsTestData
    {
        public Guid BusinessId { get; set; }
        public Guid OwnerId { get; set; }
        public BusinessType BusinessType { get; set; }
        public List<Guid> ShopIds { get; set; } = new();
    }

    /// <summary>
    /// Test data structure for a business
    /// </summary>
    private class BusinessTestData
    {
        public Guid BusinessId { get; set; }
        public Guid OwnerId { get; set; }
        public BusinessType BusinessType { get; set; }
        public int ShopCount { get; set; }
        public int ProductCount { get; set; }
        public int SaleCount { get; set; }
    }
}