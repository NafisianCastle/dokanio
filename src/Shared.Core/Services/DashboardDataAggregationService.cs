using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using System.Linq;

namespace Shared.Core.Services;

/// <summary>
/// Service implementation for dashboard data aggregation operations
/// </summary>
public class DashboardDataAggregationService : IDashboardDataAggregationService
{
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockRepository _stockRepository;
    private readonly ISaleItemRepository _saleItemRepository;
    private readonly IShopRepository _shopRepository;
    private readonly ILogger<DashboardDataAggregationService> _logger;

    public DashboardDataAggregationService(
        ISaleRepository saleRepository,
        IProductRepository productRepository,
        IStockRepository stockRepository,
        ISaleItemRepository saleItemRepository,
        IShopRepository shopRepository,
        ILogger<DashboardDataAggregationService> logger)
    {
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _stockRepository = stockRepository;
        _saleItemRepository = saleItemRepository;
        _shopRepository = shopRepository;
        _logger = logger;
    }

    #region Sales Data Aggregation

    public async Task<SalesAggregationResult> AggregateSalesDataAsync(IEnumerable<Guid> shopIds, DateRange period)
    {
        _logger.LogInformation("Aggregating sales data for {ShopCount} shops from {StartDate} to {EndDate}", 
            shopIds.Count(), period.StartDate, period.EndDate);

        var result = new SalesAggregationResult();
        var allSales = new List<Sale>();
        var revenueByShop = new Dictionary<Guid, decimal>();
        var revenueByCategory = new Dictionary<string, decimal>();

        foreach (var shopId in shopIds)
        {
            var shopSales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, period.StartDate, period.EndDate);
            allSales.AddRange(shopSales);
            
            var shopRevenue = shopSales.Sum(s => (decimal)s.TotalAmount);
            revenueByShop[shopId] = shopRevenue;

            // Aggregate by category
            foreach (var sale in shopSales)
            {
                foreach (var item in sale.Items)
                {
                    var category = item.Product.Category ?? "Uncategorized";
                    if (revenueByCategory.ContainsKey(category))
                        revenueByCategory[category] += item.TotalPrice;
                    else
                        revenueByCategory[category] = item.TotalPrice;
                }
            }
        }

        result.TotalRevenue = allSales.Sum(s => (decimal)s.TotalAmount);
        result.TotalTransactions = allSales.Count;
        result.AverageOrderValue = result.TotalTransactions > 0 ? result.TotalRevenue / result.TotalTransactions : 0;
        result.RevenueByShop = revenueByShop;
        result.RevenueByCategory = revenueByCategory;
        result.DailySales = await GenerateDailySalesDataAsync(shopIds, period);

        return result;
    }
    public async Task<IEnumerable<HourlySalesDistribution>> CalculateHourlySalesDistributionAsync(IEnumerable<Guid> shopIds, DateTime date)
    {
        var distributions = new List<HourlySalesDistribution>();
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1).AddTicks(-1);

        var allSales = new List<Sale>();
        foreach (var shopId in shopIds)
        {
            var shopSales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, dayStart, dayEnd);
            allSales.AddRange(shopSales);
        }

        var totalDayRevenue = allSales.Sum(s => (decimal)s.TotalAmount);

        for (int hour = 0; hour < 24; hour++)
        {
            var hourStart = dayStart.AddHours(hour);
            var hourEnd = hourStart.AddHours(1).AddTicks(-1);

            var hourSales = allSales.Where(s => s.CreatedAt >= hourStart && s.CreatedAt <= hourEnd).ToList();
            var hourRevenue = hourSales.Sum(s => (decimal)s.TotalAmount);
            var hourTransactions = hourSales.Count;

            var shopContributions = new Dictionary<Guid, decimal>();
            foreach (var shopId in shopIds)
            {
                var shopHourRevenue = hourSales.Where(s => s.ShopId == shopId).Sum(s => (decimal)s.TotalAmount);
                shopContributions[shopId] = shopHourRevenue;
            }

            distributions.Add(new HourlySalesDistribution
            {
                Hour = hour,
                Revenue = hourRevenue,
                TransactionCount = hourTransactions,
                PercentageOfDayRevenue = totalDayRevenue > 0 ? (hourRevenue / totalDayRevenue) * 100 : 0,
                ShopContributions = shopContributions
            });
        }

        return distributions;
    }

    public async Task<ProductPerformanceAggregation> AggregateProductPerformanceAsync(IEnumerable<Guid> shopIds, DateRange period, int topCount = 50)
    {
        var productPerformance = new Dictionary<Guid, ProductPerformanceData>();
        var categoryPerformance = new Dictionary<string, CategoryPerformanceData>();

        foreach (var shopId in shopIds)
        {
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, period.StartDate, period.EndDate);
            
            foreach (var sale in sales)
            {
                foreach (var item in sale.Items)
                {
                    var productId = item.ProductId;
                    var category = item.Product.Category ?? "Uncategorized";

                    // Aggregate product performance
                    if (productPerformance.ContainsKey(productId))
                    {
                        var existing = productPerformance[productId];
                        existing.TotalQuantitySold += item.Quantity;
                        existing.TotalRevenue += item.TotalPrice;
                        
                        if (existing.QuantityByShop.ContainsKey(shopId))
                            existing.QuantityByShop[shopId] += item.Quantity;
                        else
                            existing.QuantityByShop[shopId] = item.Quantity;
                    }
                    else
                    {
                        productPerformance[productId] = new ProductPerformanceData
                        {
                            ProductId = productId,
                            ProductName = item.Product.Name,
                            Category = category,
                            TotalQuantitySold = item.Quantity,
                            TotalRevenue = item.TotalPrice,
                            AveragePrice = item.UnitPrice,
                            QuantityByShop = new Dictionary<Guid, int> { { shopId, item.Quantity } }
                        };
                    }

                    // Aggregate category performance
                    if (categoryPerformance.ContainsKey(category))
                    {
                        var existing = categoryPerformance[category];
                        existing.TotalRevenue += item.TotalPrice;
                        existing.TotalQuantitySold += item.Quantity;
                    }
                    else
                    {
                        categoryPerformance[category] = new CategoryPerformanceData
                        {
                            CategoryName = category,
                            TotalRevenue = item.TotalPrice,
                            TotalQuantitySold = item.Quantity,
                            ProductCount = 1,
                            AveragePrice = item.UnitPrice
                        };
                    }
                }
            }
        }

        // Calculate performance scores and finalize data
        var totalRevenue = productPerformance.Values.Sum(p => p.TotalRevenue);
        foreach (var product in productPerformance.Values)
        {
            product.PerformanceScore = totalRevenue > 0 ? (double)(product.TotalRevenue / totalRevenue) * 100 : 0;
        }

        foreach (var category in categoryPerformance.Values)
        {
            category.MarketSharePercentage = totalRevenue > 0 ? (double)(category.TotalRevenue / totalRevenue) * 100 : 0;
            category.AveragePrice = category.TotalQuantitySold > 0 ? category.TotalRevenue / category.TotalQuantitySold : 0;
        }

        return new ProductPerformanceAggregation
        {
            TopPerformers = productPerformance.Values
                .OrderByDescending(p => p.TotalRevenue)
                .Take(topCount)
                .ToList(),
            LowPerformers = productPerformance.Values
                .OrderBy(p => p.TotalRevenue)
                .Take(topCount)
                .ToList(),
            CategoryPerformance = categoryPerformance
        };
    }

    #endregion

    #region Inventory Data Aggregation

    public async Task<InventoryAggregationResult> AggregateInventoryDataAsync(IEnumerable<Guid> shopIds)
    {
        var result = new InventoryAggregationResult();
        var inventoryByShop = new Dictionary<Guid, ShopInventoryData>();
        var inventoryByCategory = new Dictionary<string, CategoryInventoryData>();

        foreach (var shopId in shopIds)
        {
            var products = await _productRepository.GetProductsByShopAsync(shopId);
            var stocks = await _stockRepository.GetStockByShopAsync(shopId);
            var shop = await _shopRepository.GetByIdAsync(shopId);

            var shopInventoryValue = stocks.Sum(s => s.Quantity * (s.Product?.UnitPrice ?? 0));
            var lowStockCount = stocks.Count(s => s.Quantity <= 10);
            var outOfStockCount = stocks.Count(s => s.Quantity <= 0);
            var expiringCount = products.Count(p => p.ExpiryDate.HasValue && p.ExpiryDate.Value <= DateTime.UtcNow.AddDays(30));

            inventoryByShop[shopId] = new ShopInventoryData
            {
                ShopId = shopId,
                ShopName = shop?.Name ?? "Unknown",
                ProductCount = products.Count(),
                InventoryValue = shopInventoryValue,
                LowStockCount = lowStockCount,
                OutOfStockCount = outOfStockCount,
                InventoryTurnoverRate = 12 // Estimated - would need historical data for accurate calculation
            };

            // Aggregate by category
            foreach (var product in products)
            {
                var category = product.Category ?? "Uncategorized";
                var stock = stocks.FirstOrDefault(s => s.ProductId == product.Id);
                var stockValue = (stock?.Quantity ?? 0) * product.UnitPrice;

                if (inventoryByCategory.ContainsKey(category))
                {
                    var existing = inventoryByCategory[category];
                    existing.ProductCount++;
                    existing.TotalValue += stockValue;
                    if (stock?.Quantity <= 10) existing.LowStockCount++;
                }
                else
                {
                    inventoryByCategory[category] = new CategoryInventoryData
                    {
                        CategoryName = category,
                        ProductCount = 1,
                        TotalValue = stockValue,
                        LowStockCount = stock?.Quantity <= 10 ? 1 : 0,
                        AverageTurnoverRate = 12 // Estimated
                    };
                }
            }

            // Update totals
            result.TotalProducts += ((IEnumerable<Product>)products).Count();
            result.TotalInventoryValue += shopInventoryValue;
            result.LowStockCount += lowStockCount;
            result.OutOfStockCount += outOfStockCount;
            result.ExpiringProductsCount += expiringCount;
        }

        result.InventoryByShop = inventoryByShop;
        result.InventoryByCategory = inventoryByCategory;

        return result;
    }
    public async Task<IEnumerable<InventoryTurnoverData>> CalculateInventoryTurnoverAsync(IEnumerable<Guid> shopIds, DateRange period)
    {
        var turnoverData = new List<InventoryTurnoverData>();

        foreach (var shopId in shopIds)
        {
            var shop = await _shopRepository.GetByIdAsync(shopId);
            var products = await _productRepository.GetProductsByShopAsync(shopId);
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, period.StartDate, period.EndDate);

            var turnoverByCategory = new Dictionary<string, double>();
            var slowMovingProducts = new List<ProductTurnoverData>();
            var fastMovingProducts = new List<ProductTurnoverData>();

            foreach (var product in products)
            {
                var category = product.Category ?? "Uncategorized";
                var productSales = sales.SelectMany(s => s.Items).Where(i => i.ProductId == product.Id);
                var totalSold = productSales.Sum(i => i.Quantity);
                var currentStock = await _stockRepository.GetStockQuantityAsync(product.Id);

                // Calculate turnover rate (simplified)
                var averageStock = Math.Max(currentStock, 1);
                var turnoverRate = totalSold / (double)averageStock;
                var daysOfSupply = totalSold > 0 ? (int)(averageStock / (totalSold / period.Duration.TotalDays)) : int.MaxValue;

                var productTurnover = new ProductTurnoverData
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Category = category,
                    TurnoverRate = turnoverRate,
                    DaysOfSupply = daysOfSupply,
                    CurrentStock = currentStock
                };

                if (turnoverRate < 0.5) // Slow moving
                    slowMovingProducts.Add(productTurnover);
                else if (turnoverRate > 2.0) // Fast moving
                    fastMovingProducts.Add(productTurnover);

                // Aggregate by category
                if (turnoverByCategory.ContainsKey(category))
                    turnoverByCategory[category] = (turnoverByCategory[category] + turnoverRate) / 2;
                else
                    turnoverByCategory[category] = turnoverRate;
            }

            var overallTurnoverRate = turnoverByCategory.Values.Any() ? turnoverByCategory.Values.Average() : 0;

            turnoverData.Add(new InventoryTurnoverData
            {
                ShopId = shopId,
                ShopName = shop?.Name ?? "Unknown",
                TurnoverByCategory = turnoverByCategory,
                OverallTurnoverRate = overallTurnoverRate,
                SlowMovingProducts = slowMovingProducts.OrderBy(p => p.TurnoverRate).Take(10).ToList(),
                FastMovingProducts = fastMovingProducts.OrderByDescending(p => p.TurnoverRate).Take(10).ToList()
            });
        }

        return turnoverData;
    }

    public async Task<StockMovementAggregation> AggregateStockMovementAsync(IEnumerable<Guid> shopIds, DateRange period)
    {
        var result = new StockMovementAggregation();
        var movementsByShop = new Dictionary<Guid, List<StockMovementData>>();
        var movementsByCategory = new Dictionary<string, StockMovementSummary>();
        var movementTrends = new List<StockMovementTrend>();

        foreach (var shopId in shopIds)
        {
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, period.StartDate, period.EndDate);
            var shopMovements = new List<StockMovementData>();

            foreach (var sale in sales)
            {
                foreach (var item in sale.Items)
                {
                    var category = item.Product.Category ?? "Uncategorized";
                    
                    shopMovements.Add(new StockMovementData
                    {
                        Date = sale.CreatedAt.Date,
                        ProductId = item.ProductId,
                        ProductName = item.Product.Name,
                        QuantityIn = 0, // Sales are outgoing
                        QuantityOut = item.Quantity,
                        NetMovement = -item.Quantity,
                        MovementType = "Sale"
                    });

                    // Aggregate by category
                    if (movementsByCategory.ContainsKey(category))
                    {
                        var existing = movementsByCategory[category];
                        existing.TotalQuantityOut += item.Quantity;
                        existing.NetMovement -= item.Quantity;
                    }
                    else
                    {
                        movementsByCategory[category] = new StockMovementSummary
                        {
                            CategoryName = category,
                            TotalQuantityIn = 0,
                            TotalQuantityOut = item.Quantity,
                            NetMovement = -item.Quantity,
                            MovementVelocity = item.Quantity / period.Duration.TotalDays
                        };
                    }
                }
            }

            movementsByShop[shopId] = shopMovements;
        }

        // Generate daily movement trends
        for (var date = period.StartDate.Date; date <= period.EndDate.Date; date = date.AddDays(1))
        {
            var dayMovements = movementsByShop.Values.SelectMany(m => m).Where(m => m.Date == date);
            var totalOut = dayMovements.Sum(m => m.QuantityOut);
            var totalIn = dayMovements.Sum(m => m.QuantityIn);
            var netMovement = totalIn - totalOut;

            movementTrends.Add(new StockMovementTrend
            {
                Date = date,
                TotalQuantityIn = totalIn,
                TotalQuantityOut = totalOut,
                NetMovement = netMovement,
                Direction = netMovement > 0 ? TrendDirection.Increasing : 
                           netMovement < 0 ? TrendDirection.Decreasing : TrendDirection.Stable
            });
        }

        result.MovementsByShop = movementsByShop;
        result.MovementsByCategory = movementsByCategory;
        result.MovementTrends = movementTrends;

        return result;
    }

    #endregion

    #region Financial Data Aggregation

    public async Task<FinancialAggregationResult> AggregateFinancialDataAsync(IEnumerable<Guid> shopIds, DateRange period)
    {
        var result = new FinancialAggregationResult();
        var financialsByShop = new Dictionary<Guid, ShopFinancialData>();
        var financialsByCategory = new Dictionary<string, CategoryFinancialData>();
        var dailyFinancials = new List<DailyFinancialData>();

        foreach (var shopId in shopIds)
        {
            var shop = await _shopRepository.GetByIdAsync(shopId);
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, period.StartDate, period.EndDate);
            
            var revenue = sales.Sum(s => s.TotalAmount);
            var estimatedCosts = revenue * 0.7m; // 70% cost assumption
            var grossProfit = revenue - estimatedCosts;
            var profitMargin = revenue > 0 ? (grossProfit / revenue) * 100 : 0;

            financialsByShop[shopId] = new ShopFinancialData
            {
                ShopId = shopId,
                ShopName = shop?.Name ?? "Unknown",
                Revenue = revenue,
                Costs = estimatedCosts,
                GrossProfit = grossProfit,
                ProfitMarginPercentage = profitMargin,
                ROI = estimatedCosts > 0 ? (grossProfit / estimatedCosts) * 100 : 0
            };

            // Aggregate by category
            foreach (var sale in sales)
            {
                foreach (var item in sale.Items)
                {
                    var category = item.Product.Category ?? "Uncategorized";
                    var itemRevenue = item.TotalPrice;
                    var itemCost = itemRevenue * 0.7m;
                    var itemProfit = itemRevenue - itemCost;

                    if (financialsByCategory.ContainsKey(category))
                    {
                        var existing = financialsByCategory[category];
                        existing.Revenue += itemRevenue;
                        existing.Costs += itemCost;
                        existing.GrossProfit += itemProfit;
                    }
                    else
                    {
                        financialsByCategory[category] = new CategoryFinancialData
                        {
                            CategoryName = category,
                            Revenue = itemRevenue,
                            Costs = itemCost,
                            GrossProfit = itemProfit
                        };
                    }
                }
            }

            result.TotalRevenue += revenue;
            result.TotalCosts += estimatedCosts;
            result.GrossProfit += grossProfit;
        }

        // Calculate profit margins for categories
        foreach (var category in financialsByCategory.Values)
        {
            category.ProfitMarginPercentage = category.Revenue > 0 ? (category.GrossProfit / category.Revenue) * 100 : 0;
            category.MarketSharePercentage = result.TotalRevenue > 0 ? (double)(category.Revenue / result.TotalRevenue) * 100 : 0;
        }

        result.NetProfit = result.GrossProfit; // Simplified - would subtract operational costs
        result.ProfitMarginPercentage = result.TotalRevenue > 0 ? (result.GrossProfit / result.TotalRevenue) * 100 : 0;
        result.FinancialsByShop = financialsByShop;
        result.FinancialsByCategory = financialsByCategory;
        result.DailyFinancials = await GenerateDailyFinancialDataAsync(shopIds, period);

        return result;
    }
    public async Task<ProfitMarginAnalysis> CalculateProfitMarginsAsync(IEnumerable<Guid> shopIds, DateRange period)
    {
        var marginsByCategory = new Dictionary<string, decimal>();
        var marginsByShop = new Dictionary<Guid, decimal>();
        var highMarginProducts = new List<ProductMarginData>();
        var lowMarginProducts = new List<ProductMarginData>();
        var categoryMarginData = new Dictionary<string, (decimal sum, int count)>();

        var totalRevenue = 0m;
        var totalCosts = 0m;

        foreach (var shopId in shopIds)
        {
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, period.StartDate, period.EndDate);
            var shopRevenue = sales.Sum(s => (decimal)s.TotalAmount);
            var shopCosts = shopRevenue * 0.7m; // Estimated
            var shopMargin = shopRevenue > 0 ? ((shopRevenue - shopCosts) / shopRevenue) * 100 : 0;

            marginsByShop[shopId] = shopMargin;
            totalRevenue += shopRevenue;
            totalCosts += shopCosts;

            // Analyze product margins
            var productMargins = new Dictionary<Guid, ProductMarginData>();
            foreach (var sale in sales)
            {
                foreach (var item in sale.Items)
                {
                    var category = item.Product.Category ?? "Uncategorized";
                    var sellingPrice = item.UnitPrice;
                    var estimatedCost = sellingPrice * 0.7m;
                    var profitMargin = sellingPrice - estimatedCost;
                    var profitMarginPercentage = sellingPrice > 0 ? (profitMargin / sellingPrice) * 100 : 0;

                    if (productMargins.ContainsKey(item.ProductId))
                    {
                        productMargins[item.ProductId].UnitsSold += item.Quantity;
                    }
                    else
                    {
                        productMargins[item.ProductId] = new ProductMarginData
                        {
                            ProductId = item.ProductId,
                            ProductName = item.Product.Name,
                            Category = category,
                            SellingPrice = sellingPrice,
                            Cost = estimatedCost,
                            ProfitMargin = profitMargin,
                            ProfitMarginPercentage = profitMarginPercentage,
                            UnitsSold = item.Quantity
                        };
                    }

                    // Update category margin data
                    if (categoryMarginData.ContainsKey(category))
                    {
                        var (sum, count) = categoryMarginData[category];
                        categoryMarginData[category] = (sum + profitMarginPercentage, count + 1);
                    }
                    else
                    {
                        categoryMarginData[category] = (profitMarginPercentage, 1);
                    }
                }
            }

            // Identify high and low margin products
            highMarginProducts.AddRange(productMargins.Values.Where(p => p.ProfitMarginPercentage > 40));
            lowMarginProducts.AddRange(productMargins.Values.Where(p => p.ProfitMarginPercentage < 10));
        }

        // Populate final marginsByCategory from categoryMarginData
        marginsByCategory = categoryMarginData.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.count > 0 ? kvp.Value.sum / kvp.Value.count : 0
        );

        var overallProfitMargin = totalRevenue > 0 ? ((totalRevenue - totalCosts) / totalRevenue) * 100 : 0;

        return new ProfitMarginAnalysis
        {
            OverallProfitMargin = overallProfitMargin,
            MarginsByCategory = marginsByCategory,
            MarginsByShop = marginsByShop,
            HighMarginProducts = highMarginProducts.OrderByDescending(p => p.ProfitMarginPercentage).Take(20).ToList(),
            LowMarginProducts = lowMarginProducts.OrderBy(p => p.ProfitMarginPercentage).Take(20).ToList(),
            MarginImprovementSuggestions = GenerateMarginImprovementSuggestions(marginsByCategory, lowMarginProducts)
        };
    }

    public async Task<PaymentMethodDistribution> AggregatePaymentMethodDataAsync(IEnumerable<Guid> shopIds, DateRange period)
    {
        var paymentMethods = new Dictionary<string, PaymentMethodData>();
        var paymentMethodsByShop = new Dictionary<Guid, Dictionary<string, decimal>>();
        var paymentTrends = new List<PaymentTrendData>();

        foreach (var shopId in shopIds)
        {
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, period.StartDate, period.EndDate);
            var shopPaymentMethods = new Dictionary<string, decimal>();

            foreach (var sale in sales)
            {
                var paymentMethod = sale.PaymentMethod.ToString();
                var amount = sale.TotalAmount;

                // Aggregate overall
                if (paymentMethods.ContainsKey(paymentMethod))
                {
                    paymentMethods[paymentMethod].TotalAmount += amount;
                    paymentMethods[paymentMethod].TransactionCount++;
                }
                else
                {
                    paymentMethods[paymentMethod] = new PaymentMethodData
                    {
                        PaymentMethod = paymentMethod,
                        TotalAmount = amount,
                        TransactionCount = 1,
                        AverageTransactionValue = amount
                    };
                }

                // Aggregate by shop
                if (shopPaymentMethods.ContainsKey(paymentMethod))
                    shopPaymentMethods[paymentMethod] += amount;
                else
                    shopPaymentMethods[paymentMethod] = amount;
            }

            paymentMethodsByShop[shopId] = shopPaymentMethods;
        }

        // Calculate percentages and averages
        var totalAmount = paymentMethods.Values.Sum(p => p.TotalAmount);
        foreach (var method in paymentMethods.Values)
        {
            method.AverageTransactionValue = method.TransactionCount > 0 ? method.TotalAmount / method.TransactionCount : 0;
            method.PercentageOfTotal = totalAmount > 0 ? (double)(method.TotalAmount / totalAmount) * 100 : 0;
        }

        // Generate daily trends
        for (var date = period.StartDate.Date; date <= period.EndDate.Date; date = date.AddDays(1))
        {
            var dayStart = date;
            var dayEnd = date.AddDays(1).AddTicks(-1);
            var amountsByMethod = new Dictionary<string, decimal>();
            var countsByMethod = new Dictionary<string, int>();

            foreach (var shopId in shopIds)
            {
                var daySales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, dayStart, dayEnd);
                
                foreach (var sale in daySales)
                {
                    var method = sale.PaymentMethod.ToString();
                    
                    if (amountsByMethod.ContainsKey(method))
                    {
                        amountsByMethod[method] += sale.TotalAmount;
                        countsByMethod[method]++;
                    }
                    else
                    {
                        amountsByMethod[method] = sale.TotalAmount;
                        countsByMethod[method] = 1;
                    }
                }
            }

            paymentTrends.Add(new PaymentTrendData
            {
                Date = date,
                AmountsByPaymentMethod = amountsByMethod,
                CountsByPaymentMethod = countsByMethod
            });
        }

        return new PaymentMethodDistribution
        {
            PaymentMethods = paymentMethods,
            PaymentMethodsByShop = paymentMethodsByShop,
            PaymentTrends = paymentTrends
        };
    }

    #endregion

    #region Performance Metrics Aggregation

    public async Task<KPIAggregationResult> CalculateKPIsAsync(IEnumerable<Guid> shopIds, DateRange period)
    {
        var kpiValues = new Dictionary<string, decimal>();
        var kpisByShop = new Dictionary<Guid, Dictionary<string, decimal>>();
        var kpiTrends = new List<KPITrendData>();
        var kpiBenchmarks = new Dictionary<string, KPIBenchmark>();

        var totalRevenue = 0m;
        var totalTransactions = 0;
        var totalProducts = 0;

        foreach (var shopId in shopIds)
        {
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, period.StartDate, period.EndDate);
            var products = await _productRepository.GetProductsByShopAsync(shopId);
            var stocks = await _stockRepository.GetStockByShopAsync(shopId);

            var shopRevenue = sales.Sum(s => s.TotalAmount);
            var shopTransactions = sales.Count();
            var shopProducts = ((IEnumerable<Product>)products).Count();
            var inventoryValue = stocks.Sum(s => (decimal)(s.Quantity * (s.Product?.UnitPrice ?? 0)));

            var shopKPIs = new Dictionary<string, decimal>
            {
                ["Revenue"] = shopRevenue,
                ["Transactions"] = (decimal)shopTransactions,
                ["AverageOrderValue"] = shopTransactions > 0 ? shopRevenue / (decimal)shopTransactions : 0,
                ["ProductCount"] = (decimal)shopProducts,
                ["InventoryValue"] = inventoryValue,
                ["RevenuePerProduct"] = shopProducts > 0 ? shopRevenue / (decimal)shopProducts : 0,
                ["TransactionsPerDay"] = (decimal)(shopTransactions / Math.Max(period.Duration.TotalDays, 1))
            };

            kpisByShop[shopId] = shopKPIs;

            totalRevenue += shopRevenue;
            totalTransactions += shopTransactions;
            totalProducts += shopProducts;
        }

        // Calculate overall KPIs
        kpiValues["TotalRevenue"] = totalRevenue;
        kpiValues["TotalTransactions"] = totalTransactions;
        kpiValues["AverageOrderValue"] = totalTransactions > 0 ? totalRevenue / totalTransactions : 0;
        kpiValues["TotalProducts"] = totalProducts;
        kpiValues["RevenuePerShop"] = shopIds.Count() > 0 ? totalRevenue / shopIds.Count() : 0;
        kpiValues["TransactionsPerShop"] = shopIds.Count() > 0 ? totalTransactions / shopIds.Count() : 0;

        // Generate benchmarks (simplified - would use industry standards)
        foreach (var kpi in kpiValues)
        {
            var benchmarkValue = kpi.Value * 1.1m; // 10% above current as benchmark
            kpiBenchmarks[kpi.Key] = new KPIBenchmark
            {
                KPIName = kpi.Key,
                CurrentValue = kpi.Value,
                BenchmarkValue = benchmarkValue,
                VariancePercentage = benchmarkValue > 0 ? ((kpi.Value - benchmarkValue) / benchmarkValue) * 100 : 0,
                Rating = kpi.Value >= benchmarkValue ? PerformanceRating.Good : PerformanceRating.Average
            };
        }

        return new KPIAggregationResult
        {
            KPIValues = kpiValues,
            KPIsByShop = kpisByShop,
            KPITrends = kpiTrends,
            KPIBenchmarks = kpiBenchmarks
        };
    }
    public async Task<CustomerBehaviorAggregation> AggregateCustomerBehaviorAsync(IEnumerable<Guid> shopIds, DateRange period)
    {
        var transactionsByHour = new Dictionary<int, int>();
        var revenueByDayOfWeek = new Dictionary<DayOfWeek, decimal>();
        var customerSegments = new List<CustomerSegmentData>();

        var allSales = new List<Sale>();
        foreach (var shopId in shopIds)
        {
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, period.StartDate, period.EndDate);
            allSales.AddRange(sales);
        }

        var totalRevenue = allSales.Sum(s => s.TotalAmount);
        var totalTransactions = allSales.Count;
        var averageOrderValue = totalTransactions > 0 ? totalRevenue / totalTransactions : 0;

        // Analyze by hour
        for (int hour = 0; hour < 24; hour++)
        {
            var hourTransactions = allSales.Count(s => s.CreatedAt.Hour == hour);
            transactionsByHour[hour] = hourTransactions;
        }

        // Analyze by day of week
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            var dayRevenue = allSales.Where(s => s.CreatedAt.DayOfWeek == day).Sum(s => s.TotalAmount);
            revenueByDayOfWeek[day] = dayRevenue;
        }

        // Simple customer segmentation (would be more sophisticated with actual customer data)
        var highValueTransactions = allSales.Where(s => s.TotalAmount > averageOrderValue * 2).ToList();
        var mediumValueTransactions = allSales.Where(s => s.TotalAmount >= averageOrderValue && s.TotalAmount <= averageOrderValue * 2).ToList();
        var lowValueTransactions = allSales.Where(s => s.TotalAmount < averageOrderValue).ToList();

        customerSegments.Add(new CustomerSegmentData
        {
            SegmentName = "High Value",
            CustomerCount = highValueTransactions.Count,
            AverageOrderValue = highValueTransactions.Any() ? highValueTransactions.Average(s => s.TotalAmount) : 0,
            TotalRevenue = highValueTransactions.Sum(s => s.TotalAmount),
            RevenuePercentage = totalRevenue > 0 ? (double)(highValueTransactions.Sum(s => s.TotalAmount) / totalRevenue) * 100 : 0
        });

        customerSegments.Add(new CustomerSegmentData
        {
            SegmentName = "Medium Value",
            CustomerCount = mediumValueTransactions.Count,
            AverageOrderValue = mediumValueTransactions.Any() ? mediumValueTransactions.Average(s => s.TotalAmount) : 0,
            TotalRevenue = mediumValueTransactions.Sum(s => s.TotalAmount),
            RevenuePercentage = totalRevenue > 0 ? (double)(mediumValueTransactions.Sum(s => s.TotalAmount) / totalRevenue) * 100 : 0
        });

        customerSegments.Add(new CustomerSegmentData
        {
            SegmentName = "Low Value",
            CustomerCount = lowValueTransactions.Count,
            AverageOrderValue = lowValueTransactions.Any() ? lowValueTransactions.Average(s => s.TotalAmount) : 0,
            TotalRevenue = lowValueTransactions.Sum(s => s.TotalAmount),
            RevenuePercentage = totalRevenue > 0 ? (double)(lowValueTransactions.Sum(s => s.TotalAmount) / totalRevenue) * 100 : 0
        });

        return new CustomerBehaviorAggregation
        {
            AverageOrderValue = averageOrderValue,
            AverageTransactionFrequency = totalTransactions / Math.Max(period.Duration.TotalDays, 1),
            TransactionsByHour = transactionsByHour,
            RevenueByDayOfWeek = revenueByDayOfWeek,
            CustomerSegments = customerSegments
        };
    }

    public async Task<OperationalEfficiencyData> CalculateOperationalEfficiencyAsync(IEnumerable<Guid> shopIds, DateRange period)
    {
        var efficiencyByShop = new Dictionary<Guid, ShopEfficiencyMetrics>();
        var recommendations = new List<EfficiencyRecommendation>();
        var totalEfficiencyScore = 0.0;

        foreach (var shopId in shopIds)
        {
            var shop = await _shopRepository.GetByIdAsync(shopId);
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, period.StartDate, period.EndDate);
            var products = await _productRepository.GetProductsByShopAsync(shopId);

            var revenue = sales.Sum(s => s.TotalAmount);
            var transactions = sales.Count();
            var operatingHours = period.Duration.TotalHours; // Simplified
            var productCount = ((IEnumerable<Product>)products).Count();

            var salesPerHour = operatingHours > 0 ? revenue / (decimal)operatingHours : 0;
            var transactionsPerHour = operatingHours > 0 ? (decimal)transactions / (decimal)operatingHours : 0;
            var inventoryTurnover = 12; // Estimated annual turnover
            var staffProductivity = transactions > 0 ? revenue / (decimal)transactions : 0; // Simplified metric

            // Calculate efficiency score (0-100)
            var efficiencyScore = Math.Min(100, 
                (double)(salesPerHour / 100) * 20 + // 20% weight
                (double)transactionsPerHour * 10 + // 30% weight  
                inventoryTurnover * 5 + // 25% weight
                (double)(staffProductivity / 50) * 25); // 25% weight

            var metrics = new ShopEfficiencyMetrics
            {
                ShopId = shopId,
                ShopName = shop?.Name ?? "Unknown",
                SalesPerHour = (double)salesPerHour,
                TransactionsPerHour = (double)transactionsPerHour,
                InventoryTurnover = inventoryTurnover,
                StaffProductivity = (double)staffProductivity,
                OverallEfficiencyScore = efficiencyScore
            };

            efficiencyByShop[shopId] = metrics;
            totalEfficiencyScore += efficiencyScore;

            // Generate recommendations
            if (efficiencyScore < 60)
            {
                recommendations.Add(new EfficiencyRecommendation
                {
                    ShopId = shopId,
                    ShopName = shop?.Name ?? "Unknown",
                    RecommendationType = "Performance Improvement",
                    Description = "Shop performance is below average. Consider staff training and process optimization.",
                    EstimatedImpact = revenue * 0.15m,
                    Priority = "High"
                });
            }

            if (transactionsPerHour < 2)
            {
                recommendations.Add(new EfficiencyRecommendation
                {
                    ShopId = shopId,
                    ShopName = shop?.Name ?? "Unknown",
                    RecommendationType = "Transaction Speed",
                    Description = "Transaction processing speed is low. Consider POS system optimization.",
                    EstimatedImpact = revenue * 0.10m,
                    Priority = "Medium"
                });
            }
        }

        var overallEfficiencyScore = shopIds.Count() > 0 ? totalEfficiencyScore / shopIds.Count() : 0;

        return new OperationalEfficiencyData
        {
            EfficiencyByShop = efficiencyByShop,
            OverallEfficiencyScore = overallEfficiencyScore,
            Recommendations = recommendations.OrderBy(r => r.Priority).ToList()
        };
    }

    #endregion

    #region Trend Analysis

    public async Task<SalesTrendAnalysis> AnalyzeSalesTrendsAsync(IEnumerable<Guid> shopIds, DateRange period, TrendGranularity granularity)
    {
        var trendPoints = new List<SalesTrendPoint>();
        var insights = new List<TrendInsight>();

        var allSales = new List<Sale>();
        foreach (var shopId in shopIds)
        {
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, period.StartDate, period.EndDate);
            allSales.AddRange(sales);
        }

        // Group sales by granularity
        var groupedSales = granularity switch
        {
            TrendGranularity.Daily => allSales.GroupBy(s => s.CreatedAt.Date),
            TrendGranularity.Weekly => allSales.GroupBy(s => GetWeekStart(s.CreatedAt)),
            TrendGranularity.Monthly => allSales.GroupBy(s => new DateTime(s.CreatedAt.Year, s.CreatedAt.Month, 1)),
            _ => allSales.GroupBy(s => s.CreatedAt.Date)
        };

        SalesTrendPoint? previousPoint = null;
        foreach (var group in groupedSales.OrderBy(g => g.Key))
        {
            var revenue = group.Sum(s => s.TotalAmount);
            var transactions = group.Count();
            var averageOrderValue = transactions > 0 ? revenue / transactions : 0;

            var changePercentage = 0.0;
            var direction = TrendDirection.Stable;

            if (previousPoint != null)
            {
                changePercentage = previousPoint.Revenue > 0 ? 
                    (double)((revenue - previousPoint.Revenue) / previousPoint.Revenue) * 100 : 0;
                
                direction = changePercentage > 5 ? TrendDirection.Increasing :
                           changePercentage < -5 ? TrendDirection.Decreasing : TrendDirection.Stable;
            }

            var trendPoint = new SalesTrendPoint
            {
                Period = group.Key,
                Revenue = revenue,
                TransactionCount = transactions,
                AverageOrderValue = averageOrderValue,
                Direction = direction,
                ChangePercentage = changePercentage
            };

            trendPoints.Add(trendPoint);
            previousPoint = trendPoint;
        }

        // Analyze overall trend
        var overallTrend = TrendDirection.Stable;
        var trendStrength = 0.0;

        if (trendPoints.Count > 1)
        {
            var firstRevenue = trendPoints.First().Revenue;
            var lastRevenue = trendPoints.Last().Revenue;
            
            if (firstRevenue > 0)
            {
                var overallChange = (double)((lastRevenue - firstRevenue) / firstRevenue) * 100;
                trendStrength = Math.Abs(overallChange);
                
                overallTrend = overallChange > 10 ? TrendDirection.Increasing :
                              overallChange < -10 ? TrendDirection.Decreasing : TrendDirection.Stable;
            }
        }

        // Generate insights
        if (overallTrend == TrendDirection.Increasing && trendStrength > 20)
        {
            insights.Add(new TrendInsight
            {
                TrendName = "Strong Growth",
                Description = $"Sales have shown strong growth of {trendStrength:F1}% over the analysis period",
                Direction = TrendDirection.Increasing,
                Strength = trendStrength,
                ImpactValue = trendPoints.Last().Revenue - trendPoints.First().Revenue,
                Factors = new List<string> { "Increased customer demand", "Effective marketing", "Product popularity" }
            });
        }

        return new SalesTrendAnalysis
        {
            Granularity = granularity,
            TrendPoints = trendPoints,
            OverallTrend = overallTrend,
            TrendStrength = trendStrength,
            Insights = insights
        };
    }
    public async Task<SeasonalPatternAnalysis> AnalyzeSeasonalPatternsAsync(IEnumerable<Guid> shopIds, int analysisYears = 2)
    {
        var monthlyPatterns = new Dictionary<int, SeasonalData>();
        var weeklyPatterns = new Dictionary<DayOfWeek, SeasonalData>();
        var insights = new List<SeasonalInsight>();

        var endDate = DateTime.Now;
        var startDate = endDate.AddYears(-analysisYears);

        var allSales = new List<Sale>();
        foreach (var shopId in shopIds)
        {
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, startDate, endDate);
            allSales.AddRange(sales);
        }

        // Analyze monthly patterns
        for (int month = 1; month <= 12; month++)
        {
            var monthSales = allSales.Where(s => s.CreatedAt.Month == month).ToList();
            var monthlyRevenue = monthSales.GroupBy(s => s.CreatedAt.Year).Select(g => g.Sum(s => s.TotalAmount)).ToList();
            var monthlyTransactions = monthSales.GroupBy(s => s.CreatedAt.Year).Select(g => g.Count()).ToList();

            if (monthlyRevenue.Any())
            {
                var avgRevenue = monthlyRevenue.Average();
                var avgTransactions = (int)monthlyTransactions.Average();
                var variance = monthlyRevenue.Any() ? CalculateVariance(monthlyRevenue.Select(r => (double)r)) : 0;

                monthlyPatterns[month] = new SeasonalData
                {
                    AverageRevenue = avgRevenue,
                    AverageTransactions = avgTransactions,
                    SeasonalityIndex = (double)(avgRevenue / (allSales.Sum(s => s.TotalAmount) / 12)), // Simplified seasonality index
                    VariancePercentage = variance
                };
            }
        }

        // Analyze weekly patterns
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            var daySales = allSales.Where(s => s.CreatedAt.DayOfWeek == day).ToList();
            var dailyRevenue = daySales.GroupBy(s => s.CreatedAt.Date).Select(g => g.Sum(s => s.TotalAmount)).ToList();
            var dailyTransactions = daySales.GroupBy(s => s.CreatedAt.Date).Select(g => g.Count()).ToList();

            if (dailyRevenue.Any())
            {
                var avgRevenue = dailyRevenue.Average();
                var avgTransactions = (int)dailyTransactions.Average();
                var variance = dailyRevenue.Any() ? CalculateVariance(dailyRevenue.Select(r => (double)r)) : 0;

                weeklyPatterns[day] = new SeasonalData
                {
                    AverageRevenue = avgRevenue,
                    AverageTransactions = avgTransactions,
                    SeasonalityIndex = (double)(avgRevenue / (allSales.Sum(s => s.TotalAmount) / 7)), // Simplified
                    VariancePercentage = variance
                };
            }
        }

        // Generate seasonal insights
        var peakMonth = monthlyPatterns.OrderByDescending(kvp => kvp.Value.AverageRevenue).FirstOrDefault();
        var lowMonth = monthlyPatterns.OrderBy(kvp => kvp.Value.AverageRevenue).FirstOrDefault();

        if (peakMonth.Value != null && lowMonth.Value != null)
        {
            insights.Add(new SeasonalInsight
            {
                Pattern = "Monthly Seasonality",
                Description = $"Peak sales occur in {GetMonthName(peakMonth.Key)} with lowest in {GetMonthName(lowMonth.Key)}",
                ImpactValue = peakMonth.Value.AverageRevenue - lowMonth.Value.AverageRevenue,
                Recommendations = new List<string>
                {
                    $"Increase inventory before {GetMonthName(peakMonth.Key)}",
                    $"Plan promotions during {GetMonthName(lowMonth.Key)} to boost sales"
                }
            });
        }

        var peakDay = weeklyPatterns.OrderByDescending(kvp => kvp.Value.AverageRevenue).FirstOrDefault();
        var lowDay = weeklyPatterns.OrderBy(kvp => kvp.Value.AverageRevenue).FirstOrDefault();

        if (peakDay.Value != null && lowDay.Value != null)
        {
            insights.Add(new SeasonalInsight
            {
                Pattern = "Weekly Seasonality",
                Description = $"Peak sales occur on {peakDay.Key} with lowest on {lowDay.Key}",
                ImpactValue = peakDay.Value.AverageRevenue - lowDay.Value.AverageRevenue,
                Recommendations = new List<string>
                {
                    $"Ensure adequate staffing on {peakDay.Key}",
                    $"Consider special promotions on {lowDay.Key}"
                }
            });
        }

        return new SeasonalPatternAnalysis
        {
            MonthlyPatterns = monthlyPatterns,
            WeeklyPatterns = weeklyPatterns,
            SeasonalInsights = insights
        };
    }

    public async Task<PeriodComparisonAnalysis> ComparePeriodPerformanceAsync(IEnumerable<Guid> shopIds, DateRange currentPeriod, DateRange comparisonPeriod)
    {
        var currentSales = new List<Sale>();
        var comparisonSales = new List<Sale>();
        var shopComparisons = new Dictionary<Guid, ShopComparisonMetrics>();

        foreach (var shopId in shopIds)
        {
            var currentShopSales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, currentPeriod.StartDate, currentPeriod.EndDate);
            var comparisonShopSales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, comparisonPeriod.StartDate, comparisonPeriod.EndDate);

            currentSales.AddRange(currentShopSales);
            comparisonSales.AddRange(comparisonShopSales);

            var currentRevenue = currentShopSales.Sum(s => s.TotalAmount);
            var comparisonRevenue = comparisonShopSales.Sum(s => s.TotalAmount);
            var currentTransactions = currentShopSales.Count();
            var comparisonTransactions = comparisonShopSales.Count();

            var revenueChange = currentRevenue - comparisonRevenue;
            var revenueChangePercentage = comparisonRevenue > 0 ? (revenueChange / comparisonRevenue) * 100 : 0;
            var transactionChange = currentTransactions - comparisonTransactions;
            var transactionChangePercentage = comparisonTransactions > 0 ? ((decimal)transactionChange / comparisonTransactions) * 100 : 0;

            var shop = await _shopRepository.GetByIdAsync(shopId);
            shopComparisons[shopId] = new ShopComparisonMetrics
            {
                ShopId = shopId,
                ShopName = shop?.Name ?? "Unknown",
                RevenueChange = revenueChange,
                RevenueChangePercentage = revenueChangePercentage,
                TransactionChange = transactionChange,
                TransactionChangePercentage = transactionChangePercentage
            };
        }

        var totalCurrentRevenue = currentSales.Sum(s => s.TotalAmount);
        var totalComparisonRevenue = comparisonSales.Sum(s => s.TotalAmount);
        var totalCurrentTransactions = currentSales.Count;
        var totalComparisonTransactions = comparisonSales.Count;

        var totalRevenueChange = totalCurrentRevenue - totalComparisonRevenue;
        var totalRevenueChangePercentage = totalComparisonRevenue > 0 ? (totalRevenueChange / totalComparisonRevenue) * 100 : 0;
        var totalTransactionChange = totalCurrentTransactions - totalComparisonTransactions;
        var totalTransactionChangePercentage = totalComparisonTransactions > 0 ? ((decimal)totalTransactionChange / totalComparisonTransactions) * 100 : 0;

        var currentAOV = totalCurrentTransactions > 0 ? totalCurrentRevenue / totalCurrentTransactions : 0;
        var comparisonAOV = totalComparisonTransactions > 0 ? totalComparisonRevenue / totalComparisonTransactions : 0;
        var aovChange = currentAOV - comparisonAOV;
        var aovChangePercentage = comparisonAOV > 0 ? (aovChange / comparisonAOV) * 100 : 0;

        var insights = new List<ComparisonInsight>();

        if (Math.Abs(totalRevenueChangePercentage) > 10)
        {
            insights.Add(new ComparisonInsight
            {
                Title = totalRevenueChangePercentage > 0 ? "Significant Revenue Growth" : "Revenue Decline",
                Description = $"Revenue has {(totalRevenueChangePercentage > 0 ? "increased" : "decreased")} by {Math.Abs(totalRevenueChangePercentage):F1}% compared to the previous period",
                Type = totalRevenueChangePercentage > 0 ? InsightType.Performance : InsightType.Risk,
                ImpactValue = Math.Abs(totalRevenueChange)
            });
        }

        return new PeriodComparisonAnalysis
        {
            CurrentPeriod = currentPeriod,
            ComparisonPeriod = comparisonPeriod,
            Metrics = new ComparisonMetrics
            {
                RevenueChange = totalRevenueChange,
                RevenueChangePercentage = totalRevenueChangePercentage,
                TransactionChange = totalTransactionChange,
                TransactionChangePercentage = totalTransactionChangePercentage,
                AOVChange = aovChange,
                AOVChangePercentage = aovChangePercentage,
                ShopComparisons = shopComparisons
            },
            Insights = insights
        };
    }

    #endregion

    #region Data Quality and Validation

    public async Task<DataQualityReport> ValidateDataQualityAsync(IEnumerable<Guid> shopIds, DateRange period)
    {
        var qualityMetrics = new Dictionary<string, DataQualityMetric>();
        var issues = new List<DataQualityIssue>();
        var recommendations = new List<string>();

        foreach (var shopId in shopIds)
        {
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, period.StartDate, period.EndDate);
            var products = await _productRepository.GetProductsByShopAsync(shopId);
            var stocks = await _stockRepository.GetStockByShopAsync(shopId);

            // Validate sales data
            var totalSales = sales.Count();
            var validSales = sales.Count(s => s.TotalAmount > 0 && !string.IsNullOrEmpty(s.InvoiceNumber));
            var invalidSales = totalSales - validSales;

            qualityMetrics["Sales"] = new DataQualityMetric
            {
                MetricName = "Sales Data",
                Score = totalSales > 0 ? (double)validSales / totalSales * 100 : 100,
                TotalRecords = totalSales,
                ValidRecords = validSales,
                InvalidRecords = invalidSales,
                CompletenessPercentage = totalSales > 0 ? (double)validSales / totalSales * 100 : 100
            };

            if (invalidSales > 0)
            {
                issues.Add(new DataQualityIssue
                {
                    IssueType = "Invalid Sales Data",
                    Description = $"{invalidSales} sales records have missing or invalid data",
                    AffectedRecords = invalidSales,
                    Severity = invalidSales > totalSales * 0.1 ? "High" : "Medium",
                    AffectedShopIds = new List<Guid> { shopId }
                });
            }

            // Validate product data
            var totalProducts = products.Count();
            var validProducts = products.Count(p => !string.IsNullOrEmpty(p.Name) && p.UnitPrice > 0);
            var invalidProducts = totalProducts - validProducts;

            qualityMetrics["Products"] = new DataQualityMetric
            {
                MetricName = "Product Data",
                Score = totalProducts > 0 ? (double)validProducts / totalProducts * 100 : 100,
                TotalRecords = totalProducts,
                ValidRecords = validProducts,
                InvalidRecords = invalidProducts,
                CompletenessPercentage = totalProducts > 0 ? (double)validProducts / totalProducts * 100 : 100
            };

            if (invalidProducts > 0)
            {
                issues.Add(new DataQualityIssue
                {
                    IssueType = "Invalid Product Data",
                    Description = $"{invalidProducts} products have missing names or invalid prices",
                    AffectedRecords = invalidProducts,
                    Severity = invalidProducts > totalProducts * 0.1 ? "High" : "Medium",
                    AffectedShopIds = new List<Guid> { shopId }
                });
            }
        }

        var overallScore = qualityMetrics.Values.Any() ? qualityMetrics.Values.Average(m => m.Score) : 100;

        if (overallScore < 90)
        {
            recommendations.Add("Review data entry processes to improve data quality");
        }
        if (issues.Any(i => i.Severity == "High"))
        {
            recommendations.Add("Address high-severity data quality issues immediately");
        }

        return new DataQualityReport
        {
            OverallQualityScore = overallScore,
            QualityMetrics = qualityMetrics,
            Issues = issues,
            Recommendations = recommendations
        };
    }

    public async Task<DataAnomalyReport> DetectDataAnomaliesAsync(IEnumerable<Guid> shopIds, DateRange period)
    {
        var anomalies = new List<DataAnomaly>();
        var anomaliesByShop = new Dictionary<Guid, int>();
        var anomalyPatterns = new List<string>();

        foreach (var shopId in shopIds)
        {
            var shop = await _shopRepository.GetByIdAsync(shopId);
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, period.StartDate, period.EndDate);
            
            var shopAnomalies = 0;

            // Detect revenue anomalies
            var dailyRevenues = sales.GroupBy(s => s.CreatedAt.Date)
                .Select(g => g.Sum(s => s.TotalAmount))
                .ToList();

            if (dailyRevenues.Any())
            {
                var avgRevenue = dailyRevenues.Average();
                var stdDev = CalculateStandardDeviation(dailyRevenues.Select(r => (double)r));

                foreach (var dailyRevenue in dailyRevenues)
                {
                    var deviation = Math.Abs((double)(dailyRevenue - avgRevenue));
                    if (deviation > stdDev * 2) // 2 standard deviations
                    {
                        anomalies.Add(new DataAnomaly
                        {
                            AnomalyType = "Revenue Anomaly",
                            Description = $"Daily revenue of {dailyRevenue:C} is significantly different from average {avgRevenue:C}",
                            DetectedAt = DateTime.UtcNow,
                            ShopId = shopId,
                            ShopName = shop?.Name ?? "Unknown",
                            AnomalyValue = dailyRevenue,
                            ExpectedValue = avgRevenue,
                            DeviationPercentage = avgRevenue > 0 ? (double)((dailyRevenue - avgRevenue) / avgRevenue) * 100 : 0,
                            Severity = deviation > stdDev * 3 ? "High" : "Medium"
                        });
                        shopAnomalies++;
                    }
                }
            }

            // Detect transaction count anomalies
            var dailyTransactions = sales.GroupBy(s => s.CreatedAt.Date)
                .Select(g => g.Count())
                .ToList();

            if (dailyTransactions.Any())
            {
                var avgTransactions = dailyTransactions.Average();
                var stdDev = CalculateStandardDeviation(dailyTransactions.Select(t => (double)t));

                foreach (var dailyTransaction in dailyTransactions)
                {
                    var deviation = Math.Abs(dailyTransaction - avgTransactions);
                    if (deviation > stdDev * 2)
                    {
                        anomalies.Add(new DataAnomaly
                        {
                            AnomalyType = "Transaction Count Anomaly",
                            Description = $"Daily transaction count of {dailyTransaction} is significantly different from average {avgTransactions:F0}",
                            DetectedAt = DateTime.UtcNow,
                            ShopId = shopId,
                            ShopName = shop?.Name ?? "Unknown",
                            AnomalyValue = dailyTransaction,
                            ExpectedValue = (decimal)avgTransactions,
                            DeviationPercentage = avgTransactions > 0 ? (dailyTransaction - avgTransactions) / avgTransactions * 100 : 0,
                            Severity = deviation > stdDev * 3 ? "High" : "Medium"
                        });
                        shopAnomalies++;
                    }
                }
            }

            anomaliesByShop[shopId] = shopAnomalies;
        }

        // Identify patterns
        if (anomalies.Count(a => a.AnomalyType == "Revenue Anomaly") > anomalies.Count * 0.3)
        {
            anomalyPatterns.Add("High frequency of revenue anomalies detected across shops");
        }

        if (anomalies.Any(a => a.Severity == "High"))
        {
            anomalyPatterns.Add("Critical anomalies detected requiring immediate attention");
        }

        return new DataAnomalyReport
        {
            Anomalies = anomalies.OrderByDescending(a => a.DeviationPercentage).ToList(),
            AnomaliesByShop = anomaliesByShop,
            AnomalyPatterns = anomalyPatterns
        };
    }

    #endregion

    #region Private Helper Methods

    private async Task<List<DailySalesData>> GenerateDailySalesDataAsync(IEnumerable<Guid> shopIds, DateRange period)
    {
        var dailySales = new List<DailySalesData>();

        for (var date = period.StartDate.Date; date <= period.EndDate.Date; date = date.AddDays(1))
        {
            var dayStart = date;
            var dayEnd = date.AddDays(1).AddTicks(-1);
            var shopRevenues = new Dictionary<Guid, decimal>();
            var totalRevenue = 0m;
            var totalTransactions = 0;

            foreach (var shopId in shopIds)
            {
                var daySales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, dayStart, dayEnd);
                var shopRevenue = daySales.Sum(s => s.TotalAmount);
                
                shopRevenues[shopId] = shopRevenue;
                totalRevenue += shopRevenue;
                totalTransactions += daySales.Count();
            }

            dailySales.Add(new DailySalesData
            {
                Date = date,
                Revenue = totalRevenue,
                TransactionCount = totalTransactions,
                AverageOrderValue = totalTransactions > 0 ? totalRevenue / (decimal)totalTransactions : 0,
                ShopRevenues = shopRevenues
            });
        }

        return dailySales;
    }

    private async Task<List<DailyFinancialData>> GenerateDailyFinancialDataAsync(IEnumerable<Guid> shopIds, DateRange period)
    {
        var dailyFinancials = new List<DailyFinancialData>();

        for (var date = period.StartDate.Date; date <= period.EndDate.Date; date = date.AddDays(1))
        {
            var dayStart = date;
            var dayEnd = date.AddDays(1).AddTicks(-1);
            var profitByShop = new Dictionary<Guid, decimal>();
            var totalRevenue = 0m;
            var totalCosts = 0m;

            foreach (var shopId in shopIds)
            {
                var daySales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, dayStart, dayEnd);
                var shopRevenue = daySales.Sum(s => s.TotalAmount);
                var shopCosts = shopRevenue * 0.7m; // Estimated
                var shopProfit = shopRevenue - shopCosts;
                
                profitByShop[shopId] = shopProfit;
                totalRevenue += shopRevenue;
                totalCosts += shopCosts;
            }

            var grossProfit = totalRevenue - totalCosts;
            var netProfit = grossProfit; // Simplified

            dailyFinancials.Add(new DailyFinancialData
            {
                Date = date,
                Revenue = totalRevenue,
                Costs = totalCosts,
                GrossProfit = grossProfit,
                NetProfit = netProfit,
                ProfitByShop = profitByShop
            });
        }

        return dailyFinancials;
    }

    private List<string> GenerateMarginImprovementSuggestions(Dictionary<string, decimal> marginsByCategory, List<ProductMarginData> lowMarginProducts)
    {
        var suggestions = new List<string>();

        var lowMarginCategories = marginsByCategory.Where(kvp => kvp.Value < 20).ToList();
        if (lowMarginCategories.Any())
        {
            suggestions.Add($"Focus on improving margins in categories: {string.Join(", ", lowMarginCategories.Select(c => c.Key))}");
        }

        if (lowMarginProducts.Count > 10)
        {
            suggestions.Add($"Review pricing for {lowMarginProducts.Count} low-margin products");
        }

        suggestions.Add("Consider negotiating better supplier terms to improve cost structure");
        suggestions.Add("Implement dynamic pricing strategies for high-demand products");

        return suggestions;
    }

    private DateTime GetWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-1 * diff).Date;
    }

    private double CalculateVariance(IEnumerable<double> values)
    {
        if (!values.Any()) return 0;
        
        var mean = values.Average();
        var sumOfSquares = values.Sum(x => Math.Pow(x - mean, 2));
        return sumOfSquares / values.Count();
    }

    private double CalculateStandardDeviation(IEnumerable<double> values)
    {
        return Math.Sqrt(CalculateVariance(values));
    }

    private string GetMonthName(int month)
    {
        return new DateTime(2000, month, 1).ToString("MMMM");
    }

    #endregion
}