using Shared.Core.DTOs;

namespace Shared.Core.Services;

/// <summary>
/// Service interface for business owner dashboard functionality
/// </summary>
public interface IDashboardService
{
    #region Dashboard Overview
    
    /// <summary>
    /// Gets comprehensive dashboard overview for a business
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="filter">Optional filter criteria</param>
    /// <returns>Dashboard overview data</returns>
    Task<DashboardOverview> GetDashboardOverviewAsync(Guid businessId, DashboardFilter? filter = null);
    
    /// <summary>
    /// Gets dashboard overview for multiple businesses (for multi-business owners)
    /// </summary>
    /// <param name="businessIds">Business identifiers</param>
    /// <param name="filter">Optional filter criteria</param>
    /// <returns>Collection of dashboard overviews</returns>
    Task<IEnumerable<DashboardOverview>> GetMultiBusinessDashboardAsync(IEnumerable<Guid> businessIds, DashboardFilter? filter = null);
    
    #endregion
    
    #region Real-Time Sales Monitoring
    
    /// <summary>
    /// Gets real-time sales data across all shops in a business
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="shopIds">Optional specific shop identifiers</param>
    /// <returns>Real-time sales data</returns>
    Task<RealTimeSalesData> GetRealTimeSalesDataAsync(Guid businessId, IEnumerable<Guid>? shopIds = null);
    
    /// <summary>
    /// Gets hourly sales data for today
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="shopIds">Optional specific shop identifiers</param>
    /// <returns>Hourly sales data for today</returns>
    Task<IEnumerable<HourlySalesData>> GetTodayHourlySalesAsync(Guid businessId, IEnumerable<Guid>? shopIds = null);
    
    /// <summary>
    /// Gets top selling products across all shops
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="period">Time period for analysis</param>
    /// <param name="topCount">Number of top products to return</param>
    /// <param name="shopIds">Optional specific shop identifiers</param>
    /// <returns>Top selling products</returns>
    Task<IEnumerable<TopSellingProduct>> GetTopSellingProductsAsync(Guid businessId, DateRange period, int topCount = 10, IEnumerable<Guid>? shopIds = null);
    
    #endregion
    
    #region Inventory Status Tracking
    
    /// <summary>
    /// Gets inventory status summary across all shops
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="shopIds">Optional specific shop identifiers</param>
    /// <returns>Inventory status summary</returns>
    Task<InventoryStatusSummary> GetInventoryStatusSummaryAsync(Guid businessId, IEnumerable<Guid>? shopIds = null);
    
    /// <summary>
    /// Gets low stock alerts across all shops
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="threshold">Stock threshold for alerts</param>
    /// <param name="shopIds">Optional specific shop identifiers</param>
    /// <returns>Low stock alerts</returns>
    Task<IEnumerable<LowStockAlert>> GetLowStockAlertsAsync(Guid businessId, int? threshold = null, IEnumerable<Guid>? shopIds = null);
    
    /// <summary>
    /// Gets product expiry alerts for pharmacy businesses
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="daysAhead">Days ahead to check for expiry</param>
    /// <param name="shopIds">Optional specific shop identifiers</param>
    /// <returns>Product expiry alerts</returns>
    Task<IEnumerable<ExpiryAlert>> GetExpiryAlertsAsync(Guid businessId, int daysAhead = 30, IEnumerable<Guid>? shopIds = null);
    
    /// <summary>
    /// Gets inventory status for individual shops
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <returns>Shop-wise inventory status</returns>
    Task<IEnumerable<ShopInventoryStatus>> GetShopInventoryStatusesAsync(Guid businessId);
    
    #endregion
    
    #region Revenue Trend Analysis
    
    /// <summary>
    /// Gets revenue trend analysis for a business
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="period">Analysis period</param>
    /// <param name="shopIds">Optional specific shop identifiers</param>
    /// <returns>Revenue trend data</returns>
    Task<RevenueTrendData> GetRevenueTrendAnalysisAsync(Guid businessId, DateRange period, IEnumerable<Guid>? shopIds = null);
    
    /// <summary>
    /// Gets daily revenue data for a specific period
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="period">Analysis period</param>
    /// <param name="shopIds">Optional specific shop identifiers</param>
    /// <returns>Daily revenue data</returns>
    Task<IEnumerable<DailyRevenueData>> GetDailyRevenueDataAsync(Guid businessId, DateRange period, IEnumerable<Guid>? shopIds = null);
    
    /// <summary>
    /// Gets monthly revenue data for trend analysis
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="monthCount">Number of months to analyze</param>
    /// <param name="shopIds">Optional specific shop identifiers</param>
    /// <returns>Monthly revenue data</returns>
    Task<IEnumerable<MonthlyRevenueData>> GetMonthlyRevenueDataAsync(Guid businessId, int monthCount = 12, IEnumerable<Guid>? shopIds = null);
    
    #endregion
    
    #region Profit Calculations
    
    /// <summary>
    /// Gets profit analysis for a business
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="period">Analysis period</param>
    /// <param name="shopIds">Optional specific shop identifiers</param>
    /// <returns>Profit analysis data</returns>
    Task<ProfitAnalysis> GetProfitAnalysisAsync(Guid businessId, DateRange period, IEnumerable<Guid>? shopIds = null);
    
    /// <summary>
    /// Gets category-wise profit analysis
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="period">Analysis period</param>
    /// <param name="shopIds">Optional specific shop identifiers</param>
    /// <returns>Category profit data</returns>
    Task<IEnumerable<CategoryProfitData>> GetCategoryProfitAnalysisAsync(Guid businessId, DateRange period, IEnumerable<Guid>? shopIds = null);
    
    /// <summary>
    /// Gets shop-wise profit analysis
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="period">Analysis period</param>
    /// <returns>Shop profit data</returns>
    Task<IEnumerable<ShopProfitData>> GetShopProfitAnalysisAsync(Guid businessId, DateRange period);
    
    #endregion
    
    #region Shop Performance Comparison
    
    /// <summary>
    /// Gets performance summary for all shops in a business
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="period">Analysis period</param>
    /// <returns>Shop performance summaries</returns>
    Task<IEnumerable<ShopPerformanceSummary>> GetShopPerformanceSummariesAsync(Guid businessId, DateRange? period = null);
    
    /// <summary>
    /// Gets multi-shop comparison data
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="period">Comparison period</param>
    /// <returns>Multi-shop comparison data</returns>
    Task<MultiShopComparison> GetMultiShopComparisonAsync(Guid businessId, DateRange period);
    
    /// <summary>
    /// Gets shop rankings by various metrics
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="period">Analysis period</param>
    /// <returns>Shop ranking data</returns>
    Task<ShopRankingData> GetShopRankingsAsync(Guid businessId, DateRange period);
    
    #endregion
    
    #region Alerts and Notifications
    
    /// <summary>
    /// Gets all active alerts for a business
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="priority">Optional priority filter</param>
    /// <param name="alertType">Optional alert type filter</param>
    /// <returns>Active alerts</returns>
    Task<IEnumerable<AlertSummary>> GetActiveAlertsAsync(Guid businessId, AlertPriority? priority = null, AlertType? alertType = null);
    
    /// <summary>
    /// Creates a new alert
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="alert">Alert to create</param>
    /// <returns>Created alert</returns>
    Task<AlertSummary> CreateAlertAsync(Guid businessId, AlertSummary alert);
    
    /// <summary>
    /// Marks an alert as read
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="alertId">Alert identifier</param>
    /// <returns>True if successful</returns>
    Task<bool> MarkAlertAsReadAsync(Guid businessId, Guid alertId);
    
    /// <summary>
    /// Gets alert count by priority
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <returns>Alert counts by priority</returns>
    Task<Dictionary<AlertPriority, int>> GetAlertCountsByPriorityAsync(Guid businessId);
    
    #endregion
    
    #region Business Intelligence
    
    /// <summary>
    /// Gets business intelligence insights
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="period">Analysis period</param>
    /// <returns>Business intelligence insights</returns>
    Task<BusinessIntelligenceInsights> GetBusinessIntelligenceInsightsAsync(Guid businessId, DateRange period);
    
    /// <summary>
    /// Gets performance metrics for shops
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="period">Analysis period</param>
    /// <returns>Performance metrics by shop</returns>
    Task<Dictionary<Guid, PerformanceMetrics>> GetShopPerformanceMetricsAsync(Guid businessId, DateRange period);
    
    #endregion
    
    #region Data Refresh and Caching
    
    /// <summary>
    /// Refreshes dashboard data cache
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <returns>True if successful</returns>
    Task<bool> RefreshDashboardDataAsync(Guid businessId);
    
    /// <summary>
    /// Gets the last data refresh timestamp
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <returns>Last refresh timestamp</returns>
    Task<DateTime?> GetLastDataRefreshAsync(Guid businessId);
    
    #endregion
}