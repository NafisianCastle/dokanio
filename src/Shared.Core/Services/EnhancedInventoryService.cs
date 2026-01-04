using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// Enhanced inventory service with AI-powered recommendations
/// Extends the basic inventory service with intelligent features for multi-business POS system
/// </summary>
public class EnhancedInventoryService : InventoryService, IEnhancedInventoryService
{
    private readonly IShopRepository _shopRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IAIAnalyticsEngine _aiAnalyticsEngine;
    private readonly ILogger<EnhancedInventoryService> _logger;

    public EnhancedInventoryService(
        IStockRepository stockRepository,
        IProductRepository productRepository,
        ISaleItemRepository saleItemRepository,
        IShopRepository shopRepository,
        ISaleRepository saleRepository,
        IAIAnalyticsEngine aiAnalyticsEngine,
        ILogger<EnhancedInventoryService> logger)
        : base(stockRepository, productRepository, saleItemRepository)
    {
        _shopRepository = shopRepository;
        _saleRepository = saleRepository;
        _aiAnalyticsEngine = aiAnalyticsEngine;
        _logger = logger;
    }

    public async Task<List<ReorderRecommendation>> PredictLowStockAsync(Guid shopId, int daysAhead = 30)
    {
        _logger.LogInformation("Predicting low stock for shop {ShopId} for {DaysAhead} days ahead", shopId, daysAhead);

        var shop = await _shopRepository.GetShopWithBusinessAsync(shopId);
        if (shop == null)
        {
            throw new ArgumentException($"Shop with ID {shopId} not found");
        }

        var recommendations = new List<ReorderRecommendation>();
        
        // Get current inventory
        var inventory = await _stockRepository.GetStockByShopAsync(shopId);
        var products = await _productRepository.GetProductsByShopAsync(shopId);
        
        // Get sales data for the last 90 days to calculate velocity
        var analysisStartDate = DateTime.UtcNow.AddDays(-90);
        var recentSales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shopId, analysisStartDate, DateTime.UtcNow);

        foreach (var stock in inventory.Where(s => s.Quantity > 0))
        {
            var product = products.FirstOrDefault(p => p.Id == stock.ProductId);
            if (product == null || !product.IsActive) continue;

            // Calculate sales velocity (units per day)
            var productSales = recentSales.SelectMany(s => s.Items)
                .Where(i => i.ProductId == product.Id)
                .Sum(i => i.Quantity);

            var dailyVelocity = productSales / 90.0; // 90 days of data
            
            if (dailyVelocity <= 0) continue; // No sales history, skip prediction

            var predictedDaysUntilStockout = (int)(stock.Quantity / dailyVelocity);
            
            // Predict if stock will run out within the specified days ahead
            if (predictedDaysUntilStockout <= daysAhead)
            {
                var priority = DeterminePriority(predictedDaysUntilStockout);
                var recommendedQuantity = CalculateRecommendedOrderQuantity(dailyVelocity, shop.Business.Type);
                
                var recommendation = new ReorderRecommendation
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    CurrentStock = stock.Quantity,
                    RecommendedOrderQuantity = recommendedQuantity,
                    PredictedDaysUntilStockout = predictedDaysUntilStockout,
                    EstimatedMonthlySales = (decimal)(dailyVelocity * 30),
                    Priority = priority,
                    ConfidenceScore = CalculateConfidenceScore(productSales, 90),
                    Reasoning = $"Based on {dailyVelocity:F1} units sold per day over the last 90 days. " +
                               $"Current stock will last approximately {predictedDaysUntilStockout} days."
                };

                recommendations.Add(recommendation);
            }
        }

        _logger.LogInformation("Generated {Count} low stock predictions for shop {ShopId}", 
            recommendations.Count, shopId);

        return recommendations.OrderBy(r => r.PredictedDaysUntilStockout).ToList();
    }

    public async Task<List<ReorderRecommendation>> GetReorderRecommendationsAsync(Guid shopId, Guid? productId = null)
    {
        _logger.LogInformation("Getting reorder recommendations for shop {ShopId}, product {ProductId}", 
            shopId, productId);

        var shop = await _shopRepository.GetShopWithBusinessAsync(shopId);
        if (shop == null)
        {
            throw new ArgumentException($"Shop with ID {shopId} not found");
        }

        // Use AI analytics engine to generate comprehensive recommendations
        var aiRecommendations = await _aiAnalyticsEngine.GenerateInventoryRecommendationsAsync(shopId);
        
        var recommendations = aiRecommendations.ReorderSuggestions;

        // Filter by specific product if requested
        if (productId.HasValue)
        {
            recommendations = recommendations.Where(r => r.ProductId == productId.Value).ToList();
        }

        // Enhance recommendations with seasonal adjustments
        var seasonalRecommendations = await GetSeasonalRecommendationsAsync(shopId);
        foreach (var recommendation in recommendations)
        {
            var seasonalAdjustment = seasonalRecommendations
                .FirstOrDefault(s => s.ProductNames.Contains(recommendation.ProductName));
            
            if (seasonalAdjustment != null)
            {
                var adjustedQuantity = (int)(recommendation.RecommendedOrderQuantity * 
                    (1 + seasonalAdjustment.ExpectedDemandIncrease));
                
                recommendation.RecommendedOrderQuantity = adjustedQuantity;
                recommendation.Reasoning += $" Seasonal adjustment applied: +{seasonalAdjustment.ExpectedDemandIncrease:P0} " +
                                          $"for {seasonalAdjustment.Season}.";
            }
        }

        _logger.LogInformation("Generated {Count} reorder recommendations for shop {ShopId}", 
            recommendations.Count, shopId);

        return recommendations;
    }

    public async Task<List<OverstockAlert>> GetOverstockAlertsAsync(Guid shopId, double monthsOfSupplyThreshold = 6.0)
    {
        _logger.LogInformation("Getting overstock alerts for shop {ShopId} with threshold {Threshold} months", 
            shopId, monthsOfSupplyThreshold);

        var shop = await _shopRepository.GetShopWithBusinessAsync(shopId);
        if (shop == null)
        {
            throw new ArgumentException($"Shop with ID {shopId} not found");
        }

        var alerts = new List<OverstockAlert>();
        
        // Get current inventory and sales data
        var inventory = await _stockRepository.GetStockByShopAsync(shopId);
        var products = await _productRepository.GetProductsByShopAsync(shopId);
        var recentSales = await _saleRepository.GetSalesByShopAndDateRangeAsync(
            shopId, DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow);

        foreach (var stock in inventory.Where(s => s.Quantity > 0))
        {
            var product = products.FirstOrDefault(p => p.Id == stock.ProductId);
            if (product == null || !product.IsActive) continue;

            // Calculate monthly sales velocity
            var productSales = recentSales.SelectMany(s => s.Items)
                .Where(i => i.ProductId == product.Id)
                .Sum(i => i.Quantity);

            var monthlyVelocity = productSales / 6.0; // 6 months of data
            
            if (monthlyVelocity <= 0) continue; // No sales, handle separately

            var monthsOfSupply = stock.Quantity / monthlyVelocity;
            
            if (monthsOfSupply > monthsOfSupplyThreshold)
            {
                var recommendedStock = (int)(monthlyVelocity * 3); // 3 months supply
                var discountPercentage = CalculateDiscountPercentage(monthsOfSupply);
                
                var alert = new OverstockAlert
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    CurrentStock = stock.Quantity,
                    RecommendedStock = recommendedStock,
                    EstimatedMonthsOfSupply = (decimal)monthsOfSupply,
                    SuggestedDiscountPercentage = discountPercentage,
                    Recommendation = GenerateOverstockRecommendation(monthsOfSupply, discountPercentage)
                };

                alerts.Add(alert);
            }
        }

        // Handle products with no sales (dead stock)
        var noSalesProducts = inventory.Where(s => s.Quantity > 0 && 
            !recentSales.SelectMany(sale => sale.Items).Any(i => i.ProductId == s.ProductId));

        foreach (var stock in noSalesProducts)
        {
            var product = products.FirstOrDefault(p => p.Id == stock.ProductId);
            if (product == null || !product.IsActive) continue;

            var alert = new OverstockAlert
            {
                ProductId = product.Id,
                ProductName = product.Name,
                CurrentStock = stock.Quantity,
                RecommendedStock = 0,
                EstimatedMonthsOfSupply = decimal.MaxValue,
                SuggestedDiscountPercentage = 0.5m, // 50% discount for dead stock
                Recommendation = "No sales in the last 6 months. Consider significant discount, donation, or removal from inventory."
            };

            alerts.Add(alert);
        }

        _logger.LogInformation("Generated {Count} overstock alerts for shop {ShopId}", alerts.Count, shopId);

        return alerts.OrderByDescending(a => a.EstimatedMonthsOfSupply).ToList();
    }

    public async Task<ExpiryRiskAlert[]> GetExpiryRiskAlertsAsync(Guid shopId, int daysAhead = 60)
    {
        _logger.LogInformation("Getting expiry risk alerts for shop {ShopId} for {DaysAhead} days ahead", 
            shopId, daysAhead);

        // Delegate to AI analytics engine for expiry risk analysis
        var alerts = await _aiAnalyticsEngine.GetExpiryRiskAlertsAsync(shopId);
        
        // Filter by days ahead threshold
        var filteredAlerts = alerts.Where(a => a.DaysUntilExpiry <= daysAhead).ToArray();

        _logger.LogInformation("Generated {Count} expiry risk alerts for shop {ShopId}", 
            filteredAlerts.Length, shopId);

        return filteredAlerts;
    }

    public async Task<List<SeasonalRecommendation>> GetSeasonalRecommendationsAsync(Guid shopId, int seasonMonthsAhead = 1)
    {
        _logger.LogInformation("Getting seasonal recommendations for shop {ShopId} for {MonthsAhead} months ahead", 
            shopId, seasonMonthsAhead);

        var shop = await _shopRepository.GetShopWithBusinessAsync(shopId);
        if (shop == null)
        {
            throw new ArgumentException($"Shop with ID {shopId} not found");
        }

        var recommendations = new List<SeasonalRecommendation>();
        var targetMonth = DateTime.UtcNow.AddMonths(seasonMonthsAhead).Month;
        var targetSeason = GetSeasonFromMonth(targetMonth);

        // Generate business type-specific seasonal recommendations
        switch (shop.Business.Type)
        {
            case BusinessType.Pharmacy:
                recommendations.AddRange(await GeneratePharmacySeasonalRecommendations(shopId, targetSeason));
                break;
                
            case BusinessType.Grocery:
                recommendations.AddRange(await GenerateGrocerySeasonalRecommendations(shopId, targetSeason));
                break;
                
            case BusinessType.SuperShop:
                recommendations.AddRange(await GenerateSuperShopSeasonalRecommendations(shopId, targetSeason));
                break;
                
            default:
                recommendations.AddRange(await GenerateGeneralSeasonalRecommendations(shopId, targetSeason));
                break;
        }

        _logger.LogInformation("Generated {Count} seasonal recommendations for shop {ShopId}", 
            recommendations.Count, shopId);

        return recommendations;
    }

    public async Task<InventoryTurnoverAnalysis> AnalyzeInventoryTurnoverAsync(Guid shopId, int analysisMonths = 6)
    {
        _logger.LogInformation("Analyzing inventory turnover for shop {ShopId} for {Months} months", 
            shopId, analysisMonths);

        var shop = await _shopRepository.GetShopWithBusinessAsync(shopId);
        if (shop == null)
        {
            throw new ArgumentException($"Shop with ID {shopId} not found");
        }

        var analysis = new InventoryTurnoverAnalysis
        {
            ShopId = shopId,
            AnalysisMonths = analysisMonths
        };

        // Get inventory and sales data
        var inventory = await _stockRepository.GetStockByShopAsync(shopId);
        var products = await _productRepository.GetProductsByShopAsync(shopId);
        var salesData = await _saleRepository.GetSalesByShopAndDateRangeAsync(
            shopId, DateTime.UtcNow.AddMonths(-analysisMonths), DateTime.UtcNow);

        var productInsights = new List<ProductTurnoverInsight>();
        var turnoverRates = new List<double>();

        foreach (var product in products.Where(p => p.IsActive))
        {
            var stock = inventory.FirstOrDefault(s => s.ProductId == product.Id);
            var currentStock = stock?.Quantity ?? 0;
            
            if (currentStock <= 0) continue;

            // Calculate sales for this product
            var productSales = salesData.SelectMany(s => s.Items)
                .Where(i => i.ProductId == product.Id)
                .Sum(i => i.Quantity);

            // Calculate turnover rate (sales / average inventory)
            var averageInventory = currentStock; // Simplified - in production would use historical averages
            var turnoverRate = averageInventory > 0 ? (double)productSales / averageInventory : 0;
            
            var daysOfSupply = turnoverRate > 0 ? (int)(averageInventory / (productSales / (analysisMonths * 30.0))) : int.MaxValue;
            var inventoryValue = currentStock * product.UnitPrice;
            
            var insight = new ProductTurnoverInsight
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Category = product.Category ?? "Unknown",
                TurnoverRate = turnoverRate,
                DaysOfSupply = daysOfSupply,
                InventoryValue = inventoryValue,
                TurnoverCategory = ClassifyTurnoverRate(turnoverRate),
                Recommendations = GenerateTurnoverRecommendations(turnoverRate, daysOfSupply)
            };

            productInsights.Add(insight);
            if (turnoverRate > 0) turnoverRates.Add(turnoverRate);
        }

        analysis.ProductInsights = productInsights;
        analysis.AverageTurnoverRate = turnoverRates.Any() ? turnoverRates.Average() : 0;
        
        // Classify products
        analysis.SlowMovingProducts = productInsights
            .Where(p => p.TurnoverCategory == TurnoverCategory.Slow || p.TurnoverCategory == TurnoverCategory.Dead)
            .Select(p => new ProductInsight
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                Category = p.Category,
                PerformanceScore = p.TurnoverRate
            })
            .ToList();

        analysis.FastMovingProducts = productInsights
            .Where(p => p.TurnoverCategory == TurnoverCategory.Fast)
            .Select(p => new ProductInsight
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                Category = p.Category,
                PerformanceScore = p.TurnoverRate
            })
            .ToList();

        // Generate overall recommendations
        analysis.Recommendations = GenerateOverallTurnoverRecommendations(analysis);

        _logger.LogInformation("Completed inventory turnover analysis for shop {ShopId}. Average turnover: {Rate:F2}", 
            shopId, analysis.AverageTurnoverRate);

        return analysis;
    }

    public async Task<InventoryRecommendations> GetComprehensiveInventoryRecommendationsAsync(Guid shopId)
    {
        _logger.LogInformation("Getting comprehensive inventory recommendations for shop {ShopId}", shopId);

        // Use AI analytics engine for comprehensive analysis
        var recommendations = await _aiAnalyticsEngine.GenerateInventoryRecommendationsAsync(shopId);

        _logger.LogInformation("Generated comprehensive inventory recommendations for shop {ShopId}. " +
                              "Reorder: {ReorderCount}, Overstock: {OverstockCount}, Expiry: {ExpiryCount}",
            shopId, recommendations.ReorderSuggestions.Count, 
            recommendations.OverstockAlerts.Count, recommendations.ExpiryRisks.Count);

        return recommendations;
    }

    public async Task<SafetyStockRecommendation> CalculateSafetyStockAsync(Guid shopId, Guid productId, double serviceLevel = 0.95)
    {
        _logger.LogInformation("Calculating safety stock for product {ProductId} in shop {ShopId} with service level {ServiceLevel}", 
            productId, shopId, serviceLevel);

        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null || product.ShopId != shopId)
        {
            throw new ArgumentException($"Product with ID {productId} not found in shop {shopId}");
        }

        // Get sales data for demand analysis
        var salesData = await _saleRepository.GetSalesByShopAndDateRangeAsync(
            shopId, DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow);

        var productSales = salesData.SelectMany(s => s.Items)
            .Where(i => i.ProductId == productId)
            .GroupBy(i => i.Sale.CreatedAt.Date)
            .Select(g => g.Sum(i => i.Quantity))
            .ToList();

        if (!productSales.Any())
        {
            return new SafetyStockRecommendation
            {
                ProductId = productId,
                ProductName = product.Name,
                CurrentSafetyStock = 0,
                RecommendedSafetyStock = 0,
                ServiceLevel = serviceLevel,
                DemandVariability = 0,
                LeadTimeDays = 7, // Default lead time
                Reasoning = "No sales history available for safety stock calculation",
                ConfidenceScore = 0
            };
        }

        // Calculate demand statistics
        var averageDailyDemand = productSales.Average();
        var demandVariability = CalculateStandardDeviation(productSales);
        var leadTimeDays = 7; // Simplified - in production would be configurable per supplier
        
        // Calculate safety stock using standard formula: Z * σ * √LT
        var zScore = GetZScoreForServiceLevel(serviceLevel);
        var safetyStock = (int)(zScore * demandVariability * Math.Sqrt(leadTimeDays));
        
        var recommendation = new SafetyStockRecommendation
        {
            ProductId = productId,
            ProductName = product.Name,
            CurrentSafetyStock = 0, // Would need to be stored in product configuration
            RecommendedSafetyStock = Math.Max(0, safetyStock),
            ServiceLevel = serviceLevel,
            DemandVariability = demandVariability,
            LeadTimeDays = leadTimeDays,
            Reasoning = $"Based on {productSales.Count} days of sales data. " +
                       $"Average daily demand: {averageDailyDemand:F1}, " +
                       $"Demand variability: {demandVariability:F1}",
            ConfidenceScore = CalculateConfidenceScore(productSales.Sum(), productSales.Count)
        };

        _logger.LogInformation("Calculated safety stock for product {ProductId}: {SafetyStock} units", 
            productId, recommendation.RecommendedSafetyStock);

        return recommendation;
    }

    public async Task<InventoryValueAnalysis> AnalyzeInventoryValueAsync(Guid shopId)
    {
        _logger.LogInformation("Analyzing inventory value for shop {ShopId}", shopId);

        var shop = await _shopRepository.GetShopWithBusinessAsync(shopId);
        if (shop == null)
        {
            throw new ArgumentException($"Shop with ID {shopId} not found");
        }

        var analysis = new InventoryValueAnalysis
        {
            ShopId = shopId
        };

        // Get inventory and turnover analysis
        var inventory = await _stockRepository.GetStockByShopAsync(shopId);
        var products = await _productRepository.GetProductsByShopAsync(shopId);
        var turnoverAnalysis = await AnalyzeInventoryTurnoverAsync(shopId);

        var categoryBreakdown = new Dictionary<string, CategoryValueInsight>();
        decimal totalValue = 0;
        decimal fastMovingValue = 0;
        decimal slowMovingValue = 0;
        decimal deadStockValue = 0;

        foreach (var stock in inventory.Where(s => s.Quantity > 0))
        {
            var product = products.FirstOrDefault(p => p.Id == stock.ProductId);
            if (product == null || !product.IsActive) continue;

            var value = stock.Quantity * product.UnitPrice;
            totalValue += value;

            var category = product.Category ?? "Unknown";
            if (!categoryBreakdown.ContainsKey(category))
            {
                categoryBreakdown[category] = new CategoryValueInsight
                {
                    CategoryName = category,
                    TotalValue = 0,
                    ProductCount = 0,
                    AverageTurnoverRate = 0
                };
            }

            categoryBreakdown[category].TotalValue += value;
            categoryBreakdown[category].ProductCount++;

            // Classify by turnover
            var turnoverInsight = turnoverAnalysis.ProductInsights.FirstOrDefault(p => p.ProductId == product.Id);
            if (turnoverInsight != null)
            {
                switch (turnoverInsight.TurnoverCategory)
                {
                    case TurnoverCategory.Fast:
                        fastMovingValue += value;
                        break;
                    case TurnoverCategory.Slow:
                        slowMovingValue += value;
                        break;
                    case TurnoverCategory.Dead:
                        deadStockValue += value;
                        break;
                }
            }
        }

        // Calculate category percentages and average turnover rates
        foreach (var category in categoryBreakdown.Values)
        {
            category.PercentageOfTotal = totalValue > 0 ? (category.TotalValue / totalValue) * 100 : 0;
            
            var categoryProducts = turnoverAnalysis.ProductInsights
                .Where(p => p.Category == category.CategoryName);
            category.AverageTurnoverRate = categoryProducts.Any() ? 
                categoryProducts.Average(p => p.TurnoverRate) : 0;
        }

        analysis.TotalInventoryValue = totalValue;
        analysis.FastMovingValue = fastMovingValue;
        analysis.SlowMovingValue = slowMovingValue;
        analysis.DeadStockValue = deadStockValue;
        analysis.CategoryBreakdown = categoryBreakdown.Values.OrderByDescending(c => c.TotalValue).ToList();
        analysis.Recommendations = GenerateInventoryValueRecommendations(analysis);

        _logger.LogInformation("Completed inventory value analysis for shop {ShopId}. Total value: {Value:C}", 
            shopId, analysis.TotalInventoryValue);

        return analysis;
    }

    #region Private Helper Methods

    private ReorderPriority DeterminePriority(int daysUntilStockout)
    {
        return daysUntilStockout switch
        {
            <= 3 => ReorderPriority.Critical,
            <= 7 => ReorderPriority.High,
            <= 14 => ReorderPriority.Medium,
            _ => ReorderPriority.Low
        };
    }

    private int CalculateRecommendedOrderQuantity(double dailyVelocity, BusinessType businessType)
    {
        // Base calculation: 60 days supply
        var baseQuantity = (int)(dailyVelocity * 60);
        
        // Adjust based on business type
        var multiplier = businessType switch
        {
            BusinessType.Pharmacy => 1.2, // Higher safety stock for medicines
            BusinessType.Grocery => 0.8,  // Lower due to perishability
            BusinessType.SuperShop => 1.1, // Slightly higher for variety
            _ => 1.0
        };

        return Math.Max(1, (int)(baseQuantity * multiplier));
    }

    private double CalculateConfidenceScore(double totalSales, int dataPoints)
    {
        // Simple confidence calculation based on data volume
        if (dataPoints < 7) return 0.3;
        if (dataPoints < 30) return 0.6;
        if (totalSales < 10) return 0.4;
        return 0.8;
    }

    private decimal CalculateDiscountPercentage(double monthsOfSupply)
    {
        return monthsOfSupply switch
        {
            > 24 => 0.5m,  // 50% for very old stock
            > 12 => 0.3m,  // 30% for old stock
            > 9 => 0.2m,   // 20% for slow-moving
            _ => 0.1m      // 10% for moderate overstock
        };
    }

    private string GenerateOverstockRecommendation(double monthsOfSupply, decimal discountPercentage)
    {
        if (monthsOfSupply > 24)
            return $"Critical overstock situation. Apply {discountPercentage:P0} discount immediately or consider donation.";
        if (monthsOfSupply > 12)
            return $"Significant overstock. Apply {discountPercentage:P0} discount and review ordering patterns.";
        if (monthsOfSupply > 9)
            return $"Moderate overstock. Consider {discountPercentage:P0} promotional pricing.";
        
        return $"Monitor closely and consider {discountPercentage:P0} discount if situation persists.";
    }

    private string GetSeasonFromMonth(int month)
    {
        return month switch
        {
            12 or 1 or 2 => "Winter",
            3 or 4 or 5 => "Spring",
            6 or 7 or 8 => "Summer",
            9 or 10 or 11 => "Fall",
            _ => "Unknown"
        };
    }

    private async Task<List<SeasonalRecommendation>> GeneratePharmacySeasonalRecommendations(Guid shopId, string season)
    {
        var recommendations = new List<SeasonalRecommendation>();

        switch (season)
        {
            case "Winter":
                recommendations.Add(new SeasonalRecommendation
                {
                    Season = season,
                    ProductNames = new List<string> { "Cold Medicine", "Cough Syrup", "Throat Lozenges", "Vitamins", "Hand Sanitizer" },
                    ExpectedDemandIncrease = 0.4m,
                    Reasoning = "Winter season increases demand for cold, flu, and immunity-related medications"
                });
                break;
                
            case "Summer":
                recommendations.Add(new SeasonalRecommendation
                {
                    Season = season,
                    ProductNames = new List<string> { "Sunscreen", "Oral Rehydration Salts", "Anti-diarrheal", "Insect Repellent" },
                    ExpectedDemandIncrease = 0.3m,
                    Reasoning = "Summer season increases demand for sun protection and digestive health products"
                });
                break;
        }

        return recommendations;
    }

    private async Task<List<SeasonalRecommendation>> GenerateGrocerySeasonalRecommendations(Guid shopId, string season)
    {
        var recommendations = new List<SeasonalRecommendation>();

        switch (season)
        {
            case "Summer":
                recommendations.Add(new SeasonalRecommendation
                {
                    Season = season,
                    ProductNames = new List<string> { "Ice Cream", "Cold Drinks", "Fresh Fruits", "Salad Ingredients", "Frozen Foods" },
                    ExpectedDemandIncrease = 0.5m,
                    Reasoning = "Summer season increases demand for cooling products and fresh produce"
                });
                break;
                
            case "Winter":
                recommendations.Add(new SeasonalRecommendation
                {
                    Season = season,
                    ProductNames = new List<string> { "Hot Beverages", "Soup", "Warm Spices", "Preserved Foods" },
                    ExpectedDemandIncrease = 0.3m,
                    Reasoning = "Winter season increases demand for warming foods and beverages"
                });
                break;
        }

        return recommendations;
    }

    private async Task<List<SeasonalRecommendation>> GenerateSuperShopSeasonalRecommendations(Guid shopId, string season)
    {
        var recommendations = new List<SeasonalRecommendation>();
        
        // Combine pharmacy and grocery recommendations for super shops
        recommendations.AddRange(await GeneratePharmacySeasonalRecommendations(shopId, season));
        recommendations.AddRange(await GenerateGrocerySeasonalRecommendations(shopId, season));

        return recommendations;
    }

    private async Task<List<SeasonalRecommendation>> GenerateGeneralSeasonalRecommendations(Guid shopId, string season)
    {
        var recommendations = new List<SeasonalRecommendation>();

        switch (season)
        {
            case "Winter":
                recommendations.Add(new SeasonalRecommendation
                {
                    Season = season,
                    ProductNames = new List<string> { "Winter Clothing", "Heating Products", "Warm Accessories" },
                    ExpectedDemandIncrease = 0.2m,
                    Reasoning = "Winter season increases demand for warming products"
                });
                break;
                
            case "Summer":
                recommendations.Add(new SeasonalRecommendation
                {
                    Season = season,
                    ProductNames = new List<string> { "Summer Clothing", "Cooling Products", "Outdoor Equipment" },
                    ExpectedDemandIncrease = 0.2m,
                    Reasoning = "Summer season increases demand for cooling and outdoor products"
                });
                break;
        }

        return recommendations;
    }

    private TurnoverCategory ClassifyTurnoverRate(double turnoverRate)
    {
        return turnoverRate switch
        {
            > 12 => TurnoverCategory.Fast,    // More than 12 times per year
            > 4 => TurnoverCategory.Medium,   // 4-12 times per year
            > 0 => TurnoverCategory.Slow,     // Less than 4 times per year
            _ => TurnoverCategory.Dead        // No turnover
        };
    }

    private List<string> GenerateTurnoverRecommendations(double turnoverRate, int daysOfSupply)
    {
        var recommendations = new List<string>();

        switch (ClassifyTurnoverRate(turnoverRate))
        {
            case TurnoverCategory.Fast:
                recommendations.Add("Excellent performance. Ensure adequate stock levels to avoid stockouts.");
                recommendations.Add("Consider increasing order quantities to take advantage of bulk discounts.");
                break;
                
            case TurnoverCategory.Medium:
                recommendations.Add("Good performance. Monitor for optimization opportunities.");
                break;
                
            case TurnoverCategory.Slow:
                recommendations.Add("Slow-moving item. Consider promotional pricing or bundling.");
                recommendations.Add($"Current stock will last {daysOfSupply} days. Reduce future orders.");
                break;
                
            case TurnoverCategory.Dead:
                recommendations.Add("No sales activity. Consider significant discount or removal from inventory.");
                recommendations.Add("Investigate reasons for poor performance - pricing, placement, or demand issues.");
                break;
        }

        return recommendations;
    }

    private List<string> GenerateOverallTurnoverRecommendations(InventoryTurnoverAnalysis analysis)
    {
        var recommendations = new List<string>();

        if (analysis.AverageTurnoverRate < 4)
        {
            recommendations.Add("Overall inventory turnover is below optimal. Review product mix and pricing strategy.");
        }

        if (analysis.SlowMovingProducts.Count > analysis.FastMovingProducts.Count)
        {
            recommendations.Add("High proportion of slow-moving products. Focus on promotional activities and inventory reduction.");
        }

        var deadStockCount = analysis.ProductInsights.Count(p => p.TurnoverCategory == TurnoverCategory.Dead);
        if (deadStockCount > 0)
        {
            recommendations.Add($"{deadStockCount} products have no sales activity. Consider clearance strategies.");
        }

        return recommendations;
    }

    private List<string> GenerateInventoryValueRecommendations(InventoryValueAnalysis analysis)
    {
        var recommendations = new List<string>();

        var deadStockPercentage = analysis.TotalInventoryValue > 0 ? 
            (analysis.DeadStockValue / analysis.TotalInventoryValue) * 100 : 0;

        if (deadStockPercentage > 20)
        {
            recommendations.Add($"High dead stock value ({deadStockPercentage:F1}% of total). Implement aggressive clearance strategies.");
        }

        var slowMovingPercentage = analysis.TotalInventoryValue > 0 ? 
            (analysis.SlowMovingValue / analysis.TotalInventoryValue) * 100 : 0;

        if (slowMovingPercentage > 40)
        {
            recommendations.Add($"High slow-moving inventory ({slowMovingPercentage:F1}% of total). Review ordering patterns and consider promotions.");
        }

        // Identify top value categories for focus
        var topCategory = analysis.CategoryBreakdown.FirstOrDefault();
        if (topCategory != null && topCategory.PercentageOfTotal > 50)
        {
            recommendations.Add($"Category '{topCategory.CategoryName}' represents {topCategory.PercentageOfTotal:F1}% of inventory value. Focus optimization efforts here.");
        }

        return recommendations;
    }

    private double CalculateStandardDeviation(List<int> values)
    {
        if (values.Count < 2) return 0;

        var average = values.Average();
        var sumOfSquares = values.Sum(x => Math.Pow(x - average, 2));
        return Math.Sqrt(sumOfSquares / (values.Count - 1));
    }

    private double GetZScoreForServiceLevel(double serviceLevel)
    {
        // Simplified Z-score lookup for common service levels
        return serviceLevel switch
        {
            >= 0.99 => 2.33,
            >= 0.95 => 1.65,
            >= 0.90 => 1.28,
            >= 0.85 => 1.04,
            _ => 0.84
        };
    }

    #endregion
}