using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Shared.Core.Services;

/// <summary>
/// Service for handling pagination and lazy loading of large datasets
/// Optimized for performance with large data volumes
/// </summary>
public class PaginationService : IPaginationService
{
    private readonly ILogger<PaginationService> _logger;
    private readonly ICachingStrategyService _cachingService;

    public PaginationService(
        ILogger<PaginationService> logger,
        ICachingStrategyService cachingService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cachingService = cachingService ?? throw new ArgumentNullException(nameof(cachingService));
    }

    /// <summary>
    /// Creates a paginated result with optimized query execution
    /// </summary>
    public async Task<PaginatedResult<T>> GetPaginatedResultAsync<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        string? cacheKey = null,
        TimeSpan? cacheExpiration = null) where T : class
    {
        if (page < 0) page = 0;
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 1000) pageSize = 1000; // Prevent excessive page sizes

        var effectiveCacheKey = cacheKey ?? $"pagination_{typeof(T).Name}_{page}_{pageSize}_{query.GetHashCode()}";

        // Try to get from cache first
        if (!string.IsNullOrEmpty(cacheKey))
        {
            var cachedResult = await _cachingService.GetFromMemoryCacheAsync<PaginatedResult<T>>(effectiveCacheKey);
            if (cachedResult != null)
            {
                _logger.LogDebug("Pagination cache hit for key: {CacheKey}", effectiveCacheKey);
                return cachedResult;
            }
        }

        var totalCount = await Task.Run(() => query.Count());
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await Task.Run(() => 
            query.Skip(page * pageSize)
                 .Take(pageSize)
                 .ToList());

        var result = new PaginatedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasNextPage = page < totalPages - 1,
            HasPreviousPage = page > 0
        };

        // Cache the result if cache key is provided
        if (!string.IsNullOrEmpty(cacheKey))
        {
            await _cachingService.SetMemoryCacheAsync(effectiveCacheKey, result, cacheExpiration);
        }

        _logger.LogDebug("Paginated query executed: Page {Page}/{TotalPages}, Items {ItemCount}/{TotalCount}",
            page + 1, totalPages, items.Count, totalCount);

        return result;
    }

    /// <summary>
    /// Creates a lazy-loaded enumerable for streaming large datasets
    /// </summary>
    public IAsyncEnumerable<T> GetLazyLoadedAsync<T>(
        IQueryable<T> query,
        int batchSize = 100) where T : class
    {
        if (batchSize <= 0) batchSize = 100;
        if (batchSize > 1000) batchSize = 1000;

        return GetLazyLoadedInternalAsync(query, batchSize);
    }

    /// <summary>
    /// Gets paginated results with custom ordering
    /// </summary>
    public async Task<PaginatedResult<T>> GetPaginatedResultWithOrderingAsync<T, TKey>(
        IQueryable<T> query,
        Expression<Func<T, TKey>> orderBy,
        bool descending,
        int page,
        int pageSize,
        string? cacheKey = null) where T : class
    {
        var orderedQuery = descending 
            ? query.OrderByDescending(orderBy)
            : query.OrderBy(orderBy);

        return await GetPaginatedResultAsync(orderedQuery, page, pageSize, cacheKey);
    }

    /// <summary>
    /// Gets paginated results with filtering
    /// </summary>
    public async Task<PaginatedResult<T>> GetPaginatedResultWithFilterAsync<T>(
        IQueryable<T> query,
        Expression<Func<T, bool>> filter,
        int page,
        int pageSize,
        string? cacheKey = null) where T : class
    {
        var filteredQuery = query.Where(filter);
        return await GetPaginatedResultAsync(filteredQuery, page, pageSize, cacheKey);
    }

    /// <summary>
    /// Gets paginated results with search functionality
    /// </summary>
    public async Task<PaginatedResult<T>> GetPaginatedResultWithSearchAsync<T>(
        IQueryable<T> query,
        string searchTerm,
        Expression<Func<T, string>>[] searchFields,
        int page,
        int pageSize,
        string? cacheKey = null) where T : class
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || searchFields == null || !searchFields.Any())
        {
            return await GetPaginatedResultAsync(query, page, pageSize, cacheKey);
        }

        // Build search expression
        Expression<Func<T, bool>>? searchExpression = null;
        
        foreach (var field in searchFields)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Invoke(field, parameter);
            var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
            var searchValue = Expression.Constant(searchTerm, typeof(string));
            var containsCall = Expression.Call(property, containsMethod!, searchValue);
            
            var lambda = Expression.Lambda<Func<T, bool>>(containsCall, parameter);
            
            searchExpression = searchExpression == null 
                ? lambda 
                : CombineExpressions(searchExpression, lambda, Expression.OrElse);
        }

        var searchQuery = searchExpression != null ? query.Where(searchExpression) : query;
        return await GetPaginatedResultAsync(searchQuery, page, pageSize, cacheKey);
    }

    /// <summary>
    /// Invalidates pagination cache for a specific type
    /// </summary>
    public async Task InvalidatePaginationCacheAsync<T>() where T : class
    {
        var pattern = $"pagination_{typeof(T).Name}_";
        await _cachingService.InvalidateCacheAsync(pattern);
        _logger.LogDebug("Invalidated pagination cache for type: {TypeName}", typeof(T).Name);
    }

    /// <summary>
    /// Gets pagination metadata without loading items
    /// </summary>
    public async Task<PaginationMetadata> GetPaginationMetadataAsync<T>(
        IQueryable<T> query,
        int pageSize) where T : class
    {
        var totalCount = await Task.Run(() => query.Count());
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return new PaginationMetadata
        {
            TotalCount = totalCount,
            TotalPages = totalPages,
            PageSize = pageSize
        };
    }

    private async IAsyncEnumerable<T> GetLazyLoadedInternalAsync<T>(
        IQueryable<T> query,
        int batchSize) where T : class
    {
        var skip = 0;
        List<T> batch;

        do
        {
            batch = await Task.Run(() => 
                query.Skip(skip)
                     .Take(batchSize)
                     .ToList());

            foreach (var item in batch)
            {
                yield return item;
            }

            skip += batchSize;
            
            // Small delay to prevent overwhelming the system
            if (batch.Count == batchSize)
            {
                await Task.Delay(1);
            }

        } while (batch.Count == batchSize);

        _logger.LogDebug("Lazy loading completed: {TotalItems} items processed in batches of {BatchSize}",
            skip, batchSize);
    }

    private Expression<Func<T, bool>> CombineExpressions<T>(
        Expression<Func<T, bool>> first,
        Expression<Func<T, bool>> second,
        Func<Expression, Expression, Expression> merge)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var firstBody = ReplaceParameter(first.Body, first.Parameters[0], parameter);
        var secondBody = ReplaceParameter(second.Body, second.Parameters[0], parameter);
        var merged = merge(firstBody, secondBody);
        return Expression.Lambda<Func<T, bool>>(merged, parameter);
    }

    private Expression ReplaceParameter(Expression expression, ParameterExpression oldParameter, ParameterExpression newParameter)
    {
        return new ParameterReplacer(oldParameter, newParameter).Visit(expression);
    }

    private class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParameter;
        private readonly ParameterExpression _newParameter;

        public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _oldParameter ? _newParameter : base.VisitParameter(node);
        }
    }
}

/// <summary>
/// Represents a paginated result set
/// </summary>
public class PaginatedResult<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}

/// <summary>
/// Represents pagination metadata
/// </summary>
public class PaginationMetadata
{
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
}