using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Services;
using System.Diagnostics;
using Xunit;

namespace Shared.Core.Tests;

/// <summary>
/// Simple performance demonstration showing the optimization services work
/// This test focuses on the core performance optimization functionality
/// </summary>
public class SimplePerformanceDemo
{
    [Fact]
    public async Task PerformanceOptimizationService_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IPerformanceOptimizationService, PerformanceOptimizationService>();
        
        var serviceProvider = services.BuildServiceProvider();
        var performanceService = serviceProvider.GetRequiredService<IPerformanceOptimizationService>();
        
        // Configure for low-end device
        performanceService.ConfigureForDeviceCapability(DeviceCapability.LowEnd);

        // Act & Assert - Test caching functionality
        var cacheKey = "test_key";
        var stopwatch = Stopwatch.StartNew();
        
        // First call (cache miss)
        var firstResult = await performanceService.OptimizeQueryAsync(cacheKey, async () =>
        {
            await Task.Delay(100); // Simulate slow operation
            return "Test Data";
        });
        stopwatch.Stop();
        var firstCallTime = stopwatch.ElapsedMilliseconds;

        // Second call (cache hit)
        stopwatch.Restart();
        var secondResult = await performanceService.OptimizeQueryAsync(cacheKey, async () =>
        {
            await Task.Delay(100); // This should not be called
            return "Test Data";
        });
        stopwatch.Stop();
        var secondCallTime = stopwatch.ElapsedMilliseconds;

        // Assert
        Assert.Equal("Test Data", firstResult);
        Assert.Equal("Test Data", secondResult);
        Assert.True(secondCallTime < firstCallTime / 2, 
            $"Cache hit took {secondCallTime}ms, should be faster than first call {firstCallTime}ms");
        Assert.True(secondCallTime < 50, 
            $"Cache hit took {secondCallTime}ms, should be under 50ms");
    }

    [Fact]
    public async Task BatchOperations_ShouldImprovePerformance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IPerformanceOptimizationService, PerformanceOptimizationService>();
        
        var serviceProvider = services.BuildServiceProvider();
        var performanceService = serviceProvider.GetRequiredService<IPerformanceOptimizationService>();

        var items = Enumerable.Range(1, 20).ToList();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var results = await performanceService.OptimizeBatchOperationsAsync(
            items,
            async batch =>
            {
                await Task.Delay(10); // Simulate network operation
                return batch.Select(i => $"Result_{i}");
            },
            batchSize: 5
        );
        stopwatch.Stop();

        // Assert
        Assert.Equal(20, results.Count());
        Assert.True(stopwatch.ElapsedMilliseconds < 200, 
            $"Batch operations took {stopwatch.ElapsedMilliseconds}ms, should be under 200ms");
        Assert.All(results, result => Assert.StartsWith("Result_", result));
    }

    [Fact]
    public void MemoryOptimization_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IPerformanceOptimizationService, PerformanceOptimizationService>();
        
        var serviceProvider = services.BuildServiceProvider();
        var performanceService = serviceProvider.GetRequiredService<IPerformanceOptimizationService>();

        var initialMemory = GC.GetTotalMemory(false);

        // Create some memory pressure
        var largeObjects = new List<byte[]>();
        for (int i = 0; i < 10; i++)
        {
            largeObjects.Add(new byte[1024 * 1024]); // 1MB each
        }

        var memoryBeforeOptimization = GC.GetTotalMemory(false);

        // Act
        performanceService.OptimizeMemoryUsage();
        largeObjects.Clear(); // Release references

        var memoryAfterOptimization = GC.GetTotalMemory(false);

        // Assert
        var memoryReduction = memoryBeforeOptimization - memoryAfterOptimization;
        Assert.True(memoryReduction > 0, "Memory optimization should reduce memory usage");
    }

    [Fact]
    public void PerformanceMetrics_ShouldProvideData()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IPerformanceOptimizationService, PerformanceOptimizationService>();
        
        var serviceProvider = services.BuildServiceProvider();
        var performanceService = serviceProvider.GetRequiredService<IPerformanceOptimizationService>();

        // Act
        var metrics = performanceService.GetPerformanceMetrics();

        // Assert
        Assert.NotNull(metrics);
        Assert.True(metrics.MemoryUsage >= 0);
        Assert.True(metrics.CacheHitRatio >= 0.0 && metrics.CacheHitRatio <= 1.0);
        Assert.True(metrics.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public async Task CachingStrategyService_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ICachingStrategyService, CachingStrategyService>();
        
        var serviceProvider = services.BuildServiceProvider();
        var cachingService = serviceProvider.GetRequiredService<ICachingStrategyService>();

        var testData = "Test Cache Data";
        var cacheKey = "test_cache_key";

        // Act - Set cache
        await cachingService.SetMemoryCacheAsync(cacheKey, testData);

        // Get from cache
        var cachedData = await cachingService.GetFromMemoryCacheAsync<string>(cacheKey);

        // Assert
        Assert.Equal(testData, cachedData);

        // Test cache statistics
        var stats = await cachingService.GetCacheStatisticsAsync();
        Assert.NotNull(stats);
        Assert.True(stats.MemoryCacheSize > 0);
    }
}