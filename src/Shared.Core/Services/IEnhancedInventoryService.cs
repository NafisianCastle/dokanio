using Shared.Core.DTOs;
using Shared.Core.Entities;

namespace Shared.Core.Services;

/// <summary>
/// Enhanced inventory service with AI-powered recommendations
/// Extends the basic inventory service with intelligent features
/// </summary>
public interface IEnhancedInventoryService : IInventoryService
{
    /// <summary>
    /// Predicts low-stock situations based on sales patterns and lead times
    /// </summary>
    /// <param name="shopId">The shop to analyze</param>
    /// <param name="daysAhead">Number of days to predict ahead (default 30)</param>
    /// <returns>List of products predicted to go out of stock</returns>
    Task<List<ReorderRecommendation>> PredictLowStockAsync(Guid shopId, int daysAhead = 30);

    /// <summary>
    /// Recommends reorder quantities based on historical consumption and seasonal trends
    /// </summary>
    /// <param name="shopId">The shop to generate recommendations for</param>
    /// <param name="productId">Optional specific product ID, if null analyzes all products</param>
    /// <returns>List of reorder recommendations with quantities and reasoning</returns>
    Task<List<ReorderRecommendation>> GetReorderRecommendationsAsync(Guid shopId, Guid? productId = null);

    /// <summary>
    /// Identifies over-stocked items that may require promotional pricing
    /// </summary>
    /// <param name="shopId">The shop to analyze</param>
    /// <param name="monthsOfSupplyThreshold">Threshold for months of supply to consider overstock (default 6)</param>
    /// <returns>List of overstock alerts with recommendations</returns>
    Task<List<OverstockAlert>> GetOverstockAlertsAsync(Guid shopId, double monthsOfSupplyThreshold = 6.0);

    /// <summary>
    /// Provides expiry risk alerts for products approaching expiration (pharmacy-specific)
    /// </summary>
    /// <param name="shopId">The shop to check for expiry risks</param>
    /// <param name="daysAhead">Number of days ahead to check for expiry (default 60)</param>
    /// <returns>Array of expiry risk alerts with recommendations</returns>
    Task<ExpiryRiskAlert[]> GetExpiryRiskAlertsAsync(Guid shopId, int daysAhead = 60);

    /// <summary>
    /// Generates seasonal inventory recommendations based on business type and historical patterns
    /// </summary>
    /// <param name="shopId">The shop to generate recommendations for</param>
    /// <param name="seasonMonthsAhead">Number of months ahead to prepare for seasonal changes (default 1)</param>
    /// <returns>List of seasonal recommendations</returns>
    Task<List<SeasonalRecommendation>> GetSeasonalRecommendationsAsync(Guid shopId, int seasonMonthsAhead = 1);

    /// <summary>
    /// Calculates inventory turnover rates and identifies slow-moving items
    /// </summary>
    /// <param name="shopId">The shop to analyze</param>
    /// <param name="analysisMonths">Number of months to analyze (default 6)</param>
    /// <returns>Inventory turnover analysis with recommendations</returns>
    Task<InventoryTurnoverAnalysis> AnalyzeInventoryTurnoverAsync(Guid shopId, int analysisMonths = 6);

    /// <summary>
    /// Provides comprehensive inventory recommendations combining all AI insights
    /// </summary>
    /// <param name="shopId">The shop to generate recommendations for</param>
    /// <returns>Complete inventory recommendations with all insights</returns>
    Task<InventoryRecommendations> GetComprehensiveInventoryRecommendationsAsync(Guid shopId);

    /// <summary>
    /// Calculates optimal safety stock levels based on demand variability and lead times
    /// </summary>
    /// <param name="shopId">The shop to analyze</param>
    /// <param name="productId">The product to calculate safety stock for</param>
    /// <param name="serviceLevel">Desired service level (0.95 = 95% service level)</param>
    /// <returns>Recommended safety stock level</returns>
    Task<SafetyStockRecommendation> CalculateSafetyStockAsync(Guid shopId, Guid productId, double serviceLevel = 0.95);

    /// <summary>
    /// Analyzes inventory value and provides insights on capital tied up in stock
    /// </summary>
    /// <param name="shopId">The shop to analyze</param>
    /// <returns>Inventory value analysis with recommendations</returns>
    Task<InventoryValueAnalysis> AnalyzeInventoryValueAsync(Guid shopId);
}

/// <summary>
/// Inventory turnover analysis results
/// </summary>
public class InventoryTurnoverAnalysis
{
    public Guid ShopId { get; set; }
    public int AnalysisMonths { get; set; }
    public List<ProductTurnoverInsight> ProductInsights { get; set; } = new();
    public double AverageTurnoverRate { get; set; }
    public List<ProductInsight> SlowMovingProducts { get; set; } = new();
    public List<ProductInsight> FastMovingProducts { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Product turnover insight
/// </summary>
public class ProductTurnoverInsight
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double TurnoverRate { get; set; }
    public int DaysOfSupply { get; set; }
    public decimal InventoryValue { get; set; }
    public TurnoverCategory TurnoverCategory { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Safety stock recommendation
/// </summary>
public class SafetyStockRecommendation
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int CurrentSafetyStock { get; set; }
    public int RecommendedSafetyStock { get; set; }
    public double ServiceLevel { get; set; }
    public double DemandVariability { get; set; }
    public int LeadTimeDays { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
}

/// <summary>
/// Inventory value analysis
/// </summary>
public class InventoryValueAnalysis
{
    public Guid ShopId { get; set; }
    public decimal TotalInventoryValue { get; set; }
    public decimal FastMovingValue { get; set; }
    public decimal SlowMovingValue { get; set; }
    public decimal DeadStockValue { get; set; }
    public List<CategoryValueInsight> CategoryBreakdown { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Category value insight
/// </summary>
public class CategoryValueInsight
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal TotalValue { get; set; }
    public decimal PercentageOfTotal { get; set; }
    public double AverageTurnoverRate { get; set; }
    public int ProductCount { get; set; }
}

/// <summary>
/// Turnover category classification
/// </summary>
public enum TurnoverCategory
{
    Fast,       // High turnover, good performance
    Medium,     // Average turnover
    Slow,       // Low turnover, needs attention
    Dead        // No movement, consider removal
}