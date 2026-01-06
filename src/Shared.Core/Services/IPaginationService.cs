using System.Linq.Expressions;

namespace Shared.Core.Services;

/// <summary>
/// Interface for pagination and lazy loading services
/// </summary>
public interface IPaginationService
{
    /// <summary>
    /// Creates a paginated result with optimized query execution
    /// </summary>
    Task<PaginatedResult<T>> GetPaginatedResultAsync<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        string? cacheKey = null,
        TimeSpan? cacheExpiration = null) where T : class;

    /// <summary>
    /// Creates a lazy-loaded enumerable for streaming large datasets
    /// </summary>
    IAsyncEnumerable<T> GetLazyLoadedAsync<T>(
        IQueryable<T> query,
        int batchSize = 100) where T : class;

    /// <summary>
    /// Gets paginated results with custom ordering
    /// </summary>
    Task<PaginatedResult<T>> GetPaginatedResultWithOrderingAsync<T, TKey>(
        IQueryable<T> query,
        Expression<Func<T, TKey>> orderBy,
        bool descending,
        int page,
        int pageSize,
        string? cacheKey = null) where T : class;

    /// <summary>
    /// Gets paginated results with filtering
    /// </summary>
    Task<PaginatedResult<T>> GetPaginatedResultWithFilterAsync<T>(
        IQueryable<T> query,
        Expression<Func<T, bool>> filter,
        int page,
        int pageSize,
        string? cacheKey = null) where T : class;

    /// <summary>
    /// Gets paginated results with search functionality
    /// </summary>
    Task<PaginatedResult<T>> GetPaginatedResultWithSearchAsync<T>(
        IQueryable<T> query,
        string searchTerm,
        Expression<Func<T, string>>[] searchFields,
        int page,
        int pageSize,
        string? cacheKey = null) where T : class;

    /// <summary>
    /// Invalidates pagination cache for a specific type
    /// </summary>
    Task InvalidatePaginationCacheAsync<T>() where T : class;

    /// <summary>
    /// Gets pagination metadata without loading items
    /// </summary>
    Task<PaginationMetadata> GetPaginationMetadataAsync<T>(
        IQueryable<T> query,
        int pageSize) where T : class;
}