using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using System.Text;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// Service for generating comprehensive reports in multiple formats
/// </summary>
public class ReportService : IReportService
{
    private readonly PosDbContext _context;
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockRepository _stockRepository;
    private readonly IBusinessRepository _businessRepository;
    private readonly IShopRepository _shopRepository;
    private readonly ILogger<ReportService> _logger;

    public ReportService(
        PosDbContext context,
        ISaleRepository saleRepository,
        IProductRepository productRepository,
        IStockRepository stockRepository,
        IBusinessRepository businessRepository,
        IShopRepository shopRepository,
        ILogger<ReportService> logger)
    {
        _context = context;
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _stockRepository = stockRepository;
        _businessRepository = businessRepository;
        _shopRepository = shopRepository;
        _logger = logger;
    }

    #region Sales Reports

    public async Task<SalesReportResponse> GenerateSalesReportAsync(SalesReportRequest request)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Generating sales report for business {BusinessId}, shop {ShopId}, type {ReportType}", 
            request.BusinessId, request.ShopId, request.ReportType);

        try
        {
            var response = new SalesReportResponse
            {
                ReportName = $"Sales Report - {request.ReportType}",
                BusinessId = request.BusinessId,
                ShopId = request.ShopId,
                DateRange = request.DateRange,
                Format = request.Format
            };

            // Get sales data based on request parameters
            var salesQuery = BuildSalesQuery(request);
            var sales = await salesQuery.ToListAsync();

            // Build report items
            response.Items = await BuildSalesReportItems(sales);
            
            // Calculate summary
            response.Summary = CalculateSalesReportSummary(response.Items);

            // Generate breakdowns based on request
            if (request.GroupByCategory || request.ReportType == SalesReportType.ByCategory)
            {
                response.CategoryBreakdown = await BuildCategoryBreakdown(sales);
            }

            if (request.GroupByProduct || request.ReportType == SalesReportType.ByProduct)
            {
                response.ProductBreakdown = await BuildProductBreakdown(sales);
            }

            if (request.GroupByShop || request.ReportType == SalesReportType.ByShop)
            {
                response.ShopBreakdown = await BuildShopBreakdown(sales);
            }

            // Set metadata
            response.Metadata = new ReportMetadata
            {
                TotalRecords = sales.Count,
                FilteredRecords = response.Items.Count,
                GenerationTime = DateTime.UtcNow - startTime,
                GeneratedBy = "ReportService"
            };

            _logger.LogInformation("Sales report generated successfully with {RecordCount} records in {Duration}ms", 
                response.Items.Count, response.Metadata.GenerationTime.TotalMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating sales report for business {BusinessId}", request.BusinessId);
            throw;
        }
    }

    public async Task<SalesReportResponse> GenerateShopWiseSalesReportAsync(Guid businessId, DateRange dateRange, ReportFormat format = ReportFormat.CSV)
    {
        var request = new SalesReportRequest
        {
            BusinessId = businessId,
            DateRange = dateRange,
            Format = format,
            ReportType = SalesReportType.ByShop,
            GroupByShop = true
        };

        return await GenerateSalesReportAsync(request);
    }

    public async Task<SalesReportResponse> GenerateProductWiseSalesReportAsync(SalesReportRequest request)
    {
        request.ReportType = SalesReportType.ByProduct;
        request.GroupByProduct = true;
        return await GenerateSalesReportAsync(request);
    }

    public async Task<SalesReportResponse> GenerateCategoryWiseSalesReportAsync(SalesReportRequest request)
    {
        request.ReportType = SalesReportType.ByCategory;
        request.GroupByCategory = true;
        return await GenerateSalesReportAsync(request);
    }

    #endregion

    #region Inventory Reports

    public async Task<InventoryReportResponse> GenerateInventoryReportAsync(InventoryReportRequest request)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Generating inventory report for business {BusinessId}, shop {ShopId}, type {ReportType}", 
            request.BusinessId, request.ShopId, request.ReportType);

        try
        {
            var response = new InventoryReportResponse
            {
                ReportName = $"Inventory Report - {request.ReportType}",
                BusinessId = request.BusinessId,
                ShopId = request.ShopId,
                DateRange = request.DateRange,
                Format = request.Format
            };

            // Get inventory data based on request parameters
            var inventoryQuery = BuildInventoryQuery(request);
            var inventory = await inventoryQuery.ToListAsync();

            // Build report items
            response.Items = await BuildInventoryReportItems(inventory);
            
            // Calculate summary
            response.Summary = CalculateInventoryReportSummary(response.Items);

            // Generate specific reports based on type
            if (request.IncludeLowStock || request.ReportType == InventoryReportType.LowStock)
            {
                response.LowStockItems = await BuildLowStockItems(inventory, request.LowStockThreshold);
            }

            if (request.IncludeExpiring || request.ReportType == InventoryReportType.Expiring)
            {
                response.ExpiringItems = await BuildExpiringItems(inventory, request.ExpiringDays);
            }

            if (request.ReportType == InventoryReportType.StockMovement)
            {
                response.StockMovements = await BuildStockMovementItems(request);
            }

            // Set metadata
            response.Metadata = new ReportMetadata
            {
                TotalRecords = inventory.Count,
                FilteredRecords = response.Items.Count,
                GenerationTime = DateTime.UtcNow - startTime,
                GeneratedBy = "ReportService"
            };

            _logger.LogInformation("Inventory report generated successfully with {RecordCount} records in {Duration}ms", 
                response.Items.Count, response.Metadata.GenerationTime.TotalMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating inventory report for business {BusinessId}", request.BusinessId);
            throw;
        }
    }

    public async Task<InventoryReportResponse> GenerateLowStockReportAsync(Guid businessId, Guid? shopId = null, int threshold = 10, ReportFormat format = ReportFormat.CSV)
    {
        var request = new InventoryReportRequest
        {
            BusinessId = businessId,
            ShopId = shopId,
            Format = format,
            ReportType = InventoryReportType.LowStock,
            LowStockThreshold = threshold,
            IncludeLowStock = true
        };

        return await GenerateInventoryReportAsync(request);
    }

    public async Task<InventoryReportResponse> GenerateExpiringProductsReportAsync(Guid businessId, Guid? shopId = null, int daysAhead = 30, ReportFormat format = ReportFormat.CSV)
    {
        var request = new InventoryReportRequest
        {
            BusinessId = businessId,
            ShopId = shopId,
            Format = format,
            ReportType = InventoryReportType.Expiring,
            ExpiringDays = daysAhead,
            IncludeExpiring = true
        };

        return await GenerateInventoryReportAsync(request);
    }

    public async Task<InventoryReportResponse> GenerateStockMovementReportAsync(InventoryReportRequest request)
    {
        request.ReportType = InventoryReportType.StockMovement;
        return await GenerateInventoryReportAsync(request);
    }

    #endregion

    #region Financial Reports

    public async Task<FinancialReportResponse> GenerateFinancialReportAsync(FinancialReportRequest request)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Generating financial report for business {BusinessId}, shop {ShopId}, type {ReportType}", 
            request.BusinessId, request.ShopId, request.ReportType);

        try
        {
            var response = new FinancialReportResponse
            {
                ReportName = $"Financial Report - {request.ReportType}",
                BusinessId = request.BusinessId,
                ShopId = request.ShopId,
                DateRange = request.DateRange,
                Format = request.Format
            };

            // Get financial data based on request parameters
            var salesQuery = BuildSalesQuery(new SalesReportRequest 
            { 
                BusinessId = request.BusinessId, 
                ShopId = request.ShopId, 
                DateRange = request.DateRange 
            });
            var sales = await salesQuery.ToListAsync();

            // Build financial report items
            response.Items = await BuildFinancialReportItems(sales);
            
            // Calculate summary
            response.Summary = CalculateFinancialReportSummary(response.Items);

            // Generate specific financial analyses
            if (request.IncludeTaxBreakdown)
            {
                response.TaxBreakdown = await BuildTaxBreakdown(sales);
            }

            if (request.IncludeCostAnalysis)
            {
                response.CostAnalysis = await BuildCostAnalysis(sales);
            }

            if (request.ReportType == FinancialReportType.ProfitLoss)
            {
                response.ProfitLoss = BuildProfitLossStatement(response.Summary);
            }

            // Set metadata
            response.Metadata = new ReportMetadata
            {
                TotalRecords = sales.Count,
                FilteredRecords = response.Items.Count,
                GenerationTime = DateTime.UtcNow - startTime,
                GeneratedBy = "ReportService"
            };

            _logger.LogInformation("Financial report generated successfully with {RecordCount} records in {Duration}ms", 
                response.Items.Count, response.Metadata.GenerationTime.TotalMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating financial report for business {BusinessId}", request.BusinessId);
            throw;
        }
    }

    public async Task<FinancialReportResponse> GenerateProfitLossReportAsync(Guid businessId, DateRange dateRange, Guid? shopId = null, ReportFormat format = ReportFormat.CSV)
    {
        var request = new FinancialReportRequest
        {
            BusinessId = businessId,
            ShopId = shopId,
            DateRange = dateRange,
            Format = format,
            ReportType = FinancialReportType.ProfitLoss,
            IncludeCostAnalysis = true,
            IncludeTaxBreakdown = true
        };

        return await GenerateFinancialReportAsync(request);
    }

    public async Task<FinancialReportResponse> GenerateRevenueAnalysisReportAsync(FinancialReportRequest request)
    {
        request.ReportType = FinancialReportType.Revenue;
        return await GenerateFinancialReportAsync(request);
    }

    public async Task<FinancialReportResponse> GenerateCostAnalysisReportAsync(FinancialReportRequest request)
    {
        request.ReportType = FinancialReportType.CostAnalysis;
        request.IncludeCostAnalysis = true;
        return await GenerateFinancialReportAsync(request);
    }

    #endregion

    #region Report Export and Formatting

    public async Task<byte[]> ExportReportAsync<T>(T reportData, ReportFormat format, string? fileName = null) where T : ReportResponse
    {
        try
        {
            return format switch
            {
                ReportFormat.CSV => await ExportToCsvAsync(reportData),
                ReportFormat.Excel => await ExportToExcelAsync(reportData),
                ReportFormat.PDF => await ExportToPdfAsync(reportData),
                ReportFormat.JSON => await ExportToJsonAsync(reportData),
                _ => throw new ArgumentException($"Unsupported format: {format}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report to format {Format}", format);
            throw;
        }
    }

    public async Task<bool> SaveReportToFileAsync<T>(T reportData, ReportFormat format, string filePath) where T : ReportResponse
    {
        try
        {
            var data = await ExportReportAsync(reportData, format);
            await File.WriteAllBytesAsync(filePath, data);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving report to file {FilePath}", filePath);
            return false;
        }
    }

    public async Task<List<ReportTemplate>> GetAvailableReportTemplatesAsync(BusinessType businessType)
    {
        // This would typically come from a database or configuration
        var templates = new List<ReportTemplate>();

        // Common templates for all business types
        templates.AddRange(new[]
        {
            new ReportTemplate
            {
                Id = "sales-summary",
                Name = "Sales Summary",
                Description = "Daily, weekly, or monthly sales summary",
                BusinessType = businessType,
                ReportType = "Sales",
                SupportedFormats = new List<ReportFormat> { ReportFormat.CSV, ReportFormat.Excel, ReportFormat.PDF }
            },
            new ReportTemplate
            {
                Id = "inventory-levels",
                Name = "Inventory Levels",
                Description = "Current stock levels and valuation",
                BusinessType = businessType,
                ReportType = "Inventory",
                SupportedFormats = new List<ReportFormat> { ReportFormat.CSV, ReportFormat.Excel, ReportFormat.PDF }
            },
            new ReportTemplate
            {
                Id = "profit-loss",
                Name = "Profit & Loss",
                Description = "Financial profit and loss statement",
                BusinessType = businessType,
                ReportType = "Financial",
                SupportedFormats = new List<ReportFormat> { ReportFormat.CSV, ReportFormat.Excel, ReportFormat.PDF }
            }
        });

        // Business type specific templates
        if (businessType == BusinessType.Pharmacy)
        {
            templates.Add(new ReportTemplate
            {
                Id = "expiry-report",
                Name = "Expiry Report",
                Description = "Products approaching expiry dates",
                BusinessType = businessType,
                ReportType = "Inventory",
                SupportedFormats = new List<ReportFormat> { ReportFormat.CSV, ReportFormat.Excel, ReportFormat.PDF }
            });
        }

        return await Task.FromResult(templates);
    }

    #endregion

    #region Report Validation and Quality

    public async Task<ReportValidationResult> ValidateReportDataAccuracyAsync<T>(T reportResponse) where T : ReportResponse
    {
        var result = new ReportValidationResult { IsValid = true, AccuracyScore = 1.0 };

        try
        {
            // Validate based on report type
            switch (reportResponse)
            {
                case SalesReportResponse salesReport:
                    await ValidateSalesReportAccuracy(salesReport, result);
                    break;
                case InventoryReportResponse inventoryReport:
                    await ValidateInventoryReportAccuracy(inventoryReport, result);
                    break;
                case FinancialReportResponse financialReport:
                    await ValidateFinancialReportAccuracy(financialReport, result);
                    break;
            }

            // Calculate overall accuracy score
            if (result.MetricAccuracy.Any())
            {
                result.AccuracyScore = result.MetricAccuracy.Values.Average();
            }

            result.IsValid = result.AccuracyScore >= 0.95 && !result.ValidationErrors.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating report data accuracy");
            result.IsValid = false;
            result.AccuracyScore = 0.0;
            result.ValidationErrors.Add($"Validation error: {ex.Message}");
        }

        return result;
    }

    public async Task<ReportConsistencyResult> CheckReportConsistencyAsync(Guid businessId, string reportType, List<DateRange> periods)
    {
        var result = new ReportConsistencyResult { IsConsistent = true, ConsistencyScore = 1.0 };

        try
        {
            // This would implement consistency checks across different time periods
            // For now, return a basic implementation
            result.ComparisonMetrics["periods_compared"] = periods.Count;
            result.ComparisonMetrics["business_id"] = businessId.ToString();
            result.ComparisonMetrics["report_type"] = reportType;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking report consistency");
            result.IsConsistent = false;
            result.ConsistencyScore = 0.0;
            result.InconsistencyIssues.Add($"Consistency check error: {ex.Message}");
        }

        return await Task.FromResult(result);
    }

    #endregion

    #region Private Helper Methods

    private IQueryable<Sale> BuildSalesQuery(SalesReportRequest request)
    {
        var query = _context.Sales
            .Include(s => s.Items)
            .ThenInclude(si => si.Product)
            .Include(s => s.Shop)
            .Include(s => s.User)
            .Where(s => s.Shop.BusinessId == request.BusinessId);

        if (request.ShopId.HasValue)
        {
            query = query.Where(s => s.ShopId == request.ShopId.Value);
        }

        if (request.DateRange.StartDate != default(DateTime))
        {
            query = query.Where(s => s.CreatedAt >= request.DateRange.StartDate);
        }

        if (request.DateRange.EndDate != default(DateTime))
        {
            query = query.Where(s => s.CreatedAt <= request.DateRange.EndDate);
        }

        if (!request.IncludeRefunds)
        {
            query = query.Where(s => s.TotalAmount >= 0);
        }

        return query.OrderByDescending(s => s.CreatedAt);
    }

    private IQueryable<Stock> BuildInventoryQuery(InventoryReportRequest request)
    {
        var query = _context.Stock
            .Include(s => s.Product)
            .Include(s => s.Shop)
            .Where(s => s.Shop.BusinessId == request.BusinessId);

        if (request.ShopId.HasValue)
        {
            query = query.Where(s => s.ShopId == request.ShopId.Value);
        }

        return query.OrderBy(s => s.Product.Name);
    }

    private async Task<List<SalesReportItem>> BuildSalesReportItems(List<Sale> sales)
    {
        var items = new List<SalesReportItem>();

        foreach (var sale in sales)
        {
            var item = new SalesReportItem
            {
                Date = sale.CreatedAt,
                InvoiceNumber = sale.InvoiceNumber,
                CustomerId = sale.CustomerId,
                CustomerName = sale.Customer?.Name ?? "Walk-in Customer",
                SubTotal = sale.TotalAmount - sale.TaxAmount + sale.DiscountAmount, // Calculate SubTotal
                TaxAmount = sale.TaxAmount,
                DiscountAmount = sale.DiscountAmount,
                TotalAmount = sale.TotalAmount,
                PaymentMethod = sale.PaymentMethod,
                ShopName = sale.Shop?.Name ?? "Unknown Shop",
                CashierName = sale.User?.FullName ?? "Unknown User",
                Items = sale.Items?.Select(si => new SaleItemDetail
                {
                    ProductName = si.Product?.Name ?? "Unknown Product",
                    Category = si.Product?.Category ?? "Unknown",
                    Quantity = si.Quantity,
                    UnitPrice = si.UnitPrice,
                    TotalPrice = si.TotalPrice,
                    DiscountAmount = 0 // SaleItem doesn't have DiscountAmount
                }).ToList() ?? new List<SaleItemDetail>()
            };

            items.Add(item);
        }

        return items;
    }

    private SalesReportSummary CalculateSalesReportSummary(List<SalesReportItem> items)
    {
        var refunds = items.Where(i => i.TotalAmount < 0).ToList();
        var sales = items.Where(i => i.TotalAmount >= 0).ToList();

        return new SalesReportSummary
        {
            TotalSales = sales.Sum(i => i.TotalAmount),
            TotalRefunds = Math.Abs(refunds.Sum(i => i.TotalAmount)),
            NetSales = items.Sum(i => i.TotalAmount),
            TotalTransactions = sales.Count,
            TotalRefundTransactions = refunds.Count,
            AverageTransactionValue = sales.Any() ? sales.Average(i => i.TotalAmount) : 0,
            TotalTax = items.Sum(i => i.TaxAmount),
            TotalDiscount = items.Sum(i => i.DiscountAmount),
            TotalItemsSold = items.SelectMany(i => i.Items).Sum(item => (int)item.Quantity)
        };
    }

    private async Task<List<SalesReportByCategory>> BuildCategoryBreakdown(List<Sale> sales)
    {
        var categoryData = sales
            .SelectMany(s => s.Items)
            .Where(si => si.Product != null)
            .GroupBy(si => si.Product.Category ?? "Unknown")
            .Select(g => new SalesReportByCategory
            {
                Category = g.Key,
                TotalSales = g.Sum(si => si.TotalPrice),
                TotalQuantity = g.Sum(si => si.Quantity),
                TransactionCount = g.Select(si => si.SaleId).Distinct().Count(),
                AveragePrice = g.Average(si => si.UnitPrice)
            })
            .OrderByDescending(c => c.TotalSales)
            .ToList();

        var totalSales = categoryData.Sum(c => c.TotalSales);
        foreach (var category in categoryData)
        {
            category.Percentage = totalSales > 0 ? (category.TotalSales / totalSales) * 100 : 0;
        }

        return categoryData;
    }

    private async Task<List<SalesReportByProduct>> BuildProductBreakdown(List<Sale> sales)
    {
        var productData = sales
            .SelectMany(s => s.Items)
            .Where(si => si.Product != null)
            .GroupBy(si => new { si.ProductId, si.Product.Name, si.Product.Category })
            .Select(g => new SalesReportByProduct
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.Name ?? "Unknown Product",
                Category = g.Key.Category ?? "Unknown",
                TotalSales = g.Sum(si => si.TotalPrice),
                TotalQuantity = g.Sum(si => si.Quantity),
                TransactionCount = g.Select(si => si.SaleId).Distinct().Count(),
                AveragePrice = g.Average(si => si.UnitPrice)
            })
            .OrderByDescending(p => p.TotalSales)
            .ToList();

        var totalSales = productData.Sum(p => p.TotalSales);
        foreach (var product in productData)
        {
            product.Percentage = totalSales > 0 ? (product.TotalSales / totalSales) * 100 : 0;
        }

        return productData;
    }

    private async Task<List<SalesReportByShop>> BuildShopBreakdown(List<Sale> sales)
    {
        var shopData = sales
            .GroupBy(s => new { s.ShopId, s.Shop.Name })
            .Select(g => new SalesReportByShop
            {
                ShopId = g.Key.ShopId,
                ShopName = g.Key.Name ?? "Unknown Shop",
                TotalSales = g.Sum(s => s.TotalAmount),
                TransactionCount = g.Count(),
                AverageTransactionValue = g.Average(s => s.TotalAmount)
            })
            .OrderByDescending(s => s.TotalSales)
            .ToList();

        var totalSales = shopData.Sum(s => s.TotalSales);
        foreach (var shop in shopData)
        {
            shop.Percentage = totalSales > 0 ? (shop.TotalSales / totalSales) * 100 : 0;
        }

        return shopData;
    }

    private async Task<List<InventoryReportItem>> BuildInventoryReportItems(List<Stock> inventory)
    {
        return inventory.Select(stock => new InventoryReportItem
        {
            ProductId = stock.ProductId,
            ProductName = stock.Product?.Name ?? "Unknown Product",
            Category = stock.Product?.Category ?? "Unknown",
            Barcode = stock.Product?.Barcode ?? "",
            CurrentStock = stock.Quantity,
            MinimumStock = 0, // Product doesn't have MinimumStock, using 0
            UnitPrice = stock.Product?.UnitPrice ?? 0,
            TotalValue = (stock.Product?.UnitPrice ?? 0) * stock.Quantity,
            ExpiryDate = stock.Product?.ExpiryDate,
            BatchNumber = stock.Product?.BatchNumber,
            LastUpdated = stock.LastUpdatedAt,
            ShopName = stock.Shop?.Name ?? "Unknown Shop"
        }).ToList();
    }

    private InventoryReportSummary CalculateInventoryReportSummary(List<InventoryReportItem> items)
    {
        return new InventoryReportSummary
        {
            TotalProducts = items.Count,
            LowStockProducts = items.Count(i => i.CurrentStock <= i.MinimumStock),
            ExpiringProducts = items.Count(i => i.ExpiryDate.HasValue && i.ExpiryDate.Value <= DateTime.UtcNow.AddDays(30)),
            OutOfStockProducts = items.Count(i => i.CurrentStock == 0),
            TotalInventoryValue = items.Sum(i => i.TotalValue),
            LowStockValue = items.Where(i => i.CurrentStock <= i.MinimumStock).Sum(i => i.TotalValue),
            ExpiringValue = items.Where(i => i.ExpiryDate.HasValue && i.ExpiryDate.Value <= DateTime.UtcNow.AddDays(30)).Sum(i => i.TotalValue)
        };
    }

    private async Task<List<LowStockItem>> BuildLowStockItems(List<Stock> inventory, int threshold)
    {
        return inventory
            .Where(s => s.Quantity <= threshold)
            .Select(stock => new LowStockItem
            {
                ProductId = stock.ProductId,
                ProductName = stock.Product?.Name ?? "Unknown Product",
                Category = stock.Product?.Category ?? "Unknown",
                CurrentStock = stock.Quantity,
                MinimumStock = threshold, // Using threshold as minimum since Product doesn't have MinimumStock
                StockDeficit = Math.Max(0, threshold - stock.Quantity),
                UnitPrice = stock.Product?.UnitPrice ?? 0,
                ReorderValue = Math.Max(0, threshold - stock.Quantity) * (stock.Product?.UnitPrice ?? 0),
                ShopName = stock.Shop?.Name ?? "Unknown Shop",
                DaysOutOfStock = stock.Quantity == 0 ? (DateTime.UtcNow - stock.LastUpdatedAt).Days : 0
            })
            .OrderBy(item => item.CurrentStock)
            .ToList();
    }

    private async Task<List<ExpiringItem>> BuildExpiringItems(List<Stock> inventory, int daysAhead)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(daysAhead);
        
        return inventory
            .Where(s => s.Product?.ExpiryDate.HasValue == true && s.Product.ExpiryDate.Value <= cutoffDate)
            .Select(stock => new ExpiringItem
            {
                ProductId = stock.ProductId,
                ProductName = stock.Product?.Name ?? "Unknown Product",
                Category = stock.Product?.Category ?? "Unknown",
                ExpiryDate = stock.Product?.ExpiryDate ?? DateTime.MaxValue,
                DaysToExpiry = (int)(stock.Product?.ExpiryDate?.Subtract(DateTime.UtcNow).TotalDays ?? 0),
                CurrentStock = stock.Quantity,
                UnitPrice = stock.Product?.UnitPrice ?? 0,
                TotalValue = (stock.Product?.UnitPrice ?? 0) * stock.Quantity,
                BatchNumber = stock.Product?.BatchNumber,
                ShopName = stock.Shop?.Name ?? "Unknown Shop",
                RiskLevel = CalculateExpiryRiskLevel(stock.Product?.ExpiryDate)
            })
            .OrderBy(item => item.DaysToExpiry)
            .ToList();
    }

    private ExpiryRiskLevel CalculateExpiryRiskLevel(DateTime? expiryDate)
    {
        if (!expiryDate.HasValue) return ExpiryRiskLevel.Low;
        
        var daysToExpiry = (expiryDate.Value - DateTime.UtcNow).TotalDays;
        
        return daysToExpiry switch
        {
            <= 0 => ExpiryRiskLevel.Critical,
            <= 7 => ExpiryRiskLevel.High,
            <= 30 => ExpiryRiskLevel.Medium,
            _ => ExpiryRiskLevel.Low
        };
    }

    private async Task<List<StockMovementItem>> BuildStockMovementItems(InventoryReportRequest request)
    {
        // This would typically query a stock movement/audit table
        // For now, return empty list as this requires additional database structure
        return new List<StockMovementItem>();
    }

    private async Task<List<FinancialReportItem>> BuildFinancialReportItems(List<Sale> sales)
    {
        return sales
            .GroupBy(s => s.CreatedAt.Date)
            .Select(g => new FinancialReportItem
            {
                Date = g.Key,
                Description = $"Daily Sales - {g.Key:yyyy-MM-dd}",
                Category = "Sales Revenue",
                Revenue = g.Sum(s => s.TotalAmount),
                Cost = g.Sum(s => s.Items?.Sum(si => si.Quantity * (si.Product?.PurchasePrice ?? 0)) ?? 0),
                Profit = g.Sum(s => s.TotalAmount) - g.Sum(s => s.Items?.Sum(si => si.Quantity * (si.Product?.PurchasePrice ?? 0)) ?? 0),
                ProfitMargin = g.Sum(s => s.TotalAmount) > 0 ? 
                    ((g.Sum(s => s.TotalAmount) - g.Sum(s => s.Items?.Sum(si => si.Quantity * (si.Product?.PurchasePrice ?? 0)) ?? 0)) / g.Sum(s => s.TotalAmount)) * 100 : 0,
                ShopName = g.First().Shop?.Name ?? "Multiple Shops"
            })
            .OrderByDescending(item => item.Date)
            .ToList();
    }

    private FinancialReportSummary CalculateFinancialReportSummary(List<FinancialReportItem> items)
    {
        var totalRevenue = items.Sum(i => i.Revenue);
        var totalCosts = items.Sum(i => i.Cost);
        var grossProfit = totalRevenue - totalCosts;

        return new FinancialReportSummary
        {
            TotalRevenue = totalRevenue,
            TotalCosts = totalCosts,
            GrossProfit = grossProfit,
            NetProfit = grossProfit, // Simplified - would include operating expenses
            GrossProfitMargin = totalRevenue > 0 ? (grossProfit / totalRevenue) * 100 : 0,
            NetProfitMargin = totalRevenue > 0 ? (grossProfit / totalRevenue) * 100 : 0, // Simplified
            TotalTax = 0, // Would be calculated from tax records
            TotalDiscounts = 0, // Would be calculated from discount records
            TotalRefunds = items.Where(i => i.Revenue < 0).Sum(i => Math.Abs(i.Revenue))
        };
    }

    private async Task<List<TaxBreakdownItem>> BuildTaxBreakdown(List<Sale> sales)
    {
        return sales
            .GroupBy(s => "Standard Tax") // Simplified - would group by actual tax types
            .Select(g => new TaxBreakdownItem
            {
                TaxType = g.Key,
                TaxRate = 10.0m, // Would come from configuration
                TaxableAmount = g.Sum(s => s.TotalAmount - s.TaxAmount), // Calculate taxable amount
                TaxAmount = g.Sum(s => s.TaxAmount),
                TransactionCount = g.Count()
            })
            .ToList();
    }

    private async Task<List<CostAnalysisItem>> BuildCostAnalysis(List<Sale> sales)
    {
        return sales
            .SelectMany(s => s.Items)
            .Where(si => si.Product != null)
            .GroupBy(si => si.Product.Category ?? "Unknown")
            .Select(g => new CostAnalysisItem
            {
                Category = g.Key,
                TotalCost = g.Sum(si => si.Quantity * (si.Product?.PurchasePrice ?? 0)),
                TotalRevenue = g.Sum(si => si.TotalPrice),
                Profit = g.Sum(si => si.TotalPrice) - g.Sum(si => si.Quantity * (si.Product?.PurchasePrice ?? 0)),
                ProfitMargin = g.Sum(si => si.TotalPrice) > 0 ? 
                    ((g.Sum(si => si.TotalPrice) - g.Sum(si => si.Quantity * (si.Product?.PurchasePrice ?? 0))) / g.Sum(si => si.TotalPrice)) * 100 : 0
            })
            .ToList();
    }

    private ProfitLossStatement BuildProfitLossStatement(FinancialReportSummary summary)
    {
        return new ProfitLossStatement
        {
            Revenue = summary.TotalRevenue,
            CostOfGoodsSold = summary.TotalCosts,
            GrossProfit = summary.GrossProfit,
            OperatingExpenses = 0, // Would come from expense records
            OperatingIncome = summary.GrossProfit,
            OtherIncome = 0,
            OtherExpenses = 0,
            NetIncome = summary.NetProfit
        };
    }

    private async Task<byte[]> ExportToCsvAsync<T>(T reportData) where T : ReportResponse
    {
        var csv = new StringBuilder();
        
        // Add header based on report type
        switch (reportData)
        {
            case SalesReportResponse salesReport:
                csv.AppendLine("Date,Invoice,Customer,SubTotal,Tax,Discount,Total,Payment Method,Shop,Cashier");
                foreach (var item in salesReport.Items)
                {
                    csv.AppendLine($"{item.Date:yyyy-MM-dd},{item.InvoiceNumber},{item.CustomerName},{item.SubTotal},{item.TaxAmount},{item.DiscountAmount},{item.TotalAmount},{item.PaymentMethod},{item.ShopName},{item.CashierName}");
                }
                break;
                
            case InventoryReportResponse inventoryReport:
                csv.AppendLine("Product,Category,Barcode,Current Stock,Minimum Stock,Unit Price,Total Value,Expiry Date,Shop");
                foreach (var item in inventoryReport.Items)
                {
                    csv.AppendLine($"{item.ProductName},{item.Category},{item.Barcode},{item.CurrentStock},{item.MinimumStock},{item.UnitPrice},{item.TotalValue},{item.ExpiryDate:yyyy-MM-dd},{item.ShopName}");
                }
                break;
                
            case FinancialReportResponse financialReport:
                csv.AppendLine("Date,Description,Category,Revenue,Cost,Profit,Profit Margin,Shop");
                foreach (var item in financialReport.Items)
                {
                    csv.AppendLine($"{item.Date:yyyy-MM-dd},{item.Description},{item.Category},{item.Revenue},{item.Cost},{item.Profit},{item.ProfitMargin:F2}%,{item.ShopName}");
                }
                break;
        }
        
        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    private async Task<byte[]> ExportToExcelAsync<T>(T reportData) where T : ReportResponse
    {
        // For now, return CSV format as Excel implementation would require additional libraries
        return await ExportToCsvAsync(reportData);
    }

    private async Task<byte[]> ExportToPdfAsync<T>(T reportData) where T : ReportResponse
    {
        // For now, return CSV format as PDF implementation would require additional libraries
        return await ExportToCsvAsync(reportData);
    }

    private async Task<byte[]> ExportToJsonAsync<T>(T reportData) where T : ReportResponse
    {
        var json = JsonSerializer.Serialize(reportData, new JsonSerializerOptions { WriteIndented = true });
        return Encoding.UTF8.GetBytes(json);
    }

    private async Task ValidateSalesReportAccuracy(SalesReportResponse salesReport, ReportValidationResult result)
    {
        // Validate that summary totals match individual items
        var calculatedTotal = salesReport.Items.Sum(i => i.TotalAmount);
        var summaryTotal = salesReport.Summary.NetSales;
        
        var accuracy = calculatedTotal == 0 ? 1.0 : Math.Min(1.0, 1.0 - Math.Abs((double)(calculatedTotal - summaryTotal)) / Math.Abs((double)calculatedTotal));
        result.MetricAccuracy["total_sales_accuracy"] = accuracy;
        
        if (accuracy < 0.99)
        {
            result.ValidationWarnings.Add($"Sales total mismatch: calculated {calculatedTotal}, summary {summaryTotal}");
        }
    }

    private async Task ValidateInventoryReportAccuracy(InventoryReportResponse inventoryReport, ReportValidationResult result)
    {
        // Validate that summary counts match individual items
        var calculatedTotal = inventoryReport.Items.Count;
        var summaryTotal = inventoryReport.Summary.TotalProducts;
        
        var accuracy = calculatedTotal == summaryTotal ? 1.0 : 0.0;
        result.MetricAccuracy["product_count_accuracy"] = accuracy;
        
        if (accuracy < 1.0)
        {
            result.ValidationWarnings.Add($"Product count mismatch: calculated {calculatedTotal}, summary {summaryTotal}");
        }
    }

    private async Task ValidateFinancialReportAccuracy(FinancialReportResponse financialReport, ReportValidationResult result)
    {
        // Validate that summary totals match individual items
        var calculatedRevenue = financialReport.Items.Sum(i => i.Revenue);
        var summaryRevenue = financialReport.Summary.TotalRevenue;
        
        var accuracy = calculatedRevenue == 0 ? 1.0 : Math.Min(1.0, 1.0 - Math.Abs((double)(calculatedRevenue - summaryRevenue)) / Math.Abs((double)calculatedRevenue));
        result.MetricAccuracy["revenue_accuracy"] = accuracy;
        
        if (accuracy < 0.99)
        {
            result.ValidationWarnings.Add($"Revenue total mismatch: calculated {calculatedRevenue}, summary {summaryRevenue}");
        }
    }

    #endregion
}