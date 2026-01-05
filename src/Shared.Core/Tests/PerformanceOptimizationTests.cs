using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Shared.Core.Tests;

/// <summary>
/// Performance optimization and scalability tests for multi-tenant architecture
/// Tests database query optimization, caching strategies, and system performance
/// </summary>
public class PerformanceOptimizationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;
    private readonly IPerformanceOptimizationService _performanceService;
    private readonly IDatabaseQueryOptimizationService _queryOptimizationService;
    private readonly ICachingStrategyService _cachingService;

    public PerformanceOptimizationTests(ITestOutputHelper output)
    {
        _output = output;

        var services = new ServiceCollection();
        services.AddDbContext<PosDbContext>(options =>
            options.UseSqlite("Data Source=:memory:"));
        services.AddLogging(builder => builder.AddConsole());
        services.AddScoped<IPerformanceOptimizationService, PerformanceOptimizationService>();
        services.AddScoped<IDatabaseQueryOptimizationService, DatabaseQueryOptimizationService>();
        services.AddScoped<ICachingStrategyService, CachingStrategyService>();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        _performanceService = _serviceProvider.GetRequiredService<IPerformanceOptimizationService>();
        _queryOptimizationService = _serviceProvider.GetRequiredService<IDatabaseQueryOptimizationService>();
        _cachingService = _serviceProvider.GetRequiredService<ICachingStrategyService>();

        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task DatabaseQueryOptimization_ShouldImproveQueryPerformance()
    {
        // Arrange
        var testData = await CreateTestDataAsync();
        var businessId = testData.Business.Id;
        var shopId = testData.Shop.Id;

        // Act & Assert - Test optimized business query
        var stopwatch = Stopwatch.StartNew();
        var businesses = await _queryOptimizationService.GetBusinessesOptimizedAsync(testData.Business.OwnerId);
        stopwatch.Stop();

        Assert.NotEmpty(businesses);
        Assert.True(stopwatch.ElapsedMilliseconds < 100, $"Business query took {stopwatch.ElapsedMilliseconds}ms, expected < 100ms");
        _output.WriteLine($"Optimized business query completed in {stopwatch.ElapsedMilliseconds}ms");

        // Test optimized shop query with pagination
        stopwatch.Restart();
        var shops = await _queryOptimizationService.GetShopsOptimizedAsync(businessId, 0, 10);
        stopwatch.Stop();

        Assert.NotEmpty(shops);
        Assert.True(stopwatch.ElapsedMilliseconds < 50, $"Shop query took {stopwatch.ElapsedMilliseconds}ms, expected < 50ms");
        _output.WriteLine($"Optimized shop query completed in {stopwatch.ElapsedMilliseconds}ms");

        // Test optimized product query
        stopwatch.Restart();
        var products = await _queryOptimizationService.GetProductsOptimizedAsync(shopId);
        stopwatch.Stop();

        Assert.NotEmpty(products);
        Assert.True(stopwatch.ElapsedMilliseconds < 50, $"Product query took {stopwatch.ElapsedMilliseconds}ms, expected < 50ms");
        _output.WriteLine($"Optimized product query completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task CachingStrategy_ShouldReduceQueryTime()
    {
        // Arrange
        var testData = await CreateTestDataAsync();
        var cacheKey = $"test_business_{testData.Business.Id}";

        // Act - First call (cache miss)
        var stopwatch = Stopwatch.StartNew();
        var firstResult = await _cachingService.GetWithFallbackAsync(cacheKey, async () =>
        {
            await Task.Delay(100); // Simulate slow data retrieval
            return testData.Business;
        });
        stopwatch.Stop();
        var firstCallTime = stopwatch.ElapsedMilliseconds;

        // Second call (cache hit)
        stopwatch.Restart();
        var secondResult = await _cachingService.GetWithFallbackAsync(cacheKey, async () =>
        {
            await Task.Delay(100); // This should not be called
            return testData.Business;
        });
        stopwatch.Stop();
        var secondCallTime = stopwatch.ElapsedMilliseconds;

        // Assert
        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);
        Assert.Equal(firstResult.Id, secondResult.Id);
        Assert.True(secondCallTime < firstCallTime / 2, 
            $"Cache hit took {secondCallTime}ms, expected < {firstCallTime / 2}ms (first call: {firstCallTime}ms)");
        
        _output.WriteLine($"First call (cache miss): {firstCallTime}ms");
        _output.WriteLine($"Second call (cache hit): {secondCallTime}ms");
        _output.WriteLine($"Performance improvement: {((double)(firstCallTime - secondCallTime) / firstCallTime * 100):F1}%");
    }

    [Fact]
    public async Task MultiTenantConcurrency_ShouldHandleMultipleBusinesses()
    {
        // Arrange
        var businesses = new List<Business>();
        var shops = new List<Shop>();
        
        // Create multiple businesses with shops
        for (int i = 0; i < 10; i++)
        {
            var business = new Business
            {
                Id = Guid.NewGuid(),
                Name = $"Test Business {i}",
                Type = BusinessType.GeneralRetail,
                OwnerId = Guid.NewGuid(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            businesses.Add(business);

            for (int j = 0; j < 5; j++)
            {
                var shop = new Shop
                {
                    Id = Guid.NewGuid(),
                    BusinessId = business.Id,
                    Name = $"Shop {j} for Business {i}",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                shops.Add(shop);
            }
        }

        _context.Businesses.AddRange(businesses);
        _context.Shops.AddRange(shops);
        await _context.SaveChangesAsync();

        // Act - Concurrent queries for different businesses
        var tasks = businesses.Select(async business =>
        {
            var stopwatch = Stopwatch.StartNew();
            var businessShops = await _queryOptimizationService.GetShopsOptimizedAsync(business.Id);
            stopwatch.Stop();
            
            return new { Business = business, Shops = businessShops, Duration = stopwatch.ElapsedMilliseconds };
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, result =>
        {
            Assert.Equal(5, result.Shops.Count()); // Each business should have 5 shops
            Assert.True(result.Duration < 100, $"Query for business {result.Business.Name} took {result.Duration}ms, expected < 100ms");
        });

        var averageDuration = results.Average(r => r.Duration);
        var maxDuration = results.Max(r => r.Duration);
        
        _output.WriteLine($"Concurrent queries completed - Average: {averageDuration:F1}ms, Max: {maxDuration}ms");
        Assert.True(maxDuration < 200, $"Maximum query duration {maxDuration}ms exceeded threshold");
    }

    [Fact]
    public async Task MemoryOptimization_ShouldReduceMemoryUsage()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(false);
        var testData = await CreateLargeDataSetAsync();

        // Act - Create memory pressure
        var largeDataSets = new List<object>();
        for (int i = 0; i < 100; i++)
        {
            largeDataSets.Add(new byte[1024 * 1024]); // 1MB each
        }

        var memoryBeforeOptimization = GC.GetTotalMemory(false);
        
        // Optimize memory usage
        _performanceService.OptimizeMemoryUsage();
        largeDataSets.Clear(); // Release references
        
        var memoryAfterOptimization = GC.GetTotalMemory(false);

        // Assert
        var memoryReduction = memoryBeforeOptimization - memoryAfterOptimization;
        Assert.True(memoryReduction > 0, "Memory optimization should reduce memory usage");
        
        _output.WriteLine($"Initial memory: {initialMemory / 1024 / 1024}MB");
        _output.WriteLine($"Before optimization: {memoryBeforeOptimization / 1024 / 1024}MB");
        _output.WriteLine($"After optimization: {memoryAfterOptimization / 1024 / 1024}MB");
        _output.WriteLine($"Memory reduced by: {memoryReduction / 1024 / 1024}MB");
    }

    [Fact]
    public async Task BatchOperations_ShouldImproveNetworkEfficiency()
    {
        // Arrange
        var testData = await CreateTestDataAsync();
        var productIds = Enumerable.Range(1, 100).Select(_ => Guid.NewGuid()).ToList();

        // Act - Test batch operations
        var stopwatch = Stopwatch.StartNew();
        var results = await _performanceService.OptimizeBatchOperationsAsync(
            productIds,
            async batch =>
            {
                // Simulate network operation
                await Task.Delay(10);
                return batch.Select(id => $"Result for {id}");
            },
            batchSize: 10
        );
        stopwatch.Stop();

        // Assert
        Assert.Equal(100, results.Count());
        Assert.True(stopwatch.ElapsedMilliseconds < 500, 
            $"Batch operations took {stopwatch.ElapsedMilliseconds}ms, expected < 500ms");
        
        _output.WriteLine($"Batch operations completed in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Processed {results.Count()} items in batches of 10");
    }

    [Fact]
    public async Task DatabaseIndexOptimization_ShouldImproveQueryPlanning()
    {
        // Arrange
        await CreateLargeDataSetAsync();

        // Act
        var stopwatch = Stopwatch.StartNew();
        await _queryOptimizationService.OptimizeDatabaseIndexesAsync();
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, 
            $"Index optimization took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");
        
        _output.WriteLine($"Database index optimization completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task CacheStatistics_ShouldProvidePerformanceMetrics()
    {
        // Arrange
        var testData = await CreateTestDataAsync();
        
        // Populate cache with test data
        await _cachingService.SetMemoryCacheAsync("test1", testData.Business);
        await _cachingService.SetMemoryCacheAsync("test2", testData.Shop);
        await _cachingService.SetPersistentCacheAsync("test3", testData.Products.First());

        // Act
        var statistics = await _cachingService.GetCacheStatisticsAsync();

        // Assert
        Assert.True(statistics.MemoryCacheSize > 0);
        Assert.True(statistics.PersistentCacheSize > 0);
        Assert.True(statistics.TotalMemoryUsage > 0);
        
        _output.WriteLine($"Memory cache size: {statistics.MemoryCacheSize}");
        _output.WriteLine($"Persistent cache size: {statistics.PersistentCacheSize}");
        _output.WriteLine($"Total memory usage: {statistics.TotalMemoryUsage} bytes");
        _output.WriteLine($"Memory hit ratio: {statistics.MemoryHitRatio:P2}");
        _output.WriteLine($"Persistent hit ratio: {statistics.PersistentHitRatio:P2}");
    }

    [Fact]
    public async Task LowEndDevicePerformance_ShouldMeetPerformanceTargets()
    {
        // Arrange - Configure for low-end device
        _performanceService.ConfigureForDeviceCapability(DeviceCapability.LowEnd);
        var testData = await CreateTestDataAsync();

        // Act - Test various operations with performance targets for low-end devices
        var operations = new List<(string Name, Func<Task> Operation, int MaxDurationMs)>
        {
            ("Business Query", async () => await _queryOptimizationService.GetBusinessesOptimizedAsync(testData.Business.OwnerId), 200),
            ("Shop Query", async () => await _queryOptimizationService.GetShopsOptimizedAsync(testData.Business.Id), 150),
            ("Product Query", async () => await _queryOptimizationService.GetProductsOptimizedAsync(testData.Shop.Id), 150),
            ("Cache Operation", async () => await _cachingService.SetMemoryCacheAsync("test", testData.Business), 50),
            ("Memory Optimization", () => { _performanceService.OptimizeMemoryUsage(); return Task.CompletedTask; }, 100)
        };

        var results = new List<(string Name, long Duration, bool Passed)>();

        foreach (var (name, operation, maxDuration) in operations)
        {
            var stopwatch = Stopwatch.StartNew();
            await operation();
            stopwatch.Stop();

            var passed = stopwatch.ElapsedMilliseconds <= maxDuration;
            results.Add((name, stopwatch.ElapsedMilliseconds, passed));
            
            _output.WriteLine($"{name}: {stopwatch.ElapsedMilliseconds}ms (target: {maxDuration}ms) - {(passed ? "PASS" : "FAIL")}");
        }

        // Assert
        Assert.All(results, result => 
            Assert.True(result.Passed, $"{result.Name} took {result.Duration}ms, exceeded target"));
    }

    [Fact]
    public async Task ScalabilityTest_ShouldHandleLargeDataVolumes()
    {
        // Arrange - Create large dataset
        var businesses = new List<Business>();
        var shops = new List<Shop>();
        var products = new List<Product>();
        var sales = new List<Sale>();

        var ownerId = Guid.NewGuid();
        
        // Create 50 businesses
        for (int i = 0; i < 50; i++)
        {
            var business = new Business
            {
                Id = Guid.NewGuid(),
                Name = $"Scalability Test Business {i}",
                Type = BusinessType.GeneralRetail,
                OwnerId = ownerId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            businesses.Add(business);

            // 10 shops per business
            for (int j = 0; j < 10; j++)
            {
                var shop = new Shop
                {
                    Id = Guid.NewGuid(),
                    BusinessId = business.Id,
                    Name = $"Shop {j}",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                shops.Add(shop);

                // 100 products per shop
                for (int k = 0; k < 100; k++)
                {
                    var product = new Product
                    {
                        Id = Guid.NewGuid(),
                        ShopId = shop.Id,
                        Name = $"Product {k}",
                        Barcode = $"BAR{i:D3}{j:D2}{k:D3}",
                        Category = $"Category {k % 10}",
                        UnitPrice = 10.00m + k,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    products.Add(product);
                }

                // 50 sales per shop
                for (int s = 0; s < 50; s++)
                {
                    var sale = new Sale
                    {
                        Id = Guid.NewGuid(),
                        ShopId = shop.Id,
                        UserId = Guid.NewGuid(),
                        InvoiceNumber = $"INV{i:D3}{j:D2}{s:D3}",
                        TotalAmount = 100.00m + s,
                        PaymentMethod = PaymentMethod.Cash,
                        CreatedAt = DateTime.UtcNow.AddDays(-s),
                        UpdatedAt = DateTime.UtcNow
                    };
                    sales.Add(sale);
                }
            }
        }

        _context.Businesses.AddRange(businesses);
        _context.Shops.AddRange(shops);
        _context.Products.AddRange(products);
        _context.Sales.AddRange(sales);
        await _context.SaveChangesAsync();

        _output.WriteLine($"Created test data: {businesses.Count} businesses, {shops.Count} shops, {products.Count} products, {sales.Count} sales");

        // Act & Assert - Test scalability with large dataset
        var stopwatch = Stopwatch.StartNew();
        var businessResults = await _queryOptimizationService.GetBusinessesOptimizedAsync(ownerId);
        stopwatch.Stop();

        Assert.Equal(50, businessResults.Count());
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, 
            $"Large business query took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");
        
        _output.WriteLine($"Large dataset business query: {stopwatch.ElapsedMilliseconds}ms");

        // Test concurrent access to different shops
        var randomShops = shops.OrderBy(x => Guid.NewGuid()).Take(20).ToList();
        var concurrentTasks = randomShops.Select(async shop =>
        {
            var sw = Stopwatch.StartNew();
            var shopProducts = await _queryOptimizationService.GetProductsOptimizedAsync(shop.Id);
            sw.Stop();
            return new { Shop = shop, ProductCount = shopProducts.Count(), Duration = sw.ElapsedMilliseconds };
        });

        var concurrentResults = await Task.WhenAll(concurrentTasks);
        var maxConcurrentDuration = concurrentResults.Max(r => r.Duration);
        var avgConcurrentDuration = concurrentResults.Average(r => r.Duration);

        Assert.True(maxConcurrentDuration < 500, 
            $"Maximum concurrent query duration {maxConcurrentDuration}ms exceeded threshold");
        
        _output.WriteLine($"Concurrent scalability test - Avg: {avgConcurrentDuration:F1}ms, Max: {maxConcurrentDuration}ms");
    }

    private async Task<TestData> CreateTestDataAsync()
    {
        var business = new Business
        {
            Id = Guid.NewGuid(),
            Name = "Test Business",
            Type = BusinessType.GeneralRetail,
            OwnerId = Guid.NewGuid(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var shop = new Shop
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            Name = "Test Shop",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var products = new List<Product>();
        for (int i = 0; i < 10; i++)
        {
            products.Add(new Product
            {
                Id = Guid.NewGuid(),
                ShopId = shop.Id,
                Name = $"Test Product {i}",
                Barcode = $"TEST{i:D3}",
                Category = "Test Category",
                UnitPrice = 10.00m + i,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        _context.Businesses.Add(business);
        _context.Shops.Add(shop);
        _context.Products.AddRange(products);
        await _context.SaveChangesAsync();

        return new TestData { Business = business, Shop = shop, Products = products };
    }

    private async Task<TestData> CreateLargeDataSetAsync()
    {
        var testData = await CreateTestDataAsync();
        
        // Add more products for testing
        var additionalProducts = new List<Product>();
        for (int i = 10; i < 1000; i++)
        {
            additionalProducts.Add(new Product
            {
                Id = Guid.NewGuid(),
                ShopId = testData.Shop.Id,
                Name = $"Large Dataset Product {i}",
                Barcode = $"LARGE{i:D4}",
                Category = $"Category {i % 20}",
                UnitPrice = 10.00m + i,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        _context.Products.AddRange(additionalProducts);
        await _context.SaveChangesAsync();

        testData.Products.AddRange(additionalProducts);
        return testData;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }

    private class TestData
    {
        public Business Business { get; set; } = null!;
        public Shop Shop { get; set; } = null!;
        public List<Product> Products { get; set; } = new();
    }
}