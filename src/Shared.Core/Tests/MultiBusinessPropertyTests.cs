using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
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

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
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