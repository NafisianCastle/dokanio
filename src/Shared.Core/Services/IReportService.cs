using Shared.Core.DTOs;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Service for generating comprehensive reports in multiple formats
/// </summary>
public interface IReportService
{
    #region Sales Reports
    
    /// <summary>
    /// Generates a sales report based on the specified criteria
    /// </summary>
    /// <param name="request">Sales report request parameters</param>
    /// <returns>Sales report response with data and metadata</returns>
    Task<SalesReportResponse> GenerateSalesReportAsync(SalesReportRequest request);
    
    /// <summary>
    /// Generates a shop-wise sales comparison report
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="dateRange">Date range for the report</param>
    /// <param name="format">Output format</param>
    /// <returns>Sales report with shop-wise breakdown</returns>
    Task<SalesReportResponse> GenerateShopWiseSalesReportAsync(Guid businessId, DateRange dateRange, ReportFormat format = ReportFormat.CSV);
    
    /// <summary>
    /// Generates a product-wise sales performance report
    /// </summary>
    /// <param name="request">Sales report request with product-specific parameters</param>
    /// <returns>Sales report with product-wise breakdown</returns>
    Task<SalesReportResponse> GenerateProductWiseSalesReportAsync(SalesReportRequest request);
    
    /// <summary>
    /// Generates a category-wise sales analysis report
    /// </summary>
    /// <param name="request">Sales report request with category-specific parameters</param>
    /// <returns>Sales report with category-wise breakdown</returns>
    Task<SalesReportResponse> GenerateCategoryWiseSalesReportAsync(SalesReportRequest request);
    
    #endregion
    
    #region Inventory Reports
    
    /// <summary>
    /// Generates an inventory report based on the specified criteria
    /// </summary>
    /// <param name="request">Inventory report request parameters</param>
    /// <returns>Inventory report response with data and metadata</returns>
    Task<InventoryReportResponse> GenerateInventoryReportAsync(InventoryReportRequest request);
    
    /// <summary>
    /// Generates a low stock alert report
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="shopId">Optional shop identifier for shop-specific report</param>
    /// <param name="threshold">Low stock threshold</param>
    /// <param name="format">Output format</param>
    /// <returns>Inventory report with low stock items</returns>
    Task<InventoryReportResponse> GenerateLowStockReportAsync(Guid businessId, Guid? shopId = null, int threshold = 10, ReportFormat format = ReportFormat.CSV);
    
    /// <summary>
    /// Generates an expiring products report
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="shopId">Optional shop identifier for shop-specific report</param>
    /// <param name="daysAhead">Number of days ahead to check for expiry</param>
    /// <param name="format">Output format</param>
    /// <returns>Inventory report with expiring items</returns>
    Task<InventoryReportResponse> GenerateExpiringProductsReportAsync(Guid businessId, Guid? shopId = null, int daysAhead = 30, ReportFormat format = ReportFormat.CSV);
    
    /// <summary>
    /// Generates a stock movement analysis report
    /// </summary>
    /// <param name="request">Inventory report request with movement-specific parameters</param>
    /// <returns>Inventory report with stock movement analysis</returns>
    Task<InventoryReportResponse> GenerateStockMovementReportAsync(InventoryReportRequest request);
    
    #endregion
    
    #region Financial Reports
    
    /// <summary>
    /// Generates a financial report based on the specified criteria
    /// </summary>
    /// <param name="request">Financial report request parameters</param>
    /// <returns>Financial report response with data and metadata</returns>
    Task<FinancialReportResponse> GenerateFinancialReportAsync(FinancialReportRequest request);
    
    /// <summary>
    /// Generates a profit and loss statement
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="dateRange">Date range for the report</param>
    /// <param name="shopId">Optional shop identifier for shop-specific report</param>
    /// <param name="format">Output format</param>
    /// <returns>Financial report with profit and loss analysis</returns>
    Task<FinancialReportResponse> GenerateProfitLossReportAsync(Guid businessId, DateRange dateRange, Guid? shopId = null, ReportFormat format = ReportFormat.CSV);
    
    /// <summary>
    /// Generates a revenue analysis report
    /// </summary>
    /// <param name="request">Financial report request with revenue-specific parameters</param>
    /// <returns>Financial report with revenue analysis</returns>
    Task<FinancialReportResponse> GenerateRevenueAnalysisReportAsync(FinancialReportRequest request);
    
    /// <summary>
    /// Generates a cost analysis report
    /// </summary>
    /// <param name="request">Financial report request with cost-specific parameters</param>
    /// <returns>Financial report with cost analysis</returns>
    Task<FinancialReportResponse> GenerateCostAnalysisReportAsync(FinancialReportRequest request);
    
    #endregion
    
    #region Report Export and Formatting
    
    /// <summary>
    /// Exports a report to the specified format
    /// </summary>
    /// <param name="reportData">Report data to export</param>
    /// <param name="format">Target format</param>
    /// <param name="fileName">Optional custom file name</param>
    /// <returns>Exported report as byte array</returns>
    Task<byte[]> ExportReportAsync<T>(T reportData, ReportFormat format, string? fileName = null) where T : ReportResponse;
    
    /// <summary>
    /// Saves a report to file system
    /// </summary>
    /// <param name="reportData">Report data to save</param>
    /// <param name="format">Target format</param>
    /// <param name="filePath">File path to save to</param>
    /// <returns>Success status</returns>
    Task<bool> SaveReportToFileAsync<T>(T reportData, ReportFormat format, string filePath) where T : ReportResponse;
    
    /// <summary>
    /// Gets available report templates for a business type
    /// </summary>
    /// <param name="businessType">Business type</param>
    /// <returns>List of available report templates</returns>
    Task<List<ReportTemplate>> GetAvailableReportTemplatesAsync(BusinessType businessType);
    
    #endregion
    
    #region Report Validation and Quality
    
    /// <summary>
    /// Validates report data accuracy against source data
    /// </summary>
    /// <param name="reportResponse">Report to validate</param>
    /// <returns>Validation result with accuracy metrics</returns>
    Task<ReportValidationResult> ValidateReportDataAccuracyAsync<T>(T reportResponse) where T : ReportResponse;
    
    /// <summary>
    /// Checks report data consistency across different time periods
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="reportType">Type of report to check</param>
    /// <param name="periods">Time periods to compare</param>
    /// <returns>Consistency analysis result</returns>
    Task<ReportConsistencyResult> CheckReportConsistencyAsync(Guid businessId, string reportType, List<DateRange> periods);
    
    #endregion
}

#region Supporting DTOs

public class ReportTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BusinessType BusinessType { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public List<ReportFormat> SupportedFormats { get; set; } = new();
    public Dictionary<string, object> DefaultParameters { get; set; } = new();
}

public class ReportValidationResult
{
    public bool IsValid { get; set; }
    public double AccuracyScore { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public List<string> ValidationWarnings { get; set; } = new();
    public Dictionary<string, double> MetricAccuracy { get; set; } = new();
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}

public class ReportConsistencyResult
{
    public bool IsConsistent { get; set; }
    public double ConsistencyScore { get; set; }
    public List<string> InconsistencyIssues { get; set; } = new();
    public Dictionary<string, object> ComparisonMetrics { get; set; } = new();
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

#endregion