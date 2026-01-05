using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using System.Globalization;
using System.Linq;

namespace Shared.Core.Services;

/// <summary>
/// Service implementation for business owner dashboard functionality
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly IBusinessRepository _businessRepository;
    private readonly IShopRepository _shopRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockRepository _stockRepository;
    private readonly ISaleItemRepository _saleItemRepository;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        IBusinessRepository businessRepository,
        IShopRepository shopRepository,
        ISaleRepository saleRepository,
        IProductRepository productRepository,
        IStockRepository stockRepository,
        ISaleItemRepository saleItemRepository,
        ILogger<DashboardService> logger)
    {
        _businessRepository = businessRepository;
        _shopRepository = shopRepository;
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _stockRepository = stockRepository;
        _saleItemRepository = saleItemRepository;
        _logger = logger;
    }

    #region Dashboard Overview

    public async Task<DashboardOverview> GetDashboardOverviewAsync(Guid businessId, DashboardFilter? filter = null)
    {
        _logger.LogInformation("Getting dashboard overview for business: {BusinessId}", businessId);

        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var shopIds = filter?.ShopIds ?? business.Shops.Select(s => s.Id).ToList();
        var dateRange = filter?.DateRange ?? new DateRange 
        { 
            StartDate = DateTime.Today, 
            EndDate = DateTime.Today.AddDays(1).AddTicks(-1) 
        };

        var overview = new DashboardOverview
        {
            BusinessId = businessId,
            BusinessName = business.Name,
            BusinessType = business.Type,
            TotalShops = business.Shops.Count(s => s.IsActive),
            RealTimeSales = await GetRealTimeSalesDataAsync(businessId, shopIds),
            InventoryStatus = await GetInventoryStatusSummaryAsync(businessId, shopIds),
            RevenueTrends = await GetRevenueTrendAnalysisAsync(businessId, dateRange, shopIds),
            ShopPerformances = (await GetShopPerformanceSummariesAsync(businessId, dateRange)).ToList(),
            Alerts = (await GetActiveAlertsAsync(businessId)).ToList()
        };

        _logger.LogInformation("Dashboard overview generated for business: {BusinessId}", businessId);
        return overview;
    }

    public async Task<IEnumerable<DashboardOverview>> GetMultiBusinessDashboardAsync(IEnumerable<Guid> businessIds, DashboardFilter? filter = null)
    {
        var overviews = new List<DashboardOverview>();

        foreach (var businessId in businessIds)
        {
            try
            {
                var overview = await GetDashboardOverviewAsync(businessId, filter);
                overviews.Add(overview);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard overview for business: {BusinessId}", businessId);
            }
        }

        return overviews;
    }

    #endregion

    #region Real-Time Sales Monitoring

    public async Task<RealTimeSalesData> GetRealTimeSalesDataAsync(Guid businessId, IEnumerable<Guid>? shopIds = null)
    {
        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var targetShopIds = shopIds?.ToList() ?? business.Shops.Select(s => s.Id).ToList();
        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);

        // Get today's sales
        var todaySales = new List<Sale>();
        var yesterdaySales = new List<Sale>();

        foreach (var shopId in targetShopIds)
        {
            var shopTodaySales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, today, today.AddDays(1).AddTicks(-1));
            var shopYesterdaySales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, yesterday, today.AddTicks(-1));
            
            todaySales.AddRange(shopTodaySales);
            yesterdaySales.AddRange(shopYesterdaySales);
        }

        var todayRevenue = todaySales.Sum(s => s.TotalAmount);
        var yesterdayRevenue = yesterdaySales.Sum(s => s.TotalAmount);
        var todayTransactionCount = todaySales.Count;
        var averageOrderValue = todayTransactionCount > 0 ? todayRevenue / todayTransactionCount : 0;

        var revenueChangePercentage = yesterdayRevenue > 0 
            ? ((todayRevenue - yesterdayRevenue) / yesterdayRevenue) * 100 
            : 0;

        var transactionChangePercentage = yesterdaySales.Count > 0 
            ? ((todayTransactionCount - yesterdaySales.Count) / (decimal)yesterdaySales.Count) * 100 
            : 0;

        return new RealTimeSalesData
        {
            TodayRevenue = todayRevenue,
            TodayTransactionCount = todayTransactionCount,
            AverageOrderValue = averageOrderValue,
            YesterdayRevenue = yesterdayRevenue,
            RevenueChangePercentage = revenueChangePercentage,
            TransactionChangePercentage = (int)transactionChangePercentage,
            HourlySales = (await GetTodayHourlySalesAsync(businessId, targetShopIds)).ToList(),
            TopProducts = (await GetTopSellingProductsAsync(businessId, new DateRange { StartDate = today, EndDate = today.AddDays(1).AddTicks(-1) }, 5, targetShopIds)).ToList()
        };
    }

    public async Task<IEnumerable<HourlySalesData>> GetTodayHourlySalesAsync(Guid businessId, IEnumerable<Guid>? shopIds = null)
    {
        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var targetShopIds = shopIds?.ToList() ?? business.Shops.Select(s => s.Id).ToList();
        var today = DateTime.Today;
        var hourlySales = new List<HourlySalesData>();

        for (int hour = 0; hour < 24; hour++)
        {
            var hourStart = today.AddHours(hour);
            var hourEnd = hourStart.AddHours(1).AddTicks(-1);

            var hourSales = new List<Sale>();
            foreach (var shopId in targetShopIds)
            {
                var shopHourSales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, hourStart, hourEnd);
                hourSales.AddRange(shopHourSales);
            }

            var revenue = hourSales.Sum(s => s.TotalAmount);
            var transactionCount = hourSales.Count;
            var averageOrderValue = transactionCount > 0 ? revenue / transactionCount : 0;

            hourlySales.Add(new HourlySalesData
            {
                Hour = hourStart,
                Revenue = revenue,
                TransactionCount = transactionCount,
                AverageOrderValue = averageOrderValue
            });
        }

        return hourlySales;
    }

    public async Task<IEnumerable<TopSellingProduct>> GetTopSellingProductsAsync(Guid businessId, DateRange period, int topCount = 10, IEnumerable<Guid>? shopIds = null)
    {
        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var targetShopIds = shopIds?.ToList() ?? business.Shops.Select(s => s.Id).ToList();
        var productSales = new Dictionary<Guid, (int quantity, decimal revenue, Product product, Shop shop)>();

        foreach (var shopId in targetShopIds)
        {
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, period.StartDate, period.EndDate);
            var shop = business.Shops.First(s => s.Id == shopId);

            foreach (var sale in sales)
            {
                foreach (var item in sale.Items)
                {
                    if (productSales.ContainsKey(item.ProductId))
                    {
                        var existing = productSales[item.ProductId];
                        productSales[item.ProductId] = (
                            existing.quantity + item.Quantity,
                            existing.revenue + item.TotalPrice,
                            existing.product,
                            existing.shop
                        );
                    }
                    else
                    {
                        productSales[item.ProductId] = (
                            item.Quantity,
                            item.TotalPrice,
                            item.Product,
                            shop
                        );
                    }
                }
            }
        }

        return productSales
            .OrderByDescending(kvp => kvp.Value.quantity)
            .Take(topCount)
            .Select(kvp => new TopSellingProduct
            {
                ProductId = kvp.Key,
                ProductName = kvp.Value.product.Name,
                Category = kvp.Value.product.Category ?? "Uncategorized",
                QuantitySold = kvp.Value.quantity,
                Revenue = kvp.Value.revenue,
                ShopId = kvp.Value.shop.Id,
                ShopName = kvp.Value.shop.Name
            });
    }

    #endregion

    #region Inventory Status Tracking

    public async Task<InventoryStatusSummary> GetInventoryStatusSummaryAsync(Guid businessId, IEnumerable<Guid>? shopIds = null)
    {
        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var targetShopIds = shopIds?.ToList() ?? business.Shops.Select(s => s.Id).ToList();
        var summary = new InventoryStatusSummary();

        var shopInventoryStatuses = new List<ShopInventoryStatus>();

        foreach (var shopId in targetShopIds)
        {
            var shop = business.Shops.First(s => s.Id == shopId);
            var products = await _productRepository.GetProductsByShopAsync(shopId);
            var stocks = await _stockRepository.GetStockByShopAsync(shopId);

            var lowStockProducts = stocks.Where(s => s.Quantity <= 10).ToList(); // Default threshold
            var outOfStockProducts = stocks.Where(s => s.Quantity <= 0).ToList();
            var expiringProducts = products.Where(p => p.ExpiryDate.HasValue && p.ExpiryDate.Value <= DateTime.UtcNow.AddDays(30)).ToList();

            var inventoryValue = stocks.Sum(s => s.Quantity * (s.Product?.UnitPrice ?? 0));

            var shopStatus = new ShopInventoryStatus
            {
                ShopId = shopId,
                ShopName = shop.Name,
                TotalProducts = products.Count(),
                LowStockCount = lowStockProducts.Count,
                OutOfStockCount = outOfStockProducts.Count,
                ExpiringCount = expiringProducts.Count(),
                InventoryValue = inventoryValue
            };

            shopInventoryStatuses.Add(shopStatus);

            // Aggregate to summary
            summary.TotalProducts += shopStatus.TotalProducts;
            summary.LowStockProducts += shopStatus.LowStockCount;
            summary.OutOfStockProducts += shopStatus.OutOfStockCount;
            summary.ExpiringProducts += shopStatus.ExpiringCount;
            summary.TotalInventoryValue += shopStatus.InventoryValue;
        }

        summary.ShopInventoryStatuses = shopInventoryStatuses;
        summary.LowStockAlerts = (await GetLowStockAlertsAsync(businessId, null, targetShopIds)).ToList();
        summary.ExpiryAlerts = (await GetExpiryAlertsAsync(businessId, 30, targetShopIds)).ToList();

        return summary;
    }

    public async Task<IEnumerable<LowStockAlert>> GetLowStockAlertsAsync(Guid businessId, int? threshold = null, IEnumerable<Guid>? shopIds = null)
    {
        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var targetShopIds = shopIds?.ToList() ?? business.Shops.Select(s => s.Id).ToList();
        var lowStockThreshold = threshold ?? 10;
        var alerts = new List<LowStockAlert>();

        foreach (var shopId in targetShopIds)
        {
            var shop = business.Shops.First(s => s.Id == shopId);
            var lowStocks = await _stockRepository.GetLowStockAsync(lowStockThreshold);
            var shopLowStocks = lowStocks.Where(s => s.Product.ShopId == shopId);

            foreach (var stock in shopLowStocks)
            {
                var priority = stock.Quantity <= 0 ? AlertPriority.Critical :
                              stock.Quantity <= 5 ? AlertPriority.High :
                              AlertPriority.Medium;

                alerts.Add(new LowStockAlert
                {
                    ProductId = stock.ProductId,
                    ProductName = stock.Product.Name,
                    Category = stock.Product.Category ?? "Uncategorized",
                    CurrentStock = stock.Quantity,
                    LowStockThreshold = lowStockThreshold,
                    ShopId = shopId,
                    ShopName = shop.Name,
                    Priority = priority
                });
            }
        }

        return alerts.OrderByDescending(a => a.Priority).ThenBy(a => a.CurrentStock);
    }

    public async Task<IEnumerable<ExpiryAlert>> GetExpiryAlertsAsync(Guid businessId, int daysAhead = 30, IEnumerable<Guid>? shopIds = null)
    {
        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        // Only applicable for pharmacy businesses
        if (business.Type != BusinessType.Pharmacy)
        {
            return new List<ExpiryAlert>();
        }

        var targetShopIds = shopIds?.ToList() ?? business.Shops.Select(s => s.Id).ToList();
        var expiryThreshold = DateTime.UtcNow.AddDays(daysAhead);
        var alerts = new List<ExpiryAlert>();

        foreach (var shopId in targetShopIds)
        {
            var shop = business.Shops.First(s => s.Id == shopId);
            var expiringProducts = await _productRepository.GetExpiringMedicinesAsync(expiryThreshold);
            var shopExpiringProducts = expiringProducts.Where(p => p.ShopId == shopId);

            foreach (var product in shopExpiringProducts)
            {
                if (!product.ExpiryDate.HasValue) continue;

                var daysUntilExpiry = (int)(product.ExpiryDate.Value - DateTime.UtcNow).TotalDays;
                var stock = await _stockRepository.GetByProductIdAsync(product.Id);
                var quantity = stock?.Quantity ?? 0;
                var value = quantity * product.UnitPrice;

                var priority = daysUntilExpiry <= 7 ? AlertPriority.Critical :
                              daysUntilExpiry <= 15 ? AlertPriority.High :
                              AlertPriority.Medium;

                alerts.Add(new ExpiryAlert
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    BatchNumber = product.BatchNumber ?? "N/A",
                    ExpiryDate = product.ExpiryDate.Value,
                    DaysUntilExpiry = daysUntilExpiry,
                    Quantity = quantity,
                    Value = value,
                    ShopId = shopId,
                    ShopName = shop.Name,
                    Priority = priority
                });
            }
        }

        return alerts.OrderBy(a => a.DaysUntilExpiry).ThenByDescending(a => a.Value);
    }

    public async Task<IEnumerable<ShopInventoryStatus>> GetShopInventoryStatusesAsync(Guid businessId)
    {
        var summary = await GetInventoryStatusSummaryAsync(businessId);
        return summary.ShopInventoryStatuses;
    }

    #endregion

    #region Revenue Trend Analysis

    public async Task<RevenueTrendData> GetRevenueTrendAnalysisAsync(Guid businessId, DateRange period, IEnumerable<Guid>? shopIds = null)
    {
        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var targetShopIds = shopIds?.ToList() ?? business.Shops.Select(s => s.Id).ToList();
        
        // Calculate previous period for comparison
        var periodDuration = period.EndDate - period.StartDate;
        var previousPeriod = new DateRange
        {
            StartDate = period.StartDate - periodDuration,
            EndDate = period.StartDate.AddTicks(-1)
        };

        var currentPeriodSales = new List<Sale>();
        var previousPeriodSales = new List<Sale>();

        foreach (var shopId in targetShopIds)
        {
            var currentSales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, period.StartDate, period.EndDate);
            var previousSales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, previousPeriod.StartDate, previousPeriod.EndDate);
            
            currentPeriodSales.AddRange(currentSales);
            previousPeriodSales.AddRange(previousSales);
        }

        var totalRevenue = currentPeriodSales.Sum(s => s.TotalAmount);
        var previousPeriodRevenue = previousPeriodSales.Sum(s => s.TotalAmount);
        var totalTransactions = currentPeriodSales.Count;
        var averageOrderValue = totalTransactions > 0 ? totalRevenue / totalTransactions : 0;

        var revenueGrowthPercentage = previousPeriodRevenue > 0 
            ? ((totalRevenue - previousPeriodRevenue) / previousPeriodRevenue) * 100 
            : 0;

        return new RevenueTrendData
        {
            Period = period,
            TotalRevenue = totalRevenue,
            PreviousPeriodRevenue = previousPeriodRevenue,
            RevenueGrowthPercentage = revenueGrowthPercentage,
            AverageOrderValue = averageOrderValue,
            TotalTransactions = totalTransactions,
            DailyRevenues = (await GetDailyRevenueDataAsync(businessId, period, targetShopIds)).ToList(),
            MonthlyRevenues = (await GetMonthlyRevenueDataAsync(businessId, 12, targetShopIds)).ToList(),
            ProfitAnalysis = await GetProfitAnalysisAsync(businessId, period, targetShopIds)
        };
    }

    public async Task<IEnumerable<DailyRevenueData>> GetDailyRevenueDataAsync(Guid businessId, DateRange period, IEnumerable<Guid>? shopIds = null)
    {
        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var targetShopIds = shopIds?.ToList() ?? business.Shops.Select(s => s.Id).ToList();
        var dailyData = new List<DailyRevenueData>();

        for (var date = period.StartDate.Date; date <= period.EndDate.Date; date = date.AddDays(1))
        {
            var dayStart = date;
            var dayEnd = date.AddDays(1).AddTicks(-1);

            var daySales = new List<Sale>();
            foreach (var shopId in targetShopIds)
            {
                var shopDaySales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, dayStart, dayEnd);
                daySales.AddRange(shopDaySales);
            }

            var revenue = daySales.Sum(s => s.TotalAmount);
            var transactionCount = daySales.Count;
            var averageOrderValue = transactionCount > 0 ? revenue / transactionCount : 0;
            
            // Estimate profit (assuming 30% margin for simplicity)
            var profitEstimate = revenue * 0.3m;

            dailyData.Add(new DailyRevenueData
            {
                Date = date,
                Revenue = revenue,
                TransactionCount = transactionCount,
                AverageOrderValue = averageOrderValue,
                ProfitEstimate = profitEstimate
            });
        }

        return dailyData;
    }

    public async Task<IEnumerable<MonthlyRevenueData>> GetMonthlyRevenueDataAsync(Guid businessId, int monthCount = 12, IEnumerable<Guid>? shopIds = null)
    {
        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var targetShopIds = shopIds?.ToList() ?? business.Shops.Select(s => s.Id).ToList();
        var monthlyData = new List<MonthlyRevenueData>();
        var currentDate = DateTime.Now;

        for (int i = monthCount - 1; i >= 0; i--)
        {
            var monthStart = new DateTime(currentDate.Year, currentDate.Month, 1).AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

            var monthSales = new List<Sale>();
            foreach (var shopId in targetShopIds)
            {
                var shopMonthSales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, monthStart, monthEnd);
                monthSales.AddRange(shopMonthSales);
            }

            var revenue = monthSales.Sum(s => s.TotalAmount);
            var transactionCount = monthSales.Count;
            var averageOrderValue = transactionCount > 0 ? revenue / transactionCount : 0;
            var profitEstimate = revenue * 0.3m; // Estimate

            // Calculate growth percentage compared to previous month
            var growthPercentage = 0m;
            if (monthlyData.Any())
            {
                var previousMonthRevenue = monthlyData.Last().Revenue;
                if (previousMonthRevenue > 0)
                {
                    growthPercentage = ((revenue - previousMonthRevenue) / previousMonthRevenue) * 100;
                }
            }

            monthlyData.Add(new MonthlyRevenueData
            {
                Year = monthStart.Year,
                Month = monthStart.Month,
                MonthName = monthStart.ToString("MMMM", CultureInfo.InvariantCulture),
                Revenue = revenue,
                TransactionCount = transactionCount,
                AverageOrderValue = averageOrderValue,
                ProfitEstimate = profitEstimate,
                GrowthPercentage = growthPercentage
            });
        }

        return monthlyData;
    }

    #endregion

    #region Profit Calculations

    public async Task<ProfitAnalysis> GetProfitAnalysisAsync(Guid businessId, DateRange period, IEnumerable<Guid>? shopIds = null)
    {
        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var targetShopIds = shopIds?.ToList() ?? business.Shops.Select(s => s.Id).ToList();
        
        var allSales = new List<Sale>();
        foreach (var shopId in targetShopIds)
        {
            var shopSales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, period.StartDate, period.EndDate);
            allSales.AddRange(shopSales);
        }

        var totalRevenue = allSales.Sum(s => s.TotalAmount);
        
        // Estimate costs (this would ideally come from purchase prices and operational costs)
        var estimatedCosts = totalRevenue * 0.7m; // Assuming 70% cost ratio
        var estimatedProfit = totalRevenue - estimatedCosts;
        var profitMarginPercentage = totalRevenue > 0 ? (estimatedProfit / totalRevenue) * 100 : 0;

        return new ProfitAnalysis
        {
            TotalRevenue = totalRevenue,
            EstimatedCosts = estimatedCosts,
            EstimatedProfit = estimatedProfit,
            ProfitMarginPercentage = profitMarginPercentage,
            CategoryProfits = (await GetCategoryProfitAnalysisAsync(businessId, period, targetShopIds)).ToList(),
            ShopProfits = (await GetShopProfitAnalysisAsync(businessId, period)).ToList()
        };
    }

    public async Task<IEnumerable<CategoryProfitData>> GetCategoryProfitAnalysisAsync(Guid businessId, DateRange period, IEnumerable<Guid>? shopIds = null)
    {
        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var targetShopIds = shopIds?.ToList() ?? business.Shops.Select(s => s.Id).ToList();
        var categoryData = new Dictionary<string, (decimal revenue, decimal cost)>();

        foreach (var shopId in targetShopIds)
        {
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, period.StartDate, period.EndDate);
            
            foreach (var sale in sales)
            {
                foreach (var item in sale.Items)
                {
                    var category = item.Product.Category ?? "Uncategorized";
                    var revenue = item.TotalPrice;
                    var estimatedCost = revenue * 0.7m; // Estimate

                    if (categoryData.ContainsKey(category))
                    {
                        var existing = categoryData[category];
                        categoryData[category] = (existing.revenue + revenue, existing.cost + estimatedCost);
                    }
                    else
                    {
                        categoryData[category] = (revenue, estimatedCost);
                    }
                }
            }
        }

        return categoryData.Select(kvp => new CategoryProfitData
        {
            CategoryName = kvp.Key,
            Revenue = kvp.Value.revenue,
            EstimatedCost = kvp.Value.cost,
            EstimatedProfit = kvp.Value.revenue - kvp.Value.cost,
            ProfitMarginPercentage = kvp.Value.revenue > 0 ? ((kvp.Value.revenue - kvp.Value.cost) / kvp.Value.revenue) * 100 : 0
        }).OrderByDescending(c => c.Revenue);
    }

    public async Task<IEnumerable<ShopProfitData>> GetShopProfitAnalysisAsync(Guid businessId, DateRange period)
    {
        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var shopProfits = new List<ShopProfitData>();

        foreach (var shop in business.Shops.Where(s => s.IsActive))
        {
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shop.Id, period.StartDate, period.EndDate);
            var revenue = sales.Sum(s => s.TotalAmount);
            var estimatedCost = revenue * 0.7m; // Estimate
            var estimatedProfit = revenue - estimatedCost;
            var profitMarginPercentage = revenue > 0 ? (estimatedProfit / revenue) * 100 : 0;

            shopProfits.Add(new ShopProfitData
            {
                ShopId = shop.Id,
                ShopName = shop.Name,
                Revenue = revenue,
                EstimatedCost = estimatedCost,
                EstimatedProfit = estimatedProfit,
                ProfitMarginPercentage = profitMarginPercentage
            });
        }

        return shopProfits.OrderByDescending(s => s.Revenue);
    }

    #endregion

    #region Shop Performance Comparison

    public async Task<IEnumerable<ShopPerformanceSummary>> GetShopPerformanceSummariesAsync(Guid businessId, DateRange? period = null)
    {
        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var analysisDate = period ?? new DateRange 
        { 
            StartDate = DateTime.Today, 
            EndDate = DateTime.Today.AddDays(1).AddTicks(-1) 
        };

        var summaries = new List<ShopPerformanceSummary>();

        foreach (var shop in business.Shops.Where(s => s.IsActive))
        {
            var todaySales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shop.Id, analysisDate.StartDate, analysisDate.EndDate);
            var yesterdaySales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shop.Id, analysisDate.StartDate.AddDays(-1), analysisDate.StartDate.AddTicks(-1));

            var todayRevenue = todaySales.Sum(s => (decimal)s.TotalAmount);
            var yesterdayRevenue = yesterdaySales.Sum(s => (decimal)s.TotalAmount);
            var todayTransactions = todaySales.Count;
            var averageOrderValue = todayTransactions > 0 ? todayRevenue / (decimal)todayTransactions : 0;

            var revenueGrowthPercentage = yesterdayRevenue > 0 
                ? ((todayRevenue - yesterdayRevenue) / yesterdayRevenue) * 100 
                : 0;

            var lowStockAlerts = await GetLowStockAlertsAsync(businessId, null, new[] { shop.Id });
            var expiryAlerts = await GetExpiryAlertsAsync(businessId, 30, new[] { shop.Id });

            var rating = CalculatePerformanceRating(todayRevenue, revenueGrowthPercentage, lowStockAlerts.Count(), expiryAlerts.Count());

            summaries.Add(new ShopPerformanceSummary
            {
                ShopId = shop.Id,
                ShopName = shop.Name,
                TodayRevenue = todayRevenue,
                TodayTransactions = todayTransactions,
                AverageOrderValue = averageOrderValue,
                RevenueGrowthPercentage = revenueGrowthPercentage,
                LowStockAlerts = lowStockAlerts.Count(),
                ExpiryAlerts = expiryAlerts.Count(),
                Rating = rating,
                KeyInsights = GenerateShopInsights(todayRevenue, revenueGrowthPercentage, todayTransactions, lowStockAlerts.Count(), expiryAlerts.Count())
            });
        }

        return summaries.OrderByDescending(s => s.TodayRevenue);
    }

    public async Task<MultiShopComparison> GetMultiShopComparisonAsync(Guid businessId, DateRange period)
    {
        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var comparisons = new List<ShopComparisonData>();

        foreach (var shop in business.Shops.Where(s => s.IsActive))
        {
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shop.Id, period.StartDate, period.EndDate);
            var products = await _productRepository.GetProductsByShopAsync(shop.Id);
            var stocks = await _stockRepository.GetStockByShopAsync(shop.Id);

            var revenue = sales.Sum(s => (decimal)s.TotalAmount);
            var transactionCount = sales.Count;
            var averageOrderValue = transactionCount > 0 ? revenue / (decimal)transactionCount : 0;
            var profitEstimate = revenue * 0.3m; // Estimate
            var inventoryValue = stocks.Sum(s => (decimal)(s.Quantity * (s.Product?.UnitPrice ?? 0)));

            comparisons.Add(new ShopComparisonData
            {
                ShopId = shop.Id,
                ShopName = shop.Name,
                Revenue = revenue,
                TransactionCount = transactionCount,
                AverageOrderValue = averageOrderValue,
                ProfitEstimate = profitEstimate,
                ProductCount = ((IEnumerable<Product>)products).Count(),
                InventoryValue = inventoryValue,
                Performance = new PerformanceMetrics
                {
                    ProfitMargin = revenue > 0 ? (profitEstimate / revenue) * 100 : 0,
                    OverallRating = CalculatePerformanceRating(revenue, 0, 0, 0) // Simplified
                }
            });
        }

        return new MultiShopComparison
        {
            BusinessId = businessId,
            ComparisonPeriod = period,
            ShopComparisons = comparisons,
            Rankings = await GetShopRankingsAsync(businessId, period),
            Insights = GenerateMultiShopInsights(comparisons)
        };
    }

    public async Task<ShopRankingData> GetShopRankingsAsync(Guid businessId, DateRange period)
    {
        var comparison = await GetMultiShopComparisonAsync(businessId, period);
        var shops = comparison.ShopComparisons.ToList();

        var totalRevenue = shops.Sum(s => s.Revenue);
        var totalTransactions = shops.Sum(s => s.TransactionCount);
        var totalProfit = shops.Sum(s => s.ProfitEstimate);

        return new ShopRankingData
        {
            RevenueRankings = shops
                .OrderByDescending(s => s.Revenue)
                .Select((s, index) => new ShopRanking
                {
                    Rank = index + 1,
                    ShopId = s.ShopId,
                    ShopName = s.ShopName,
                    Value = s.Revenue,
                    MetricName = "Revenue",
                    PercentageOfTotal = totalRevenue > 0 ? (s.Revenue / totalRevenue) * 100 : 0
                }).ToList(),

            TransactionRankings = shops
                .OrderByDescending(s => s.TransactionCount)
                .Select((s, index) => new ShopRanking
                {
                    Rank = index + 1,
                    ShopId = s.ShopId,
                    ShopName = s.ShopName,
                    Value = s.TransactionCount,
                    MetricName = "Transactions",
                    PercentageOfTotal = totalTransactions > 0 ? ((decimal)s.TransactionCount / totalTransactions) * 100 : 0
                }).ToList(),

            ProfitRankings = shops
                .OrderByDescending(s => s.ProfitEstimate)
                .Select((s, index) => new ShopRanking
                {
                    Rank = index + 1,
                    ShopId = s.ShopId,
                    ShopName = s.ShopName,
                    Value = s.ProfitEstimate,
                    MetricName = "Profit",
                    PercentageOfTotal = totalProfit > 0 ? (s.ProfitEstimate / totalProfit) * 100 : 0
                }).ToList(),

            EfficiencyRankings = shops
                .OrderByDescending(s => s.AverageOrderValue)
                .Select((s, index) => new ShopRanking
                {
                    Rank = index + 1,
                    ShopId = s.ShopId,
                    ShopName = s.ShopName,
                    Value = s.AverageOrderValue,
                    MetricName = "Average Order Value",
                    PercentageOfTotal = 0 // Not applicable for AOV
                }).ToList()
        };
    }

    #endregion

    #region Alerts and Notifications

    public async Task<IEnumerable<AlertSummary>> GetActiveAlertsAsync(Guid businessId, AlertPriority? priority = null, AlertType? alertType = null)
    {
        var alerts = new List<AlertSummary>();

        // Get low stock alerts
        var lowStockAlerts = await GetLowStockAlertsAsync(businessId);
        alerts.AddRange(lowStockAlerts.Select(a => new AlertSummary
        {
            Type = AlertType.LowStock,
            Priority = a.Priority,
            Title = "Low Stock Alert",
            Message = $"{a.ProductName} is running low in {a.ShopName} (Current: {a.CurrentStock})",
            ShopId = a.ShopId,
            ShopName = a.ShopName,
            Metadata = new Dictionary<string, object>
            {
                ["ProductId"] = a.ProductId,
                ["CurrentStock"] = a.CurrentStock,
                ["Threshold"] = a.LowStockThreshold
            }
        }));

        // Get expiry alerts
        var expiryAlerts = await GetExpiryAlertsAsync(businessId);
        alerts.AddRange(expiryAlerts.Select(a => new AlertSummary
        {
            Type = AlertType.ProductExpiry,
            Priority = a.Priority,
            Title = "Product Expiry Alert",
            Message = $"{a.ProductName} expires in {a.DaysUntilExpiry} days in {a.ShopName}",
            ShopId = a.ShopId,
            ShopName = a.ShopName,
            Metadata = new Dictionary<string, object>
            {
                ["ProductId"] = a.ProductId,
                ["ExpiryDate"] = a.ExpiryDate,
                ["DaysUntilExpiry"] = a.DaysUntilExpiry,
                ["BatchNumber"] = a.BatchNumber
            }
        }));

        // Apply filters
        if (priority.HasValue)
        {
            alerts = alerts.Where(a => a.Priority == priority.Value).ToList();
        }

        if (alertType.HasValue)
        {
            alerts = alerts.Where(a => a.Type == alertType.Value).ToList();
        }

        return alerts.OrderByDescending(a => a.Priority).ThenBy(a => a.CreatedAt);
    }

    public async Task<AlertSummary> CreateAlertAsync(Guid businessId, AlertSummary alert)
    {
        // In a real implementation, this would save to a database
        alert.CreatedAt = DateTime.UtcNow;
        _logger.LogInformation("Alert created for business {BusinessId}: {AlertTitle}", businessId, alert.Title);
        return alert;
    }

    public async Task<bool> MarkAlertAsReadAsync(Guid businessId, Guid alertId)
    {
        // In a real implementation, this would update the database
        _logger.LogInformation("Alert {AlertId} marked as read for business {BusinessId}", alertId, businessId);
        return await Task.FromResult(true);
    }

    public async Task<Dictionary<AlertPriority, int>> GetAlertCountsByPriorityAsync(Guid businessId)
    {
        var alerts = await GetActiveAlertsAsync(businessId);
        
        return alerts
            .GroupBy(a => a.Priority)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    #endregion

    #region Business Intelligence

    public async Task<BusinessIntelligenceInsights> GetBusinessIntelligenceInsightsAsync(Guid businessId, DateRange period)
    {
        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var revenueTrends = await GetRevenueTrendAnalysisAsync(businessId, period);
        var shopPerformances = await GetShopPerformanceSummariesAsync(businessId, period);
        var inventoryStatus = await GetInventoryStatusSummaryAsync(businessId);

        var insights = new List<BusinessInsight>();
        var recommendations = new List<RecommendationInsight>();
        var trends = new List<TrendInsight>();
        var opportunities = new List<OpportunityInsight>();

        // Generate insights based on data
        if (revenueTrends.RevenueGrowthPercentage > 10)
        {
            insights.Add(new BusinessInsight
            {
                Title = "Strong Revenue Growth",
                Description = $"Revenue has grown by {revenueTrends.RevenueGrowthPercentage:F1}% compared to the previous period",
                Type = InsightType.Performance,
                Priority = InsightPriority.High,
                ImpactValue = revenueTrends.TotalRevenue
            });
        }

        if (inventoryStatus.LowStockProducts > 0)
        {
            recommendations.Add(new RecommendationInsight
            {
                Title = "Inventory Replenishment Needed",
                Description = $"{inventoryStatus.LowStockProducts} products are running low across your shops",
                ActionRequired = "Review and reorder low stock items",
                EstimatedImpact = inventoryStatus.LowStockProducts * 100, // Estimated lost sales
                Category = RecommendationCategory.Inventory
            });
        }

        return new BusinessIntelligenceInsights
        {
            BusinessId = businessId,
            AnalysisPeriod = period,
            KeyInsights = insights,
            Recommendations = recommendations,
            Trends = trends,
            Opportunities = opportunities
        };
    }

    public async Task<Dictionary<Guid, PerformanceMetrics>> GetShopPerformanceMetricsAsync(Guid businessId, DateRange period)
    {
        var shopPerformances = await GetShopPerformanceSummariesAsync(businessId, period);
        
        return shopPerformances.ToDictionary(
            s => s.ShopId,
            s => new PerformanceMetrics
            {
                ProfitMargin = 30, // Estimated
                OverallRating = s.Rating,
                TransactionsPerHour = s.TodayTransactions / 24m, // Simplified
                InventoryTurnoverRate = 12 // Estimated annual turnover
            });
    }

    #endregion

    #region Data Refresh and Caching

    public async Task<bool> RefreshDashboardDataAsync(Guid businessId)
    {
        _logger.LogInformation("Refreshing dashboard data for business: {BusinessId}", businessId);
        
        // In a real implementation, this would clear caches and trigger data refresh
        await Task.Delay(100); // Simulate refresh operation
        
        _logger.LogInformation("Dashboard data refreshed for business: {BusinessId}", businessId);
        return true;
    }

    public async Task<DateTime?> GetLastDataRefreshAsync(Guid businessId)
    {
        // In a real implementation, this would return the actual last refresh timestamp
        return await Task.FromResult(DateTime.UtcNow.AddMinutes(-5));
    }

    #endregion

    #region Private Helper Methods

    private PerformanceRating CalculatePerformanceRating(decimal revenue, decimal growthPercentage, int lowStockAlerts, int expiryAlerts)
    {
        var score = 0;

        // Revenue score
        if (revenue > 1000) score += 2;
        else if (revenue > 500) score += 1;

        // Growth score
        if (growthPercentage > 10) score += 2;
        else if (growthPercentage > 0) score += 1;
        else if (growthPercentage < -10) score -= 1;

        // Alert penalties
        if (lowStockAlerts > 5) score -= 1;
        if (expiryAlerts > 3) score -= 1;

        return score switch
        {
            >= 4 => PerformanceRating.Excellent,
            >= 2 => PerformanceRating.Good,
            >= 0 => PerformanceRating.Average,
            >= -1 => PerformanceRating.BelowAverage,
            _ => PerformanceRating.Poor
        };
    }

    private List<string> GenerateShopInsights(decimal revenue, decimal growthPercentage, int transactions, int lowStockAlerts, int expiryAlerts)
    {
        var insights = new List<string>();

        if (growthPercentage > 10)
            insights.Add($"Strong growth of {growthPercentage:F1}% compared to yesterday");
        else if (growthPercentage < -10)
            insights.Add($"Revenue declined by {Math.Abs(growthPercentage):F1}% - needs attention");

        if (transactions > 50)
            insights.Add("High transaction volume indicates good customer traffic");
        else if (transactions < 10)
            insights.Add("Low transaction count - consider marketing initiatives");

        if (lowStockAlerts > 0)
            insights.Add($"{lowStockAlerts} products need restocking");

        if (expiryAlerts > 0)
            insights.Add($"{expiryAlerts} products approaching expiry");

        return insights;
    }

    private List<string> GenerateMultiShopInsights(List<ShopComparisonData> shops)
    {
        var insights = new List<string>();

        if (shops.Any())
        {
            var topShop = shops.OrderByDescending(s => s.Revenue).First();
            var bottomShop = shops.OrderBy(s => s.Revenue).First();

            insights.Add($"{topShop.ShopName} is the top performer with {topShop.Revenue:C} revenue");
            
            if (shops.Count > 1)
            {
                insights.Add($"{bottomShop.ShopName} has the lowest revenue at {bottomShop.Revenue:C}");
                
                var revenueGap = topShop.Revenue - bottomShop.Revenue;
                if (revenueGap > topShop.Revenue * 0.5m)
                {
                    insights.Add("Significant performance gap between shops - consider best practice sharing");
                }
            }

            var avgAOV = shops.Average(s => s.AverageOrderValue);
            var highAOVShops = shops.Where(s => s.AverageOrderValue > avgAOV * 1.2m).ToList();
            
            if (highAOVShops.Any())
            {
                insights.Add($"{string.Join(", ", highAOVShops.Select(s => s.ShopName))} have above-average order values");
            }
        }

        return insights;
    }

    #endregion
}