using Shared.Core.DTOs;

namespace WebDashboard.Services;

public interface IDashboardApiService
{
    Task<DashboardOverview> GetDashboardOverviewAsync(Guid businessId, DashboardFilter filter);
    Task<IEnumerable<AlertSummary>> GetActiveAlertsAsync(Guid businessId);
    Task<SalesAnalytics> GetSalesAnalyticsAsync(Guid businessId, DateRange dateRange);
    Task<InventoryAnalytics> GetInventoryAnalyticsAsync(Guid businessId);
    Task<FinancialReport> GetFinancialReportAsync(Guid businessId, DateRange dateRange);
}