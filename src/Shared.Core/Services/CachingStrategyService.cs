using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// Advanced caching strategy service for frequently accessed data
/// Implements multi-level caching with different expiration policies
/// </summary>
public class CachingStrategyService : ICachingStrategyService, IDisposable
{
    private readonly ILogger<CachingStrategyService> _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _memoryCache = new();
    private readonly ConcurrentDictionary<string, CacheEntry> _persistentCache = new();
    private readonly Timer _cleanupTimer;
    private readonly object _lockObject = new();

    // Cache configuration
    private readonly TimeSpan _memoryExpiration = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _persistentExpiration = TimeSpan.FromHours(1);
    private readonly int _maxMemoryCacheSize = 1000;
    private readonly int _maxPersistentCacheSize = 5000;

    public CachingStrategyService(ILogger<CachingStrategyService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Start cleanup timer
        _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Gets or sets data in memory cache with short expiration
    /// </summary>
    public async Task<T?> GetFromMemoryCacheAsync<T>(string key) where T : class
    {
        if (_memoryCache.TryGetValue(key, out var entry) && !entry.IsExpired)
        {
            entry.LastAccessed = DateTime.UtcNow;
            _logger.LogDebug("Memory cache hit for key: {Key}", key);
            
            try
            {
                return JsonSerializer.Deserialize<T>(entry.Data);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cached data for key: {Key}", key);
                _memoryCache.TryRemove(key, out _);
                return null;
            }
        }

        _logger.LogDebug("Memory cache miss for key: {Key}", key);
        return null;
    }

    /// <summary>
    /// Sets data in memory cache
    /// </summary>
    public async Task SetMemoryCacheAsync<T>(string key, T data, TimeSpan? customExpiration = null) where T : class
    {
        var expiration = customExpiration ?? _memoryExpiration;
        var serializedData = JsonSerializer.Serialize(data);
        
        var cacheEntry = new CacheEntry
        {
            Key = key,
            Data = serializedData,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(expiration),
            LastAccessed = DateTime.UtcNow,
            AccessCount = 1,
            CacheLevel = CacheLevel.Memory
        };

        // Implement LRU eviction if cache is full
        if (_memoryCache.Count >= _maxMemoryCacheSize)
        {
            await EvictLeastRecentlyUsedAsync(_memoryCache, _maxMemoryCacheSize / 4); // Remove 25% of entries
        }

        _memoryCache.AddOrUpdate(key, cacheEntry, (k, existing) =>
        {
            existing.Data = serializedData;
            existing.ExpiresAt = DateTime.UtcNow.Add(expiration);
            existing.LastAccessed = DateTime.UtcNow;
            existing.AccessCount++;
            return existing;
        });

        _logger.LogDebug("Data cached in memory for key: {Key}, expires at: {ExpiresAt}", key, cacheEntry.ExpiresAt);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets or sets data in persistent cache with longer expiration
    /// </summary>
    public async Task<T?> GetFromPersistentCacheAsync<T>(string key) where T : class
    {
        if (_persistentCache.TryGetValue(key, out var entry) && !entry.IsExpired)
        {
            entry.LastAccessed = DateTime.UtcNow;
            entry.AccessCount++;
            _logger.LogDebug("Persistent cache hit for key: {Key}", key);
            
            try
            {
                return JsonSerializer.Deserialize<T>(entry.Data);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize persistent cached data for key: {Key}", key);
                _persistentCache.TryRemove(key, out _);
                return null;
            }
        }

        _logger.LogDebug("Persistent cache miss for key: {Key}", key);
        return null;
    }

    /// <summary>
    /// Sets data in persistent cache
    /// </summary>
    public async Task SetPersistentCacheAsync<T>(string key, T data, TimeSpan? customExpiration = null) where T : class
    {
        var expiration = customExpiration ?? _persistentExpiration;
        var serializedData = JsonSerializer.Serialize(data);
        
        var cacheEntry = new CacheEntry
        {
            Key = key,
            Data = serializedData,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(expiration),
            LastAccessed = DateTime.UtcNow,
            AccessCount = 1,
            CacheLevel = CacheLevel.Persistent
        };

        // Implement LRU eviction if cache is full
        if (_persistentCache.Count >= _maxPersistentCacheSize)
        {
            await EvictLeastRecentlyUsedAsync(_persistentCache, _maxPersistentCacheSize / 4); // Remove 25% of entries
        }

        _persistentCache.AddOrUpdate(key, cacheEntry, (k, existing) =>
        {
            existing.Data = serializedData;
            existing.ExpiresAt = DateTime.UtcNow.Add(expiration);
            existing.LastAccessed = DateTime.UtcNow;
            existing.AccessCount++;
            return existing;
        });

        _logger.LogDebug("Data cached persistently for key: {Key}, expires at: {ExpiresAt}", key, cacheEntry.ExpiresAt);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets data with multi-level cache fallback
    /// </summary>
    public async Task<T?> GetWithFallbackAsync<T>(string key, Func<Task<T?>> dataProvider, CacheStrategy strategy = CacheStrategy.MemoryFirst) where T : class
    {
        T? result = null;

        switch (strategy)
        {
            case CacheStrategy.MemoryFirst:
                // Try memory cache first
                result = await GetFromMemoryCacheAsync<T>(key);
                if (result != null) return result;

                // Try persistent cache
                result = await GetFromPersistentCacheAsync<T>(key);
                if (result != null)
                {
                    // Promote to memory cache
                    await SetMemoryCacheAsync(key, result);
                    return result;
                }
                break;

            case CacheStrategy.PersistentFirst:
                // Try persistent cache first
                result = await GetFromPersistentCacheAsync<T>(key);
                if (result != null) return result;

                // Try memory cache
                result = await GetFromMemoryCacheAsync<T>(key);
                if (result != null) return result;
                break;

            case CacheStrategy.MemoryOnly:
                result = await GetFromMemoryCacheAsync<T>(key);
                if (result != null) return result;
                break;

            case CacheStrategy.PersistentOnly:
                result = await GetFromPersistentCacheAsync<T>(key);
                if (result != null) return result;
                break;
        }

        // Cache miss - get from data provider
        result = await dataProvider();
        if (result != null)
        {
            // Cache based on strategy
            switch (strategy)
            {
                case CacheStrategy.MemoryFirst:
                case CacheStrategy.MemoryOnly:
                    await SetMemoryCacheAsync(key, result);
                    break;

                case CacheStrategy.PersistentFirst:
                case CacheStrategy.PersistentOnly:
                    await SetPersistentCacheAsync(key, result);
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Invalidates cache entries by pattern
    /// </summary>
    public async Task InvalidateCacheAsync(string pattern)
    {
        var memoryKeysToRemove = _memoryCache.Keys.Where(k => k.Contains(pattern)).ToList();
        var persistentKeysToRemove = _persistentCache.Keys.Where(k => k.Contains(pattern)).ToList();

        foreach (var key in memoryKeysToRemove)
        {
            _memoryCache.TryRemove(key, out _);
        }

        foreach (var key in persistentKeysToRemove)
        {
            _persistentCache.TryRemove(key, out _);
        }

        _logger.LogInformation("Invalidated {MemoryCount} memory cache entries and {PersistentCount} persistent cache entries matching pattern: {Pattern}",
            memoryKeysToRemove.Count, persistentKeysToRemove.Count, pattern);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets cache statistics for monitoring
    /// </summary>
    public async Task<CacheStatistics> GetCacheStatisticsAsync()
    {
        var memoryStats = CalculateCacheStats(_memoryCache);
        var persistentStats = CalculateCacheStats(_persistentCache);

        var statistics = new CacheStatistics
        {
            MemoryCacheSize = _memoryCache.Count,
            PersistentCacheSize = _persistentCache.Count,
            MemoryHitRatio = memoryStats.HitRatio,
            PersistentHitRatio = persistentStats.HitRatio,
            TotalMemoryUsage = EstimateMemoryUsage(_memoryCache) + EstimateMemoryUsage(_persistentCache),
            ExpiredEntriesCount = memoryStats.ExpiredCount + persistentStats.ExpiredCount,
            MostAccessedKeys = GetMostAccessedKeys(),
            Timestamp = DateTime.UtcNow
        };

        await Task.CompletedTask;
        return statistics;
    }

    /// <summary>
    /// Clears all cache entries
    /// </summary>
    public async Task ClearAllCacheAsync()
    {
        var memoryCount = _memoryCache.Count;
        var persistentCount = _persistentCache.Count;

        _memoryCache.Clear();
        _persistentCache.Clear();

        _logger.LogInformation("Cleared all cache entries: {MemoryCount} memory, {PersistentCount} persistent",
            memoryCount, persistentCount);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Warms up cache with frequently accessed data
    /// </summary>
    public async Task WarmupCacheAsync(IEnumerable<CacheWarmupItem> warmupItems)
    {
        var tasks = warmupItems.Select(async item =>
        {
            try
            {
                var data = await item.DataProvider();
                if (data != null)
                {
                    switch (item.CacheLevel)
                    {
                        case CacheLevel.Memory:
                            await SetMemoryCacheAsync(item.Key, data, item.Expiration);
                            break;
                        case CacheLevel.Persistent:
                            await SetPersistentCacheAsync(item.Key, data, item.Expiration);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warm up cache for key: {Key}", item.Key);
            }
        });

        await Task.WhenAll(tasks);
        _logger.LogInformation("Cache warmup completed for {Count} items", warmupItems.Count());
    }

    private async Task EvictLeastRecentlyUsedAsync(ConcurrentDictionary<string, CacheEntry> cache, int evictionCount)
    {
        var entriesToEvict = cache.Values
            .OrderBy(e => e.LastAccessed)
            .Take(evictionCount)
            .ToList();

        foreach (var entry in entriesToEvict)
        {
            cache.TryRemove(entry.Key, out _);
        }

        _logger.LogDebug("Evicted {Count} LRU cache entries", entriesToEvict.Count);
        await Task.CompletedTask;
    }

    private (double HitRatio, int ExpiredCount) CalculateCacheStats(ConcurrentDictionary<string, CacheEntry> cache)
    {
        if (cache.IsEmpty) return (0.0, 0);

        var entries = cache.Values.ToList();
        var totalAccesses = entries.Sum(e => e.AccessCount);
        var expiredCount = entries.Count(e => e.IsExpired);
        var hitRatio = totalAccesses > 0 ? (double)entries.Count / totalAccesses : 0.0;

        return (hitRatio, expiredCount);
    }

    private long EstimateMemoryUsage(ConcurrentDictionary<string, CacheEntry> cache)
    {
        return cache.Values.Sum(e => e.Key.Length * 2 + e.Data.Length * 2 + 100); // Rough estimate
    }

    private List<string> GetMostAccessedKeys()
    {
        var allEntries = _memoryCache.Values.Concat(_persistentCache.Values);
        return allEntries
            .OrderByDescending(e => e.AccessCount)
            .Take(10)
            .Select(e => e.Key)
            .ToList();
    }

    private void PerformCleanup(object? state)
    {
        try
        {
            CleanupExpiredEntries(_memoryCache, "memory");
            CleanupExpiredEntries(_persistentCache, "persistent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache cleanup");
        }
    }

    private void CleanupExpiredEntries(ConcurrentDictionary<string, CacheEntry> cache, string cacheType)
    {
        var expiredKeys = cache.Values
            .Where(e => e.IsExpired)
            .Select(e => e.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            cache.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired entries from {CacheType} cache", expiredKeys.Count, cacheType);
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _memoryCache.Clear();
        _persistentCache.Clear();
    }
}

/// <summary>
/// Cache entry with metadata
/// </summary>
public class CacheEntry
{
    public string Key { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime LastAccessed { get; set; }
    public int AccessCount { get; set; }
    public CacheLevel CacheLevel { get; set; }
    
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}

/// <summary>
/// Cache level enumeration
/// </summary>
public enum CacheLevel
{
    Memory,
    Persistent
}

/// <summary>
/// Cache strategy enumeration
/// </summary>
public enum CacheStrategy
{
    MemoryFirst,
    PersistentFirst,
    MemoryOnly,
    PersistentOnly
}

/// <summary>
/// Cache statistics for monitoring
/// </summary>
public class CacheStatistics
{
    public int MemoryCacheSize { get; set; }
    public int PersistentCacheSize { get; set; }
    public double MemoryHitRatio { get; set; }
    public double PersistentHitRatio { get; set; }
    public long TotalMemoryUsage { get; set; }
    public int ExpiredEntriesCount { get; set; }
    public List<string> MostAccessedKeys { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Cache warmup item
/// </summary>
public class CacheWarmupItem
{
    public string Key { get; set; } = string.Empty;
    public Func<Task<object?>> DataProvider { get; set; } = null!;
    public CacheLevel CacheLevel { get; set; } = CacheLevel.Memory;
    public TimeSpan? Expiration { get; set; }
}