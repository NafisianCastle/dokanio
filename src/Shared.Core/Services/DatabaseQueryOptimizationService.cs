using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using System.Diagnostics;

namespace Shared.Core.Services;

/// <summary>
/// Service for optimizing database queries in multi-tenant architecture
/// Implements query optimization strategies for improved performance
/// </summary>
public class DatabaseQueryOptimizationService : IDatabaseQueryOptimizationService
{
    private readonly PosDbContext _context;
    private readonly IPerformanceOptimizationService _performanceService;
    private readonly ILogger<DatabaseQueryOptimizationService> _logger;

    public DatabaseQueryOptimizationService(
        PosDbContext context,
        IPerformanceOptimizationService performanceService,
        ILogger<DatabaseQueryOptimizationService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _performanceService = performanceService ?? throw new ArgumentNullException(nameof(performanceService));
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
        
        return await _performanceService.OptimizeQueryAsync(cacheKey, async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Optimized query with pagination and selective loading
            var shops = await _context.Shops
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
                .OrderBy(s => s.Name)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            stopwatch.Stop();
            _logger.LogDebug("Optimized shop query completed in {Duration}ms for business {BusinessId}", 
                stopwatch.ElapsedMilliseconds, businessId);
            
            return shops;
        }, TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Gets products with optimized query and filtering
    /// </summary>
    public async Task<IEnumerable<Product>> GetProductsOptimizedAsync(Guid shopId, string? category = null, bool activeOnly = true)
    {
        var cacheKey = $"products_shop_{shopId}_category_{category ?? "all"}_active_{activeOnly}";
        
        return await _performanceService.OptimizeQueryAsync(cacheKey, async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            
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
            
            // Optimized projection to reduce data transfer
            var products = await query
                .Select(p => new Product
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
                .OrderBy(p => p.Name)
                .ToListAsync();
            
            stopwatch.Stop();
            _logger.LogDebug("Optimized product query completed in {Duration}ms for shop {ShopId}", 
                stopwatch.ElapsedMilliseconds, shopId);
            
            return products;
        }, TimeSpan.FromMinutes(3));
    }

    /// <summary>
    /// Gets sales with optimized query and date range filtering
    /// </summary>
    public async Task<IEnumerable<Sale>> GetSalesOptimizedAsync(Guid shopId, DateTime? fromDate = null, DateTime? toDate = null, int page = 0, int pageSize = 50)
    {
        var cacheKey = $"sales_shop_{shopId}_from_{fromDate?.ToString("yyyyMMdd") ?? "null"}_to_{toDate?.ToString("yyyyMMdd") ?? "null"}_page_{page}_size_{pageSize}";
        
        return await _performanceService.OptimizeQueryAsync(cacheKey, async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            
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
            
            // Optimized query with selective loading
            var sales = await query
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
                .OrderByDescending(s => s.CreatedAt)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            stopwatch.Stop();
            _logger.LogDebug("Optimized sales query completed in {Duration}ms for shop {ShopId}", 
                stopwatch.ElapsedMilliseconds, shopId);
            
            return sales;
        }, TimeSpan.FromMinutes(2));
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
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // SQLite-specific optimizations
            await _context.Database.ExecuteSqlRawAsync("PRAGMA optimize");
            await _context.Database.ExecuteSqlRawAsync("ANALYZE");
            
            // Update statistics for better query planning
            await _context.Database.ExecuteSqlRawAsync("PRAGMA analysis_limit=1000");
            
            stopwatch.Stop();
            _logger.LogInformation("Database optimization completed in {Duration}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing database indexes");
            throw;
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