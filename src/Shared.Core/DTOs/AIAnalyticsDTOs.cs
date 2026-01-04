using Shared.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Shared.Core.DTOs;

/// <summary>
/// Date range for analytics queries
/// </summary>
public class DateRange
{
    [Required]
    public DateTime StartDate { get; set; }
    
    [Required]
    public DateTime EndDate { get; set; }
    
    public bool IsValid => StartDate <= EndDate;
    
    public TimeSpan Duration => EndDate - StartDate;
}

/// <summary>
/// Sales insights from AI analysis
/// </summary>
public class SalesInsights
{
    public Guid BusinessId { get; set; }
    public DateRange AnalysisPeriod { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int TotalTransactions { get; set; }
    public List<SalesTrend> Trends { get; set; } = new();
    public List<ProductInsight> TopProducts { get; set; } = new();
    public List<ProductInsight> LowPerformingProducts { get; set; } = new();
    public List<PeakTime> PeakSalesTimes { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Sales trend data point
/// </summary>
public class SalesTrend
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public TrendDirection Direction { get; set; }
    public double ConfidenceScore { get; set; }
}

/// <summary>
/// Product performance insight
/// </summary>
public class ProductInsight
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public decimal ProfitMargin { get; set; }
    public double PerformanceScore { get; set; }
    public TrendDirection Trend { get; set; }
    public List<string> Insights { get; set; } = new();
}

/// <summary>
/// Peak sales time analysis
/// </summary>
public class PeakTime
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public decimal AverageRevenue { get; set; }
    public int AverageTransactions { get; set; }
    public double IntensityScore { get; set; }
}

/// <summary>
/// Inventory recommendations from AI analysis
/// </summary>
public class InventoryRecommendations
{
    public Guid ShopId { get; set; }
    public BusinessType BusinessType { get; set; }
    public List<ReorderRecommendation> ReorderSuggestions { get; set; } = new();
    public List<OverstockAlert> OverstockAlerts { get; set; } = new();
    public List<ExpiryRiskAlert> ExpiryRisks { get; set; } = new();
    public List<SeasonalRecommendation> SeasonalRecommendations { get; set; } = new();
    public List<string> GeneralRecommendations { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Reorder recommendation for a product
/// </summary>
public class ReorderRecommendation
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int RecommendedOrderQuantity { get; set; }
    public int PredictedDaysUntilStockout { get; set; }
    public decimal EstimatedMonthlySales { get; set; }
    public ReorderPriority Priority { get; set; }
    public double ConfidenceScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

/// <summary>
/// Overstock alert for slow-moving inventory
/// </summary>
public class OverstockAlert
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int RecommendedStock { get; set; }
    public decimal EstimatedMonthsOfSupply { get; set; }
    public decimal SuggestedDiscountPercentage { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}

/// <summary>
/// Expiry risk alert for products approaching expiration
/// </summary>
public class ExpiryRiskAlert
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public int DaysUntilExpiry { get; set; }
    public int QuantityAtRisk { get; set; }
    public decimal ValueAtRisk { get; set; }
    public ExpiryRiskLevel RiskLevel { get; set; }
    public List<string> RecommendedActions { get; set; } = new();
}

/// <summary>
/// Seasonal recommendation based on historical patterns
/// </summary>
public class SeasonalRecommendation
{
    public string Season { get; set; } = string.Empty;
    public List<Guid> RecommendedProductIds { get; set; } = new();
    public List<string> ProductNames { get; set; } = new();
    public decimal ExpectedDemandIncrease { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

/// <summary>
/// Product recommendations for cross-sell and up-sell
/// </summary>
public class ProductRecommendations
{
    public Guid ShopId { get; set; }
    public Guid? CustomerId { get; set; }
    public List<AIProductRecommendation> CrossSellRecommendations { get; set; } = new();
    public List<AIProductRecommendation> UpSellRecommendations { get; set; } = new();
    public List<ProductBundle> BundleRecommendations { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Individual AI product recommendation
/// </summary>
public class AIProductRecommendation
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public double RelevanceScore { get; set; }
    public RecommendationType Type { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public List<Guid> BasedOnProducts { get; set; } = new();
}

/// <summary>
/// Product bundle recommendation
/// </summary>
public class ProductBundle
{
    public string BundleName { get; set; } = string.Empty;
    public List<Guid> ProductIds { get; set; } = new();
    public List<string> ProductNames { get; set; } = new();
    public decimal IndividualPrice { get; set; }
    public decimal BundlePrice { get; set; }
    public decimal SavingsAmount { get; set; }
    public double RelevanceScore { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Price optimization suggestions
/// </summary>
public class PriceOptimizationSuggestions
{
    public Guid BusinessId { get; set; }
    public List<PriceOptimization> Optimizations { get; set; } = new();
    public List<DemandElasticityInsight> DemandInsights { get; set; } = new();
    public List<CompetitiveAnalysis> CompetitiveInsights { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Price optimization for a specific product
/// </summary>
public class PriceOptimization
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal RecommendedPrice { get; set; }
    public decimal ExpectedRevenueChange { get; set; }
    public decimal ExpectedVolumeChange { get; set; }
    public double ConfidenceScore { get; set; }
    public PriceOptimizationType OptimizationType { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

/// <summary>
/// Demand elasticity insight for pricing decisions
/// </summary>
public class DemandElasticityInsight
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public double ElasticityCoefficient { get; set; }
    public DemandElasticity ElasticityType { get; set; }
    public string Interpretation { get; set; } = string.Empty;
}

/// <summary>
/// Competitive analysis insight
/// </summary>
public class CompetitiveAnalysis
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal OurPrice { get; set; }
    public decimal MarketAveragePrice { get; set; }
    public decimal CompetitivePosition { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}

/// <summary>
/// AI model training data
/// </summary>
public class AIModelData
{
    public Guid BusinessId { get; set; }
    public AIDataType[] DataTypes { get; set; } = Array.Empty<AIDataType>();
    public Dictionary<string, object> ProcessedData { get; set; } = new();
    public DataQualityMetrics QualityMetrics { get; set; } = new();
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Data quality metrics for AI model training
/// </summary>
public class DataQualityMetrics
{
    public int TotalRecords { get; set; }
    public int ValidRecords { get; set; }
    public int MissingValueCount { get; set; }
    public int OutlierCount { get; set; }
    public double CompletenessScore { get; set; }
    public double AccuracyScore { get; set; }
    public List<string> QualityIssues { get; set; } = new();
}

/// <summary>
/// Sales forecast with confidence intervals
/// </summary>
public class SalesForecast
{
    public Guid ShopId { get; set; }
    public DateRange ForecastPeriod { get; set; } = new();
    public List<SalesForecastPoint> ForecastPoints { get; set; } = new();
    public ForecastAccuracy AccuracyMetrics { get; set; } = new();
    public List<string> Assumptions { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Individual forecast data point
/// </summary>
public class SalesForecastPoint
{
    public DateTime Date { get; set; }
    public decimal PredictedRevenue { get; set; }
    public decimal LowerBound { get; set; }
    public decimal UpperBound { get; set; }
    public int PredictedTransactions { get; set; }
    public double ConfidenceLevel { get; set; }
}

/// <summary>
/// Forecast accuracy metrics
/// </summary>
public class ForecastAccuracy
{
    public double MeanAbsoluteError { get; set; }
    public double MeanAbsolutePercentageError { get; set; }
    public double RootMeanSquareError { get; set; }
    public double R2Score { get; set; }
}

/// <summary>
/// Product performance analysis
/// </summary>
public class ProductPerformanceAnalysis
{
    public Guid BusinessId { get; set; }
    public DateRange AnalysisPeriod { get; set; } = new();
    public List<ProductInsight> BestSellers { get; set; } = new();
    public List<ProductInsight> LowPerformers { get; set; } = new();
    public List<ProductInsight> TrendingProducts { get; set; } = new();
    public List<CategoryPerformance> CategoryAnalysis { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Category performance analysis
/// </summary>
public class CategoryPerformance
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public int TotalQuantitySold { get; set; }
    public decimal AveragePrice { get; set; }
    public double MarketShare { get; set; }
    public TrendDirection Trend { get; set; }
}

/// <summary>
/// Peak time analysis results
/// </summary>
public class PeakTimeAnalysis
{
    public Guid BusinessId { get; set; }
    public DateRange AnalysisPeriod { get; set; } = new();
    public List<PeakTime> DailyPeaks { get; set; } = new();
    public List<PeakTime> WeeklyPeaks { get; set; } = new();
    public List<PeakTime> MonthlyPeaks { get; set; } = new();
    public List<string> StaffingRecommendations { get; set; } = new();
    public List<string> InventoryRecommendations { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Enums for AI analytics
/// </summary>
public enum TrendDirection
{
    Increasing,
    Decreasing,
    Stable,
    Volatile
}

public enum ReorderPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum ExpiryRiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum RecommendationType
{
    CrossSell,
    UpSell,
    Substitute,
    Complementary
}

public enum PriceOptimizationType
{
    IncreasePrice,
    DecreasePrice,
    DynamicPricing,
    BundlePricing
}

public enum DemandElasticity
{
    Elastic,
    Inelastic,
    UnitElastic
}

public enum AIDataType
{
    SalesData,
    InventoryData,
    CustomerData,
    ProductData,
    PricingData,
    SeasonalData
}