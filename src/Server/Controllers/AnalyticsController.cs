using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Core.DTOs;
using Shared.Core.Services;
using System.Security.Claims;

namespace Server.Controllers;

/// <summary>
/// Controller for analytics and dashboard data consumption
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(IDashboardService dashboardService, ILogger<AnalyticsController> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    #region Dashboard Overview

    /// <summary>
    /// Gets comprehensive dashboard overview for a business
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="shopIds">Optional specific shop IDs</param>
    /// <param name="startDate">Optional start date filter</param>
    /// <param name="endDate">Optional end date filter</param>
    /// <returns>Dashboard overview data</returns>
    [HttpGet("dashboard/{businessId}")]
    public async Task<ActionResult<SyncApiResult<DashboardOverview>>> GetDashboardOverview(
        Guid businessId,
        [FromQuery] string? shopIds = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var filter = new DashboardFilter
            {
                BusinessId = businessId,
                ShopIds = ParseShopIds(shopIds),
                DateRange = CreateDateRange(startDate, endDate)
            };

            var overview = await _dashboardService.GetDashboardOverviewAsync(businessId, filter);

            return Ok(new SyncApiResult<DashboardOverview>
            {
                Success = true,
                Message = "Dashboard overview retrieved successfully",
                StatusCode = 200,
                Data = overview
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dashboard overview for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<DashboardOverview>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Gets dashboard overview for multiple businesses
    /// </summary>
    /// <param name="businessIds">Business IDs</param>
    /// <returns>Collection of dashboard overviews</returns>
    [HttpPost("dashboard/multi-business")]
    public async Task<ActionResult<SyncApiResult<IEnumerable<DashboardOverview>>>> GetMultiBusinessDashboard([FromBody] MultiBusinessDashboardRequest request)
    {
        try
        {
            var filter = new DashboardFilter
            {
                DateRange = request.DateRange
            };

            var overviews = await _dashboardService.GetMultiBusinessDashboardAsync(request.BusinessIds, filter);

            return Ok(new SyncApiResult<IEnumerable<DashboardOverview>>
            {
                Success = true,
                Message = "Multi-business dashboard retrieved successfully",
                StatusCode = 200,
                Data = overviews
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving multi-business dashboard");
            return StatusCode(500, new SyncApiResult<IEnumerable<DashboardOverview>>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    #endregion

    #region Real-Time Sales Monitoring

    /// <summary>
    /// Gets real-time sales data across all shops in a business
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="shopIds">Optional specific shop IDs</param>
    /// <returns>Real-time sales data</returns>
    [HttpGet("sales/real-time/{businessId}")]
    public async Task<ActionResult<SyncApiResult<RealTimeSalesData>>> GetRealTimeSalesData(
        Guid businessId,
        [FromQuery] string? shopIds = null)
    {
        try
        {
            var shopIdList = ParseShopIds(shopIds);
            var salesData = await _dashboardService.GetRealTimeSalesDataAsync(businessId, shopIdList);

            return Ok(new SyncApiResult<RealTimeSalesData>
            {
                Success = true,
                Message = "Real-time sales data retrieved successfully",
                StatusCode = 200,
                Data = salesData
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving real-time sales data for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<RealTimeSalesData>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Gets hourly sales data for today
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="shopIds">Optional specific shop IDs</param>
    /// <returns>Hourly sales data for today</returns>
    [HttpGet("sales/hourly/{businessId}")]
    public async Task<ActionResult<SyncApiResult<IEnumerable<HourlySalesData>>>> GetTodayHourlySales(
        Guid businessId,
        [FromQuery] string? shopIds = null)
    {
        try
        {
            var shopIdList = ParseShopIds(shopIds);
            var hourlySales = await _dashboardService.GetTodayHourlySalesAsync(businessId, shopIdList);

            return Ok(new SyncApiResult<IEnumerable<HourlySalesData>>
            {
                Success = true,
                Message = "Hourly sales data retrieved successfully",
                StatusCode = 200,
                Data = hourlySales
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving hourly sales data for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<IEnumerable<HourlySalesData>>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Gets top selling products across all shops
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="startDate">Start date for analysis</param>
    /// <param name="endDate">End date for analysis</param>
    /// <param name="topCount">Number of top products to return</param>
    /// <param name="shopIds">Optional specific shop IDs</param>
    /// <returns>Top selling products</returns>
    [HttpGet("sales/top-products/{businessId}")]
    public async Task<ActionResult<SyncApiResult<IEnumerable<TopSellingProduct>>>> GetTopSellingProducts(
        Guid businessId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int topCount = 10,
        [FromQuery] string? shopIds = null)
    {
        try
        {
            var period = CreateDateRange(startDate, endDate) ?? new DateRange
            {
                StartDate = DateTime.Today.AddDays(-30),
                EndDate = DateTime.Today
            };

            var shopIdList = ParseShopIds(shopIds);
            var topProducts = await _dashboardService.GetTopSellingProductsAsync(businessId, period, topCount, shopIdList);

            return Ok(new SyncApiResult<IEnumerable<TopSellingProduct>>
            {
                Success = true,
                Message = "Top selling products retrieved successfully",
                StatusCode = 200,
                Data = topProducts
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving top selling products for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<IEnumerable<TopSellingProduct>>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    #endregion

    #region Inventory Status Tracking

    /// <summary>
    /// Gets inventory status summary across all shops
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="shopIds">Optional specific shop IDs</param>
    /// <returns>Inventory status summary</returns>
    [HttpGet("inventory/status/{businessId}")]
    public async Task<ActionResult<SyncApiResult<InventoryStatusSummary>>> GetInventoryStatusSummary(
        Guid businessId,
        [FromQuery] string? shopIds = null)
    {
        try
        {
            var shopIdList = ParseShopIds(shopIds);
            var inventoryStatus = await _dashboardService.GetInventoryStatusSummaryAsync(businessId, shopIdList);

            return Ok(new SyncApiResult<InventoryStatusSummary>
            {
                Success = true,
                Message = "Inventory status summary retrieved successfully",
                StatusCode = 200,
                Data = inventoryStatus
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory status for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<InventoryStatusSummary>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Gets low stock alerts across all shops
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="threshold">Stock threshold for alerts</param>
    /// <param name="shopIds">Optional specific shop IDs</param>
    /// <returns>Low stock alerts</returns>
    [HttpGet("inventory/low-stock/{businessId}")]
    public async Task<ActionResult<SyncApiResult<IEnumerable<LowStockAlert>>>> GetLowStockAlerts(
        Guid businessId,
        [FromQuery] int? threshold = null,
        [FromQuery] string? shopIds = null)
    {
        try
        {
            var shopIdList = ParseShopIds(shopIds);
            var lowStockAlerts = await _dashboardService.GetLowStockAlertsAsync(businessId, threshold, shopIdList);

            return Ok(new SyncApiResult<IEnumerable<LowStockAlert>>
            {
                Success = true,
                Message = "Low stock alerts retrieved successfully",
                StatusCode = 200,
                Data = lowStockAlerts
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving low stock alerts for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<IEnumerable<LowStockAlert>>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Gets product expiry alerts for pharmacy businesses
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="daysAhead">Days ahead to check for expiry</param>
    /// <param name="shopIds">Optional specific shop IDs</param>
    /// <returns>Product expiry alerts</returns>
    [HttpGet("inventory/expiry-alerts/{businessId}")]
    public async Task<ActionResult<SyncApiResult<IEnumerable<ExpiryAlert>>>> GetExpiryAlerts(
        Guid businessId,
        [FromQuery] int daysAhead = 30,
        [FromQuery] string? shopIds = null)
    {
        try
        {
            var shopIdList = ParseShopIds(shopIds);
            var expiryAlerts = await _dashboardService.GetExpiryAlertsAsync(businessId, daysAhead, shopIdList);

            return Ok(new SyncApiResult<IEnumerable<ExpiryAlert>>
            {
                Success = true,
                Message = "Expiry alerts retrieved successfully",
                StatusCode = 200,
                Data = expiryAlerts
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expiry alerts for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<IEnumerable<ExpiryAlert>>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    #endregion

    #region Revenue Trend Analysis

    /// <summary>
    /// Gets revenue trend analysis for a business
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="startDate">Start date for analysis</param>
    /// <param name="endDate">End date for analysis</param>
    /// <param name="shopIds">Optional specific shop IDs</param>
    /// <returns>Revenue trend data</returns>
    [HttpGet("revenue/trends/{businessId}")]
    public async Task<ActionResult<SyncApiResult<RevenueTrendData>>> GetRevenueTrendAnalysis(
        Guid businessId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? shopIds = null)
    {
        try
        {
            var period = CreateDateRange(startDate, endDate) ?? new DateRange
            {
                StartDate = DateTime.Today.AddDays(-30),
                EndDate = DateTime.Today
            };

            var shopIdList = ParseShopIds(shopIds);
            var revenueTrends = await _dashboardService.GetRevenueTrendAnalysisAsync(businessId, period, shopIdList);

            return Ok(new SyncApiResult<RevenueTrendData>
            {
                Success = true,
                Message = "Revenue trend analysis retrieved successfully",
                StatusCode = 200,
                Data = revenueTrends
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving revenue trends for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<RevenueTrendData>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Gets daily revenue data for a specific period
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="startDate">Start date for analysis</param>
    /// <param name="endDate">End date for analysis</param>
    /// <param name="shopIds">Optional specific shop IDs</param>
    /// <returns>Daily revenue data</returns>
    [HttpGet("revenue/daily/{businessId}")]
    public async Task<ActionResult<SyncApiResult<IEnumerable<DailyRevenueData>>>> GetDailyRevenueData(
        Guid businessId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? shopIds = null)
    {
        try
        {
            var period = CreateDateRange(startDate, endDate) ?? new DateRange
            {
                StartDate = DateTime.Today.AddDays(-30),
                EndDate = DateTime.Today
            };

            var shopIdList = ParseShopIds(shopIds);
            var dailyRevenue = await _dashboardService.GetDailyRevenueDataAsync(businessId, period, shopIdList);

            return Ok(new SyncApiResult<IEnumerable<DailyRevenueData>>
            {
                Success = true,
                Message = "Daily revenue data retrieved successfully",
                StatusCode = 200,
                Data = dailyRevenue
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving daily revenue data for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<IEnumerable<DailyRevenueData>>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Gets monthly revenue data for trend analysis
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="monthCount">Number of months to analyze</param>
    /// <param name="shopIds">Optional specific shop IDs</param>
    /// <returns>Monthly revenue data</returns>
    [HttpGet("revenue/monthly/{businessId}")]
    public async Task<ActionResult<SyncApiResult<IEnumerable<MonthlyRevenueData>>>> GetMonthlyRevenueData(
        Guid businessId,
        [FromQuery] int monthCount = 12,
        [FromQuery] string? shopIds = null)
    {
        try
        {
            var shopIdList = ParseShopIds(shopIds);
            var monthlyRevenue = await _dashboardService.GetMonthlyRevenueDataAsync(businessId, monthCount, shopIdList);

            return Ok(new SyncApiResult<IEnumerable<MonthlyRevenueData>>
            {
                Success = true,
                Message = "Monthly revenue data retrieved successfully",
                StatusCode = 200,
                Data = monthlyRevenue
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving monthly revenue data for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<IEnumerable<MonthlyRevenueData>>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    #endregion

    #region Shop Performance Comparison

    /// <summary>
    /// Gets performance summary for all shops in a business
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="startDate">Start date for analysis</param>
    /// <param name="endDate">End date for analysis</param>
    /// <returns>Shop performance summaries</returns>
    [HttpGet("shops/performance/{businessId}")]
    public async Task<ActionResult<SyncApiResult<IEnumerable<ShopPerformanceSummary>>>> GetShopPerformanceSummaries(
        Guid businessId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var period = CreateDateRange(startDate, endDate);
            var shopPerformances = await _dashboardService.GetShopPerformanceSummariesAsync(businessId, period);

            return Ok(new SyncApiResult<IEnumerable<ShopPerformanceSummary>>
            {
                Success = true,
                Message = "Shop performance summaries retrieved successfully",
                StatusCode = 200,
                Data = shopPerformances
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shop performance summaries for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<IEnumerable<ShopPerformanceSummary>>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Gets multi-shop comparison data
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="startDate">Start date for comparison</param>
    /// <param name="endDate">End date for comparison</param>
    /// <returns>Multi-shop comparison data</returns>
    [HttpGet("shops/comparison/{businessId}")]
    public async Task<ActionResult<SyncApiResult<MultiShopComparison>>> GetMultiShopComparison(
        Guid businessId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var period = CreateDateRange(startDate, endDate) ?? new DateRange
            {
                StartDate = DateTime.Today.AddDays(-30),
                EndDate = DateTime.Today
            };

            var comparison = await _dashboardService.GetMultiShopComparisonAsync(businessId, period);

            return Ok(new SyncApiResult<MultiShopComparison>
            {
                Success = true,
                Message = "Multi-shop comparison retrieved successfully",
                StatusCode = 200,
                Data = comparison
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving multi-shop comparison for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<MultiShopComparison>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    #endregion

    #region Alerts and Notifications

    /// <summary>
    /// Gets all active alerts for a business
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="priority">Optional priority filter</param>
    /// <param name="alertType">Optional alert type filter</param>
    /// <returns>Active alerts</returns>
    [HttpGet("alerts/{businessId}")]
    public async Task<ActionResult<SyncApiResult<IEnumerable<AlertSummary>>>> GetActiveAlerts(
        Guid businessId,
        [FromQuery] AlertPriority? priority = null,
        [FromQuery] AlertType? alertType = null)
    {
        try
        {
            var alerts = await _dashboardService.GetActiveAlertsAsync(businessId, priority, alertType);

            return Ok(new SyncApiResult<IEnumerable<AlertSummary>>
            {
                Success = true,
                Message = "Active alerts retrieved successfully",
                StatusCode = 200,
                Data = alerts
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alerts for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<IEnumerable<AlertSummary>>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Marks an alert as read
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="alertId">Alert ID</param>
    /// <returns>Update result</returns>
    [HttpPut("alerts/{businessId}/{alertId}/read")]
    public async Task<ActionResult<SyncApiResult<bool>>> MarkAlertAsRead(Guid businessId, Guid alertId)
    {
        try
        {
            var result = await _dashboardService.MarkAlertAsReadAsync(businessId, alertId);

            return Ok(new SyncApiResult<bool>
            {
                Success = true,
                Message = "Alert marked as read successfully",
                StatusCode = 200,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking alert {AlertId} as read for business {BusinessId}", alertId, businessId);
            return StatusCode(500, new SyncApiResult<bool>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    #endregion

    #region Helper Methods

    private List<Guid>? ParseShopIds(string? shopIds)
    {
        if (string.IsNullOrWhiteSpace(shopIds))
            return null;

        var ids = new List<Guid>();
        var parts = shopIds.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            if (Guid.TryParse(part.Trim(), out var id))
            {
                ids.Add(id);
            }
        }

        return ids.Count > 0 ? ids : null;
    }

    private DateRange? CreateDateRange(DateTime? startDate, DateTime? endDate)
    {
        if (startDate.HasValue || endDate.HasValue)
        {
            return new DateRange
            {
                StartDate = startDate ?? DateTime.Today.AddDays(-30),
                EndDate = endDate ?? DateTime.Today
            };
        }
        return null;
    }

    #endregion
}

/// <summary>
/// Multi-business dashboard request model
/// </summary>
public class MultiBusinessDashboardRequest
{
    public IEnumerable<Guid> BusinessIds { get; set; } = new List<Guid>();
    public DateRange? DateRange { get; set; }
}