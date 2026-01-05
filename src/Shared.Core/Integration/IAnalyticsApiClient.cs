using Shared.Core.DTOs;

namespace Shared.Core.Integration;

/// <summary>
/// Interface for external analytics API integration
/// </summary>
public interface IAnalyticsApiClient
{
    /// <summary>
    /// Sends sales data to external analytics service
    /// </summary>
    /// <param name="salesData">Sales data to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<AnalyticsApiResponse> SendSalesDataAsync(SalesAnalyticsData salesData, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends inventory data to external analytics service
    /// </summary>
    /// <param name="inventoryData">Inventory data to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<AnalyticsApiResponse> SendInventoryDataAsync(InventoryAnalyticsData inventoryData, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets analytics insights from external service
    /// </summary>
    /// <param name="request">Analytics request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<AnalyticsInsightsResponse> GetAnalyticsInsightsAsync(AnalyticsInsightsRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets business performance metrics from external service
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="period">Time period for metrics</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<BusinessPerformanceMetrics> GetBusinessPerformanceAsync(Guid businessId, DateRange period, CancellationToken cancellationToken = default);
}

/// <summary>
/// Response from analytics API
/// </summary>
public class AnalyticsApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Sales data for analytics
/// </summary>
public class SalesAnalyticsData
{
    public Guid BusinessId { get; set; }
    public Guid? ShopId { get; set; }
    public List<SaleAnalyticsItem> Sales { get; set; } = new();
    public DateRange Period { get; set; } = new();
}

/// <summary>
/// Individual sale item for analytics
/// </summary>
public class SaleAnalyticsItem
{
    public Guid SaleId { get; set; }
    public DateTime SaleDate { get; set; }
    public decimal TotalAmount { get; set; }
    public List<ProductAnalyticsItem> Items { get; set; } = new();
}

/// <summary>
/// Product item for analytics
/// </summary>
public class ProductAnalyticsItem
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

/// <summary>
/// Inventory data for analytics
/// </summary>
public class InventoryAnalyticsData
{
    public Guid BusinessId { get; set; }
    public Guid? ShopId { get; set; }
    public List<InventoryAnalyticsItem> Inventory { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Individual inventory item for analytics
/// </summary>
public class InventoryAnalyticsItem
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int MinimumStock { get; set; }
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime? LastRestocked { get; set; }
}

/// <summary>
/// Request for analytics insights
/// </summary>
public class AnalyticsInsightsRequest
{
    public Guid BusinessId { get; set; }
    public Guid? ShopId { get; set; }
    public DateRange Period { get; set; } = new();
    public List<string> MetricTypes { get; set; } = new();
}

/// <summary>
/// Response containing analytics insights
/// </summary>
public class AnalyticsInsightsResponse
{
    public bool Success { get; set; }
    public List<AnalyticsInsight> Insights { get; set; } = new();
    public Dictionary<string, object> Metrics { get; set; } = new();
}

/// <summary>
/// Individual analytics insight
/// </summary>
public class AnalyticsInsight
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? Value { get; set; }
    public string? Trend { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Business performance metrics
/// </summary>
public class BusinessPerformanceMetrics
{
    public Guid BusinessId { get; set; }
    public DateRange Period { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public decimal TotalProfit { get; set; }
    public int TotalTransactions { get; set; }
    public decimal AverageTransactionValue { get; set; }
    public List<ShopPerformanceMetrics> ShopMetrics { get; set; } = new();
}

/// <summary>
/// Shop-level performance metrics
/// </summary>
public class ShopPerformanceMetrics
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Profit { get; set; }
    public int Transactions { get; set; }
    public decimal AverageTransactionValue { get; set; }
}

/// <summary>
/// Date range for analytics queries
/// </summary>
public class DateRange
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}