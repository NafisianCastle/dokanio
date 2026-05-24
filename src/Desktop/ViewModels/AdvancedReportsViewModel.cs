using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using System.Collections.ObjectModel;
using Shared.Core.DTOs;
using Shared.Core.Services;

namespace Desktop.ViewModels;

public class AdvancedReportsViewModel : BaseViewModel
{
    private readonly IDashboardService _dashboardService;
    private readonly IBusinessManagementService _businessManagementService;
    private readonly IReportService _reportService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;

    [Reactive] public ObservableCollection<BusinessResponse> Businesses { get; set; } = new();
    [Reactive] public ObservableCollection<ShopResponse> Shops { get; set; } = new();
    [Reactive] public BusinessResponse? SelectedBusiness { get; set; }
    [Reactive] public ObservableCollection<ShopResponse> SelectedShops { get; set; } = new();
    [Reactive] public DateTime StartDate { get; set; } = DateTime.Today.AddDays(-30);
    [Reactive] public DateTime EndDate { get; set; } = DateTime.Today;
    [Reactive] public bool IsLoading { get; set; }

    [Reactive] public DashboardOverview? DashboardOverview { get; set; }
    [Reactive] public ObservableCollection<DailyRevenueData> DailyRevenueData { get; set; } = new();
    [Reactive] public ObservableCollection<MonthlyRevenueData> MonthlyRevenueData { get; set; } = new();
    [Reactive] public ObservableCollection<TopSellingProduct> TopProducts { get; set; } = new();
    [Reactive] public ObservableCollection<ShopPerformanceSummary> ShopPerformances { get; set; } = new();
    [Reactive] public ObservableCollection<CategoryProfitData> CategoryProfits { get; set; } = new();
    [Reactive] public ObservableCollection<AlertSummary> Alerts { get; set; } = new();

    [Reactive] public decimal TotalRevenue { get; set; }
    [Reactive] public int TotalTransactions { get; set; }
    [Reactive] public decimal AverageOrderValue { get; set; }
    [Reactive] public decimal EstimatedProfit { get; set; }
    [Reactive] public decimal ProfitMarginPercentage { get; set; }
    [Reactive] public int LowStockAlerts { get; set; }
    [Reactive] public int ExpiryAlerts { get; set; }

    public bool CanViewReports => _currentUserService.CurrentUser != null &&
                                  _authorizationService.CanAccessReports(_currentUserService.CurrentUser);

    public ReactiveCommand<Unit, Unit> LoadBusinessesCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadShopsForBusinessCommand { get; }
    public ReactiveCommand<Unit, Unit> GenerateReportsCommand { get; }
    public ReactiveCommand<string, Unit> ExportReportCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshDataCommand { get; }

    public AdvancedReportsViewModel(
        IDashboardService dashboardService,
        IBusinessManagementService businessManagementService,
        IReportService reportService,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService)
    {
        _dashboardService          = dashboardService;
        _businessManagementService = businessManagementService;
        _reportService             = reportService;
        _currentUserService        = currentUserService;
        _authorizationService      = authorizationService;
        Title = "Advanced Reports & Analytics";

        LoadBusinessesCommand       = ReactiveCommand.CreateFromTask(LoadBusinessesAsync);
        LoadShopsForBusinessCommand = ReactiveCommand.CreateFromTask(LoadShopsForBusinessAsync);
        GenerateReportsCommand      = ReactiveCommand.CreateFromTask(GenerateReportsAsync);
        ExportReportCommand         = ReactiveCommand.CreateFromTask<string>(ExportReportAsync);
        RefreshDataCommand          = ReactiveCommand.CreateFromTask(RefreshDataAsync);

        this.WhenAnyValue(x => x.SelectedBusiness).Subscribe(b =>
        {
            if (b != null) _ = LoadShopsForBusinessAsync();
            else { Shops.Clear(); SelectedShops.Clear(); }
        });
    }

    private async Task LoadBusinessesAsync()
    {
        if (!CanViewReports) { SetError("You don't have permission to view reports"); return; }

        IsLoading = true;
        ClearError();
        try
        {
            var user = _currentUserService.CurrentUser;
            if (user == null) { SetError("User not authenticated"); return; }

            var list = await _businessManagementService.GetBusinessesByOwnerAsync(user.Id);
            Businesses.Clear();
            foreach (var b in list) Businesses.Add(b);
            if (Businesses.Any()) SelectedBusiness = Businesses.First();
        }
        catch (Exception ex) { SetError($"Error loading businesses: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private async Task LoadShopsForBusinessAsync()
    {
        if (SelectedBusiness == null) return;
        IsLoading = true;
        ClearError();
        try
        {
            var list = await _businessManagementService.GetShopsByBusinessAsync(SelectedBusiness.Id);
            Shops.Clear(); SelectedShops.Clear();
            foreach (var s in list) { Shops.Add(s); SelectedShops.Add(s); }
        }
        catch (Exception ex) { SetError($"Error loading shops: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private async Task GenerateReportsAsync()
    {
        if (SelectedBusiness == null) { SetError("Please select a business"); return; }
        if (StartDate > EndDate) { SetError("Start date must be before end date"); return; }

        IsLoading = true;
        ClearError();
        try
        {
            var dateRange = new DateRange { StartDate = StartDate, EndDate = EndDate };
            var shopIds   = SelectedShops.Select(s => s.Id).ToList();

            DashboardOverview = await _dashboardService.GetDashboardOverviewAsync(
                SelectedBusiness.Id,
                new DashboardFilter { DateRange = dateRange, ShopIds = shopIds });

            await LoadRevenueAnalyticsAsync(dateRange, shopIds);
            await LoadProductAnalyticsAsync(dateRange, shopIds);
            await LoadShopPerformanceAsync(dateRange);
            await LoadProfitAnalysisAsync(dateRange, shopIds);
            await LoadAlertsAsync();
            UpdateSummaryMetrics();
        }
        catch (Exception ex) { SetError($"Error generating reports: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private async Task ExportReportAsync(string format)
    {
        if (SelectedBusiness == null || DashboardOverview == null)
        { SetError("Please generate reports first"); return; }

        IsLoading = true;
        ClearError();
        try
        {
            var request = new SalesReportRequest
            {
                BusinessId = SelectedBusiness.Id,
                ShopId     = SelectedShops.Count == 1 ? SelectedShops.First().Id : null,
                DateRange  = new DateRange { StartDate = StartDate, EndDate = EndDate },
                Format     = Enum.Parse<ReportFormat>(format, true),
                ReportType = SalesReportType.Summary
            };
            await _reportService.GenerateSalesReportAsync(request);
            SuccessMessage = $"Report exported successfully in {format} format";
        }
        catch (Exception ex) { SetError($"Error exporting report: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private async Task RefreshDataAsync()
    {
        if (SelectedBusiness != null)
        {
            await _dashboardService.RefreshDashboardDataAsync(SelectedBusiness.Id);
            await GenerateReportsAsync();
        }
    }

    private async Task LoadRevenueAnalyticsAsync(DateRange dateRange, List<Guid> shopIds)
    {
        if (SelectedBusiness == null) return;
        await _dashboardService.GetRevenueTrendAnalysisAsync(SelectedBusiness.Id, dateRange, shopIds);

        var daily = await _dashboardService.GetDailyRevenueDataAsync(SelectedBusiness.Id, dateRange, shopIds);
        DailyRevenueData.Clear();
        foreach (var d in daily) DailyRevenueData.Add(d);

        var monthly = await _dashboardService.GetMonthlyRevenueDataAsync(SelectedBusiness.Id, 12, shopIds);
        MonthlyRevenueData.Clear();
        foreach (var m in monthly) MonthlyRevenueData.Add(m);
    }

    private async Task LoadProductAnalyticsAsync(DateRange dateRange, List<Guid> shopIds)
    {
        if (SelectedBusiness == null) return;
        var top = await _dashboardService.GetTopSellingProductsAsync(SelectedBusiness.Id, dateRange, 20, shopIds);
        TopProducts.Clear();
        foreach (var p in top) TopProducts.Add(p);
    }

    private async Task LoadShopPerformanceAsync(DateRange dateRange)
    {
        if (SelectedBusiness == null) return;
        var perfs = await _dashboardService.GetShopPerformanceSummariesAsync(SelectedBusiness.Id, dateRange);
        ShopPerformances.Clear();
        foreach (var p in perfs) ShopPerformances.Add(p);
    }

    private async Task LoadProfitAnalysisAsync(DateRange dateRange, List<Guid> shopIds)
    {
        if (SelectedBusiness == null) return;
        var profit = await _dashboardService.GetProfitAnalysisAsync(SelectedBusiness.Id, dateRange, shopIds);
        CategoryProfits.Clear();
        foreach (var c in profit.CategoryProfits) CategoryProfits.Add(c);
    }

    private async Task LoadAlertsAsync()
    {
        if (SelectedBusiness == null) return;
        var list = await _dashboardService.GetActiveAlertsAsync(SelectedBusiness.Id);
        Alerts.Clear();
        foreach (var a in list) Alerts.Add(a);
    }

    private void UpdateSummaryMetrics()
    {
        if (DashboardOverview == null) return;
        TotalRevenue       = DashboardOverview.RealTimeSales.TodayRevenue;
        TotalTransactions  = DashboardOverview.RealTimeSales.TodayTransactionCount;
        AverageOrderValue  = DashboardOverview.RealTimeSales.AverageOrderValue;
        if (DashboardOverview.RevenueTrends.ProfitAnalysis != null)
        {
            EstimatedProfit        = DashboardOverview.RevenueTrends.ProfitAnalysis.EstimatedProfit;
            ProfitMarginPercentage = DashboardOverview.RevenueTrends.ProfitAnalysis.ProfitMarginPercentage;
        }
        LowStockAlerts = DashboardOverview.InventoryStatus.LowStockProducts;
        ExpiryAlerts   = DashboardOverview.InventoryStatus.ExpiringProducts;
    }
}
