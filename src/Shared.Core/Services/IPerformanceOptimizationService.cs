namespace Shared.Core.Services;

/// <summary>
/// Service interface for performance optimizations targeting low-end devices
/// </summary>
public interface IPerformanceOptimizationService : IDisposable
{
    /// <summary>
    /// Optimizes database queries by implementing result caching
    /// </summary>
    /// <typeparam name="T">Result type</typeparam>
    /// <param name="cacheKey">Unique cache key</param>
    /// <param name="queryFunc">Query function to execute</param>
    /// <param name="customExpiration">Custom cache expiration time</param>
    /// <returns>Query result</returns>
    Task<T> OptimizeQueryAsync<T>(string cacheKey, Func<Task<T>> queryFunc, TimeSpan? customExpiration = null);

    /// <summary>
    /// Optimizes memory usage by implementing aggressive garbage collection for low-end devices
    /// </summary>
    void OptimizeMemoryUsage();

    /// <summary>
    /// Optimizes UI rendering by implementing view recycling and lazy loading
    /// </summary>
    /// <typeparam name="T">Item type</typeparam>
    /// <param name="items">Items to render</param>
    /// <param name="pageSize">Page size for pagination</param>
    /// <param name="currentPage">Current page number</param>
    /// <returns>Paginated items</returns>
    Task<IEnumerable<T>> OptimizeListRenderingAsync<T>(IEnumerable<T> items, int pageSize = 20, int currentPage = 0);

    /// <summary>
    /// Optimizes network operations by implementing request batching and compression
    /// </summary>
    /// <typeparam name="TInput">Input type</typeparam>
    /// <typeparam name="TResult">Result type</typeparam>
    /// <param name="inputs">Input items</param>
    /// <param name="batchOperation">Batch operation function</param>
    /// <param name="batchSize">Batch size</param>
    /// <returns>Batch operation results</returns>
    Task<IEnumerable<TResult>> OptimizeBatchOperationsAsync<TInput, TResult>(
        IEnumerable<TInput> inputs,
        Func<IEnumerable<TInput>, Task<IEnumerable<TResult>>> batchOperation,
        int batchSize = 10);

    /// <summary>
    /// Optimizes database connections by implementing connection pooling and timeout management
    /// </summary>
    /// <typeparam name="T">Result type</typeparam>
    /// <param name="databaseOperation">Database operation function</param>
    /// <param name="timeout">Operation timeout</param>
    /// <returns>Operation result</returns>
    Task<T> OptimizeDatabaseOperationAsync<T>(Func<Task<T>> databaseOperation, TimeSpan? timeout = null);

    /// <summary>
    /// Clears cache entries and performs memory cleanup
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Gets current performance metrics
    /// </summary>
    /// <returns>Performance metrics</returns>
    CachePerformanceMetrics GetPerformanceMetrics();

    /// <summary>
    /// Configures performance settings for different device capabilities
    /// </summary>
    /// <param name="capability">Device capability level</param>
    void ConfigureForDeviceCapability(DeviceCapability capability);
}