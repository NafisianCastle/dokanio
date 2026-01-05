using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using System.Diagnostics;
using Xunit;

namespace Shared.Core.Tests;

/// <summary>
/// Basic performance tests for multi-tenant architecture
/// Tests core database operations and caching without complex dependencies
/// </summary>
public class BasicPerformanceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;
    private readonly IPerformanceOptimizationService _performanceService;

    // Performance targets
    private const int MAX_QUERY_TIME_MS = 500;
    private const int MAX_CACHE_OPERATION_TIME_MS = 50;
    private const int MAX_MEMORY_USAGE_MB = 100;

    public BasicPerformanceTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<PosDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddLogging();
        services.AddScoped<IPerformanceOptimizationService, PerformanceOptimizationService>();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        _performanceService = _serviceProvider.GetRequiredService<IPerformanceOptimizationService>();

        _context.Database.EnsureCreated();
        
        // Configure for low-end device performance
        _performanceService.ConfigureForDeviceCapability(DeviceCapability.LowEnd);
    }

    [Fact]
    public async Task DatabaseQueries_ShouldMeetPerformanceTargets()
    {
        // Arrange
        var testData = await CreateTestDataAsync();

        // Act & Assert - Test business query performance
        var stopwatch = Stopwatch.StartNew();
        var businesses = await _context.Businesses
            .Where(b => b.OwnerId == testData.Business.OwnerId && !b.IsDeleted)
            .ToListAsync();
        stopwatch.Stop();

        Assert.NotEmpty(businesses);
        Assert.True(stopwatch.ElapsedMilliseconds < MAX_QUERY_TIME_MS,
            $"Business query took {stopwatch.ElapsedMilliseconds}ms, expected < {MAX_QUERY_TIME_MS}ms");

        // Test shop query performance
        stopwatch.Restart();
        var shops = await _context.Shops
            .Where(s => s.BusinessId == testData.Business.Id && !s.IsDeleted)
            .ToListAsync();
        stopwatch.Stop();

        Assert.NotEmpty(shops);
        Assert.True(stopwatch.ElapsedMilliseconds < MAX_QUERY_TIME_MS,
            $"Shop query took {stopwatch.ElapsedMilliseconds}ms, expected < {MAX_QUERY_TIME_MS}ms");

        // Test product query performance
        stopwatch.Restart();
        var products = await _context.Products
            .Where(p => p.ShopId == testData.Shop.Id && !p.IsDeleted)
            .ToListAsync();
        stopwatch.Stop();

        Assert.NotEmpty(products);
        Assert.True(stopwatch.ElapsedMilliseconds < MAX_QUERY_TIME_MS,
            $"Product query took {stopwatch.ElapsedMilliseconds}ms, expected < {MAX_QUERY_TIME_MS}ms");
    }

    [Fact]
    public async Task CacheOperations_ShouldBeFast()
    {
        // Arrange
        var testData = await CreateTestDataAsync();
        var cacheKey = $"test_business_{testData.Business.Id}";

        // Act - Test cache set operation
        var stopwatch = Stopwatch.StartNew();
        var firstResult = await _performanceService.OptimizeQueryAsync(cacheKey, async () =>
        {
            await Task.Delay(100); // Simulate slow data retrieval
            return testData.Business;
        });
        stopwatch.Stop();
        var firstCallTime = stopwatch.ElapsedMilliseconds;

        // Test cache get operation
        stopwatch.Restart();
        var secondResult = await _performanceService.OptimizeQueryAsync(cacheKey, async () =>
        {
            await Task.Delay(100); // This should not be called due to cache hit
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
        Assert.True(secondCallTime < MAX_CACHE_OPERATION_TIME_MS,
            $"Cache operation took {secondCallTime}ms, expected < {MAX_CACHE_OPERATION_TIME_MS}ms");
    }

    [Fact]
    public async Task MemoryOptimization_ShouldReduceMemoryUsage()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(false);
        await CreateLargeDataSetAsync();

        // Act - Create memory pressure
        var largeDataSets = new List<object>();
        for (int i = 0; i < 50; i++)
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
        
        var netMemoryUsageMB = (memoryAfterOptimization - initialMemory) / 1024 / 1024;
        Assert.True(netMemoryUsageMB < MAX_MEMORY_USAGE_MB,
            $"Net memory usage {netMemoryUsageMB}MB exceeded limit of {MAX_MEMORY_USAGE_MB}MB");
    }

    [Fact]
    public async Task BatchOperations_ShouldImproveEfficiency()
    {
        // Arrange
        var testData = await CreateTestDataAsync();
        var productIds = Enumerable.Range(1, 50).Select(_ => Guid.NewGuid()).ToList();

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
        Assert.Equal(50, results.Count());
        Assert.True(stopwatch.ElapsedMilliseconds < 300,
            $"Batch operations took {stopwatch.ElapsedMilliseconds}ms, expected < 300ms");
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldMaintainPerformance()
    {
        // Arrange
        var testData = await CreateTestDataAsync();
        var concurrentTasks = 10;

        // Act - Simulate concurrent database operations
        var tasks = Enumerable.Range(1, concurrentTasks).Select(async i =>
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Simulate concurrent business queries
            var businesses = await _context.Businesses
                .Where(b => b.OwnerId == testData.Business.OwnerId && !b.IsDeleted)
                .ToListAsync();
            
            stopwatch.Stop();
            return new { TaskId = i, Duration = stopwatch.ElapsedMilliseconds, ResultCount = businesses.Count };
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        var maxDuration = results.Max(r => r.Duration);
        var avgDuration = results.Average(r => r.Duration);

        Assert.True(maxDuration < MAX_QUERY_TIME_MS * 2, // Allow 2x normal time for concurrent operations
            $"Maximum concurrent operation took {maxDuration}ms, expected < {MAX_QUERY_TIME_MS * 2}ms");
        
        Assert.All(results, result => 
            Assert.True(result.ResultCount > 0, $"Task {result.TaskId} should return results"));
    }

    [Fact]
    public async Task LargeDataSet_ShouldHandleScalability()
    {
        // Arrange - Create large dataset
        var businesses = new List<Business>();
        var shops = new List<Shop>();
        var products = new List<Product>();

        var ownerId = Guid.NewGuid();
        
        // Create 20 businesses
        for (int i = 0; i < 20; i++)
        {
            var business = new Business
            {
                Id = Guid.NewGuid(),
                Name = $"Scalability Test Business {i}",
                Type = BusinessType.GeneralRetail,
                OwnerId = ownerId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            businesses.Add(business);

            // 5 shops per business
            for (int j = 0; j < 5; j++)
            {
                var shop = new Shop
                {
                    Id = Guid.NewGuid(),
                    BusinessId = business.Id,
                    Name = $"Shop {j}",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                shops.Add(shop);

                // 50 products per shop
                for (int k = 0; k < 50; k++)
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
                        CreatedAt = DateTime.UtcNow
                    };
                    products.Add(product);
                }
            }
        }

        _context.Businesses.AddRange(businesses);
        _context.Shops.AddRange(shops);
        _context.Products.AddRange(products);
        await _context.SaveChangesAsync();

        // Act & Assert - Test scalability with large dataset
        var stopwatch = Stopwatch.StartNew();
        var businessResults = await _context.Businesses
            .Where(b => b.OwnerId == ownerId && !b.IsDeleted)
            .ToListAsync();
        stopwatch.Stop();

        Assert.Equal(20, businessResults.Count);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Large dataset business query took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");

        // Test concurrent access to different shops
        var randomShops = shops.OrderBy(x => Guid.NewGuid()).Take(10).ToList();
        var concurrentTasks = randomShops.Select(async shop =>
        {
            var sw = Stopwatch.StartNew();
            var shopProducts = await _context.Products
                .Where(p => p.ShopId == shop.Id && !p.IsDeleted)
                .ToListAsync();
            sw.Stop();
            return new { Shop = shop, ProductCount = shopProducts.Count, Duration = sw.ElapsedMilliseconds };
        });

        var concurrentResults = await Task.WhenAll(concurrentTasks);
        var maxConcurrentDuration = concurrentResults.Max(r => r.Duration);

        Assert.True(maxConcurrentDuration < 500,
            $"Maximum concurrent query duration {maxConcurrentDuration}ms exceeded threshold");
    }

    [Fact]
    public void PerformanceMetrics_ShouldProvideUsefulData()
    {
        // Act
        var metrics = _performanceService.GetPerformanceMetrics();

        // Assert
        Assert.NotNull(metrics);
        Assert.True(metrics.MemoryUsage >= 0);
        Assert.True(metrics.CacheHitRatio >= 0.0 && metrics.CacheHitRatio <= 1.0);
        Assert.True(metrics.Timestamp <= DateTime.UtcNow);
    }

    private async Task<TestData> CreateTestDataAsync()
    {
        var business = new Business
        {
            Id = Guid.NewGuid(),
            Name = "Performance Test Business",
            Type = BusinessType.GeneralRetail,
            OwnerId = Guid.NewGuid(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var shop = new Shop
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            Name = "Performance Test Shop",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var products = new List<Product>();
        for (int i = 0; i < 20; i++)
        {
            products.Add(new Product
            {
                Id = Guid.NewGuid(),
                ShopId = shop.Id,
                Name = $"Performance Test Product {i}",
                Barcode = $"PERF{i:D3}",
                Category = "Performance Test Category",
                UnitPrice = 10.00m + i,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
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
        for (int i = 20; i < 200; i++)
        {
            additionalProducts.Add(new Product
            {
                Id = Guid.NewGuid(),
                ShopId = testData.Shop.Id,
                Name = $"Large Dataset Product {i}",
                Barcode = $"LARGE{i:D4}",
                Category = $"Category {i % 10}",
                UnitPrice = 10.00m + i,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
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