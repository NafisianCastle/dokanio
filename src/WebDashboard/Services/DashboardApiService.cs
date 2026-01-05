using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Services;
using Shared.Core.Repositories;
using System.Text.Json;

namespace WebDashboard.Services;

public class DashboardApiService : IDashboardApiService
{
    private readonly IBusinessRepository _businessRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockRepository _stockRepository;
    private readonly IShopRepository _shopRepository;
    private readonly ILogger<DashboardApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public DashboardApiService(
        IBusinessRepository businessRepository,
        ISaleRepository saleRepository,
        IProductRepository productRepository,
        IStockRepository stockRepository,
        IShopRepository shopRepository,
        ILogger<DashboardApiService> logger)
    {
        _businessRepository = businessRepository;
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _stockRepository = stockRepository;
        _shopRepository = shopRepository;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<DashboardOverview> GetDashboardOverviewAsync(Guid businessId, DashboardFilter? filter = null)
    {
        try
        {
            _logger.LogInformation("Getting dashboard overview for business {BusinessId}", businessId);

            var business = await _businessRepository.GetByIdAsync(businessId);
            if (business == null)
            {
                throw new ArgumentException($"Business with ID {businessId} not found");
            }

            var shops = await _shopRepository.FindAsync(s => s.BusinessId == businessId);
            var shopIds = shops.Select(s => s.Id).ToList();

            // Get today's sales data
            var today = DateTime.UtcNow.Date;
            var yesterday = today.AddDays(-1);
            
            var todaySales = await _saleRepository.FindAsync(s => 
                shopIds.Contains(s.ShopId) && 
                s.CreatedAt >= today && 
                s.CreatedAt < today.AddDays(1));
            
            var yesterdaySales = await _saleRepository.FindAsync(s => 
                shopIds.Contains(s.ShopId) && 
                s.CreatedAt >= yesterday && 
                s.CreatedAt < today);

            var todayRevenue = todaySales.Sum(s => s.TotalAmount);
            var yesterdayRevenue = yesterdaySales.Sum(s => s.TotalAmount);
            var todayTransactionCount = todaySales.Count();
            var yesterdayTransactionCount = yesterdaySales.Count();

            // Calculate percentage changes
            var revenueChangePercentage = yesterdayRevenue > 0 
                ? ((todayRevenue - yesterdayRevenue) / yesterdayRevenue) * 100 
                : 0;
            var transactionChangePercentage = yesterdayTransactionCount > 0 
                ? ((todayTransactionCount - yesterdayTransactionCount) / (decimal)yesterdayTransactionCount) * 100 
                : 0;

            // Get inventory status
            var allStock = await _stockRepository.FindAsync(s => shopIds.Contains(s.ShopId));
            var products = await _productRepository.FindAsync(p => allStock.Select(s => s.ProductId).Contains(p.Id));
            
            var lowStockThreshold = 10; // This could come from configuration
            var lowStockProducts = allStock.Count(s => s.Quantity <= lowStockThreshold);
            var outOfStockProducts = allStock.Count(s => s.Quantity == 0);
            var expiringProducts = products.Count(p => p.ExpiryDate.HasValue && p.ExpiryDate.Value <= DateTime.UtcNow.AddDays(30));
            var totalInventoryValue = allStock.Sum(s => s.Quantity * (products.FirstOrDefault(p => p.Id == s.ProductId)?.UnitPrice ?? 0));

            return new DashboardOverview
            {
                BusinessId = businessId,
                BusinessName = business.Name,
                BusinessType = business.Type,
                TotalShops = shops.Count(),
                RealTimeSales = new RealTimeSalesData
                {
                    TodayRevenue = todayRevenue,
                    TodayTransactionCount = todayTransactionCount,
                    AverageOrderValue = todayTransactionCount > 0 ? todayRevenue / todayTransactionCount : 0,
                    YesterdayRevenue = yesterdayRevenue,
                    RevenueChangePercentage = revenueChangePercentage,
                    TransactionChangePercentage = (int)transactionChangePercentage,
                    LastUpdated = DateTime.UtcNow
                },
                InventoryStatus = new InventoryStatusSummary
                {
                    TotalProducts = products.Count(),
                    LowStockProducts = lowStockProducts,
                    OutOfStockProducts = outOfStockProducts,
                    ExpiringProducts = expiringProducts,
                    TotalInventoryValue = totalInventoryValue
                },
                GeneratedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard overview for business {BusinessId}", businessId);
            throw;
        }
    }

    public async Task<IEnumerable<AlertSummary>> GetActiveAlertsAsync(Guid businessId)
    {
        try
        {
            var alerts = new List<AlertSummary>();
            var shops = await _shopRepository.FindAsync(s => s.BusinessId == businessId);
            var shopIds = shops.Select(s => s.Id).ToList();

            // Get inventory-based alerts
            var allStock = await _stockRepository.FindAsync(s => shopIds.Contains(s.ShopId));
            var products = await _productRepository.FindAsync(p => allStock.Select(s => s.ProductId).Contains(p.Id));

            var lowStockThreshold = 10;
            var lowStockCount = allStock.Count(s => s.Quantity <= lowStockThreshold && s.Quantity > 0);
            var outOfStockCount = allStock.Count(s => s.Quantity == 0);
            var expiringCount = products.Count(p => p.ExpiryDate.HasValue && p.ExpiryDate.Value <= DateTime.UtcNow.AddDays(7));

            if (lowStockCount > 0)
            {
                alerts.Add(new AlertSummary
                {
                    Type = AlertType.LowStock,
                    Priority = AlertPriority.Medium,
                    Title = "Low Stock Alert",
                    Message = $"{lowStockCount} products are running low on stock",
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    IsRead = false
                });
            }

            if (outOfStockCount > 0)
            {
                alerts.Add(new AlertSummary
                {
                    Type = AlertType.OutOfStock,
                    Priority = AlertPriority.High,
                    Title = "Out of Stock",
                    Message = $"{outOfStockCount} products are completely out of stock",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                    IsRead = false
                });
            }

            if (expiringCount > 0)
            {
                alerts.Add(new AlertSummary
                {
                    Type = AlertType.ProductExpiry,
                    Priority = AlertPriority.Medium,
                    Title = "Products Expiring Soon",
                    Message = $"{expiringCount} products are expiring within 7 days",
                    CreatedAt = DateTime.UtcNow.AddHours(-3),
                    IsRead = false
                });
            }

            return alerts.OrderByDescending(a => a.CreatedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active alerts for business {BusinessId}", businessId);
            return new List<AlertSummary>();
        }
    }

    public async Task<SalesAnalytics> GetSalesAnalyticsAsync(Guid businessId, DateRange dateRange)
    {
        try
        {
            var shops = await _shopRepository.FindAsync(s => s.BusinessId == businessId);
            var shopIds = shops.Select(s => s.Id).ToList();

            var sales = await _saleRepository.FindAsync(s => 
                shopIds.Contains(s.ShopId) && 
                s.CreatedAt >= dateRange.StartDate && 
                s.CreatedAt <= dateRange.EndDate);

            // Get previous period for comparison
            var periodLength = dateRange.EndDate - dateRange.StartDate;
            var previousPeriodStart = dateRange.StartDate - periodLength;
            var previousPeriodEnd = dateRange.StartDate;

            var previousSales = await _saleRepository.FindAsync(s => 
                shopIds.Contains(s.ShopId) && 
                s.CreatedAt >= previousPeriodStart && 
                s.CreatedAt < previousPeriodEnd);

            var totalRevenue = sales.Sum(s => s.TotalAmount);
            var totalTransactions = sales.Count();
            var averageOrderValue = totalTransactions > 0 ? totalRevenue / totalTransactions : 0;
            var itemsSold = sales.SelectMany(s => s.Items).Sum(i => (int)i.Quantity);

            var previousRevenue = previousSales.Sum(s => s.TotalAmount);
            var previousTransactions = previousSales.Count();
            var previousAOV = previousTransactions > 0 ? previousRevenue / previousTransactions : 0;
            var previousItemsSold = previousSales.SelectMany(s => s.Items).Sum(i => (int)i.Quantity);

            // Calculate growth percentages
            var revenueGrowth = previousRevenue > 0 ? ((totalRevenue - previousRevenue) / previousRevenue) * 100 : 0;
            var transactionGrowth = previousTransactions > 0 ? ((totalTransactions - previousTransactions) / (decimal)previousTransactions) * 100 : 0;
            var aovGrowth = previousAOV > 0 ? ((averageOrderValue - previousAOV) / previousAOV) * 100 : 0;
            var itemsGrowth = previousItemsSold > 0 ? ((itemsSold - previousItemsSold) / (decimal)previousItemsSold) * 100 : 0;

            return new SalesAnalytics
            {
                BusinessId = businessId,
                Period = dateRange,
                TotalRevenue = totalRevenue,
                TotalTransactions = totalTransactions,
                AverageOrderValue = averageOrderValue,
                ItemsSold = itemsSold,
                RevenueGrowth = revenueGrowth,
                TransactionGrowth = transactionGrowth,
                AOVGrowth = aovGrowth,
                ItemsGrowth = itemsGrowth,
                GeneratedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sales analytics for business {BusinessId}", businessId);
            throw;
        }
    }

    public async Task<InventoryAnalytics> GetInventoryAnalyticsAsync(Guid businessId)
    {
        try
        {
            var shops = await _shopRepository.FindAsync(s => s.BusinessId == businessId);
            var shopIds = shops.Select(s => s.Id).ToList();

            var allStock = await _stockRepository.FindAsync(s => shopIds.Contains(s.ShopId));
            var products = await _productRepository.FindAsync(p => allStock.Select(s => s.ProductId).Contains(p.Id));

            var lowStockThreshold = 10;
            var lowStockProducts = allStock.Count(s => s.Quantity <= lowStockThreshold && s.Quantity > 0);
            var outOfStockProducts = allStock.Count(s => s.Quantity == 0);
            var expiringProducts = products.Count(p => p.ExpiryDate.HasValue && p.ExpiryDate.Value <= DateTime.UtcNow.AddDays(30));
            var totalInventoryValue = allStock.Sum(s => s.Quantity * (products.FirstOrDefault(p => p.Id == s.ProductId)?.UnitPrice ?? 0));

            return new InventoryAnalytics
            {
                BusinessId = businessId,
                TotalProducts = products.Count(),
                TotalInventoryValue = totalInventoryValue,
                LowStockProducts = lowStockProducts,
                OutOfStockProducts = outOfStockProducts,
                ExpiringProducts = expiringProducts,
                GeneratedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inventory analytics for business {BusinessId}", businessId);
            throw;
        }
    }

    public async Task<FinancialReport> GetFinancialReportAsync(Guid businessId, DateRange dateRange)
    {
        try
        {
            var shops = await _shopRepository.FindAsync(s => s.BusinessId == businessId);
            var shopIds = shops.Select(s => s.Id).ToList();

            var sales = await _saleRepository.FindAsync(s => 
                shopIds.Contains(s.ShopId) && 
                s.CreatedAt >= dateRange.StartDate && 
                s.CreatedAt <= dateRange.EndDate);

            var totalRevenue = sales.Sum(s => s.TotalAmount);
            
            // Calculate costs based on purchase prices (simplified)
            var totalCosts = sales.SelectMany(s => s.Items)
                .Sum(i => i.Quantity * (i.Product?.PurchasePrice ?? 0));

            var grossProfit = totalRevenue - totalCosts;
            var netProfit = grossProfit; // Simplified - would subtract operating expenses
            var profitMargin = totalRevenue > 0 ? (grossProfit / totalRevenue) * 100 : 0;

            return new FinancialReport
            {
                BusinessId = businessId,
                Period = dateRange,
                TotalRevenue = totalRevenue,
                TotalCosts = totalCosts,
                GrossProfit = grossProfit,
                NetProfit = netProfit,
                ProfitMargin = profitMargin,
                GeneratedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting financial report for business {BusinessId}", businessId);
            throw;
        }
    }
}