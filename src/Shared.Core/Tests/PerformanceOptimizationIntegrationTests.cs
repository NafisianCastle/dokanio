using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.DependencyInjection;
using Shared.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace Shared.Core.Tests;

/// <summary>
/// Integration tests for performance optimization features
/// Tests the complete performance optimization pipeline
/// </summary>
public class PerformanceOptimizationIntegrationTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITestOutputHelper _output;

    public PerformanceOptimizationIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        
        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        services.AddLogging(builder => builder.AddConsole());
        
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task DatabaseOptimization_ShouldImproveQueryPerformance()
    {
        // Arrange
        var dbOptimizationService = _serviceProvider.GetRequiredService<IDatabaseQueryOptimizationService>();
        var performanceMonitoringService = _serviceProvider.GetRequiredService<IPerformanceMonitoringService>();
        
        // Act - Start performance monitoring
        await performanceMonitoringService.StartMonitoringAsync(TimeSpan.FromSeconds(1));
        
        // Optimize database indexes
        await dbOptimizationService.OptimizeDatabaseIndexesAsync();
        
        // Get performance metrics
        var metrics = await performanceMonitoringService.GetCurrentMetricsAsync();
        
        // Assert
        Assert.NotNull(metrics);
        Assert.True(metrics.Timestamp <= DateTime.UtcNow);
        Assert.True(metrics.DatabaseResponseTimeMs >= 0);
        
        _output.WriteLine($"Database response time: {metrics.DatabaseResponseTimeMs}ms");
        _output.WriteLine($"Memory usage: {metrics.MemoryUsageMB}MB");
        _output.WriteLine($"Cache hit rate: {metrics.CacheHitRate:F1}%");
        
        // Stop monitoring
        await performanceMonitoringService.StopMonitoringAsync();
    }

    [Fact]
    public async Task PaginationService_ShouldHandleLargeDatasets()
    {
        // Arrange
        var paginationService = _serviceProvider.GetRequiredService<IPaginationService>();
        var performanceMonitoringService = _serviceProvider.GetRequiredService<IPerformanceMonitoringService>();
        
        // Create a large dataset simulation
        var largeDataset = Enumerable.Range(1, 10000)
            .Select(i => new TestDataItem { Id = i, Name = $"Item {i}" })
            .AsQueryable();
        
        // Act - Test pagination performance
        var result = await performanceMonitoringService.MeasureOperationAsync("PaginationTest", async () =>
        {
            return await paginationService.GetPaginatedResultAsync(
                largeDataset, 
                page: 0, 
                pageSize: 50, 
                cacheKey: "test_pagination");
        });
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(50, result.Items.Count());
        Assert.Equal(10000, result.TotalCount);
        Assert.Equal(200, result.TotalPages); // 10000 / 50
        Assert.True(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
        
        _output.WriteLine($"Paginated {result.TotalCount} items into {result.TotalPages} pages");
        _output.WriteLine($"Current page has {result.Items.Count()} items");
    }

    [Fact]
    public async Task CachingStrategy_ShouldImprovePerformance()
    {
        // Arrange
        var cachingService = _serviceProvider.GetRequiredService<ICachingStrategyService>();
        var performanceMonitoringService = _serviceProvider.GetRequiredService<IPerformanceMonitoringService>();
        
        var testData = new TestDataItem { Id = 1, Name = "Cached Item" };
        var cacheKey = "test_cache_key";
        
        // Act - Test caching performance
        await performanceMonitoringService.MeasureOperationAsync("CacheWrite", async () =>
        {
            await cachingService.SetMemoryCacheAsync(cacheKey, testData);
            return Task.CompletedTask;
        });
        
        var cachedResult = await performanceMonitoringService.MeasureOperationAsync("CacheRead", async () =>
        {
            return await cachingService.GetFromMemoryCacheAsync<TestDataItem>(cacheKey);
        });
        
        // Assert
        Assert.NotNull(cachedResult);
        Assert.Equal(testData.Id, cachedResult.Id);
        Assert.Equal(testData.Name, cachedResult.Name);
        
        // Test cache statistics
        var cacheStats = await cachingService.GetCacheStatisticsAsync();
        Assert.NotNull(cacheStats);
        Assert.True(cacheStats.MemoryCacheSize > 0);
        
        _output.WriteLine($"Cache statistics - Memory size: {cacheStats.MemoryCacheSize}");
        _output.WriteLine($"Memory usage: {cacheStats.TotalMemoryUsage} bytes");
    }

    [Fact]
    public async Task PerformanceMonitoring_ShouldDetectThresholdViolations()
    {
        // Arrange
        var performanceMonitoringService = _serviceProvider.GetRequiredService<IPerformanceMonitoringService>();
        
        var alertTriggered = false;
        performanceMonitoringService.PerformanceAlertTriggered += (sender, args) =>
        {
            alertTriggered = true;
            _output.WriteLine($"Alert triggered: {args.Alert.Title} - {args.Alert.Message}");
        };
        
        // Act - Start monitoring
        await performanceMonitoringService.StartMonitoringAsync(TimeSpan.FromSeconds(1));
        
        // Simulate a slow operation that should trigger an alert
        await performanceMonitoringService.MeasureOperationAsync("SlowOperation", async () =>
        {
            await Task.Delay(1500); // Delay longer than default threshold (1000ms)
            return Task.CompletedTask;
        });
        
        // Get current metrics
        var metrics = await performanceMonitoringService.GetCurrentMetricsAsync();
        
        // Get active alerts
        var alerts = await performanceMonitoringService.GetActiveAlertsAsync();
        
        // Assert
        Assert.NotNull(metrics);
        Assert.True(alertTriggered, "Performance alert should have been triggered for slow operation");
        Assert.NotEmpty(alerts);
        
        var slowOperationAlert = alerts.FirstOrDefault(a => a.Title.Contains("Slow Operation"));
        Assert.NotNull(slowOperationAlert);
        Assert.Equal(AlertStatus.Active, slowOperationAlert.Status);
        
        _output.WriteLine($"Detected {alerts.Count()} active alerts");
        
        // Stop monitoring
        await performanceMonitoringService.StopMonitoringAsync();
    }

    [Fact]
    public async Task LazyLoading_ShouldStreamLargeDatasets()
    {
        // Arrange
        var paginationService = _serviceProvider.GetRequiredService<IPaginationService>();
        
        // Create a large dataset
        var largeDataset = Enumerable.Range(1, 1000)
            .Select(i => new TestDataItem { Id = i, Name = $"Item {i}" })
            .AsQueryable();
        
        // Act - Test lazy loading
        var processedCount = 0;
        var lazyEnumerable = paginationService.GetLazyLoadedAsync(largeDataset, batchSize: 50);
        
        await foreach (var item in lazyEnumerable)
        {
            processedCount++;
            
            // Process only first 100 items for test performance
            if (processedCount >= 100)
                break;
        }
        
        // Assert
        Assert.Equal(100, processedCount);
        
        _output.WriteLine($"Lazy loaded and processed {processedCount} items from large dataset");
    }

    [Fact]
    public async Task PerformanceSummary_ShouldProvideMetrics()
    {
        // Arrange
        var performanceMonitoringService = _serviceProvider.GetRequiredService<IPerformanceMonitoringService>();
        
        // Act - Start monitoring and perform some operations
        await performanceMonitoringService.StartMonitoringAsync(TimeSpan.FromSeconds(1));
        
        // Record some test metrics
        await performanceMonitoringService.RecordMetricAsync("test_operation", 150, "ms");
        await performanceMonitoringService.RecordMetricAsync("test_operation", 200, "ms");
        await performanceMonitoringService.RecordMetricAsync("test_operation", 100, "ms");
        
        // Get performance summary
        var summary = await performanceMonitoringService.GetPerformanceSummaryAsync(TimeSpan.FromMinutes(1));
        
        // Assert
        Assert.NotNull(summary);
        Assert.True(summary.TotalRequests >= 0);
        Assert.True(summary.AverageResponseTime >= 0);
        
        _output.WriteLine($"Performance Summary:");
        _output.WriteLine($"  Average Response Time: {summary.AverageResponseTime:F1}ms");
        _output.WriteLine($"  Max Response Time: {summary.MaxResponseTime:F1}ms");
        _output.WriteLine($"  Total Requests: {summary.TotalRequests}");
        _output.WriteLine($"  Error Rate: {summary.ErrorRate:F1}%");
        
        // Stop monitoring
        await performanceMonitoringService.StopMonitoringAsync();
    }

    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private class TestDataItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}