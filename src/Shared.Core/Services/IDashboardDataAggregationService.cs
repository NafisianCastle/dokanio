using Shared.Core.DTOs;

namespace Shared.Core.Services;

/// <summary>
/// Service interface for dashboard data aggregation operations
/// </summary>
public interface IDashboardDataAggregationService
{
    #region Sales Data Aggregation
    
    /// <summary>
    /// Aggregates sales data across multiple shops for a given period
    /// </summary>
    /// <param name="shopIds">Shop identifiers</param>
    /// <param name="period">Time period for aggregation</param>
    /// <returns>Aggregated sales data</returns>
    Task<SalesAggregationResult> AggregateSalesDataAsync(IEnumerable<Guid> shopIds, DateRange period);
    
    /// <summary>
    /// Calculates hourly sales distribution for performance analysis
    /// </summary>
    /// <param name="shopIds">Shop identifiers</param>
    /// <param name="date">Date for hourly analysis</param>
    /// <returns>Hourly sales distribution</returns>
    Task<IEnumerable<HourlySalesDistribution>> CalculateHourlySalesDistributionAsync(IEnumerable<Guid> shopIds, DateTime date);
    
    /// <summary>
    /// Aggregates product performance data across shops
    /// </summary>
    /// <param name="shopIds">Shop identifiers</param>
    /// <param name="period">Analysis period</param>
    /// <param name="topCount">Number of top products to return</param>
    /// <returns>Product performance aggregation</returns>
    Task<ProductPerformanceAggregation> AggregateProductPerformanceAsync(IEnumerable<Guid> shopIds, DateRange period, int topCount = 50);
    
    #endregion
    
    #region Inventory Data Aggregation
    
    /// <summary>
    /// Aggregates inventory status across multiple shops
    /// </summary>
    /// <param name="shopIds">Shop identifiers</param>
    /// <returns>Inventory aggregation result</returns>
    Task<InventoryAggregationResult> AggregateInventoryDataAsync(IEnumerable<Guid> shopIds);
    
    /// <summary>
    /// Calculates inventory turnover rates for shops
    /// </summary>
    /// <param name="shopIds">Shop identifiers</param>
    /// <param name="period">Analysis period</param>
    /// <returns>Inventory turnover data</returns>
    Task<IEnumerable<InventoryTurnoverData>> CalculateInventoryTurnoverAsync(IEnumerable<Guid> shopIds, DateRange period);
    
    /// <summary>
    /// Aggregates stock movement data for trend analysis
    /// </summary>
    /// <param name="shopIds">Shop identifiers</param>
    /// <param name="period">Analysis period</param>
    /// <returns>Stock movement aggregation</returns>
    Task<StockMovementAggregation> AggregateStockMovementAsync(IEnumerable<Guid> shopIds, DateRange period);
    
    #endregion
    
    #region Financial Data Aggregation
    
    /// <summary>
    /// Aggregates financial performance data across shops
    /// </summary>
    /// <param name="shopIds">Shop identifiers</param>
    /// <param name="period">Analysis period</param>
    /// <returns>Financial aggregation result</returns>
    Task<FinancialAggregationResult> AggregateFinancialDataAsync(IEnumerable<Guid> shopIds, DateRange period);
    
    /// <summary>
    /// Calculates profit margins by category and shop
    /// </summary>
    /// <param name="shopIds">Shop identifiers</param>
    /// <param name="period">Analysis period</param>
    /// <returns>Profit margin analysis</returns>
    Task<ProfitMarginAnalysis> CalculateProfitMarginsAsync(IEnumerable<Guid> shopIds, DateRange period);
    
    /// <summary>
    /// Aggregates payment method distribution data
    /// </summary>
    /// <param name="shopIds">Shop identifiers</param>
    /// <param name="period">Analysis period</param>
    /// <returns>Payment method distribution</returns>
    Task<PaymentMethodDistribution> AggregatePaymentMethodDataAsync(IEnumerable<Guid> shopIds, DateRange period);
    
    #endregion
    
    #region Performance Metrics Aggregation
    
    /// <summary>
    /// Calculates key performance indicators for shops
    /// </summary>
    /// <param name="shopIds">Shop identifiers</param>
    /// <param name="period">Analysis period</param>
    /// <returns>KPI aggregation result</returns>
    Task<KPIAggregationResult> CalculateKPIsAsync(IEnumerable<Guid> shopIds, DateRange period);
    
    /// <summary>
    /// Aggregates customer behavior data
    /// </summary>
    /// <param name="shopIds">Shop identifiers</param>
    /// <param name="period">Analysis period</param>
    /// <returns>Customer behavior aggregation</returns>
    Task<CustomerBehaviorAggregation> AggregateCustomerBehaviorAsync(IEnumerable<Guid> shopIds, DateRange period);
    
    /// <summary>
    /// Calculates operational efficiency metrics
    /// </summary>
    /// <param name="shopIds">Shop identifiers</param>
    /// <param name="period">Analysis period</param>
    /// <returns>Operational efficiency data</returns>
    Task<OperationalEfficiencyData> CalculateOperationalEfficiencyAsync(IEnumerable<Guid> shopIds, DateRange period);
    
    #endregion
    
    #region Trend Analysis
    
    /// <summary>
    /// Analyzes sales trends over time
    /// </summary>
    /// <param name="shopIds">Shop identifiers</param>
    /// <param name="period">Analysis period</param>
    /// <param name="granularity">Trend granularity (daily, weekly, monthly)</param>
    /// <returns>Sales trend analysis</returns>
    Task<SalesTrendAnalysis> AnalyzeSalesTrendsAsync(IEnumerable<Guid> shopIds, DateRange period, TrendGranularity granularity);
    
    /// <summary>
    /// Analyzes seasonal patterns in sales data
    /// </summary>
    /// <param name="shopIds">Shop identifiers</param>
    /// <param name="analysisYears">Number of years to analyze</param>
    /// <returns>Seasonal pattern analysis</returns>
    Task<SeasonalPatternAnalysis> AnalyzeSeasonalPatternsAsync(IEnumerable<Guid> shopIds, int analysisYears = 2);
    
    /// <summary>
    /// Compares performance between different time periods
    /// </summary>
    /// <param name="shopIds">Shop identifiers</param>
    /// <param name="currentPeriod">Current period</param>
    /// <param name="comparisonPeriod">Comparison period</param>
    /// <returns>Period comparison analysis</returns>
    Task<PeriodComparisonAnalysis> ComparePeriodPerformanceAsync(IEnumerable<Guid> shopIds, DateRange currentPeriod, DateRange comparisonPeriod);
    
    #endregion
    
    #region Data Quality and Validation
    
    /// <summary>
    /// Validates data quality for dashboard aggregations
    /// </summary>
    /// <param name="shopIds">Shop identifiers</param>
    /// <param name="period">Validation period</param>
    /// <returns>Data quality report</returns>
    Task<DataQualityReport> ValidateDataQualityAsync(IEnumerable<Guid> shopIds, DateRange period);
    
    /// <summary>
    /// Identifies data anomalies in sales and inventory data
    /// </summary>
    /// <param name="shopIds">Shop identifiers</param>
    /// <param name="period">Analysis period</param>
    /// <returns>Data anomaly report</returns>
    Task<DataAnomalyReport> DetectDataAnomaliesAsync(IEnumerable<Guid> shopIds, DateRange period);
    
    #endregion
}

/// <summary>
/// Supporting DTOs for data aggregation
/// </summary>

public class SalesAggregationResult
{
    public decimal TotalRevenue { get; set; }
    public int TotalTransactions { get; set; }
    public decimal AverageOrderValue { get; set; }
    public Dictionary<Guid, decimal> RevenueByShop { get; set; } = new();
    public Dictionary<string, decimal> RevenueByCategory { get; set; } = new();
    public List<DailySalesData> DailySales { get; set; } = new();
    public DateTime AggregatedAt { get; set; } = DateTime.UtcNow;
}

public class DailySalesData
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public Dictionary<Guid, decimal> ShopRevenues { get; set; } = new();
}

public class HourlySalesDistribution
{
    public int Hour { get; set; }
    public decimal Revenue { get; set; }
    public int TransactionCount { get; set; }
    public decimal PercentageOfDayRevenue { get; set; }
    public Dictionary<Guid, decimal> ShopContributions { get; set; } = new();
}

public class ProductPerformanceAggregation
{
    public List<ProductPerformanceData> TopPerformers { get; set; } = new();
    public List<ProductPerformanceData> LowPerformers { get; set; } = new();
    public Dictionary<string, CategoryPerformanceData> CategoryPerformance { get; set; } = new();
    public DateTime AggregatedAt { get; set; } = DateTime.UtcNow;
}

public class ProductPerformanceData
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int TotalQuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AveragePrice { get; set; }
    public Dictionary<Guid, int> QuantityByShop { get; set; } = new();
    public double PerformanceScore { get; set; }
}

public class CategoryPerformanceData
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public int TotalQuantitySold { get; set; }
    public int ProductCount { get; set; }
    public decimal AveragePrice { get; set; }
    public double MarketSharePercentage { get; set; }
}

public class InventoryAggregationResult
{
    public int TotalProducts { get; set; }
    public decimal TotalInventoryValue { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public int ExpiringProductsCount { get; set; }
    public Dictionary<Guid, ShopInventoryData> InventoryByShop { get; set; } = new();
    public Dictionary<string, CategoryInventoryData> InventoryByCategory { get; set; } = new();
    public DateTime AggregatedAt { get; set; } = DateTime.UtcNow;
}

public class ShopInventoryData
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public decimal InventoryValue { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public double InventoryTurnoverRate { get; set; }
}

public class CategoryInventoryData
{
    public string CategoryName { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public decimal TotalValue { get; set; }
    public int LowStockCount { get; set; }
    public double AverageTurnoverRate { get; set; }
}

public class InventoryTurnoverData
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public Dictionary<string, double> TurnoverByCategory { get; set; } = new();
    public double OverallTurnoverRate { get; set; }
    public List<ProductTurnoverData> SlowMovingProducts { get; set; } = new();
    public List<ProductTurnoverData> FastMovingProducts { get; set; } = new();
}

public class ProductTurnoverData
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double TurnoverRate { get; set; }
    public int DaysOfSupply { get; set; }
    public decimal CurrentStock { get; set; }
}

public class StockMovementAggregation
{
    public Dictionary<Guid, List<StockMovementData>> MovementsByShop { get; set; } = new();
    public Dictionary<string, StockMovementSummary> MovementsByCategory { get; set; } = new();
    public List<StockMovementTrend> MovementTrends { get; set; } = new();
    public DateTime AggregatedAt { get; set; } = DateTime.UtcNow;
}

public class StockMovementData
{
    public DateTime Date { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int QuantityIn { get; set; }
    public int QuantityOut { get; set; }
    public int NetMovement { get; set; }
    public string MovementType { get; set; } = string.Empty; // Sale, Purchase, Adjustment
}

public class StockMovementSummary
{
    public string CategoryName { get; set; } = string.Empty;
    public int TotalQuantityIn { get; set; }
    public int TotalQuantityOut { get; set; }
    public int NetMovement { get; set; }
    public double MovementVelocity { get; set; }
}

public class StockMovementTrend
{
    public DateTime Date { get; set; }
    public int TotalQuantityIn { get; set; }
    public int TotalQuantityOut { get; set; }
    public int NetMovement { get; set; }
    public TrendDirection Direction { get; set; }
}

public class FinancialAggregationResult
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalCosts { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal NetProfit { get; set; }
    public decimal ProfitMarginPercentage { get; set; }
    public Dictionary<Guid, ShopFinancialData> FinancialsByShop { get; set; } = new();
    public Dictionary<string, CategoryFinancialData> FinancialsByCategory { get; set; } = new();
    public List<DailyFinancialData> DailyFinancials { get; set; } = new();
    public DateTime AggregatedAt { get; set; } = DateTime.UtcNow;
}

public class ShopFinancialData
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Costs { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal ProfitMarginPercentage { get; set; }
    public decimal ROI { get; set; }
}

public class CategoryFinancialData
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Costs { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal ProfitMarginPercentage { get; set; }
    public double MarketSharePercentage { get; set; }
}

public class DailyFinancialData
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
    public decimal Costs { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal NetProfit { get; set; }
    public Dictionary<Guid, decimal> ProfitByShop { get; set; } = new();
}

public class ProfitMarginAnalysis
{
    public decimal OverallProfitMargin { get; set; }
    public Dictionary<string, decimal> MarginsByCategory { get; set; } = new();
    public Dictionary<Guid, decimal> MarginsByShop { get; set; } = new();
    public List<ProductMarginData> HighMarginProducts { get; set; } = new();
    public List<ProductMarginData> LowMarginProducts { get; set; } = new();
    public List<string> MarginImprovementSuggestions { get; set; } = new();
}

public class ProductMarginData
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
    public decimal Cost { get; set; }
    public decimal ProfitMargin { get; set; }
    public decimal ProfitMarginPercentage { get; set; }
    public int UnitsSold { get; set; }
}

public class PaymentMethodDistribution
{
    public Dictionary<string, PaymentMethodData> PaymentMethods { get; set; } = new();
    public Dictionary<Guid, Dictionary<string, decimal>> PaymentMethodsByShop { get; set; } = new();
    public List<PaymentTrendData> PaymentTrends { get; set; } = new();
    public DateTime AggregatedAt { get; set; } = DateTime.UtcNow;
}

public class PaymentMethodData
{
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageTransactionValue { get; set; }
    public double PercentageOfTotal { get; set; }
}

public class PaymentTrendData
{
    public DateTime Date { get; set; }
    public Dictionary<string, decimal> AmountsByPaymentMethod { get; set; } = new();
    public Dictionary<string, int> CountsByPaymentMethod { get; set; } = new();
}

public class KPIAggregationResult
{
    public Dictionary<string, decimal> KPIValues { get; set; } = new();
    public Dictionary<Guid, Dictionary<string, decimal>> KPIsByShop { get; set; } = new();
    public List<KPITrendData> KPITrends { get; set; } = new();
    public Dictionary<string, KPIBenchmark> KPIBenchmarks { get; set; } = new();
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

public class KPITrendData
{
    public DateTime Date { get; set; }
    public Dictionary<string, decimal> KPIValues { get; set; } = new();
    public TrendDirection OverallTrend { get; set; }
}

public class KPIBenchmark
{
    public string KPIName { get; set; } = string.Empty;
    public decimal CurrentValue { get; set; }
    public decimal BenchmarkValue { get; set; }
    public decimal VariancePercentage { get; set; }
    public PerformanceRating Rating { get; set; }
}

public class CustomerBehaviorAggregation
{
    public decimal AverageOrderValue { get; set; }
    public double AverageTransactionFrequency { get; set; }
    public Dictionary<int, int> TransactionsByHour { get; set; } = new();
    public Dictionary<DayOfWeek, decimal> RevenueByDayOfWeek { get; set; } = new();
    public List<CustomerSegmentData> CustomerSegments { get; set; } = new();
    public DateTime AggregatedAt { get; set; } = DateTime.UtcNow;
}

public class CustomerSegmentData
{
    public string SegmentName { get; set; } = string.Empty;
    public int CustomerCount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public double PurchaseFrequency { get; set; }
    public decimal TotalRevenue { get; set; }
    public double RevenuePercentage { get; set; }
}

public class OperationalEfficiencyData
{
    public Dictionary<Guid, ShopEfficiencyMetrics> EfficiencyByShop { get; set; } = new();
    public double OverallEfficiencyScore { get; set; }
    public List<EfficiencyRecommendation> Recommendations { get; set; } = new();
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

public class ShopEfficiencyMetrics
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public double SalesPerHour { get; set; }
    public double TransactionsPerHour { get; set; }
    public double InventoryTurnover { get; set; }
    public double StaffProductivity { get; set; }
    public double OverallEfficiencyScore { get; set; }
}

public class EfficiencyRecommendation
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string RecommendationType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal EstimatedImpact { get; set; }
    public string Priority { get; set; } = string.Empty;
}

public class SalesTrendAnalysis
{
    public TrendGranularity Granularity { get; set; }
    public List<SalesTrendPoint> TrendPoints { get; set; } = new();
    public TrendDirection OverallTrend { get; set; }
    public double TrendStrength { get; set; }
    public List<TrendInsight> Insights { get; set; } = new();
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

public class SalesTrendPoint
{
    public DateTime Period { get; set; }
    public decimal Revenue { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public TrendDirection Direction { get; set; }
    public double ChangePercentage { get; set; }
}

public class SeasonalPatternAnalysis
{
    public Dictionary<int, SeasonalData> MonthlyPatterns { get; set; } = new();
    public Dictionary<DayOfWeek, SeasonalData> WeeklyPatterns { get; set; } = new();
    public List<SeasonalInsight> SeasonalInsights { get; set; } = new();
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

public class SeasonalData
{
    public decimal AverageRevenue { get; set; }
    public int AverageTransactions { get; set; }
    public double SeasonalityIndex { get; set; }
    public double VariancePercentage { get; set; }
}

public class SeasonalInsight
{
    public string Pattern { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal ImpactValue { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

public class PeriodComparisonAnalysis
{
    public DateRange CurrentPeriod { get; set; } = new();
    public DateRange ComparisonPeriod { get; set; } = new();
    public ComparisonMetrics Metrics { get; set; } = new();
    public List<ComparisonInsight> Insights { get; set; } = new();
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

public class ComparisonMetrics
{
    public decimal RevenueChange { get; set; }
    public decimal RevenueChangePercentage { get; set; }
    public int TransactionChange { get; set; }
    public decimal TransactionChangePercentage { get; set; }
    public decimal AOVChange { get; set; }
    public decimal AOVChangePercentage { get; set; }
    public Dictionary<Guid, ShopComparisonMetrics> ShopComparisons { get; set; } = new();
}

public class ShopComparisonMetrics
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public decimal RevenueChange { get; set; }
    public decimal RevenueChangePercentage { get; set; }
    public int TransactionChange { get; set; }
    public decimal TransactionChangePercentage { get; set; }
}

public class ComparisonInsight
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public InsightType Type { get; set; }
    public decimal ImpactValue { get; set; }
    public List<Guid> AffectedShopIds { get; set; } = new();
}

public class DataQualityReport
{
    public double OverallQualityScore { get; set; }
    public int TotalRecords { get; set; }
    public int ValidRecords { get; set; }
    public Dictionary<string, int> MissingValueCounts { get; set; } = new();
    public Dictionary<string, DataTypeInfo> ColumnInfo { get; set; } = new();
    public Dictionary<string, DataQualityMetric> QualityMetrics { get; set; } = new();
    public List<DataQualityIssue> Issues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class DataQualityMetric
{
    public string MetricName { get; set; } = string.Empty;
    public double Score { get; set; }
    public int TotalRecords { get; set; }
    public int ValidRecords { get; set; }
    public int InvalidRecords { get; set; }
    public double CompletenessPercentage { get; set; }
}

public class DataQualityIssue
{
    public DataQualityIssueType Type { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
    public int AffectedRecords { get; set; }
    public DataQualityIssueSeverity Severity { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
    public List<Guid> AffectedShopIds { get; set; } = new();
}

public class DataAnomalyReport
{
    public List<DataAnomaly> Anomalies { get; set; } = new();
    public Dictionary<Guid, int> AnomaliesByShop { get; set; } = new();
    public List<string> AnomalyPatterns { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class DataAnomaly
{
    public string AnomalyType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public decimal AnomalyValue { get; set; }
    public decimal ExpectedValue { get; set; }
    public double DeviationPercentage { get; set; }
    public string Severity { get; set; } = string.Empty;
}

public enum TrendGranularity
{
    Hourly,
    Daily,
    Weekly,
    Monthly,
    Quarterly,
    Yearly
}

/// <summary>
/// Information about a data column
/// </summary>
public class DataTypeInfo
{
    public string DataType { get; set; } = string.Empty;
    public int NonNullCount { get; set; }
    public int NullCount { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public double? Mean { get; set; }
    public double? StandardDeviation { get; set; }
    public List<object> UniqueValues { get; set; } = new();
}

/// <summary>
/// Data quality issue types
/// </summary>
public enum DataQualityIssueType
{
    MissingValues,
    Outliers,
    InconsistentFormat,
    DuplicateRecords,
    InvalidValues,
    DataTypeInconsistency
}

/// <summary>
/// Data quality issue severity levels
/// </summary>
public enum DataQualityIssueSeverity
{
    Low,
    Medium,
    High,
    Critical
}