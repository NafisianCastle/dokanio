using Shared.Core.Enums;

namespace Shared.Core.DTOs;

#region Report Request DTOs

public class ReportRequest
{
    public Guid BusinessId { get; set; }
    public Guid? ShopId { get; set; } // Null for business-wide reports
    public DateRange DateRange { get; set; } = new();
    public ReportFormat Format { get; set; } = ReportFormat.CSV;
    public string? CustomFilters { get; set; }
}

public class SalesReportRequest : ReportRequest
{
    public SalesReportType ReportType { get; set; } = SalesReportType.Summary;
    public bool IncludeRefunds { get; set; } = true;
    public bool GroupByCategory { get; set; } = false;
    public bool GroupByProduct { get; set; } = false;
    public bool GroupByShop { get; set; } = false;
}

public class InventoryReportRequest : ReportRequest
{
    public InventoryReportType ReportType { get; set; } = InventoryReportType.StockLevels;
    public bool IncludeLowStock { get; set; } = true;
    public bool IncludeExpiring { get; set; } = true;
    public int LowStockThreshold { get; set; } = 10;
    public int ExpiringDays { get; set; } = 30;
}

public class FinancialReportRequest : ReportRequest
{
    public FinancialReportType ReportType { get; set; } = FinancialReportType.ProfitLoss;
    public bool IncludeTaxBreakdown { get; set; } = true;
    public bool IncludeCostAnalysis { get; set; } = true;
    public string? Currency { get; set; } = "USD";
}

#endregion

#region Report Response DTOs

public class ReportResponse
{
    public Guid ReportId { get; set; } = Guid.NewGuid();
    public string ReportName { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public Guid BusinessId { get; set; }
    public Guid? ShopId { get; set; }
    public DateRange DateRange { get; set; } = new();
    public ReportFormat Format { get; set; }
    public string? FilePath { get; set; }
    public byte[]? Data { get; set; }
    public ReportMetadata Metadata { get; set; } = new();
}

public class SalesReportResponse : ReportResponse
{
    public SalesReportSummary Summary { get; set; } = new();
    public List<SalesReportItem> Items { get; set; } = new();
    public List<SalesReportByCategory> CategoryBreakdown { get; set; } = new();
    public List<SalesReportByProduct> ProductBreakdown { get; set; } = new();
    public List<SalesReportByShop> ShopBreakdown { get; set; } = new();
}

public class InventoryReportResponse : ReportResponse
{
    public InventoryReportSummary Summary { get; set; } = new();
    public List<InventoryReportItem> Items { get; set; } = new();
    public List<LowStockItem> LowStockItems { get; set; } = new();
    public List<ExpiringItem> ExpiringItems { get; set; } = new();
    public List<StockMovementItem> StockMovements { get; set; } = new();
}

public class FinancialReportResponse : ReportResponse
{
    public FinancialReportSummary Summary { get; set; } = new();
    public List<FinancialReportItem> Items { get; set; } = new();
    public List<TaxBreakdownItem> TaxBreakdown { get; set; } = new();
    public List<CostAnalysisItem> CostAnalysis { get; set; } = new();
    public ProfitLossStatement ProfitLoss { get; set; } = new();
}

#endregion

#region Report Data DTOs

public class ReportMetadata
{
    public int TotalRecords { get; set; }
    public int FilteredRecords { get; set; }
    public TimeSpan GenerationTime { get; set; }
    public string GeneratedBy { get; set; } = string.Empty;
    public Dictionary<string, object> CustomMetadata { get; set; } = new();
}

public class SalesReportSummary
{
    public decimal TotalSales { get; set; }
    public decimal TotalRefunds { get; set; }
    public decimal NetSales { get; set; }
    public int TotalTransactions { get; set; }
    public int TotalRefundTransactions { get; set; }
    public decimal AverageTransactionValue { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalDiscount { get; set; }
    public int TotalItemsSold { get; set; }
}

public class SalesReportItem
{
    public DateTime Date { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public List<SaleItemDetail> Items { get; set; } = new();
}

public class SaleItemDetail
{
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal DiscountAmount { get; set; }
}

public class SalesReportByCategory
{
    public string Category { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public int TotalQuantity { get; set; }
    public int TransactionCount { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal Percentage { get; set; }
}

public class SalesReportByProduct
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public int TotalQuantity { get; set; }
    public int TransactionCount { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal Percentage { get; set; }
}

public class SalesReportByShop
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageTransactionValue { get; set; }
    public decimal Percentage { get; set; }
}

public class InventoryReportSummary
{
    public int TotalProducts { get; set; }
    public int LowStockProducts { get; set; }
    public int ExpiringProducts { get; set; }
    public int OutOfStockProducts { get; set; }
    public decimal TotalInventoryValue { get; set; }
    public decimal LowStockValue { get; set; }
    public decimal ExpiringValue { get; set; }
}

public class InventoryReportItem
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int MinimumStock { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime LastUpdated { get; set; }
    public string ShopName { get; set; } = string.Empty;
}

public class LowStockItem
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int MinimumStock { get; set; }
    public int StockDeficit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal ReorderValue { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public int DaysOutOfStock { get; set; }
}

public class ExpiringItem
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public int DaysToExpiry { get; set; }
    public int CurrentStock { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalValue { get; set; }
    public string? BatchNumber { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public ExpiryRiskLevel RiskLevel { get; set; }
}

public class StockMovementItem
{
    public DateTime Date { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty; // Sale, Purchase, Adjustment, Transfer
    public int QuantityBefore { get; set; }
    public int QuantityChanged { get; set; }
    public int QuantityAfter { get; set; }
    public string Reference { get; set; } = string.Empty; // Invoice number, adjustment reason, etc.
    public string ShopName { get; set; } = string.Empty;
}

public class FinancialReportSummary
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalCosts { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal NetProfit { get; set; }
    public decimal GrossProfitMargin { get; set; }
    public decimal NetProfitMargin { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal TotalRefunds { get; set; }
}

public class FinancialReportItem
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
    public decimal ProfitMargin { get; set; }
    public string ShopName { get; set; } = string.Empty;
}

public class TaxBreakdownItem
{
    public string TaxType { get; set; } = string.Empty;
    public decimal TaxRate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public int TransactionCount { get; set; }
}

public class CostAnalysisItem
{
    public string Category { get; set; } = string.Empty;
    public decimal TotalCost { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal Profit { get; set; }
    public decimal ProfitMargin { get; set; }
    public decimal Percentage { get; set; }
}

public class ProfitLossStatement
{
    public decimal Revenue { get; set; }
    public decimal CostOfGoodsSold { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal OperatingExpenses { get; set; }
    public decimal OperatingIncome { get; set; }
    public decimal OtherIncome { get; set; }
    public decimal OtherExpenses { get; set; }
    public decimal NetIncome { get; set; }
}

#endregion

#region Enums

public enum ReportFormat
{
    CSV,
    Excel,
    PDF,
    JSON
}

public enum SalesReportType
{
    Summary,
    Detailed,
    ByCategory,
    ByProduct,
    ByShop,
    ByCustomer
}

public enum InventoryReportType
{
    StockLevels,
    LowStock,
    Expiring,
    StockMovement,
    Valuation
}

public enum FinancialReportType
{
    ProfitLoss,
    Revenue,
    CostAnalysis,
    TaxSummary
}

#endregion