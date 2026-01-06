using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// AI-powered analytics and recommendation engine implementation
/// </summary>
public class AIAnalyticsEngine : IAIAnalyticsEngine
{
    private readonly IBusinessRepository _businessRepository;
    private readonly IShopRepository _shopRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockRepository _stockRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ILogger<AIAnalyticsEngine> _logger;

    public AIAnalyticsEngine(
        IBusinessRepository businessRepository,
        IShopRepository shopRepository,
        ISaleRepository saleRepository,
        IProductRepository productRepository,
        IStockRepository stockRepository,
        ICustomerRepository customerRepository,
        ILogger<AIAnalyticsEngine> logger)
    {
        _businessRepository = businessRepository;
        _shopRepository = shopRepository;
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _stockRepository = stockRepository;
        _customerRepository = customerRepository;
        _logger = logger;
    }

    public async Task<SalesInsights> AnalyzeSalesTrendsAsync(Guid businessId, DateRange period)
    {
        _logger.LogInformation("Analyzing sales trends for business {BusinessId} from {StartDate} to {EndDate}",
            businessId, period.StartDate, period.EndDate);

        if (!period.IsValid)
        {
            throw new ArgumentException("Invalid date range provided");
        }

        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var insights = new SalesInsights
        {
            BusinessId = businessId,
            AnalysisPeriod = period
        };

        // Get all sales for the business within the period
        var allSales = new List<Sale>();
        foreach (var shop in business.Shops.Where(s => s.IsActive))
        {
            var shopSales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shop.Id, period.StartDate, period.EndDate);
            allSales.AddRange(shopSales);
        }

        if (!allSales.Any())
        {
            _logger.LogWarning("No sales data found for business {BusinessId} in the specified period", businessId);
            insights.Recommendations.Add("No sales data available for analysis. Consider extending the date range.");
            return insights;
        }

        // Calculate basic metrics
        insights.TotalRevenue = allSales.Sum(s => s.TotalAmount);
        insights.TotalTransactions = allSales.Count;
        insights.AverageOrderValue = insights.TotalTransactions > 0 ? insights.TotalRevenue / insights.TotalTransactions : 0;

        // Analyze trends
        insights.Trends = await AnalyzeSalesTrendsInternal(allSales, period);

        // Identify top and low-performing products
        var productPerformance = await AnalyzeProductPerformanceInternal(allSales);
        insights.TopProducts = productPerformance.BestSellers.Take(10).ToList();
        insights.LowPerformingProducts = productPerformance.LowPerformers.Take(10).ToList();

        // Analyze peak times
        insights.PeakSalesTimes = await AnalyzePeakTimesInternal(allSales);

        // Generate recommendations
        insights.Recommendations = await GenerateSalesRecommendations(insights, business.Type);

        _logger.LogInformation("Sales trend analysis completed for business {BusinessId}. Revenue: {Revenue}, Transactions: {Transactions}",
            businessId, insights.TotalRevenue, insights.TotalTransactions);

        return insights;
    }

    public async Task<InventoryRecommendations> GenerateInventoryRecommendationsAsync(Guid shopId)
    {
        _logger.LogInformation("Generating inventory recommendations for shop {ShopId}", shopId);

        var shop = await _shopRepository.GetShopWithBusinessAsync(shopId);
        if (shop == null)
        {
            throw new ArgumentException($"Shop with ID {shopId} not found");
        }

        var recommendations = new InventoryRecommendations
        {
            ShopId = shopId,
            BusinessType = shop.Business.Type
        };

        // Get current inventory
        var inventory = await _stockRepository.GetStockByShopAsync(shopId);
        var products = await _productRepository.GetProductsByShopAsync(shopId);

        // Get recent sales data for analysis (last 90 days)
        var analysisStartDate = DateTime.UtcNow.AddDays(-90);
        var recentSales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, analysisStartDate, DateTime.UtcNow);

        // Generate reorder recommendations
        recommendations.ReorderSuggestions = await GenerateReorderRecommendations(inventory, recentSales, products);

        // Identify overstock situations
        recommendations.OverstockAlerts = await GenerateOverstockAlerts(inventory, recentSales, products);

        // Generate expiry risk alerts (for pharmacy businesses)
        if (shop.Business.Type == BusinessType.Pharmacy)
        {
            var expiryAlerts = await GetExpiryRiskAlertsAsync(shopId);
            recommendations.ExpiryRisks = expiryAlerts.ToList();
        }

        // Generate seasonal recommendations
        recommendations.SeasonalRecommendations = await GenerateSeasonalRecommendations(shopId, shop.Business.Type);

        // Generate general recommendations
        recommendations.GeneralRecommendations = await GenerateGeneralInventoryRecommendations(recommendations);

        _logger.LogInformation("Inventory recommendations generated for shop {ShopId}. Reorder suggestions: {ReorderCount}, Overstock alerts: {OverstockCount}",
            shopId, recommendations.ReorderSuggestions.Count, recommendations.OverstockAlerts.Count);

        return recommendations;
    }

    public async Task<ProductRecommendations> GetProductRecommendationsAsync(Guid shopId, Guid? customerId = null)
    {
        _logger.LogInformation("Generating product recommendations for shop {ShopId}, customer {CustomerId}", shopId, customerId);

        var shop = await _shopRepository.GetShopWithBusinessAsync(shopId);
        if (shop == null)
        {
            throw new ArgumentException($"Shop with ID {shopId} not found");
        }

        var recommendations = new ProductRecommendations
        {
            ShopId = shopId,
            CustomerId = customerId
        };

        var products = await _productRepository.GetProductsByShopAsync(shopId);
        var recentSales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        // Generate cross-sell recommendations
        recommendations.CrossSellRecommendations = await GenerateCrossSellRecommendations(products, recentSales, customerId);

        // Generate up-sell recommendations
        recommendations.UpSellRecommendations = await GenerateUpSellRecommendations(products, recentSales, customerId);

        // Generate bundle recommendations
        recommendations.BundleRecommendations = await GenerateBundleRecommendations(products, recentSales);

        _logger.LogInformation("Product recommendations generated for shop {ShopId}. Cross-sell: {CrossSellCount}, Up-sell: {UpSellCount}, Bundles: {BundleCount}",
            shopId, recommendations.CrossSellRecommendations.Count, recommendations.UpSellRecommendations.Count, recommendations.BundleRecommendations.Count);

        return recommendations;
    }

    public async Task<PriceOptimizationSuggestions> AnalyzePricingOpportunitiesAsync(Guid businessId)
    {
        _logger.LogInformation("Analyzing pricing opportunities for business {BusinessId}", businessId);

        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var suggestions = new PriceOptimizationSuggestions
        {
            BusinessId = businessId
        };

        // Get all products and sales data for the business
        var allProducts = new List<Product>();
        var allSales = new List<Sale>();

        foreach (var shop in business.Shops.Where(s => s.IsActive))
        {
            var shopProducts = await _productRepository.GetProductsByShopAsync(shop.Id);
            var shopSales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shop.Id, DateTime.UtcNow.AddDays(-90), DateTime.UtcNow);
            
            allProducts.AddRange(shopProducts);
            allSales.AddRange(shopSales);
        }

        // Analyze price optimization opportunities
        suggestions.Optimizations = await GeneratePriceOptimizations(allProducts, allSales);

        // Analyze demand elasticity
        suggestions.DemandInsights = await AnalyzeDemandElasticity(allProducts, allSales);

        // Generate competitive analysis (placeholder - would integrate with external data)
        suggestions.CompetitiveInsights = await GenerateCompetitiveAnalysis(allProducts);

        _logger.LogInformation("Price optimization analysis completed for business {BusinessId}. Optimizations: {OptimizationCount}",
            businessId, suggestions.Optimizations.Count);

        return suggestions;
    }

    public async Task<ExpiryRiskAlert[]> GetExpiryRiskAlertsAsync(Guid shopId)
    {
        _logger.LogInformation("Getting expiry risk alerts for shop {ShopId}", shopId);

        var shop = await _shopRepository.GetShopWithBusinessAsync(shopId);
        if (shop == null)
        {
            throw new ArgumentException($"Shop with ID {shopId} not found");
        }

        var products = await _productRepository.GetProductsByShopAsync(shopId);
        var inventory = await _stockRepository.GetStockByShopAsync(shopId);

        var alerts = new List<ExpiryRiskAlert>();

        foreach (var product in products.Where(p => p.ExpiryDate.HasValue))
        {
            var stock = inventory.FirstOrDefault(s => s.ProductId == product.Id);
            if (stock == null || stock.Quantity <= 0 || !product.ExpiryDate.HasValue) continue;

            var daysUntilExpiry = (int)(product.ExpiryDate.Value - DateTime.UtcNow).TotalDays;
            
            if (daysUntilExpiry <= 60) // Alert for products expiring within 60 days
            {
                var riskLevel = DetermineExpiryRiskLevel(daysUntilExpiry);
                var valueAtRisk = stock.Quantity * product.UnitPrice;

                var alert = new ExpiryRiskAlert
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    BatchNumber = product.BatchNumber ?? "Unknown",
                    ExpiryDate = product.ExpiryDate.Value,
                    DaysUntilExpiry = daysUntilExpiry,
                    QuantityAtRisk = stock.Quantity,
                    ValueAtRisk = valueAtRisk,
                    RiskLevel = riskLevel,
                    RecommendedActions = GenerateExpiryRecommendations(daysUntilExpiry, stock.Quantity, valueAtRisk)
                };

                alerts.Add(alert);
            }
        }

        _logger.LogInformation("Generated {AlertCount} expiry risk alerts for shop {ShopId}", alerts.Count, shopId);

        return alerts.OrderBy(a => a.DaysUntilExpiry).ToArray();
    }

    public async Task<AIModelData> CollectAndPreprocessDataAsync(Guid businessId, AIDataType[] dataTypes)
    {
        _logger.LogInformation("Collecting and preprocessing AI model data for business {BusinessId}", businessId);

        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var modelData = new AIModelData
        {
            BusinessId = businessId,
            DataTypes = dataTypes
        };

        var qualityMetrics = new DataQualityMetrics();
        var processedData = new Dictionary<string, object>();

        foreach (var dataType in dataTypes)
        {
            switch (dataType)
            {
                case AIDataType.SalesData:
                    var salesData = await CollectSalesData(business);
                    processedData["sales"] = salesData;
                    UpdateQualityMetrics(qualityMetrics, salesData, "sales");
                    break;

                case AIDataType.InventoryData:
                    var inventoryData = await CollectInventoryData(business);
                    processedData["inventory"] = inventoryData;
                    UpdateQualityMetrics(qualityMetrics, inventoryData, "inventory");
                    break;

                case AIDataType.ProductData:
                    var productData = await CollectProductData(business);
                    processedData["products"] = productData;
                    UpdateQualityMetrics(qualityMetrics, productData, "products");
                    break;

                case AIDataType.CustomerData:
                    var customerData = await CollectCustomerData(business);
                    processedData["customers"] = customerData;
                    UpdateQualityMetrics(qualityMetrics, customerData, "customers");
                    break;
            }
        }

        modelData.ProcessedData = processedData;
        modelData.QualityMetrics = qualityMetrics;

        _logger.LogInformation("AI model data collection completed for business {BusinessId}. Data types: {DataTypeCount}, Quality score: {QualityScore}",
            businessId, dataTypes.Length, qualityMetrics.CompletenessScore);

        return modelData;
    }

    public async Task<SalesForecast> PredictSalesAsync(Guid shopId, DateRange forecastPeriod)
    {
        _logger.LogInformation("Predicting sales for shop {ShopId} from {StartDate} to {EndDate}",
            shopId, forecastPeriod.StartDate, forecastPeriod.EndDate);

        var shop = await _shopRepository.GetShopWithBusinessAsync(shopId);
        if (shop == null)
        {
            throw new ArgumentException($"Shop with ID {shopId} not found");
        }

        // Get historical sales data (last 6 months)
        var historicalStartDate = DateTime.UtcNow.AddMonths(-6);
        var historicalSales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, historicalStartDate, DateTime.UtcNow);

        var forecast = new SalesForecast
        {
            ShopId = shopId,
            ForecastPeriod = forecastPeriod
        };

        // Simple forecasting algorithm (in production, this would use more sophisticated ML models)
        forecast.ForecastPoints = await GenerateSimpleForecast(historicalSales, forecastPeriod);
        forecast.AccuracyMetrics = await CalculateForecastAccuracy(historicalSales);
        forecast.Assumptions = GenerateForecastAssumptions(shop.Business.Type);

        _logger.LogInformation("Sales forecast generated for shop {ShopId}. Forecast points: {PointCount}",
            shopId, forecast.ForecastPoints.Count);

        return forecast;
    }

    public async Task<ProductPerformanceAnalysis> AnalyzeProductPerformanceAsync(Guid businessId, DateRange period)
    {
        _logger.LogInformation("Analyzing product performance for business {BusinessId}", businessId);

        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var analysis = new ProductPerformanceAnalysis
        {
            BusinessId = businessId,
            AnalysisPeriod = period
        };

        // Get all sales for the business within the period
        var allSales = new List<Sale>();
        foreach (var shop in business.Shops.Where(s => s.IsActive))
        {
            var shopSales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shop.Id, period.StartDate, period.EndDate);
            allSales.AddRange(shopSales);
        }

        var productPerformance = await AnalyzeProductPerformanceInternal(allSales);
        
        analysis.BestSellers = productPerformance.BestSellers;
        analysis.LowPerformers = productPerformance.LowPerformers;
        analysis.TrendingProducts = productPerformance.TrendingProducts;
        analysis.CategoryAnalysis = await AnalyzeCategoryPerformance(allSales);

        return analysis;
    }

    public async Task<PeakTimeAnalysis> AnalyzePeakTimesAsync(Guid businessId, DateRange period)
    {
        _logger.LogInformation("Analyzing peak times for business {BusinessId}", businessId);

        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        var analysis = new PeakTimeAnalysis
        {
            BusinessId = businessId,
            AnalysisPeriod = period
        };

        // Get all sales for the business within the period
        var allSales = new List<Sale>();
        foreach (var shop in business.Shops.Where(s => s.IsActive))
        {
            var shopSales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shop.Id, period.StartDate, period.EndDate);
            allSales.AddRange(shopSales);
        }

        analysis.DailyPeaks = await AnalyzePeakTimesInternal(allSales);
        analysis.WeeklyPeaks = await AnalyzeWeeklyPeaks(allSales);
        analysis.MonthlyPeaks = await AnalyzeMonthlyPeaks(allSales);
        
        analysis.StaffingRecommendations = GenerateStaffingRecommendations(analysis.DailyPeaks);
        analysis.InventoryRecommendations = GenerateInventoryRecommendationsFromPeaks(analysis.DailyPeaks);

        return analysis;
    }

    

    private async Task<List<SalesTrend>> AnalyzeSalesTrendsInternal(List<Sale> sales, DateRange period)
    {
        var trends = new List<SalesTrend>();
        var dailyGroups = sales.GroupBy(s => s.CreatedAt.Date).OrderBy(g => g.Key);

        foreach (var group in dailyGroups)
        {
            var dailySales = group.ToList();
            var trend = new SalesTrend
            {
                Date = group.Key,
                Revenue = dailySales.Sum(s => s.TotalAmount),
                TransactionCount = dailySales.Count,
                AverageOrderValue = dailySales.Count > 0 ? dailySales.Sum(s => s.TotalAmount) / dailySales.Count : 0,
                Direction = Shared.Core.DTOs.TrendDirection.Stable, // Simplified - would calculate based on previous periods
                ConfidenceScore = 0.8 // Placeholder
            };
            trends.Add(trend);
        }

        return trends;
    }

    private async Task<ProductPerformanceAnalysis> AnalyzeProductPerformanceInternal(List<Sale> sales)
    {
        var analysis = new ProductPerformanceAnalysis();
        
        // Group sales by product
        var productSales = new Dictionary<Guid, List<SaleItem>>();
        foreach (var sale in sales)
        {
            foreach (var item in sale.Items)
            {
                if (!productSales.ContainsKey(item.ProductId))
                    productSales[item.ProductId] = new List<SaleItem>();
                productSales[item.ProductId].Add(item);
            }
        }

        var insights = new List<ProductInsight>();
        foreach (var kvp in productSales)
        {
            var items = kvp.Value;
            var insight = new ProductInsight
            {
                ProductId = kvp.Key,
                ProductName = items.First().Product?.Name ?? "Unknown",
                Category = items.First().Product?.Category ?? "Unknown",
                QuantitySold = items.Sum(i => i.Quantity),
                Revenue = items.Sum(i => i.TotalPrice),
                ProfitMargin = 0.2m, // Placeholder
                PerformanceScore = (double)items.Sum(i => i.TotalPrice), // Simplified scoring
                Trend = Shared.Core.DTOs.TrendDirection.Stable
            };
            insights.Add(insight);
        }

        analysis.BestSellers = insights.OrderByDescending(i => i.Revenue).Take(10).ToList();
        analysis.LowPerformers = insights.OrderBy(i => i.Revenue).Take(10).ToList();
        analysis.TrendingProducts = insights.OrderByDescending(i => i.PerformanceScore).Take(10).ToList();

        return analysis;
    }

    private async Task<List<PeakTime>> AnalyzePeakTimesInternal(List<Sale> sales)
    {
        var hourlyGroups = sales.GroupBy(s => s.CreatedAt.Hour);
        var peaks = new List<PeakTime>();

        foreach (var group in hourlyGroups)
        {
            var hourlySales = group.ToList();
            var peak = new PeakTime
            {
                StartTime = new TimeSpan(group.Key, 0, 0),
                EndTime = new TimeSpan(group.Key + 1, 0, 0),
                AverageRevenue = hourlySales.Sum(s => s.TotalAmount),
                AverageTransactions = hourlySales.Count,
                IntensityScore = (double)(hourlySales.Count * hourlySales.Sum(s => s.TotalAmount)) // Simplified scoring
            };
            peaks.Add(peak);
        }

        return peaks.OrderByDescending(p => p.IntensityScore).Take(5).ToList();
    }

    private async Task<List<string>> GenerateSalesRecommendations(SalesInsights insights, BusinessType businessType)
    {
        var recommendations = new List<string>();

        if (insights.TotalRevenue == 0)
        {
            recommendations.Add("No sales recorded in this period. Focus on marketing and customer acquisition.");
            return recommendations;
        }

        if (insights.AverageOrderValue < 50) // Arbitrary threshold
        {
            recommendations.Add("Consider implementing upselling strategies to increase average order value.");
        }

        if (insights.TopProducts.Count < 5)
        {
            recommendations.Add("Diversify product offerings to reduce dependency on few products.");
        }

        switch (businessType)
        {
            case BusinessType.Pharmacy:
                recommendations.Add("Monitor expiry dates closely and implement FIFO inventory management.");
                break;
            case BusinessType.Grocery:
                recommendations.Add("Focus on fresh produce turnover and seasonal product planning.");
                break;
        }

        return recommendations;
    }

    private async Task<List<ReorderRecommendation>> GenerateReorderRecommendations(
        IEnumerable<Stock> inventory, IEnumerable<Sale> recentSales, IEnumerable<Product> products)
    {
        var recommendations = new List<ReorderRecommendation>();

        foreach (var stock in inventory.Where(s => s.Quantity < 20)) // Low stock threshold
        {
            var product = products.FirstOrDefault(p => p.Id == stock.ProductId);
            if (product == null) continue;

            // Calculate sales velocity (simplified)
            var productSales = recentSales.SelectMany(s => s.Items)
                .Where(i => i.ProductId == product.Id)
                .Sum(i => i.Quantity);

            var dailyVelocity = productSales / 90.0; // 90 days of data
            var daysUntilStockout = dailyVelocity > 0 ? (int)(stock.Quantity / dailyVelocity) : int.MaxValue;

            if (daysUntilStockout < 30) // Reorder if stockout expected within 30 days
            {
                var recommendation = new ReorderRecommendation
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    CurrentStock = stock.Quantity,
                    RecommendedOrderQuantity = (int)(dailyVelocity * 60), // 60 days supply
                    PredictedDaysUntilStockout = daysUntilStockout,
                    EstimatedMonthlySales = (decimal)(dailyVelocity * 30),
                    Priority = daysUntilStockout < 7 ? ReorderPriority.Critical : 
                              daysUntilStockout < 14 ? ReorderPriority.High : ReorderPriority.Medium,
                    ConfidenceScore = 0.7,
                    Reasoning = $"Based on {dailyVelocity:F1} units sold per day over the last 90 days"
                };

                recommendations.Add(recommendation);
            }
        }

        return recommendations.OrderBy(r => r.PredictedDaysUntilStockout).ToList();
    }

    private async Task<List<OverstockAlert>> GenerateOverstockAlerts(
        IEnumerable<Stock> inventory, IEnumerable<Sale> recentSales, IEnumerable<Product> products)
    {
        var alerts = new List<OverstockAlert>();

        foreach (var stock in inventory.Where(s => s.Quantity > 100)) // High stock threshold
        {
            var product = products.FirstOrDefault(p => p.Id == stock.ProductId);
            if (product == null) continue;

            // Calculate sales velocity
            var productSales = recentSales.SelectMany(s => s.Items)
                .Where(i => i.ProductId == product.Id)
                .Sum(i => i.Quantity);

            var monthlyVelocity = productSales / 3.0; // 90 days = 3 months
            var monthsOfSupply = monthlyVelocity > 0 ? stock.Quantity / monthlyVelocity : double.MaxValue;

            if (monthsOfSupply > 6) // Alert if more than 6 months of supply
            {
                var alert = new OverstockAlert
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    CurrentStock = stock.Quantity,
                    RecommendedStock = (int)(monthlyVelocity * 3), // 3 months supply
                    EstimatedMonthsOfSupply = (decimal)monthsOfSupply,
                    SuggestedDiscountPercentage = monthsOfSupply > 12 ? 0.2m : 0.1m,
                    Recommendation = monthsOfSupply > 12 ? 
                        "Consider significant discount or promotional pricing" :
                        "Monitor closely and consider moderate discount"
                };

                alerts.Add(alert);
            }
        }

        return alerts;
    }

    private async Task<List<SeasonalRecommendation>> GenerateSeasonalRecommendations(Guid shopId, BusinessType businessType)
    {
        var recommendations = new List<SeasonalRecommendation>();
        var currentMonth = DateTime.UtcNow.Month;

        // Simplified seasonal recommendations based on business type
        switch (businessType)
        {
            case BusinessType.Pharmacy:
                if (currentMonth >= 10 || currentMonth <= 3) // Winter months
                {
                    recommendations.Add(new SeasonalRecommendation
                    {
                        Season = "Winter",
                        ProductNames = new List<string> { "Cold Medicine", "Cough Syrup", "Vitamins" },
                        ExpectedDemandIncrease = 0.3m,
                        Reasoning = "Increased demand for cold and flu medications during winter months"
                    });
                }
                break;

            case BusinessType.Grocery:
                if (currentMonth >= 6 && currentMonth <= 8) // Summer months
                {
                    recommendations.Add(new SeasonalRecommendation
                    {
                        Season = "Summer",
                        ProductNames = new List<string> { "Ice Cream", "Cold Drinks", "Fresh Fruits" },
                        ExpectedDemandIncrease = 0.4m,
                        Reasoning = "Higher demand for cooling products and fresh produce in summer"
                    });
                }
                break;
        }

        return recommendations;
    }

    private async Task<List<string>> GenerateGeneralInventoryRecommendations(InventoryRecommendations recommendations)
    {
        var general = new List<string>();

        if (recommendations.ReorderSuggestions.Any(r => r.Priority == ReorderPriority.Critical))
        {
            general.Add("Immediate attention required for critical stock levels.");
        }

        if (recommendations.OverstockAlerts.Count > 5)
        {
            general.Add("Consider implementing promotional strategies to reduce overstock.");
        }

        if (recommendations.ExpiryRisks.Any(e => e.RiskLevel == ExpiryRiskLevel.Critical))
        {
            general.Add("Urgent action needed for products approaching expiry.");
        }

        return general;
    }

    private async Task<List<AIProductRecommendation>> GenerateCrossSellRecommendations(
        IEnumerable<Product> products, IEnumerable<Sale> recentSales, Guid? customerId)
    {
        _logger.LogInformation("Generating cross-sell recommendations for customer {CustomerId}", customerId);
        
        var recommendations = new List<AIProductRecommendation>();
        var productList = products.ToList();
        var salesList = recentSales.ToList();

        // Build product association matrix from sales data
        var productAssociations = await BuildProductAssociationMatrix(salesList);
        
        // Get customer's purchase history if available
        var customerPurchases = new List<Guid>();
        if (customerId.HasValue)
        {
            customerPurchases = salesList
                .Where(s => s.CustomerId == customerId.Value)
                .SelectMany(s => s.Items.Select(i => i.ProductId))
                .Distinct()
                .ToList();
        }

        // Generate recommendations based on association rules
        foreach (var association in productAssociations.OrderByDescending(a => a.Confidence).Take(10))
        {
            var targetProduct = productList.FirstOrDefault(p => p.Id == association.ProductB);
            if (targetProduct == null || !targetProduct.IsActive) continue;

            var baseProduct = productList.FirstOrDefault(p => p.Id == association.ProductA);
            var baseProductName = baseProduct?.Name ?? "Unknown";

            var recommendation = new AIProductRecommendation
            {
                ProductId = targetProduct.Id,
                ProductName = targetProduct.Name,
                Category = targetProduct.Category ?? "Unknown",
                Price = targetProduct.UnitPrice,
                RelevanceScore = association.Confidence,
                Type = RecommendationType.CrossSell,
                Reasoning = $"Customers who buy {baseProductName} often purchase {targetProduct.Name} (confidence: {association.Confidence:P1})",
                BasedOnProducts = new List<Guid> { association.ProductA }
            };

            recommendations.Add(recommendation);
        }

        // Add category-based cross-sell recommendations
        var categoryRecommendations = await GenerateCategoryBasedCrossSell(productList, salesList);
        recommendations.AddRange(categoryRecommendations);

        // Add business type-specific cross-sell recommendations
        var businessTypeRecommendations = await GenerateBusinessTypeSpecificCrossSell(productList);
        recommendations.AddRange(businessTypeRecommendations);

        _logger.LogInformation("Generated {Count} cross-sell recommendations", recommendations.Count);
        return recommendations.OrderByDescending(r => r.RelevanceScore).Take(5).ToList();
    }

    private async Task<List<AIProductRecommendation>> GenerateUpSellRecommendations(
        IEnumerable<Product> products, IEnumerable<Sale> recentSales, Guid? customerId)
    {
        _logger.LogInformation("Generating up-sell recommendations for customer {CustomerId}", customerId);
        
        var recommendations = new List<AIProductRecommendation>();
        var productList = products.ToList();
        var salesList = recentSales.ToList();

        // Get customer's purchase history and preferences
        var customerPurchaseHistory = new Dictionary<string, List<Product>>();
        if (customerId.HasValue)
        {
            var customerSales = salesList.Where(s => s.CustomerId == customerId.Value);
            foreach (var sale in customerSales)
            {
                foreach (var item in sale.Items)
                {
                    var product = productList.FirstOrDefault(p => p.Id == item.ProductId);
                    if (product?.Category != null)
                    {
                        if (!customerPurchaseHistory.ContainsKey(product.Category))
                            customerPurchaseHistory[product.Category] = new List<Product>();
                        customerPurchaseHistory[product.Category].Add(product);
                    }
                }
            }
        }

        // Generate up-sell recommendations based on purchase history
        foreach (var categoryGroup in customerPurchaseHistory)
        {
            var category = categoryGroup.Key;
            var purchasedProducts = categoryGroup.Value;
            var avgPurchasePrice = purchasedProducts.Average(p => p.UnitPrice);
            if (avgPurchasePrice <= 0m)
                continue;

            // Find higher-priced products in the same category
            var premiumProducts = productList
                .Where(p => p.Category == category && p.UnitPrice > avgPurchasePrice * 1.2m)
                .OrderByDescending(p => p.UnitPrice)
                .Take(2);

            foreach (var premiumProduct in premiumProducts)
            {
                var priceIncrease = ((premiumProduct.UnitPrice - avgPurchasePrice) / avgPurchasePrice) * 100;
                var recommendation = new AIProductRecommendation
                {
                    ProductId = premiumProduct.Id,
                    ProductName = premiumProduct.Name,
                    Category = premiumProduct.Category ?? "Unknown",
                    Price = premiumProduct.UnitPrice,
                    RelevanceScore = Math.Min(0.9, 1.0 - (double)(priceIncrease / 200)), // Higher relevance for smaller price increases
                    Type = RecommendationType.UpSell,
                    Reasoning = $"Premium {category} option - {priceIncrease:F0}% higher quality than your usual purchases",
                    BasedOnProducts = purchasedProducts.Select(p => p.Id).ToList()
                };

                recommendations.Add(recommendation);
            }
        }

        // Add general up-sell recommendations based on sales patterns
        var generalUpSells = await GenerateGeneralUpSellRecommendations(productList, salesList);
        recommendations.AddRange(generalUpSells);

        _logger.LogInformation("Generated {Count} up-sell recommendations", recommendations.Count);
        return recommendations.OrderByDescending(r => r.RelevanceScore).Take(5).ToList();
    }

    private async Task<List<ProductBundle>> GenerateBundleRecommendations(
        IEnumerable<Product> products, IEnumerable<Sale> recentSales)
    {
        _logger.LogInformation("Generating product bundle recommendations");
        
        var bundles = new List<ProductBundle>();
        var productList = products.ToList();
        var salesList = recentSales.ToList();

        // Build product co-occurrence matrix
        var coOccurrenceMatrix = await BuildProductCoOccurrenceMatrix(salesList);
        
        // Generate bundles based on frequently bought together patterns
        var frequentPairs = coOccurrenceMatrix
            .Where(co => co.CoOccurrenceCount >= 3) // Minimum 3 co-occurrences
            .OrderByDescending(co => co.CoOccurrenceScore)
            .Take(10);

        foreach (var pair in frequentPairs)
        {
            var productA = productList.FirstOrDefault(p => p.Id == pair.ProductA);
            var productB = productList.FirstOrDefault(p => p.Id == pair.ProductB);
            
            if (productA == null || productB == null) continue;

            var individualPrice = productA.UnitPrice + productB.UnitPrice;
            var discountPercentage = Math.Min(0.15m, (decimal)pair.CoOccurrenceScore * 0.2m); // Max 15% discount
            var bundlePrice = individualPrice * (1 - discountPercentage);

            var bundle = new ProductBundle
            {
                BundleName = $"{productA.Name} + {productB.Name} Combo",
                ProductIds = new List<Guid> { productA.Id, productB.Id },
                ProductNames = new List<string> { productA.Name, productB.Name },
                IndividualPrice = individualPrice,
                BundlePrice = bundlePrice,
                SavingsAmount = individualPrice - bundlePrice,
                RelevanceScore = pair.CoOccurrenceScore,
                Description = $"Save {discountPercentage:P0} when you buy these items together - frequently purchased by other customers"
            };

            bundles.Add(bundle);
        }

        // Generate category-based bundles
        var categoryBundles = await GenerateCategoryBasedBundles(productList, salesList);
        bundles.AddRange(categoryBundles);

        // Generate business type-specific bundles
        var businessTypeBundles = await GenerateBusinessTypeSpecificBundles(productList);
        bundles.AddRange(businessTypeBundles);

        _logger.LogInformation("Generated {Count} bundle recommendations", bundles.Count);
        return bundles.OrderByDescending(b => b.RelevanceScore).Take(5).ToList();
    }

    private async Task<List<PriceOptimization>> GeneratePriceOptimizations(
        List<Product> products, List<Sale> sales)
    {
        var optimizations = new List<PriceOptimization>();

        // Simplified price optimization logic
        foreach (var product in products.Take(5))
        {
            var productSales = sales.SelectMany(s => s.Items)
                .Where(i => i.ProductId == product.Id);

            if (productSales.Any())
            {
                var avgQuantity = productSales.Average(i => i.Quantity);
                var optimization = new PriceOptimization
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    CurrentPrice = product.UnitPrice,
                    RecommendedPrice = avgQuantity > 10 ? product.UnitPrice * 1.05m : product.UnitPrice * 0.95m,
                    ExpectedRevenueChange = avgQuantity > 10 ? 0.03m : -0.02m,
                    ExpectedVolumeChange = avgQuantity > 10 ? -0.02m : 0.05m,
                    ConfidenceScore = 0.6,
                    OptimizationType = avgQuantity > 10 ? PriceOptimizationType.IncreasePrice : PriceOptimizationType.DecreasePrice,
                    Reasoning = avgQuantity > 10 ? "High demand allows for price increase" : "Low demand suggests price reduction needed"
                };
                optimizations.Add(optimization);
            }
        }

        return optimizations;
    }

    private async Task<List<DemandElasticityInsight>> AnalyzeDemandElasticity(
        List<Product> products, List<Sale> sales)
    {
        // Simplified elasticity analysis
        var insights = new List<DemandElasticityInsight>();

        foreach (var product in products.Take(3))
        {
            insights.Add(new DemandElasticityInsight
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ElasticityCoefficient = -0.5, // Placeholder
                ElasticityType = DemandElasticity.Inelastic,
                Interpretation = "Demand is relatively insensitive to price changes"
            });
        }

        return insights;
    }

    private async Task<List<CompetitiveAnalysis>> GenerateCompetitiveAnalysis(List<Product> products)
    {
        // Placeholder competitive analysis
        var analysis = new List<CompetitiveAnalysis>();

        foreach (var product in products.Take(3))
        {
            analysis.Add(new CompetitiveAnalysis
            {
                ProductId = product.Id,
                ProductName = product.Name,
                OurPrice = product.UnitPrice,
                MarketAveragePrice = product.UnitPrice * 1.1m, // Placeholder
                CompetitivePosition = -0.1m,
                Recommendation = "Consider slight price increase to match market average"
            });
        }

        return analysis;
    }

    private ExpiryRiskLevel DetermineExpiryRiskLevel(int daysUntilExpiry)
    {
        return daysUntilExpiry switch
        {
            <= 7 => ExpiryRiskLevel.Critical,
            <= 14 => ExpiryRiskLevel.High,
            <= 30 => ExpiryRiskLevel.Medium,
            _ => ExpiryRiskLevel.Low
        };
    }

    private List<string> GenerateExpiryRecommendations(int daysUntilExpiry, int quantity, decimal value)
    {
        var recommendations = new List<string>();

        if (daysUntilExpiry <= 7)
        {
            recommendations.Add("URGENT: Implement immediate discount pricing (30-50% off)");
            recommendations.Add("Contact regular customers about special offers");
            recommendations.Add("Consider donation to avoid total loss");
        }
        else if (daysUntilExpiry <= 14)
        {
            recommendations.Add("Apply moderate discount (15-25% off)");
            recommendations.Add("Promote through social media or newsletters");
        }
        else if (daysUntilExpiry <= 30)
        {
            recommendations.Add("Monitor closely and prepare promotional strategy");
            recommendations.Add("Ensure FIFO (First In, First Out) inventory management");
        }

        return recommendations;
    }

    private async Task<object> CollectSalesData(Business business)
    {
        var salesData = new List<object>();
        
        foreach (var shop in business.Shops.Where(s => s.IsActive))
        {
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(
                shop.Id, DateTime.UtcNow.AddMonths(-12), DateTime.UtcNow);
            
            salesData.AddRange(sales.Select(s => new
            {
                s.Id,
                s.ShopId,
                s.TotalAmount,
                s.CreatedAt,
                s.PaymentMethod,
                ItemCount = s.Items.Count
            }));
        }

        return salesData;
    }

    private async Task<object> CollectInventoryData(Business business)
    {
        var inventoryData = new List<object>();
        
        foreach (var shop in business.Shops.Where(s => s.IsActive))
        {
            var inventory = await _stockRepository.GetStockByShopAsync(shop.Id);
            inventoryData.AddRange(inventory.Select(i => new
            {
                i.ProductId,
                i.Quantity,
                i.LastUpdatedAt,
                ShopId = shop.Id
            }));
        }

        return inventoryData;
    }

    private async Task<object> CollectProductData(Business business)
    {
        var productData = new List<object>();
        
        foreach (var shop in business.Shops.Where(s => s.IsActive))
        {
            var products = await _productRepository.GetProductsByShopAsync(shop.Id);
            productData.AddRange(products.Select(p => new
            {
                p.Id,
                p.Name,
                p.Category,
                p.UnitPrice,
                p.ExpiryDate,
                p.IsWeightBased,
                ShopId = shop.Id
            }));
        }

        return productData;
    }

    private async Task<object> CollectCustomerData(Business business)
    {
        // Placeholder - would collect customer data if available
        return new List<object>();
    }

    private void UpdateQualityMetrics(DataQualityMetrics metrics, object data, string dataType)
    {
        // Simplified quality metrics calculation
        if (data is IEnumerable<object> collection)
        {
            var count = collection.Count();
            metrics.TotalRecords += count;
            metrics.ValidRecords += count; // Simplified - assume all valid
            metrics.CompletenessScore = metrics.TotalRecords > 0 ? 
                (double)metrics.ValidRecords / metrics.TotalRecords : 0;
            metrics.AccuracyScore = 0.9; // Placeholder
        }
    }

    private async Task<List<SalesForecastPoint>> GenerateSimpleForecast(
        IEnumerable<Sale> historicalSales, DateRange forecastPeriod)
    {
        var forecastPoints = new List<SalesForecastPoint>();
        
        // Simple moving average forecast
        var dailyAverageRevenue = historicalSales.Any() ? 
            historicalSales.Sum(s => s.TotalAmount) / (decimal)historicalSales.Count() : 0;
        
        var currentDate = forecastPeriod.StartDate;
        while (currentDate <= forecastPeriod.EndDate)
        {
            var point = new SalesForecastPoint
            {
                Date = currentDate,
                PredictedRevenue = dailyAverageRevenue,
                LowerBound = dailyAverageRevenue * 0.8m,
                UpperBound = dailyAverageRevenue * 1.2m,
                PredictedTransactions = (int)(dailyAverageRevenue / 50), // Assume $50 average order
                ConfidenceLevel = 0.7
            };
            forecastPoints.Add(point);
            currentDate = currentDate.AddDays(1);
        }

        return forecastPoints;
    }

    private async Task<ForecastAccuracy> CalculateForecastAccuracy(IEnumerable<Sale> historicalSales)
    {
        // Simplified accuracy metrics
        return new ForecastAccuracy
        {
            MeanAbsoluteError = 50.0,
            MeanAbsolutePercentageError = 0.15,
            RootMeanSquareError = 75.0,
            R2Score = 0.7
        };
    }

    private List<string> GenerateForecastAssumptions(BusinessType businessType)
    {
        var assumptions = new List<string>
        {
            "Historical patterns will continue",
            "No major market disruptions",
            "Seasonal patterns remain consistent"
        };

        switch (businessType)
        {
            case BusinessType.Pharmacy:
                assumptions.Add("No major health crises affecting demand");
                break;
            case BusinessType.Grocery:
                assumptions.Add("Weather patterns remain normal");
                break;
        }

        return assumptions;
    }

    private async Task<List<CategoryPerformance>> AnalyzeCategoryPerformance(List<Sale> sales)
    {
        var categoryPerformance = new List<CategoryPerformance>();
        
        var categoryGroups = sales.SelectMany(s => s.Items)
            .GroupBy(i => i.Product?.Category ?? "Unknown");

        foreach (var group in categoryGroups)
        {
            var items = group.ToList();
            var performance = new CategoryPerformance
            {
                CategoryName = group.Key,
                TotalRevenue = items.Sum(i => i.TotalPrice),
                TotalQuantitySold = items.Sum(i => i.Quantity),
                AveragePrice = items.Any() ? items.Average(i => i.UnitPrice) : 0,
                MarketShare = 0.1, // Placeholder
                Trend = Shared.Core.DTOs.TrendDirection.Stable
            };
            categoryPerformance.Add(performance);
        }

        return categoryPerformance.OrderByDescending(c => c.TotalRevenue).ToList();
    }

    private async Task<List<PeakTime>> AnalyzeWeeklyPeaks(List<Sale> sales)
    {
        var weeklyGroups = sales.GroupBy(s => s.CreatedAt.DayOfWeek);
        var peaks = new List<PeakTime>();

        foreach (var group in weeklyGroups)
        {
            var weeklySales = group.ToList();
            var peak = new PeakTime
            {
                DayOfWeek = group.Key,
                AverageRevenue = weeklySales.Sum(s => s.TotalAmount),
                AverageTransactions = weeklySales.Count,
                IntensityScore = (double)(weeklySales.Count * weeklySales.Sum(s => s.TotalAmount))
            };
            peaks.Add(peak);
        }

        return peaks.OrderByDescending(p => p.IntensityScore).Take(3).ToList();
    }

    private async Task<List<PeakTime>> AnalyzeMonthlyPeaks(List<Sale> sales)
    {
        var monthlyGroups = sales.GroupBy(s => s.CreatedAt.Month);
        var peaks = new List<PeakTime>();

        foreach (var group in monthlyGroups)
        {
            var monthlySales = group.ToList();
            var peak = new PeakTime
            {
                AverageRevenue = monthlySales.Sum(s => s.TotalAmount),
                AverageTransactions = monthlySales.Count,
                IntensityScore = (double)(monthlySales.Count * monthlySales.Sum(s => s.TotalAmount))
            };
            peaks.Add(peak);
        }

        return peaks.OrderByDescending(p => p.IntensityScore).Take(3).ToList();
    }

    private List<string> GenerateStaffingRecommendations(List<PeakTime> dailyPeaks)
    {
        var recommendations = new List<string>();
        
        if (dailyPeaks.Any())
        {
            var topPeak = dailyPeaks.First();
            recommendations.Add($"Schedule additional staff during {topPeak.StartTime:hh\\:mm} - {topPeak.EndTime:hh\\:mm}");
            recommendations.Add("Consider flexible scheduling to match demand patterns");
        }

        return recommendations;
    }

    private List<string> GenerateInventoryRecommendationsFromPeaks(List<PeakTime> dailyPeaks)
    {
        var recommendations = new List<string>();
        
        if (dailyPeaks.Any())
        {
            recommendations.Add("Ensure adequate stock levels before peak hours");
            recommendations.Add("Consider pre-positioning fast-moving items during peak times");
        }

        return recommendations;
    }

    #region Business Recommendation Helper Methods

    private async Task<List<ProductAssociation>> BuildProductAssociationMatrix(List<Sale> sales)
    {
        _logger.LogInformation("Building product association matrix from {SalesCount} sales", sales.Count);
        
        var associations = new List<ProductAssociation>();
        var productPairs = new Dictionary<(Guid ProductA, Guid ProductB), int>();
        var productCounts = new Dictionary<Guid, int>();

        // Count individual products and product pairs
        foreach (var sale in sales)
        {
            var productIds = sale.Items.Select(i => i.ProductId).Distinct().ToList();
            
            // Count individual products
            foreach (var productId in productIds)
            {
                productCounts[productId] = productCounts.GetValueOrDefault(productId, 0) + 1;
            }

            // Count product pairs (combinations)
            for (int i = 0; i < productIds.Count; i++)
            {
                for (int j = i + 1; j < productIds.Count; j++)
                {
                    var pair = (productIds[i], productIds[j]);
                    var reversePair = (productIds[j], productIds[i]);
                    
                    // Use consistent ordering for pairs
                    var orderedPair = pair.Item1.CompareTo(pair.Item2) < 0 ? pair : reversePair;
                    productPairs[orderedPair] = productPairs.GetValueOrDefault(orderedPair, 0) + 1;
                }
            }
        }

        // Calculate association rules (A -> B)
        foreach (var pair in productPairs.Where(p => p.Value >= 2)) // Minimum 2 co-occurrences
        {
            var productA = pair.Key.ProductA;
            var productB = pair.Key.ProductB;
            var coOccurrences = pair.Value;
            
            var countA = productCounts.GetValueOrDefault(productA, 0);
            var countB = productCounts.GetValueOrDefault(productB, 0);
            
            if (countA > 0 && countB > 0)
            {
                // Calculate confidence: P(B|A) = count(A,B) / count(A)
                var confidenceAB = (double)coOccurrences / countA;
                var confidenceBA = (double)coOccurrences / countB;
                
                // Calculate support: P(A,B) = count(A,B) / total_transactions
                var support = (double)coOccurrences / sales.Count;
                
                // Calculate lift: P(B|A) / P(B)
                var liftAB = confidenceAB / ((double)countB / sales.Count);
                var liftBA = confidenceBA / ((double)countA / sales.Count);

                // Add both directions if they meet minimum thresholds
                if (confidenceAB >= 0.1 && support >= 0.01 && liftAB > 1.0) // Minimum thresholds
                {
                    associations.Add(new ProductAssociation
                    {
                        ProductA = productA,
                        ProductB = productB,
                        Support = support,
                        Confidence = confidenceAB,
                        Lift = liftAB,
                        CoOccurrences = coOccurrences
                    });
                }

                if (confidenceBA >= 0.1 && support >= 0.01 && liftBA > 1.0)
                {
                    associations.Add(new ProductAssociation
                    {
                        ProductA = productB,
                        ProductB = productA,
                        Support = support,
                        Confidence = confidenceBA,
                        Lift = liftBA,
                        CoOccurrences = coOccurrences
                    });
                }
            }
        }

        _logger.LogInformation("Generated {AssociationCount} product associations", associations.Count);
        return associations.OrderByDescending(a => a.Confidence).ToList();
    }

    private async Task<List<ProductCoOccurrence>> BuildProductCoOccurrenceMatrix(List<Sale> sales)
    {
        _logger.LogInformation("Building product co-occurrence matrix from {SalesCount} sales", sales.Count);
        
        var coOccurrences = new List<ProductCoOccurrence>();
        var productPairs = new Dictionary<(Guid ProductA, Guid ProductB), int>();

        // Count product co-occurrences in the same transaction
        foreach (var sale in sales)
        {
            var productIds = sale.Items.Select(i => i.ProductId).Distinct().ToList();
            
            for (int i = 0; i < productIds.Count; i++)
            {
                for (int j = i + 1; j < productIds.Count; j++)
                {
                    var pair = (productIds[i], productIds[j]);
                    var reversePair = (productIds[j], productIds[i]);
                    
                    // Use consistent ordering for pairs
                    var orderedPair = pair.Item1.CompareTo(pair.Item2) < 0 ? pair : reversePair;
                    productPairs[orderedPair] = productPairs.GetValueOrDefault(orderedPair, 0) + 1;
                }
            }
        }

        // Convert to co-occurrence objects with scores
        foreach (var pair in productPairs.Where(p => p.Value >= 2)) // Minimum 2 co-occurrences
        {
            var coOccurrenceScore = Math.Min(1.0, (double)pair.Value / sales.Count * 10); // Normalize score
            
            coOccurrences.Add(new ProductCoOccurrence
            {
                ProductA = pair.Key.ProductA,
                ProductB = pair.Key.ProductB,
                CoOccurrenceCount = pair.Value,
                CoOccurrenceScore = coOccurrenceScore
            });
        }

        _logger.LogInformation("Generated {CoOccurrenceCount} product co-occurrences", coOccurrences.Count);
        return coOccurrences.OrderByDescending(c => c.CoOccurrenceScore).ToList();
    }

    private async Task<List<AIProductRecommendation>> GenerateCategoryBasedCrossSell(
        List<Product> products, List<Sale> sales)
    {
        _logger.LogInformation("Generating category-based cross-sell recommendations");
        
        var recommendations = new List<AIProductRecommendation>();
        
        // Group products by category
        var categoryGroups = products.GroupBy(p => p.Category ?? "Unknown").ToList();
        
        // Analyze cross-category purchase patterns
        var categorySales = new Dictionary<string, List<string>>();
        
        foreach (var sale in sales)
        {
            var categories = sale.Items
                .Select(i => i.Product?.Category ?? "Unknown")
                .Distinct()
                .ToList();
            
            foreach (var category in categories)
            {
                if (!categorySales.ContainsKey(category))
                    categorySales[category] = new List<string>();
                
                categorySales[category].AddRange(categories.Where(c => c != category));
            }
        }

        // Generate recommendations based on category associations
        foreach (var categoryGroup in categoryGroups.Take(5)) // Limit to top 5 categories
        {
            var category = categoryGroup.Key;
            var categoryProducts = categoryGroup.ToList();
            
            if (categorySales.ContainsKey(category))
            {
                var associatedCategories = categorySales[category]
                    .GroupBy(c => c)
                    .OrderByDescending(g => g.Count())
                    .Take(2)
                    .Select(g => g.Key);

                foreach (var associatedCategory in associatedCategories)
                {
                    var associatedProducts = products
                        .Where(p => p.Category == associatedCategory)
                        .OrderByDescending(p => p.UnitPrice) // Prefer higher-value items
                        .Take(2);

                    foreach (var product in associatedProducts)
                    {
                        var associationStrength = (double)categorySales[category].Count(c => c == associatedCategory) / 
                                                categorySales[category].Count;

                        recommendations.Add(new AIProductRecommendation
                        {
                            ProductId = product.Id,
                            ProductName = product.Name,
                            Category = product.Category ?? "Unknown",
                            Price = product.UnitPrice,
                            RelevanceScore = associationStrength,
                            Type = RecommendationType.CrossSell,
                            Reasoning = $"Customers who buy {category} products often also purchase {associatedCategory} items",
                            BasedOnProducts = categoryProducts.Select(p => p.Id).ToList()
                        });
                    }
                }
            }
        }

        return recommendations.OrderByDescending(r => r.RelevanceScore).Take(5).ToList();
    }

    private async Task<List<AIProductRecommendation>> GenerateBusinessTypeSpecificCrossSell(List<Product> products)
    {
        _logger.LogInformation("Generating business type-specific cross-sell recommendations");
        
        var recommendations = new List<AIProductRecommendation>();
        
        // Get business type from the first product's shop (simplified approach)
        var firstProduct = products.FirstOrDefault();
        if (firstProduct?.Shop?.Business?.Type == null)
            return recommendations;

        var businessType = firstProduct.Shop.Business.Type;
        
        // Generate business type-specific cross-sell patterns
        switch (businessType)
        {
            case BusinessType.Pharmacy:
                recommendations.AddRange(GeneratePharmacyCrossSell(products));
                break;
                
            case BusinessType.Grocery:
                recommendations.AddRange(GenerateGroceryCrossSell(products));
                break;
                
            case BusinessType.SuperShop:
                recommendations.AddRange(GenerateSuperShopCrossSell(products));
                break;
                
            case BusinessType.GeneralRetail:
                recommendations.AddRange(GenerateGeneralRetailCrossSell(products));
                break;
        }

        return recommendations.Take(3).ToList();
    }

    private List<AIProductRecommendation> GeneratePharmacyCrossSell(List<Product> products)
    {
        var recommendations = new List<AIProductRecommendation>();
        
        // Common pharmacy cross-sell patterns
        var crossSellPatterns = new Dictionary<string[], string[]>
        {
            { new[] { "medicine", "tablet", "capsule" }, new[] { "vitamin", "supplement", "health" } },
            { new[] { "pain", "relief" }, new[] { "gel", "cream", "ointment" } },
            { new[] { "cold", "flu" }, new[] { "tissue", "throat", "cough" } },
            { new[] { "diabetes" }, new[] { "glucose", "test", "strip" } },
            { new[] { "baby" }, new[] { "diaper", "formula", "care" } }
        };

        foreach (var pattern in crossSellPatterns)
        {
            var triggerProducts = products.Where(p =>
                p.Name != null && pattern.Key.Any(keyword => p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var recommendedProducts = products.Where(p =>
                p.Name != null && pattern.Value.Any(keyword => p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var triggerProduct in triggerProducts.Take(2))
            {
                foreach (var recommendedProduct in recommendedProducts.Take(1))
                {
                    recommendations.Add(new AIProductRecommendation
                    {
                        ProductId = recommendedProduct.Id,
                        ProductName = recommendedProduct.Name,
                        Category = recommendedProduct.Category ?? "Unknown",
                        Price = recommendedProduct.UnitPrice,
                        RelevanceScore = 0.7,
                        Type = RecommendationType.CrossSell,
                        Reasoning = "Commonly purchased together in pharmacy settings",
                        BasedOnProducts = new List<Guid> { triggerProduct.Id }
                    });
                }
            }
        }

        return recommendations;
    }

    private List<AIProductRecommendation> GenerateGroceryCrossSell(List<Product> products)
    {
        var recommendations = new List<AIProductRecommendation>();
        
        // Common grocery cross-sell patterns
        var crossSellPatterns = new Dictionary<string[], string[]>
        {
            { new[] { "bread", "loaf" }, new[] { "butter", "jam", "cheese" } },
            { new[] { "pasta" }, new[] { "sauce", "cheese", "olive" } },
            { new[] { "cereal" }, new[] { "milk", "banana", "yogurt" } },
            { new[] { "chicken", "meat" }, new[] { "spice", "seasoning", "marinade" } },
            { new[] { "coffee" }, new[] { "sugar", "cream", "cookie" } }
        };

        foreach (var pattern in crossSellPatterns)
        {
            var triggerProducts = products.Where(p => 
                pattern.Key.Any(keyword => p.Name.ToLower().Contains(keyword))).ToList();
            
            var recommendedProducts = products.Where(p => 
                pattern.Value.Any(keyword => p.Name.ToLower().Contains(keyword))).ToList();

            foreach (var triggerProduct in triggerProducts.Take(2))
            {
                foreach (var recommendedProduct in recommendedProducts.Take(1))
                {
                    recommendations.Add(new AIProductRecommendation
                    {
                        ProductId = recommendedProduct.Id,
                        ProductName = recommendedProduct.Name,
                        Category = recommendedProduct.Category ?? "Unknown",
                        Price = recommendedProduct.UnitPrice,
                        RelevanceScore = 0.8,
                        Type = RecommendationType.CrossSell,
                        Reasoning = "Frequently bought together in grocery shopping",
                        BasedOnProducts = new List<Guid> { triggerProduct.Id }
                    });
                }
            }
        }

        return recommendations;
    }

    private List<AIProductRecommendation> GenerateSuperShopCrossSell(List<Product> products)
    {
        var recommendations = new List<AIProductRecommendation>();
        
        // Super shop combines grocery and general retail patterns
        recommendations.AddRange(GenerateGroceryCrossSell(products));
        recommendations.AddRange(GenerateGeneralRetailCrossSell(products));
        
        return recommendations.Take(3).ToList();
    }

    private List<AIProductRecommendation> GenerateGeneralRetailCrossSell(List<Product> products)
    {
        var recommendations = new List<AIProductRecommendation>();
        
        // General retail cross-sell patterns
        var crossSellPatterns = new Dictionary<string[], string[]>
        {
            { new[] { "phone", "mobile" }, new[] { "case", "charger", "screen" } },
            { new[] { "laptop", "computer" }, new[] { "mouse", "keyboard", "bag" } },
            { new[] { "shirt", "clothing" }, new[] { "belt", "accessory", "shoe" } },
            { new[] { "book" }, new[] { "bookmark", "light", "pen" } },
            { new[] { "gift" }, new[] { "wrap", "card", "bag" } }
        };

        foreach (var pattern in crossSellPatterns)
        {
            var triggerProducts = products.Where(p => 
                pattern.Key.Any(keyword => p.Name.ToLower().Contains(keyword))).ToList();
            
            var recommendedProducts = products.Where(p => 
                pattern.Value.Any(keyword => p.Name.ToLower().Contains(keyword))).ToList();

            foreach (var triggerProduct in triggerProducts.Take(2))
            {
                foreach (var recommendedProduct in recommendedProducts.Take(1))
                {
                    recommendations.Add(new AIProductRecommendation
                    {
                        ProductId = recommendedProduct.Id,
                        ProductName = recommendedProduct.Name,
                        Category = recommendedProduct.Category ?? "Unknown",
                        Price = recommendedProduct.UnitPrice,
                        RelevanceScore = 0.6,
                        Type = RecommendationType.CrossSell,
                        Reasoning = "Complementary products often purchased together",
                        BasedOnProducts = new List<Guid> { triggerProduct.Id }
                    });
                }
            }
        }

        return recommendations;
    }

    private async Task<List<AIProductRecommendation>> GenerateGeneralUpSellRecommendations(
        List<Product> products, List<Sale> sales)
    {
        _logger.LogInformation("Generating general up-sell recommendations");
        
        var recommendations = new List<AIProductRecommendation>();
        
        // Group products by category and find premium alternatives
        var categoryGroups = products.GroupBy(p => p.Category ?? "Unknown");
        
        foreach (var categoryGroup in categoryGroups.Take(3))
        {
            var categoryProducts = categoryGroup.OrderBy(p => p.UnitPrice).ToList();
            if (categoryProducts.Count < 2) continue;
            
            // Find products in the lower price range
            var medianPrice = categoryProducts[categoryProducts.Count / 2].UnitPrice;
            var lowerPriceProducts = categoryProducts.Where(p => p.UnitPrice <= medianPrice).ToList();
            var higherPriceProducts = categoryProducts.Where(p => p.UnitPrice > medianPrice).ToList();
            
            // Generate up-sell recommendations from lower to higher price products
            foreach (var lowerProduct in lowerPriceProducts.Take(2))
            {
                var premiumAlternative = higherPriceProducts
                    .Where(p => p.UnitPrice <= lowerProduct.UnitPrice * 1.5m) // Max 50% price increase
                    .OrderBy(p => p.UnitPrice)
                    .FirstOrDefault();
                
                if (premiumAlternative != null)
                {
                    var priceIncrease = ((premiumAlternative.UnitPrice - lowerProduct.UnitPrice) / lowerProduct.UnitPrice) * 100;
                    
                    recommendations.Add(new AIProductRecommendation
                    {
                        ProductId = premiumAlternative.Id,
                        ProductName = premiumAlternative.Name,
                        Category = premiumAlternative.Category ?? "Unknown",
                        Price = premiumAlternative.UnitPrice,
                        RelevanceScore = Math.Max(0.3, 1.0 - (double)(priceIncrease / 100)), // Higher relevance for smaller price increases
                        Type = RecommendationType.UpSell,
                        Reasoning = $"Premium {categoryGroup.Key} option with {priceIncrease:F0}% better value",
                        BasedOnProducts = new List<Guid> { lowerProduct.Id }
                    });
                }
            }
        }

        return recommendations.OrderByDescending(r => r.RelevanceScore).Take(3).ToList();
    }

    private async Task<List<ProductBundle>> GenerateCategoryBasedBundles(
        List<Product> products, List<Sale> sales)
    {
        _logger.LogInformation("Generating category-based product bundles");
        
        var bundles = new List<ProductBundle>();
        
        // Analyze which categories are frequently bought together
        var categoryCoOccurrences = new Dictionary<(string CategoryA, string CategoryB), int>();
        
        foreach (var sale in sales)
        {
            var categories = sale.Items
                .Select(i => i.Product?.Category ?? "Unknown")
                .Distinct()
                .ToList();
            
            for (int i = 0; i < categories.Count; i++)
            {
                for (int j = i + 1; j < categories.Count; j++)
                {
                    var pair = (categories[i], categories[j]);
                    var reversePair = (categories[j], categories[i]);
                    
                    // Use consistent ordering
                    var orderedPair = string.Compare(pair.Item1, pair.Item2) < 0 ? pair : reversePair;
                    categoryCoOccurrences[orderedPair] = categoryCoOccurrences.GetValueOrDefault(orderedPair, 0) + 1;
                }
            }
        }

        // Generate bundles for frequently co-occurring categories
        var topCategoryPairs = categoryCoOccurrences
            .Where(c => c.Value >= 3) // Minimum 3 co-occurrences
            .OrderByDescending(c => c.Value)
            .Take(5);

        foreach (var categoryPair in topCategoryPairs)
        {
            var categoryA = categoryPair.Key.CategoryA;
            var categoryB = categoryPair.Key.CategoryB;
            
            var productsA = products.Where(p => p.Category == categoryA).OrderBy(p => p.UnitPrice).Take(2);
            var productsB = products.Where(p => p.Category == categoryB).OrderBy(p => p.UnitPrice).Take(2);
            
            foreach (var productA in productsA)
            {
                foreach (var productB in productsB)
                {
                    var individualPrice = productA.UnitPrice + productB.UnitPrice;
                    var discountPercentage = 0.1m; // 10% bundle discount
                    var bundlePrice = individualPrice * (1 - discountPercentage);
                    
                    bundles.Add(new ProductBundle
                    {
                        BundleName = $"{categoryA} & {categoryB} Bundle",
                        ProductIds = new List<Guid> { productA.Id, productB.Id },
                        ProductNames = new List<string> { productA.Name, productB.Name },
                        IndividualPrice = individualPrice,
                        BundlePrice = bundlePrice,
                        SavingsAmount = individualPrice - bundlePrice,
                        RelevanceScore = (double)categoryPair.Value / sales.Count,
                        Description = $"Popular combination: {productA.Name} + {productB.Name}"
                    });
                }
            }
        }

        return bundles.OrderByDescending(b => b.RelevanceScore).Take(3).ToList();
    }

    private async Task<List<ProductBundle>> GenerateBusinessTypeSpecificBundles(List<Product> products)
    {
        _logger.LogInformation("Generating business type-specific product bundles");
        
        var bundles = new List<ProductBundle>();
        
        // Get business type from the first product's shop (simplified approach)
        var firstProduct = products.FirstOrDefault();
        if (firstProduct?.Shop?.Business?.Type == null)
            return bundles;

        var businessType = firstProduct.Shop.Business.Type;
        
        // Generate business type-specific bundles
        switch (businessType)
        {
            case BusinessType.Pharmacy:
                bundles.AddRange(GeneratePharmacyBundles(products));
                break;
                
            case BusinessType.Grocery:
                bundles.AddRange(GenerateGroceryBundles(products));
                break;
                
            case BusinessType.SuperShop:
                bundles.AddRange(GenerateGroceryBundles(products)); // Super shop uses grocery patterns
                break;
        }

        return bundles.Take(2).ToList();
    }

    private List<ProductBundle> GeneratePharmacyBundles(List<Product> products)
    {
        var bundles = new List<ProductBundle>();
        
        // Common pharmacy bundles
        var bundlePatterns = new[]
        {
            new { Name = "Cold & Flu Care Bundle", Keywords = new[] { "cold", "flu", "cough", "throat" } },
            new { Name = "Pain Relief Bundle", Keywords = new[] { "pain", "relief", "gel", "cream" } },
            new { Name = "Baby Care Bundle", Keywords = new[] { "baby", "diaper", "formula", "care" } },
            new { Name = "Diabetes Care Bundle", Keywords = new[] { "diabetes", "glucose", "test", "strip" } }
        };

        foreach (var pattern in bundlePatterns)
        {
            var matchingProducts = products.Where(p => 
                pattern.Keywords.Any(keyword => p.Name.ToLower().Contains(keyword)))
                .OrderBy(p => p.UnitPrice)
                .Take(3)
                .ToList();

            if (matchingProducts.Count >= 2)
            {
                var individualPrice = matchingProducts.Sum(p => p.UnitPrice);
                var discountPercentage = 0.15m; // 15% bundle discount for pharmacy
                var bundlePrice = individualPrice * (1 - discountPercentage);
                
                bundles.Add(new ProductBundle
                {
                    BundleName = pattern.Name,
                    ProductIds = matchingProducts.Select(p => p.Id).ToList(),
                    ProductNames = matchingProducts.Select(p => p.Name).ToList(),
                    IndividualPrice = individualPrice,
                    BundlePrice = bundlePrice,
                    SavingsAmount = individualPrice - bundlePrice,
                    RelevanceScore = 0.8,
                    Description = $"Complete {pattern.Name.Replace(" Bundle", "")} solution"
                });
            }
        }

        return bundles;
    }

    private List<ProductBundle> GenerateGroceryBundles(List<Product> products)
    {
        var bundles = new List<ProductBundle>();
        
        // Common grocery bundles
        var bundlePatterns = new[]
        {
            new { Name = "Breakfast Bundle", Keywords = new[] { "cereal", "milk", "bread", "butter", "jam" } },
            new { Name = "Pasta Night Bundle", Keywords = new[] { "pasta", "sauce", "cheese", "olive" } },
            new { Name = "Coffee Time Bundle", Keywords = new[] { "coffee", "sugar", "cream", "cookie" } },
            new { Name = "Cooking Essentials Bundle", Keywords = new[] { "oil", "salt", "spice", "onion", "garlic" } }
        };

        foreach (var pattern in bundlePatterns)
        {
            var matchingProducts = products.Where(p => 
                pattern.Keywords.Any(keyword => p.Name.ToLower().Contains(keyword)))
                .OrderBy(p => p.UnitPrice)
                .Take(4)
                .ToList();

            if (matchingProducts.Count >= 2)
            {
                var individualPrice = matchingProducts.Sum(p => p.UnitPrice);
                var discountPercentage = 0.12m; // 12% bundle discount for grocery
                var bundlePrice = individualPrice * (1 - discountPercentage);
                
                bundles.Add(new ProductBundle
                {
                    BundleName = pattern.Name,
                    ProductIds = matchingProducts.Select(p => p.Id).ToList(),
                    ProductNames = matchingProducts.Select(p => p.Name).ToList(),
                    IndividualPrice = individualPrice,
                    BundlePrice = bundlePrice,
                    SavingsAmount = individualPrice - bundlePrice,
                    RelevanceScore = 0.7,
                    Description = $"Everything you need for {pattern.Name.Replace(" Bundle", "").ToLower()}"
                });
            }
        }

        return bundles;
    }

    #endregion

    #region Helper Classes for Business Recommendations

    private class ProductAssociation
    {
        public Guid ProductA { get; set; }
        public Guid ProductB { get; set; }
        public double Support { get; set; }
        public double Confidence { get; set; }
        public double Lift { get; set; }
        public int CoOccurrences { get; set; }
    }

    private class ProductCoOccurrence
    {
        public Guid ProductA { get; set; }
        public Guid ProductB { get; set; }
        public int CoOccurrenceCount { get; set; }
        public double CoOccurrenceScore { get; set; }
    }

    #endregion
}