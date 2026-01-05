using Shared.Core.DTOs;
using System.Text.Json;

namespace WebDashboard.Services;

public class DashboardApiService : IDashboardApiService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public DashboardApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<DashboardOverview> GetDashboardOverviewAsync(Guid businessId, DashboardFilter? filter = null)
    {
        await Task.Delay(100); // Simulate async operation
        
        // Return mock data for demo
        return new DashboardOverview
        {
            BusinessId = businessId,
            BusinessName = "Demo Business",
            BusinessType = global::Shared.Core.Enums.BusinessType.GeneralRetail,
            TotalShops = 2,
            RealTimeSales = new RealTimeSalesData
            {
                TodayRevenue = 2500.50m,
                TodayTransactionCount = 45,
                AverageOrderValue = 55.57m,
                YesterdayRevenue = 2200.00m,
                RevenueChangePercentage = 13.66m,
                TransactionChangePercentage = 8,
                LastUpdated = DateTime.UtcNow
            },
            InventoryStatus = new InventoryStatusSummary
            {
                TotalProducts = 250,
                LowStockProducts = 12,
                OutOfStockProducts = 3,
                ExpiringProducts = 5,
                TotalInventoryValue = 45000.00m
            },
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<IEnumerable<AlertSummary>> GetActiveAlertsAsync(Guid businessId)
    {
        await Task.Delay(100);
        
        return new List<AlertSummary>
        {
            new AlertSummary
            {
                Type = AlertType.LowStock,
                Priority = AlertPriority.Medium,
                Title = "Low Stock Alert",
                Message = "12 products are running low on stock",
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                IsRead = false
            },
            new AlertSummary
            {
                Type = AlertType.OutOfStock,
                Priority = AlertPriority.High,
                Title = "Out of Stock",
                Message = "3 products are completely out of stock",
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                IsRead = false
            }
        };
    }

    public async Task<SalesAnalytics> GetSalesAnalyticsAsync(Guid businessId, DateRange dateRange)
    {
        await Task.Delay(100);
        
        return new SalesAnalytics
        {
            BusinessId = businessId,
            Period = dateRange,
            TotalRevenue = 15000.00m,
            TotalTransactions = 300,
            AverageOrderValue = 50.00m,
            ItemsSold = 750,
            RevenueGrowth = 12.5m,
            TransactionGrowth = 8.3m,
            AOVGrowth = 3.8m,
            ItemsGrowth = 15.2m,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<InventoryAnalytics> GetInventoryAnalyticsAsync(Guid businessId)
    {
        await Task.Delay(100);
        
        return new InventoryAnalytics
        {
            BusinessId = businessId,
            TotalProducts = 250,
            TotalInventoryValue = 45000.00m,
            LowStockProducts = 12,
            OutOfStockProducts = 3,
            ExpiringProducts = 5,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<FinancialReport> GetFinancialReportAsync(Guid businessId, DateRange dateRange)
    {
        await Task.Delay(100);
        
        return new FinancialReport
        {
            BusinessId = businessId,
            Period = dateRange,
            TotalRevenue = 15000.00m,
            TotalCosts = 9000.00m,
            GrossProfit = 6000.00m,
            NetProfit = 5500.00m,
            ProfitMargin = 36.67m,
            GeneratedAt = DateTime.UtcNow
        };
    }
}