using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Shared.Core.Services;

/// <summary>
/// Service for optimizing database queries in multi-tenant architecture
/// Implements query optimization strategies for improved performance
/// </summary>
public class DatabaseQueryOptimizationService : IDatabaseQueryOptimizationService
{
    private readonly PosDbContext _context;
    private readonly IPerformanceOptimizationService _performanceService;
    private readonly IPaginationService _paginationService;
    private readonly IPerformanceMonitoringService _performanceMonitoringService;
    private readonly ILogger<DatabaseQueryOptimizationService> _logger;

    public DatabaseQueryOptimizationService(
        PosDbContext context,
        IPerformanceOptimizationService performanceService,
        IPaginationService paginationService,
        IPerformanceMonitoringService performanceMonitoringService,
        ILogger<DatabaseQueryOptimizationService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _performanceService = performanceService ?? throw new ArgumentNullException(nameof(performanceService));
        _paginationService = paginationService ?? throw new ArgumentNullException(nameof(paginationService));
        _performanceMonitoringService = performanceMonitoringService ?? throw new ArgumentNullException(nameof(performanceMonitoringService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets businesses with optimized query including proper indexing
    /// </summary>
    public async Task<IEnumerable<Business>> GetBusinessesOptimizedAsync(Guid ownerId)
    {
        var cacheKey = $"businesses_owner_{ownerId}";
        
        return await _performanceService.OptimizeQueryAsync(cacheKey, async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Optimized query with selective loading and proper indexing
            var businesses = await _context.Businesses
                .Where(b => b.OwnerId == ownerId && !b.IsDeleted)
                .Select(b => new Business
                {
                    Id = b.Id,
                    Name = b.Name,
                    Type = b.Type,
                    Description = b.Description,
                    Address = b.Address,
                    Phone = b.Phone,
                    Email = b.Email,
                    OwnerId = b.OwnerId,
                    IsActive = b.IsActive,
                    CreatedAt = b.CreatedAt,
                    UpdatedAt = b.UpdatedAt
                })
                .OrderBy(b => b.Name)
                .ToListAsync();
            
            stopwatch.Stop();
            _logger.LogDebug("Optimized business query completed in {Duration}ms for owner {OwnerId}", 
                stopwatch.ElapsedMilliseconds, ownerId);
            
            return businesses;
        }, TimeSpan.FromMinutes(10));
    }

    /// <summary>
    /// Gets shops with optimized query and pagination
    /// </summary>
    public async Task<IEnumerable<Shop>> GetShopsOptimizedAsync(Guid businessId, int page = 0, int pageSize = 20)
    {
        var cacheKey = $"shops_business_{businessId}_page_{page}_size_{pageSize}";
        
        return await _performanceMonitoringService.MeasureOperationAsync("GetShopsOptimized", async () =>
        {
            return await _performanceService.OptimizeQueryAsync(cacheKey, async () =>
            {
                var query = _context.Shops
                    .Where(s => s.BusinessId == businessId && !s.IsDeleted)
                    .Select(s => new Shop
                    {
                        Id = s.Id,
                        BusinessId = s.BusinessId,
                        Name = s.Name,
                        Address = s.Address,
                        Phone = s.Phone,
                        Email = s.Email,
                        IsActive = s.IsActive,
                        CreatedAt = s.CreatedAt,
                        UpdatedAt = s.UpdatedAt
                    })
                    .OrderBy(s => s.Name);

                var paginatedResult = await _paginationService.GetPaginatedResultAsync(
                    query, page, pageSize, cacheKey, TimeSpan.FromMinutes(5));

                _logger.LogDebug("Optimized shop query completed: Page {Page}/{TotalPages}, Items {ItemCount}/{TotalCount}",
                    page + 1, paginatedResult.TotalPages, paginatedResult.Items.Count(), paginatedResult.TotalCount);

                return paginatedResult.Items;
            }, TimeSpan.FromMinutes(5));
        });
    }

    /// <summary>
    /// Gets products with optimized query and filtering
    /// </summary>
    public async Task<IEnumerable<Product>> GetProductsOptimizedAsync(Guid shopId, string? category = null, bool activeOnly = true)
    {
        var cacheKey = $"products_shop_{shopId}_category_{category ?? "all"}_active_{activeOnly}";
        
        return await _performanceMonitoringService.MeasureOperationAsync("GetProductsOptimized", async () =>
        {
            return await _performanceService.OptimizeQueryAsync(cacheKey, async () =>
            {
                var query = _context.Products
                    .Where(p => p.ShopId == shopId && !p.IsDeleted);
                
                if (activeOnly)
                {
                    query = query.Where(p => p.IsActive);
                }
                
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(p => p.Category == category);
                }
                
                // Use lazy loading for large product catalogs
                var lazyProducts = _paginationService.GetLazyLoadedAsync(
                    query.Select(p => new Product
                    {
                        Id = p.Id,
                        ShopId = p.ShopId,
                        Name = p.Name,
                        Barcode = p.Barcode,
                        Category = p.Category,
                        UnitPrice = p.UnitPrice,
                        PurchasePrice = p.PurchasePrice,
                        SellingPrice = p.SellingPrice,
                        ExpiryDate = p.ExpiryDate,
                        IsWeightBased = p.IsWeightBased,
                        RatePerKilogram = p.RatePerKilogram,
                        IsActive = p.IsActive,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    })
                    .OrderBy(p => p.Name),
                    batchSize: 50);

                var products = new List<Product>();
                await foreach (var product in lazyProducts)
                {
                    products.Add(product);
                }

                _logger.LogDebug("Optimized product query completed for shop {ShopId}, loaded {Count} products", 
                    shopId, products.Count);
                
                return products;
            }, TimeSpan.FromMinutes(3));
        });
    }

    /// <summary>
    /// Gets sales with optimized query and date range filtering
    /// </summary>
    public async Task<IEnumerable<Sale>> GetSalesOptimizedAsync(Guid shopId, DateTime? fromDate = null, DateTime? toDate = null, int page = 0, int pageSize = 50)
    {
        var cacheKey = $"sales_shop_{shopId}_from_{fromDate?.ToString("yyyyMMdd") ?? "null"}_to_{toDate?.ToString("yyyyMMdd") ?? "null"}_page_{page}_size_{pageSize}";
        
        return await _performanceMonitoringService.MeasureOperationAsync("GetSalesOptimized", async () =>
        {
            return await _performanceService.OptimizeQueryAsync(cacheKey, async () =>
            {
                var query = _context.Sales
                    .Where(s => s.ShopId == shopId && !s.IsDeleted);
                
                if (fromDate.HasValue)
                {
                    query = query.Where(s => s.CreatedAt >= fromDate.Value);
                }
                
                if (toDate.HasValue)
                {
                    query = query.Where(s => s.CreatedAt <= toDate.Value);
                }
                
                var orderedQuery = query
                    .Select(s => new Sale
                    {
                        Id = s.Id,
                        ShopId = s.ShopId,
                        UserId = s.UserId,
                        CustomerId = s.CustomerId,
                        InvoiceNumber = s.InvoiceNumber,
                        TotalAmount = s.TotalAmount,
                        DiscountAmount = s.DiscountAmount,
                        TaxAmount = s.TaxAmount,
                        PaymentMethod = s.PaymentMethod,
                        CreatedAt = s.CreatedAt,
                        UpdatedAt = s.UpdatedAt
                    })
                    .OrderByDescending(s => s.CreatedAt);

                var paginatedResult = await _paginationService.GetPaginatedResultAsync(
                    orderedQuery, page, pageSize, cacheKey, TimeSpan.FromMinutes(2));

                _logger.LogDebug("Optimized sales query completed: Page {Page}/{TotalPages}, Items {ItemCount}/{TotalCount}",
                    page + 1, paginatedResult.TotalPages, paginatedResult.Items.Count(), paginatedResult.TotalCount);

                return paginatedResult.Items;
            }, TimeSpan.FromMinutes(2));
        });
    }

    /// <summary>
    /// Gets aggregated sales data with optimized query
    /// </summary>
    public async Task<SalesAggregateData> GetSalesAggregateOptimizedAsync(Guid shopId, DateTime fromDate, DateTime toDate)
    {
        var cacheKey = $"sales_aggregate_shop_{shopId}_from_{fromDate:yyyyMMdd}_to_{toDate:yyyyMMdd}";
        
        return await _performanceService.OptimizeQueryAsync(cacheKey, async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Optimized aggregate query using database-level aggregation
            var aggregateData = await _context.Sales
                .Where(s => s.ShopId == shopId && !s.IsDeleted && 
                           s.CreatedAt >= fromDate && s.CreatedAt <= toDate)
                .GroupBy(s => 1) // Group all records together
                .Select(g => new SalesAggregateData
                {
                    ShopId = shopId,
                    FromDate = fromDate,
                    ToDate = toDate,
                    TotalSales = g.Count(),
                    TotalRevenue = g.Sum(s => s.TotalAmount),
                    TotalDiscount = g.Sum(s => s.DiscountAmount),
                    TotalTax = g.Sum(s => s.TaxAmount),
                    AverageOrderValue = g.Average(s => s.TotalAmount)
                })
                .FirstOrDefaultAsync() ?? new SalesAggregateData
                {
                    ShopId = shopId,
                    FromDate = fromDate,
                    ToDate = toDate
                };
            
            stopwatch.Stop();
            _logger.LogDebug("Optimized sales aggregate query completed in {Duration}ms for shop {ShopId}", 
                stopwatch.ElapsedMilliseconds, shopId);
            
            return aggregateData;
        }, TimeSpan.FromMinutes(15));
    }

    /// <summary>
    /// Gets inventory levels with optimized query
    /// </summary>
    public async Task<IEnumerable<InventoryLevel>> GetInventoryLevelsOptimizedAsync(Guid shopId)
    {
        var cacheKey = $"inventory_levels_shop_{shopId}";
        
        return await _performanceService.OptimizeQueryAsync(cacheKey, async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Optimized query with join and aggregation
            var inventoryLevels = await _context.Stock
                .Where(s => s.ShopId == shopId && !s.IsDeleted)
                .Join(_context.Products.Where(p => !p.IsDeleted),
                      stock => stock.ProductId,
                      product => product.Id,
                      (stock, product) => new InventoryLevel
                      {
                          ProductId = product.Id,
                          ProductName = product.Name,
                          Category = product.Category,
                          CurrentStock = stock.Quantity,
                          UnitPrice = product.UnitPrice,
                          TotalValue = stock.Quantity * product.UnitPrice,
                          ExpiryDate = product.ExpiryDate,
                          IsLowStock = stock.Quantity <= 10, // Configurable threshold
                          LastUpdated = stock.UpdatedAt
                      })
                .OrderBy(i => i.ProductName)
                .ToListAsync();
            
            stopwatch.Stop();
            _logger.LogDebug("Optimized inventory levels query completed in {Duration}ms for shop {ShopId}", 
                stopwatch.ElapsedMilliseconds, shopId);
            
            return inventoryLevels;
        }, TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Optimizes database indexes for better query performance
    /// </summary>
    public async Task OptimizeDatabaseIndexesAsync()
    {
        await _performanceMonitoringService.MeasureOperationAsync("OptimizeDatabaseIndexes", async () =>
        {
            try
            {
                // Check if we're using SQLite (skip optimization for in-memory testing)
                var providerName = _context.Database.ProviderName;
                if (providerName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true &&
                    !_context.Database.IsInMemory())
                {
                    // SQLite-specific optimizations
                    await _context.Database.ExecuteSqlRawAsync("PRAGMA optimize");
                    await _context.Database.ExecuteSqlRawAsync("ANALYZE");
                    
                    // Update statistics for better query planning
                    await _context.Database.ExecuteSqlRawAsync("PRAGMA analysis_limit=1000");
                    
                    // Rebuild indexes if needed (SQLite auto-vacuum)
                    await _context.Database.ExecuteSqlRawAsync("PRAGMA auto_vacuum=INCREMENTAL");
                    await _context.Database.ExecuteSqlRawAsync("PRAGMA incremental_vacuum");
                    
                    _logger.LogInformation("SQLite database optimization completed successfully");
                }
                else
                {
                    _logger.LogInformation("Database optimization skipped for provider: {ProviderName}", providerName);
                }
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing database indexes");
                throw;
            }
        });
    }

    /// <summary>
    /// Gets paginated products with search functionality
    /// </summary>
    public async Task<PaginatedResult<Product>> GetProductsPaginatedAsync(
        Guid shopId, 
        string? searchTerm = null, 
        string? category = null, 
        bool activeOnly = true,
        int page = 0, 
        int pageSize = 20)
    {
        return await _performanceMonitoringService.MeasureOperationAsync("GetProductsPaginated", async () =>
        {
            var query = _context.Products
                .Where(p => p.ShopId == shopId && !p.IsDeleted);
            
            if (activeOnly)
            {
                query = query.Where(p => p.IsActive);
            }
            
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(p => p.Category == category);
            }

            var projectedQuery = query.Select(p => new Product
            {
                Id = p.Id,
                ShopId = p.ShopId,
                Name = p.Name,
                Barcode = p.Barcode,
                Category = p.Category,
                UnitPrice = p.UnitPrice,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt
            });

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                return await _paginationService.GetPaginatedResultWithSearchAsync(
                    projectedQuery,
                    searchTerm,
                    new Expression<Func<Product, string>>[] { p => p.Name, p => p.Barcode },
                    page,
                    pageSize,
                    $"products_search_{shopId}_{searchTerm}_{category}_{activeOnly}");
            }

            return await _paginationService.GetPaginatedResultAsync(
                projectedQuery.OrderBy(p => p.Name),
                page,
                pageSize,
                $"products_paginated_{shopId}_{category}_{activeOnly}");
        });
    }

    /// <summary>
    /// Gets paginated sales with advanced filtering
    /// </summary>
    public async Task<PaginatedResult<Sale>> GetSalesPaginatedAsync(
        Guid shopId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? customerId = null,
        decimal? minAmount = null,
        decimal? maxAmount = null,
        int page = 0,
        int pageSize = 20)
    {
        return await _performanceMonitoringService.MeasureOperationAsync("GetSalesPaginated", async () =>
        {
            var query = _context.Sales
                .Where(s => s.ShopId == shopId && !s.IsDeleted);
            
            if (fromDate.HasValue)
            {
                query = query.Where(s => s.CreatedAt >= fromDate.Value);
            }
            
            if (toDate.HasValue)
            {
                query = query.Where(s => s.CreatedAt <= toDate.Value);
            }

            if (customerId.HasValue)
            {
                query = query.Where(s => s.CustomerId == customerId.Value);
            }

            if (minAmount.HasValue)
            {
                query = query.Where(s => s.TotalAmount >= minAmount.Value);
            }

            if (maxAmount.HasValue)
            {
                query = query.Where(s => s.TotalAmount <= maxAmount.Value);
            }

            var projectedQuery = query.Select(s => new Sale
            {
                Id = s.Id,
                ShopId = s.ShopId,
                UserId = s.UserId,
                CustomerId = s.CustomerId,
                InvoiceNumber = s.InvoiceNumber,
                TotalAmount = s.TotalAmount,
                DiscountAmount = s.DiscountAmount,
                TaxAmount = s.TaxAmount,
                PaymentMethod = s.PaymentMethod,
                CreatedAt = s.CreatedAt
            });

            return await _paginationService.GetPaginatedResultWithOrderingAsync(
                projectedQuery,
                s => s.CreatedAt,
                descending: true,
                page,
                pageSize,
                $"sales_paginated_{shopId}_{fromDate?.ToString("yyyyMMdd")}_{toDate?.ToString("yyyyMMdd")}");
        });
    }

    /// <summary>
    /// Gets customer lookup results with fast mobile number search
    /// </summary>
    public async Task<IEnumerable<Customer>> GetCustomersByMobileAsync(string mobileNumber)
    {
        return await _performanceMonitoringService.MeasureOperationAsync("GetCustomersByMobile", async () =>
        {
            var cacheKey = $"customers_mobile_{mobileNumber}";
            
            return await _performanceService.OptimizeQueryAsync(cacheKey, async () =>
            {
                // Use the optimized phone index for fast lookup
                var customers = await _context.Customers
                    .Where(c => c.Phone == mobileNumber && c.IsActive && !c.IsDeleted)
                    .Select(c => new Customer
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Phone = c.Phone,
                        Email = c.Email,
                        MembershipNumber = c.MembershipNumber,
                        Tier = c.Tier,
                        TotalSpent = c.TotalSpent,
                        JoinDate = c.JoinDate,
                        IsActive = c.IsActive
                    })
                    .ToListAsync();





                _logger.LogDebug("Customer mobile lookup completed: {Count} customers found",
                    customers.Count);

                return customers;
            }, TimeSpan.FromMinutes(10));
        });
    }

    /// <summary>
    /// Gets performance analytics for database operations
    /// </summary>
    public async Task<DatabasePerformanceAnalytics> GetPerformanceAnalyticsAsync()
    {
        return await _performanceMonitoringService.MeasureOperationAsync("GetDatabasePerformanceAnalytics", async () =>
        {
            var analytics = new DatabasePerformanceAnalytics();

            // Get database size (SQLite specific)
            var providerName = _context.Database.ProviderName;
            if (providerName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true &&
                !_context.Database.IsInMemory())
            {
                try
                {
                    var connection = _context.Database.GetDbConnection();
                    if (connection.State != System.Data.ConnectionState.Open)
                    {
                        await connection.OpenAsync();
                    }

                    long pageCount;
                    long pageSize;

                    await using (var pageCountCommand = connection.CreateCommand())
                    {
                        pageCountCommand.CommandText = "PRAGMA page_count;";
                        pageCount = Convert.ToInt64(await pageCountCommand.ExecuteScalarAsync());
                    }

                    await using (var pageSizeCommand = connection.CreateCommand())
                    {
                        pageSizeCommand.CommandText = "PRAGMA page_size;";
                        pageSize = Convert.ToInt64(await pageSizeCommand.ExecuteScalarAsync());
                    }

                    analytics.DatabaseSizeBytes = pageCount * pageSize;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not get database size information");
                    analytics.DatabaseSizeBytes = 0;
                }
            }
            
            // Get table statistics
            analytics.TableStatistics = new Dictionary<string, TableStatistics>
            {
                ["Businesses"] = await GetTableStatisticsAsync("Businesses"),
                ["Shops"] = await GetTableStatisticsAsync("Shops"),
                ["Products"] = await GetTableStatisticsAsync("Products"),
                ["Sales"] = await GetTableStatisticsAsync("Sales"),
                ["SaleItems"] = await GetTableStatisticsAsync("SaleItems"),
                ["Customers"] = await GetTableStatisticsAsync("Customers"),
                ["Stock"] = await GetTableStatisticsAsync("Stock")
            };

            // Calculate cache statistics
            var cacheStats = _performanceService.GetPerformanceMetrics();
            analytics.CacheHitRatio = cacheStats.CacheHitRatio;
            analytics.CacheSize = cacheStats.CacheSize;

            analytics.Timestamp = DateTime.UtcNow;
            
            return analytics;
        });
    }

    private async Task<TableStatistics> GetTableStatisticsAsync(string tableName)
    {
        try
        {
            // Use EF Core's built-in count method instead of raw SQL for better compatibility
            var count = tableName switch
            {
                "Businesses" => await _context.Businesses.CountAsync(),
                "Shops" => await _context.Shops.CountAsync(),
                "Products" => await _context.Products.CountAsync(),
                "Sales" => await _context.Sales.CountAsync(),
                "SaleItems" => await _context.SaleItems.CountAsync(),
                "Customers" => await _context.Customers.CountAsync(),
                "Stock" => await _context.Stock.CountAsync(),
                _ => 0
            };
            
            return new TableStatistics
            {
                TableName = tableName,
                RowCount = count,
                LastUpdated = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting statistics for table {TableName}", tableName);
            return new TableStatistics
            {
                TableName = tableName,
                RowCount = 0,
                LastUpdated = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Clears query cache to free memory
    /// </summary>
    public void ClearQueryCache()
    {
        _performanceService.ClearCache();
        _logger.LogInformation("Query cache cleared");
    }
}

/// <summary>
/// Sales aggregate data for reporting
/// </summary>
public class SalesAggregateData
{
    public Guid ShopId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalSales { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalTax { get; set; }
    public decimal AverageOrderValue { get; set; }
}

/// <summary>
/// Inventory level data for stock management
/// </summary>
public class InventoryLevel
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsLowStock { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Database performance analytics
/// </summary>
public class DatabasePerformanceAnalytics
{
    public Dictionary<string, TableStatistics> TableStatistics { get; set; } = new();
    public double CacheHitRatio { get; set; }
    public int CacheSize { get; set; }
    public long DatabaseSizeBytes { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Table statistics for performance monitoring
/// </summary>
public class TableStatistics
{
    public string TableName { get; set; } = string.Empty;
    public long RowCount { get; set; }
    public long SizeBytes { get; set; }
    public DateTime LastUpdated { get; set; }
}