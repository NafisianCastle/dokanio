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
/// Mobile app performance tests for low-end devices
/// Validates performance requirements for mobile applications on resource-constrained devices
/// </summary>
public class MobileAppPerformanceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;
    private readonly IPerformanceOptimizationService _performanceService;
    private readonly IDatabaseQueryOptimizationService _queryOptimizationService;
    private readonly ICachingStrategyService _cachingService;

    // Performance targets for low-end mobile devices
    private const int MAX_STARTUP_TIME_MS = 3000;
    private const int MAX_QUERY_TIME_MS = 500;
    private const int MAX_UI_RESPONSE_TIME_MS = 200;
    private const int MAX_MEMORY_USAGE_MB = 100;
    private const int MAX_CACHE_OPERATION_TIME_MS = 50;

    public MobileAppPerformanceTests(ITestOutputHelper output)
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
        
        // Configure for low-end device performance
        _performanceService.ConfigureForDeviceCapability(DeviceCapability.LowEnd);
    }

    [Fact]
    public async Task MobileAppStartup_ShouldMeetPerformanceTargets()
    {
        // Arrange - Simulate mobile app startup sequence
        var stopwatch = Stopwatch.StartNew();

        // Act - Simulate startup operations
        await SimulateAppStartupAsync();
        
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < MAX_STARTUP_TIME_MS,
            $"App startup took {stopwatch.ElapsedMilliseconds}ms, expected < {MAX_STARTUP_TIME_MS}ms");

        _output.WriteLine($"Mobile app startup completed in {stopwatch.ElapsedMilliseconds}ms (target: {MAX_STARTUP_TIME_MS}ms)");
    }

    [Fact]
    public async Task ProductSearch_ShouldRespondQuicklyOnLowEndDevice()
    {
        // Arrange
        var testData = await CreateMobileTestDataAsync();
        var searchTerms = new[] { "Test", "Product", "Category", "123", "ABC" };

        // Act & Assert
        foreach (var searchTerm in searchTerms)
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Simulate mobile product search with caching
            var cacheKey = $"mobile_search_{testData.Shop.Id}_{searchTerm}";
            var results = await _cachingService.GetWithFallbackAsync(cacheKey, async () =>
            {
                return await _queryOptimizationService.GetProductsOptimizedAsync(
                    testData.Shop.Id, 
                    activeOnly: true);
            }, CacheStrategy.MemoryFirst);

            stopwatch.Stop();

            Assert.NotNull(results);
            Assert.True(stopwatch.ElapsedMilliseconds < MAX_QUERY_TIME_MS,
                $"Product search for '{searchTerm}' took {stopwatch.ElapsedMilliseconds}ms, expected < {MAX_QUERY_TIME_MS}ms");

            _output.WriteLine($"Product search '{searchTerm}': {stopwatch.ElapsedMilliseconds}ms");
        }
    }

    [Fact]
    public async Task SalesTransaction_ShouldProcessQuicklyOnMobile()
    {
        // Arrange
        var testData = await CreateMobileTestDataAsync();
        var saleItems = testData.Products.Take(5).Select(p => new
        {
            ProductId = p.Id,
            Quantity = 2,
            UnitPrice = p.UnitPrice
        }).ToList();

        // Act - Simulate mobile sales transaction processing
        var stopwatch = Stopwatch.StartNew();

        // Simulate real-time calculation during sale entry
        decimal subtotal = 0;
        foreach (var item in saleItems)
        {
            // Simulate adding item to sale with real-time calculation
            await Task.Delay(1); // Simulate UI update delay
            subtotal += item.Quantity * item.UnitPrice;
        }

        // Simulate final sale processing
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            ShopId = testData.Shop.Id,
            UserId = Guid.NewGuid(),
            InvoiceNumber = $"MOB{DateTime.Now:yyyyMMddHHmmss}",
            TotalAmount = subtotal,
            PaymentMethod = PaymentMethod.Cash,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Sales.Add(sale);
        await _context.SaveChangesAsync();

        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < MAX_UI_RESPONSE_TIME_MS * saleItems.Count,
            $"Sales transaction took {stopwatch.ElapsedMilliseconds}ms, expected < {MAX_UI_RESPONSE_TIME_MS * saleItems.Count}ms");

        _output.WriteLine($"Mobile sales transaction completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task OfflineDataSync_ShouldHandleLargeDataSetsEfficiently()
    {
        // Arrange
        var testData = await CreateLargeMobileDataSetAsync();
        var syncData = new List<object>();

        // Simulate offline data accumulation
        for (int i = 0; i < 100; i++)
        {
            syncData.Add(new
            {
                Type = "Sale",
                Data = new { Id = Guid.NewGuid(), Amount = 100.00m + i, Timestamp = DateTime.UtcNow }
            });
        }

        // Act - Simulate batch sync operation
        var stopwatch = Stopwatch.StartNew();

        var syncResults = await _performanceService.OptimizeBatchOperationsAsync(
            syncData,
            async batch =>
            {
                // Simulate network sync operation
                await Task.Delay(10);
                return batch.Select(item => $"Synced: {item}");
            },
            batchSize: 20
        );

        stopwatch.Stop();

        // Assert
        Assert.Equal(100, syncResults.Count());
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Offline sync took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");

        _output.WriteLine($"Offline data sync completed in {stopwatch.ElapsedMilliseconds}ms for {syncData.Count} items");
    }

    [Fact]
    public async Task MemoryUsage_ShouldStayWithinMobileLimits()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(false);
        var testData = await CreateMobileTestDataAsync();

        // Act - Simulate typical mobile app usage
        var operations = new List<Func<Task>>
        {
            async () => await _queryOptimizationService.GetProductsOptimizedAsync(testData.Shop.Id),
            async () => await _queryOptimizationService.GetSalesOptimizedAsync(testData.Shop.Id, DateTime.Today.AddDays(-7), DateTime.Today),
            async () => await _cachingService.SetMemoryCacheAsync("mobile_test", testData.Products),
            async () => await _performanceService.OptimizeListRenderingAsync(testData.Products, pageSize: 10)
        };

        foreach (var operation in operations)
        {
            await operation();
        }

        var peakMemory = GC.GetTotalMemory(false);
        
        // Optimize memory usage
        _performanceService.OptimizeMemoryUsage();
        
        var optimizedMemory = GC.GetTotalMemory(false);
        var memoryUsageMB = (optimizedMemory - initialMemory) / 1024 / 1024;

        // Assert
        Assert.True(memoryUsageMB < MAX_MEMORY_USAGE_MB,
            $"Memory usage {memoryUsageMB}MB exceeded mobile limit of {MAX_MEMORY_USAGE_MB}MB");

        _output.WriteLine($"Initial memory: {initialMemory / 1024 / 1024}MB");
        _output.WriteLine($"Peak memory: {peakMemory / 1024 / 1024}MB");
        _output.WriteLine($"Optimized memory: {optimizedMemory / 1024 / 1024}MB");
        _output.WriteLine($"Net memory usage: {memoryUsageMB}MB (limit: {MAX_MEMORY_USAGE_MB}MB)");
    }

    [Fact]
    public async Task UIListRendering_ShouldHandleLargeProductLists()
    {
        // Arrange
        var testData = await CreateLargeMobileDataSetAsync();
        var allProducts = testData.Products.ToList();

        // Act - Test paginated rendering for mobile UI
        var pageSize = 20;
        var totalPages = (allProducts.Count + pageSize - 1) / pageSize;
        var renderingTimes = new List<long>();

        for (int page = 0; page < Math.Min(totalPages, 5); page++) // Test first 5 pages
        {
            var stopwatch = Stopwatch.StartNew();
            
            var pagedProducts = await _performanceService.OptimizeListRenderingAsync(
                allProducts, pageSize, page);
            
            stopwatch.Stop();
            renderingTimes.Add(stopwatch.ElapsedMilliseconds);

            Assert.True(pagedProducts.Count() <= pageSize);
            Assert.True(stopwatch.ElapsedMilliseconds < MAX_UI_RESPONSE_TIME_MS,
                $"Page {page} rendering took {stopwatch.ElapsedMilliseconds}ms, expected < {MAX_UI_RESPONSE_TIME_MS}ms");
        }

        var averageRenderTime = renderingTimes.Average();
        var maxRenderTime = renderingTimes.Max();

        _output.WriteLine($"UI list rendering - Pages tested: {renderingTimes.Count}, Avg: {averageRenderTime:F1}ms, Max: {maxRenderTime}ms");
        _output.WriteLine($"Total products: {allProducts.Count}, Page size: {pageSize}");
    }

    [Fact]
    public async Task CacheOperations_ShouldBeFastOnMobile()
    {
        // Arrange
        var testData = await CreateMobileTestDataAsync();
        var cacheOperations = new List<(string Name, Func<Task> Operation)>
        {
            ("Set Business", async () => await _cachingService.SetMemoryCacheAsync("mobile_business", testData.Business)),
            ("Get Business", async () => await _cachingService.GetFromMemoryCacheAsync<Business>("mobile_business")),
            ("Set Products", async () => await _cachingService.SetMemoryCacheAsync("mobile_products", testData.Products)),
            ("Get Products", async () => await _cachingService.GetFromMemoryCacheAsync<List<Product>>("mobile_products")),
            ("Cache Statistics", async () => await _cachingService.GetCacheStatisticsAsync())
        };

        // Act & Assert
        foreach (var (name, operation) in cacheOperations)
        {
            var stopwatch = Stopwatch.StartNew();
            await operation();
            stopwatch.Stop();

            Assert.True(stopwatch.ElapsedMilliseconds < MAX_CACHE_OPERATION_TIME_MS,
                $"Cache operation '{name}' took {stopwatch.ElapsedMilliseconds}ms, expected < {MAX_CACHE_OPERATION_TIME_MS}ms");

            _output.WriteLine($"Cache operation '{name}': {stopwatch.ElapsedMilliseconds}ms");
        }
    }

    [Fact]
    public async Task NetworkInterruption_ShouldHandleGracefully()
    {
        // Arrange
        var testData = await CreateMobileTestDataAsync();
        var offlineOperations = new List<object>();

        // Act - Simulate network interruption during operations
        var stopwatch = Stopwatch.StartNew();

        // Simulate offline operations being queued
        for (int i = 0; i < 50; i++)
        {
            offlineOperations.Add(new
            {
                Type = "ProductUpdate",
                ProductId = testData.Products[i % testData.Products.Count].Id,
                Timestamp = DateTime.UtcNow,
                Data = new { Price = 10.00m + i }
            });
        }

        // Simulate network coming back online and processing queued operations
        var processedOperations = await _performanceService.OptimizeBatchOperationsAsync(
            offlineOperations,
            async batch =>
            {
                // Simulate processing offline operations when network is restored
                await Task.Delay(5);
                return batch.Select(op => $"Processed: {op}");
            },
            batchSize: 10
        );

        stopwatch.Stop();

        // Assert
        Assert.Equal(50, processedOperations.Count());
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Network interruption recovery took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");

        _output.WriteLine($"Network interruption recovery completed in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Processed {processedOperations.Count()} offline operations");
    }

    [Fact]
    public async Task ConcurrentMobileUsers_ShouldMaintainPerformance()
    {
        // Arrange
        var testData = await CreateMobileTestDataAsync();
        var userCount = 10;

        // Act - Simulate multiple mobile users accessing the system concurrently
        var concurrentTasks = Enumerable.Range(1, userCount).Select(async userId =>
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Simulate typical mobile user operations
            var userCacheKey = $"mobile_user_{userId}";
            
            // User login and data loading
            var userProducts = await _cachingService.GetWithFallbackAsync(
                $"{userCacheKey}_products",
                async () => await _queryOptimizationService.GetProductsOptimizedAsync(testData.Shop.Id),
                CacheStrategy.MemoryFirst
            );

            // User performs a search
            var searchResults = await _performanceService.OptimizeListRenderingAsync(
                userProducts ?? new List<Product>(), pageSize: 20);

            // User views sales data
            var userSales = await _cachingService.GetWithFallbackAsync(
                $"{userCacheKey}_sales",
                async () => await _queryOptimizationService.GetSalesOptimizedAsync(
                    testData.Shop.Id, DateTime.Today.AddDays(-7), DateTime.Today),
                CacheStrategy.MemoryFirst
            );

            stopwatch.Stop();
            return new { UserId = userId, Duration = stopwatch.ElapsedMilliseconds };
        });

        var results = await Task.WhenAll(concurrentTasks);

        // Assert
        var maxDuration = results.Max(r => r.Duration);
        var avgDuration = results.Average(r => r.Duration);

        Assert.True(maxDuration < MAX_QUERY_TIME_MS * 3, // Allow 3x normal time for concurrent operations
            $"Maximum concurrent user operation took {maxDuration}ms, expected < {MAX_QUERY_TIME_MS * 3}ms");

        _output.WriteLine($"Concurrent mobile users test - Users: {userCount}, Avg: {avgDuration:F1}ms, Max: {maxDuration}ms");
        
        foreach (var result in results.OrderBy(r => r.Duration))
        {
            _output.WriteLine($"User {result.UserId}: {result.Duration}ms");
        }
    }

    private async Task SimulateAppStartupAsync()
    {
        // Simulate database initialization
        await _context.Database.EnsureCreatedAsync();
        
        // Simulate cache warmup
        var warmupItems = new List<CacheWarmupItem>
        {
            new CacheWarmupItem
            {
                Key = "startup_config",
                DataProvider = async () => new { AppVersion = "1.0", Theme = "Light" },
                CacheLevel = CacheLevel.Memory
            }
        };
        
        await _cachingService.WarmupCacheAsync(warmupItems);
        
        // Simulate initial data loading
        await Task.Delay(100); // Simulate UI initialization
        
        // Optimize memory for mobile startup
        _performanceService.OptimizeMemoryUsage();
    }

    private async Task<MobileTestData> CreateMobileTestDataAsync()
    {
        var business = new Business
        {
            Id = Guid.NewGuid(),
            Name = "Mobile Test Business",
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
            Name = "Mobile Test Shop",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var products = new List<Product>();
        for (int i = 0; i < 50; i++)
        {
            products.Add(new Product
            {
                Id = Guid.NewGuid(),
                ShopId = shop.Id,
                Name = $"Mobile Product {i}",
                Barcode = $"MOB{i:D3}",
                Category = $"Category {i % 5}",
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

        return new MobileTestData { Business = business, Shop = shop, Products = products };
    }

    private async Task<MobileTestData> CreateLargeMobileDataSetAsync()
    {
        var testData = await CreateMobileTestDataAsync();
        
        // Add more products for mobile testing
        var additionalProducts = new List<Product>();
        for (int i = 50; i < 500; i++)
        {
            additionalProducts.Add(new Product
            {
                Id = Guid.NewGuid(),
                ShopId = testData.Shop.Id,
                Name = $"Large Mobile Product {i}",
                Barcode = $"LMOB{i:D4}",
                Category = $"Category {i % 10}",
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

    private class MobileTestData
    {
        public Business Business { get; set; } = null!;
        public Shop Shop { get; set; } = null!;
        public List<Product> Products { get; set; } = new();
    }
}