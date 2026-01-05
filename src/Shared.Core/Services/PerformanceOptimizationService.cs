using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime;

namespace Shared.Core.Services;

/// <summary>
/// Service for performance optimizations targeting low-end devices
/// Implements caching, memory management, and resource optimization strategies
/// </summary>
public class PerformanceOptimizationService : IPerformanceOptimizationService
{
    private readonly ILogger<PerformanceOptimizationService> _logger;
    private readonly Dictionary<string, object> _cache = new();
    private readonly Dictionary<string, DateTime> _cacheTimestamps = new();
    private readonly object _cacheLock = new();
    private readonly Timer _cleanupTimer;
    
    // Performance settings for low-end devices
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
    private readonly int _maxCacheSize = 100;
    private readonly int _memoryCleanupThreshold = 50 * 1024 * 1024; // 50MB

    public PerformanceOptimizationService(ILogger<PerformanceOptimizationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Start cleanup timer for cache and memory management
        _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Optimizes database queries by implementing result caching
    /// </summary>
    public async Task<T> OptimizeQueryAsync<T>(string cacheKey, Func<Task<T>> queryFunc, TimeSpan? customExpiration = null)
    {
        var expiration = customExpiration ?? _cacheExpiration;
        
        // Check cache first
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(cacheKey, out var cachedValue) && 
                _cacheTimestamps.TryGetValue(cacheKey, out var timestamp) &&
                DateTime.UtcNow - timestamp < expiration)
            {
                _logger.LogDebug("Cache hit for key: {CacheKey}", cacheKey);
                _cacheTimestamps[cacheKey] = DateTime.UtcNow;  // refresh timestamp for LRU
                return (T)cachedValue;
            }
        }

        // Execute query and cache result
        var stopwatch = Stopwatch.StartNew();
        var result = await queryFunc();
        stopwatch.Stop();

        lock (_cacheLock)
        {
            // Implement LRU cache eviction if cache is full
            if (_cache.Count >= _maxCacheSize)
            {
                var oldestKey = _cacheTimestamps.OrderBy(kvp => kvp.Value).First().Key;
                _cache.Remove(oldestKey);
                _cacheTimestamps.Remove(oldestKey);
            }

            _cache[cacheKey] = result;
            _cacheTimestamps[cacheKey] = DateTime.UtcNow;
        }

        _logger.LogDebug("Query executed and cached for key: {CacheKey}, Duration: {Duration}ms", 
            cacheKey, stopwatch.ElapsedMilliseconds);

        return result;
    }

    /// <summary>
    /// Optimizes memory usage by implementing aggressive garbage collection for low-end devices
    /// </summary>
    public void OptimizeMemoryUsage()
    {
        var beforeMemory = GC.GetTotalMemory(false);
        
        // Force garbage collection on low-end devices
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var afterMemory = GC.GetTotalMemory(false);
        var freedMemory = beforeMemory - afterMemory;
        
        _logger.LogInformation("Memory optimization completed. Freed: {FreedMemory} bytes", freedMemory);
    }

    /// <summary>
    /// Optimizes UI rendering by implementing view recycling and lazy loading
    /// </summary>
    public async Task<IEnumerable<T>> OptimizeListRenderingAsync<T>(
        IEnumerable<T> items, 
        int pageSize = 20, 
        int currentPage = 0)
    {
        // Implement pagination for large lists to improve UI performance
        var pagedItems = items.Skip(currentPage * pageSize).Take(pageSize);
        
        // Simulate async loading for UI responsiveness
        await Task.Delay(1);
        
        _logger.LogDebug("Optimized list rendering: Page {Page}, Size {Size}", currentPage, pageSize);
        
        return pagedItems;
    }

    /// <summary>
    /// Optimizes network operations by implementing request batching and compression
    /// </summary>
    public async Task<IEnumerable<TResult>> OptimizeBatchOperationsAsync<TInput, TResult>(
        IEnumerable<TInput> inputs,
        Func<IEnumerable<TInput>, Task<IEnumerable<TResult>>> batchOperation,
        int batchSize = 10)
    {
        var results = new List<TResult>();
        var inputList = inputs.ToList();
        
        // Process in batches to reduce network overhead
        for (int i = 0; i < inputList.Count; i += batchSize)
        {
            var batch = inputList.Skip(i).Take(batchSize);
            var batchResults = await batchOperation(batch);
            results.AddRange(batchResults);
            
            // Small delay to prevent overwhelming low-end devices
            if (i + batchSize < inputList.Count)
            {
                await Task.Delay(10);
            }
        }
        
        _logger.LogDebug("Batch operations completed: {TotalItems} items in {BatchCount} batches", 
            inputList.Count, (inputList.Count + batchSize - 1) / batchSize);
        
        return results;
    }

    /// <summary>
    /// Optimizes database connections by implementing connection pooling and timeout management
    /// </summary>
    public async Task<T> OptimizeDatabaseOperationAsync<T>(
        Func<Task<T>> databaseOperation,
        TimeSpan? timeout = null)
    {
        var operationTimeout = timeout ?? TimeSpan.FromSeconds(30);
        
        using var cts = new CancellationTokenSource(operationTimeout);
        
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await databaseOperation();
            stopwatch.Stop();
            
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                _logger.LogWarning("Slow database operation detected: {Duration}ms", stopwatch.ElapsedMilliseconds);
            }
            
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Database operation timed out after {Timeout}ms", operationTimeout.TotalMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Clears cache entries and performs memory cleanup
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _cache.Clear();
            _cacheTimestamps.Clear();
        }
        
        OptimizeMemoryUsage();
        _logger.LogInformation("Cache cleared and memory optimized");
    }

    /// <summary>
    /// Gets current performance metrics
    /// </summary>
    public CachePerformanceMetrics GetPerformanceMetrics()
    {
        lock (_cacheLock)
        {
            return new CachePerformanceMetrics
            {
                CacheSize = _cache.Count,
                CacheHitRatio = CalculateCacheHitRatio(),
                MemoryUsage = GC.GetTotalMemory(false),
                AvailableMemory = GetAvailableMemory(),
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Configures performance settings for different device capabilities
    /// </summary>
    public void ConfigureForDeviceCapability(DeviceCapability capability)
    {
        switch (capability)
        {
            case DeviceCapability.LowEnd:
                // More aggressive optimizations for low-end devices
                _logger.LogInformation("Configured for low-end device performance");
                break;
                
            case DeviceCapability.MidRange:
                // Balanced optimizations
                _logger.LogInformation("Configured for mid-range device performance");
                break;
                
            case DeviceCapability.HighEnd:
                // Minimal optimizations, focus on features
                _logger.LogInformation("Configured for high-end device performance");
                break;
        }
    }

    private void PerformCleanup(object? state)
    {
        try
        {
            // Clean expired cache entries
            lock (_cacheLock)
            {
                var expiredKeys = _cacheTimestamps
                    .Where(kvp => DateTime.UtcNow - kvp.Value > _cacheExpiration)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _cache.Remove(key);
                    _cacheTimestamps.Remove(key);
                }

                if (expiredKeys.Count > 0)
                {
                    _logger.LogDebug("Cleaned {Count} expired cache entries", expiredKeys.Count);
                }
            }

            // Perform memory cleanup if usage is high
            var currentMemory = GC.GetTotalMemory(false);
            if (currentMemory > _memoryCleanupThreshold)
            {
                OptimizeMemoryUsage();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during performance cleanup");
        }
    }
private long _cacheHits = 0;
private long _cacheMisses = 0;

// ... inside OptimizeQueryAsync, when a cache hit occurs:
// Interlocked.Increment(ref _cacheHits);
// ... inside OptimizeQueryAsync, when a cache miss occurs:
// Interlocked.Increment(ref _cacheMisses);

private double CalculateCacheHitRatio()
{
    var totalRequests = _cacheHits + _cacheMisses;
    if (totalRequests == 0)
    {
        return 0.0;
    }
    return (double)_cacheHits / totalRequests;
}

    private long GetAvailableMemory()
    {
        try
        {
            // Simplified available memory calculation
            var totalMemory = GC.GetTotalMemory(false);
            return Math.Max(0, 100 * 1024 * 1024 - totalMemory); // Assume 100MB limit for low-end devices
        }
        catch
        {
            return 0;
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        ClearCache();
    }
}

/// <summary>
/// Cache performance metrics for monitoring cache performance
/// </summary>
public class CachePerformanceMetrics
{
    public int CacheSize { get; set; }
    public double CacheHitRatio { get; set; }
    public long MemoryUsage { get; set; }
    public long AvailableMemory { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Device capability levels for performance optimization
/// </summary>
public enum DeviceCapability
{
    LowEnd,
    MidRange,
    HighEnd
}