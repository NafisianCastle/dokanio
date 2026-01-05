using Shared.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Shared.Core.DTOs;

/// <summary>
/// Dashboard overview data for business owners
/// </summary>
public class DashboardOverview
{
    public Guid BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public BusinessType BusinessType { get; set; }
    public int TotalShops { get; set; }
    public RealTimeSalesData RealTimeSales { get; set; } = new();
    public InventoryStatusSummary InventoryStatus { get; set; } = new();
    public RevenueTrendData RevenueTrends { get; set; } = new();
    public List<ShopPerformanceSummary> ShopPerformances { get; set; } = new();
    public List<AlertSummary> Alerts { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Real-time sales monitoring data
/// </summary>
public class RealTimeSalesData
{
    public decimal TodayRevenue { get; set; }
    public int TodayTransactionCount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal YesterdayRevenue { get; set; }
    public decimal RevenueChangePercentage { get; set; }
    public int TransactionChangePercentage { get; set; }
    public List<HourlySalesData> HourlySales { get; set; } = new();
    public List<TopSellingProduct> TopProducts { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Hourly sales data for real-time monitoring
/// </summary>
public class HourlySalesData
{
    public DateTime Hour { get; set; }
    public decimal Revenue { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageOrderValue { get; set; }
}

/// <summary>
/// Top selling product information
/// </summary>
public class TopSellingProduct
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
}

/// <summary>
/// Inventory status summary across all shops
/// </summary>
public class InventoryStatusSummary
{
    public int TotalProducts { get; set; }
    public int LowStockProducts { get; set; }
    public int OutOfStockProducts { get; set; }
    public int ExpiringProducts { get; set; }
    public decimal TotalInventoryValue { get; set; }
    public List<LowStockAlert> LowStockAlerts { get; set; } = new();
    public List<ExpiryAlert> ExpiryAlerts { get; set; } = new();
    public List<ShopInventoryStatus> ShopInventoryStatuses { get; set; } = new();
}

/// <summary>
/// Low stock alert information
/// </summary>
public class LowStockAlert
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int LowStockThreshold { get; set; }
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public AlertPriority Priority { get; set; }
}

/// <summary>
/// Product expiry alert information
/// </summary>
public class ExpiryAlert
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public int DaysUntilExpiry { get; set; }
    public int Quantity { get; set; }
    public decimal Value { get; set; }
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public AlertPriority Priority { get; set; }
}

/// <summary>
/// Shop-specific inventory status
/// </summary>
public class ShopInventoryStatus
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public int TotalProducts { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public int ExpiringCount { get; set; }
    public decimal InventoryValue { get; set; }
}

/// <summary>
/// Revenue trend analysis data
/// </summary>
public class RevenueTrendData
{
    public DateRange Period { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public decimal PreviousPeriodRevenue { get; set; }
    public decimal RevenueGrowthPercentage { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int TotalTransactions { get; set; }
    public List<DailyRevenueData> DailyRevenues { get; set; } = new();
    public List<MonthlyRevenueData> MonthlyRevenues { get; set; } = new();
    public ProfitAnalysis ProfitAnalysis { get; set; } = new();
}

/// <summary>
/// Daily revenue data point
/// </summary>
public class DailyRevenueData
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal ProfitEstimate { get; set; }
}

/// <summary>
/// Monthly revenue data point
/// </summary>
public class MonthlyRevenueData
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal ProfitEstimate { get; set; }
    public decimal GrowthPercentage { get; set; }
}

/// <summary>
/// Profit analysis data
/// </summary>
public class ProfitAnalysis
{
    public decimal TotalRevenue { get; set; }
    public decimal EstimatedCosts { get; set; }
    public decimal EstimatedProfit { get; set; }
    public decimal ProfitMarginPercentage { get; set; }
    public List<CategoryProfitData> CategoryProfits { get; set; } = new();
    public List<ShopProfitData> ShopProfits { get; set; } = new();
}

/// <summary>
/// Category-wise profit data
/// </summary>
public class CategoryProfitData
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal EstimatedProfit { get; set; }
    public decimal ProfitMarginPercentage { get; set; }
}

/// <summary>
/// Shop-wise profit data
/// </summary>
public class ShopProfitData
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal EstimatedProfit { get; set; }
    public decimal ProfitMarginPercentage { get; set; }
}

/// <summary>
/// Shop performance summary
/// </summary>
public class ShopPerformanceSummary
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public decimal TodayRevenue { get; set; }
    public int TodayTransactions { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal RevenueGrowthPercentage { get; set; }
    public int LowStockAlerts { get; set; }
    public int ExpiryAlerts { get; set; }
    public PerformanceRating Rating { get; set; }
    public List<string> KeyInsights { get; set; } = new();
}

/// <summary>
/// Alert summary for dashboard
/// </summary>
public class AlertSummary
{
    public AlertType Type { get; set; }
    public AlertPriority Priority { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? ShopId { get; set; }
    public string? ShopName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Dashboard filter options
/// </summary>
public class DashboardFilter
{
    public Guid BusinessId { get; set; }
    public List<Guid>? ShopIds { get; set; }
    public DateRange? DateRange { get; set; }
    public BusinessType? BusinessType { get; set; }
    public bool IncludeInactiveShops { get; set; } = false;
    public DashboardDataScope DataScope { get; set; } = DashboardDataScope.All;
}

/// <summary>
/// Multi-shop comparison data
/// </summary>
public class MultiShopComparison
{
    public Guid BusinessId { get; set; }
    public DateRange ComparisonPeriod { get; set; } = new();
    public List<ShopComparisonData> ShopComparisons { get; set; } = new();
    public ShopRankingData Rankings { get; set; } = new();
    public List<string> Insights { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Individual shop comparison data
/// </summary>
public class ShopComparisonData
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal ProfitEstimate { get; set; }
    public int ProductCount { get; set; }
    public decimal InventoryValue { get; set; }
    public PerformanceMetrics Performance { get; set; } = new();
}

/// <summary>
/// Shop ranking data
/// </summary>
public class ShopRankingData
{
    public List<ShopRanking> RevenueRankings { get; set; } = new();
    public List<ShopRanking> TransactionRankings { get; set; } = new();
    public List<ShopRanking> ProfitRankings { get; set; } = new();
    public List<ShopRanking> EfficiencyRankings { get; set; } = new();
}

/// <summary>
/// Individual shop ranking
/// </summary>
public class ShopRanking
{
    public int Rank { get; set; }
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public decimal PercentageOfTotal { get; set; }
}

/// <summary>
/// Performance metrics for shops
/// </summary>
public class PerformanceMetrics
{
    public decimal RevenuePerSquareFoot { get; set; }
    public decimal TransactionsPerHour { get; set; }
    public decimal CustomerRetentionRate { get; set; }
    public decimal InventoryTurnoverRate { get; set; }
    public decimal ProfitMargin { get; set; }
    public PerformanceRating OverallRating { get; set; }
}

/// <summary>
/// Business intelligence insights
/// </summary>
public class BusinessIntelligenceInsights
{
    public Guid BusinessId { get; set; }
    public DateRange AnalysisPeriod { get; set; } = new();
    public List<BusinessInsight> KeyInsights { get; set; } = new();
    public List<RecommendationInsight> Recommendations { get; set; } = new();
    public List<TrendInsight> Trends { get; set; } = new();
    public List<OpportunityInsight> Opportunities { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Individual business insight
/// </summary>
public class BusinessInsight
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public InsightType Type { get; set; }
    public InsightPriority Priority { get; set; }
    public decimal? ImpactValue { get; set; }
    public List<string> SupportingData { get; set; } = new();
    public List<Guid> RelatedShopIds { get; set; } = new();
}

/// <summary>
/// Recommendation insight
/// </summary>
public class RecommendationInsight
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ActionRequired { get; set; } = string.Empty;
    public decimal EstimatedImpact { get; set; }
    public RecommendationCategory Category { get; set; }
    public List<Guid> AffectedShopIds { get; set; } = new();
}

/// <summary>
/// Trend insight
/// </summary>
public class TrendInsight
{
    public string TrendName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TrendDirection Direction { get; set; }
    public double Strength { get; set; }
    public decimal ImpactValue { get; set; }
    public List<string> Factors { get; set; } = new();
}

/// <summary>
/// Opportunity insight
/// </summary>
public class OpportunityInsight
{
    public string OpportunityName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PotentialValue { get; set; }
    public double SuccessProbability { get; set; }
    public List<string> RequiredActions { get; set; } = new();
    public List<Guid> RelevantShopIds { get; set; } = new();
}

/// <summary>
/// Enums for dashboard functionality
/// </summary>
public enum AlertType
{
    LowStock,
    OutOfStock,
    ProductExpiry,
    HighSales,
    LowSales,
    SystemError,
    SecurityAlert,
    SyncIssue
}

public enum AlertPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum PerformanceRating
{
    Poor,
    BelowAverage,
    Average,
    Good,
    Excellent
}

public enum DashboardDataScope
{
    All,
    SalesOnly,
    InventoryOnly,
    FinancialOnly,
    AlertsOnly
}

public enum InsightType
{
    Performance,
    Trend,
    Anomaly,
    Opportunity,
    Risk
}

public enum InsightPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum RecommendationCategory
{
    Inventory,
    Pricing,
    Marketing,
    Operations,
    Finance,
    Staff
}

/// <summary>
/// Top product data for dashboard
/// </summary>
public class TopProductData
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
}

/// <summary>
/// Inventory status data for dashboard
/// </summary>
public class InventoryStatusData
{
    public int TotalProducts { get; set; }
    public int LowStockProducts { get; set; }
    public int OutOfStockProducts { get; set; }
    public int ExpiringProducts { get; set; }
    public decimal TotalInventoryValue { get; set; }
}

/// <summary>
/// Shop performance data for dashboard
/// </summary>
public class ShopPerformanceData
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public decimal TodayRevenue { get; set; }
    public int TodayTransactions { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal RevenueGrowthPercentage { get; set; }
    public int LowStockAlerts { get; set; }
    public int ExpiryAlerts { get; set; }
    public PerformanceRating Rating { get; set; }
}