namespace Shared.Core.Services;

/// <summary>
/// Interface for advanced caching strategy service
/// </summary>
public interface ICachingStrategyService : IDisposable
{
    /// <summary>
    /// Gets data from memory cache
    /// </summary>
    Task<T?> GetFromMemoryCacheAsync<T>(string key) where T : class;

    /// <summary>
    /// Sets data in memory cache
    /// </summary>
    Task SetMemoryCacheAsync<T>(string key, T data, TimeSpan? customExpiration = null) where T : class;

    /// <summary>
    /// Gets data from persistent cache
    /// </summary>
    Task<T?> GetFromPersistentCacheAsync<T>(string key) where T : class;

    /// <summary>
    /// Sets data in persistent cache
    /// </summary>
    Task SetPersistentCacheAsync<T>(string key, T data, TimeSpan? customExpiration = null) where T : class;

    /// <summary>
    /// Gets data with multi-level cache fallback
    /// </summary>
    Task<T?> GetWithFallbackAsync<T>(string key, Func<Task<T?>> dataProvider, CacheStrategy strategy = CacheStrategy.MemoryFirst) where T : class;

    /// <summary>
    /// Invalidates cache entries by pattern
    /// </summary>
    Task InvalidateCacheAsync(string pattern);

    /// <summary>
    /// Gets cache statistics for monitoring
    /// </summary>
    Task<CacheStatistics> GetCacheStatisticsAsync();

    /// <summary>
    /// Clears all cache entries
    /// </summary>
    Task ClearAllCacheAsync();

    /// <summary>
    /// Warms up cache with frequently accessed data
    /// </summary>
    Task WarmupCacheAsync(IEnumerable<CacheWarmupItem> warmupItems);
}