using Shared.Core.Enums;

namespace Shared.Core.DTOs;

/// <summary>
/// Sales analytics data
/// </summary>
public class SalesAnalytics
{
    public Guid BusinessId { get; set; }
    public DateRange Period { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public int TotalTransactions { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int ItemsSold { get; set; }
    public decimal RevenueGrowth { get; set; }
    public decimal TransactionGrowth { get; set; }
    public decimal AOVGrowth { get; set; }
    public decimal ItemsGrowth { get; set; }
    public List<DailyTrendData> DailyTrends { get; set; } = new();
    public List<CategoryAnalytics> TopCategories { get; set; } = new();
    public List<ShopAnalytics> ShopPerformances { get; set; } = new();
    public List<PaymentMethodAnalytics> PaymentMethods { get; set; } = new();
    public List<TopProductAnalytics> TopProducts { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Daily trend data for analytics
/// </summary>
public class DailyTrendData
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
    public int Transactions { get; set; }
    public int ItemsSold { get; set; }
    public decimal AverageOrderValue { get; set; }
}

/// <summary>
/// Category analytics data
/// </summary>
public class CategoryAnalytics
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int ItemsSold { get; set; }
    public decimal Percentage { get; set; }
    public decimal Growth { get; set; }
}

/// <summary>
/// Shop analytics data
/// </summary>
public class ShopAnalytics
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int Transactions { get; set; }
    public decimal Growth { get; set; }
    public decimal MarketShare { get; set; }
}

/// <summary>
/// Payment method analytics data
/// </summary>
public class PaymentMethodAnalytics
{
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }
    public decimal Percentage { get; set; }
}

/// <summary>
/// Top product analytics data
/// </summary>
public class TopProductAnalytics
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal Growth { get; set; }
}

/// <summary>
/// Inventory analytics data
/// </summary>
public class InventoryAnalytics
{
    public Guid BusinessId { get; set; }
    public int TotalProducts { get; set; }
    public decimal TotalInventoryValue { get; set; }
    public int LowStockProducts { get; set; }
    public int OutOfStockProducts { get; set; }
    public int ExpiringProducts { get; set; }
    public List<CategoryInventoryData> CategoryBreakdown { get; set; } = new();
    public List<ShopInventoryData> ShopBreakdown { get; set; } = new();
    public List<InventoryMovementData> MovementTrends { get; set; } = new();
    public List<LowStockAlert> LowStockAlerts { get; set; } = new();
    public List<ExpiryAlert> ExpiryAlerts { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Category inventory data
/// </summary>
public class CategoryInventoryData
{
    public string CategoryName { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
}

/// <summary>
/// Shop inventory data
/// </summary>
public class ShopInventoryData
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public int ExpiringCount { get; set; }
}

/// <summary>
/// Inventory movement data
/// </summary>
public class InventoryMovementData
{
    public DateTime Date { get; set; }
    public int ItemsReceived { get; set; }
    public int ItemsSold { get; set; }
    public int ItemsAdjusted { get; set; }
    public decimal ValueReceived { get; set; }
    public decimal ValueSold { get; set; }
    public decimal ValueAdjusted { get; set; }
}

/// <summary>
/// Financial report data
/// </summary>
public class FinancialReport
{
    public Guid BusinessId { get; set; }
    public DateRange Period { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public decimal TotalCosts { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal NetProfit { get; set; }
    public decimal ProfitMargin { get; set; }
    public List<RevenueBreakdown> RevenueBreakdown { get; set; } = new();
    public List<CostBreakdown> CostBreakdown { get; set; } = new();
    public List<ShopFinancialData> ShopFinancials { get; set; } = new();
    public List<MonthlyFinancialData> MonthlyTrends { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Revenue breakdown data
/// </summary>
public class RevenueBreakdown
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Percentage { get; set; }
    public decimal Growth { get; set; }
}

/// <summary>
/// Cost breakdown data
/// </summary>
public class CostBreakdown
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Percentage { get; set; }
    public decimal Growth { get; set; }
}

/// <summary>
/// Shop financial data
/// </summary>
public class ShopFinancialData
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Costs { get; set; }
    public decimal Profit { get; set; }
    public decimal ProfitMargin { get; set; }
}

/// <summary>
/// Monthly financial data
/// </summary>
public class MonthlyFinancialData
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Costs { get; set; }
    public decimal Profit { get; set; }
    public decimal ProfitMargin { get; set; }
}