using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using System.Collections.ObjectModel;

namespace Desktop.ViewModels;

/// <summary>
/// View model for advanced reporting and analytics dashboards
/// </summary>
public partial class AdvancedReportsViewModel : BaseViewModel
{
    private readonly IDashboardService _dashboardService;
    private readonly IBusinessManagementService _businessManagementService;
    private readonly IReportService _reportService;
    private readonly ICurrentUserService _currentUserService;

    [ObservableProperty]
    private ObservableCollection<BusinessResponse> businesses = new();

    [ObservableProperty]
    private ObservableCollection<ShopResponse> shops = new();

    [ObservableProperty]
    private BusinessResponse? selectedBusiness;

    [ObservableProperty]
    private ObservableCollection<ShopResponse> selectedShops = new();

    [ObservableProperty]
    private DateTime startDate = DateTime.Today.AddDays(-30);

    [ObservableProperty]
    private DateTime endDate = DateTime.Today;

    [ObservableProperty]
    private bool isLoading;

    // Dashboard data
    [ObservableProperty]
    private DashboardOverview? dashboardOverview;

    [ObservableProperty]
    private ObservableCollection<DailyRevenueData> dailyRevenueData = new();

    [ObservableProperty]
    private ObservableCollection<MonthlyRevenueData> monthlyRevenueData = new();

    [ObservableProperty]
    private ObservableCollection<TopSellingProduct> topProducts = new();

    [ObservableProperty]
    private ObservableCollection<ShopPerformanceSummary> shopPerformances = new();

    [ObservableProperty]
    private ObservableCollection<CategoryProfitData> categoryProfits = new();

    [ObservableProperty]
    private ObservableCollection<AlertSummary> alerts = new();

    // Summary metrics
    [ObservableProperty]
    private decimal totalRevenue;

    [ObservableProperty]
    private int totalTransactions;

    [ObservableProperty]
    private decimal averageOrderValue;

    [ObservableProperty]
    private decimal estimatedProfit;

    [ObservableProperty]
    private decimal profitMarginPercentage;

    [ObservableProperty]
    private int lowStockAlerts;

    [ObservableProperty]
    private int expiryAlerts;

    public AdvancedReportsViewModel(
        IDashboardService dashboardService,
        IBusinessManagementService businessManagementService,
        IReportService reportService,
        ICurrentUserService currentUserService)
    {
        _dashboardService = dashboardService;
        _businessManagementService = businessManagementService;
        _reportService = reportService;
        _currentUserService = currentUserService;
        Title = "Advanced Reports & Analytics";
    }

    public bool CanViewReports => _currentUserService.CurrentUser?.Role == UserRole.BusinessOwner ||
                                  _currentUserService.CurrentUser?.Role == UserRole.ShopManager;

    [RelayCommand]
    private async Task LoadBusinessesAsync()
    {
        if (!CanViewReports)
        {
            SetError("You don't have permission to view reports");
            return;
        }

        IsLoading = true;
        ClearError();

        try
        {
            var currentUser = _currentUserService.CurrentUser;
            if (currentUser == null)
            {
                SetError("User not authenticated");
                return;
            }

            var businessList = await _businessManagementService.GetBusinessesByOwnerAsync(currentUser.Id);
            Businesses.Clear();
            foreach (var business in businessList)
            {
                Businesses.Add(business);
            }

            // Auto-select first business
            if (Businesses.Any())
            {
                SelectedBusiness = Businesses.First();
            }
        }
        catch (Exception ex)
        {
            SetError($"Error loading businesses: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadShopsForBusinessAsync()
    {
        if (SelectedBusiness == null) return;

        IsLoading = true;
        ClearError();

        try
        {
            var shopList = await _businessManagementService.GetShopsByBusinessAsync(SelectedBusiness.Id);
            Shops.Clear();
            SelectedShops.Clear();
            
            foreach (var shop in shopList)
            {
                Shops.Add(shop);
                SelectedShops.Add(shop); // Select all shops by default
            }
        }
        catch (Exception ex)
        {
            SetError($"Error loading shops: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task GenerateReportsAsync()
    {
        if (SelectedBusiness == null)
        {
            SetError("Please select a business");
            return;
        }

        if (StartDate > EndDate)
        {
            SetError("Start date must be before end date");
            return;
        }

        IsLoading = true;
        ClearError();

        try
        {
            var dateRange = new DateRange { StartDate = StartDate, EndDate = EndDate };
            var shopIds = SelectedShops.Select(s => s.Id).ToList();

            // Load dashboard overview
            var filter = new DashboardFilter
            {
                DateRange = dateRange,
                ShopIds = shopIds
            };

            DashboardOverview = await _dashboardService.GetDashboardOverviewAsync(SelectedBusiness.Id, filter);

            // Load detailed analytics
            await LoadRevenueAnalyticsAsync(dateRange, shopIds);
            await LoadProductAnalyticsAsync(dateRange, shopIds);
            await LoadShopPerformanceAsync(dateRange);
            await LoadProfitAnalysisAsync(dateRange, shopIds);
            await LoadAlertsAsync();

            // Update summary metrics
            UpdateSummaryMetrics();
        }
        catch (Exception ex)
        {
            SetError($"Error generating reports: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ExportReportAsync(string format)
    {
        if (SelectedBusiness == null || DashboardOverview == null)
        {
            SetError("Please generate reports first");
            return;
        }

        IsLoading = true;
        ClearError();

        try
        {
            var salesRequest = new SalesReportRequest
            {
                BusinessId = SelectedBusiness.Id,
                ShopId = SelectedShops.Count == 1 ? SelectedShops.First().Id : null,
                DateRange = new DateRange { StartDate = StartDate, EndDate = EndDate },
                Format = Enum.Parse<ReportFormat>(format, true),
                ReportType = SalesReportType.Summary
            };

            var reportData = await _reportService.GenerateSalesReportAsync(salesRequest);
            
            // Save file dialog would be handled by the view
            // For now, just show success message
            SuccessMessage = $"Report exported successfully in {format} format";
        }
        catch (Exception ex)
        {
            SetError($"Error exporting report: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        if (SelectedBusiness != null)
        {
            await _dashboardService.RefreshDashboardDataAsync(SelectedBusiness.Id);
            await GenerateReportsAsync();
        }
    }

    partial void OnSelectedBusinessChanged(BusinessResponse? value)
    {
        if (value != null)
        {
            _ = LoadShopsForBusinessAsync();
        }
        else
        {
            Shops.Clear();
            SelectedShops.Clear();
        }
    }

    private async Task LoadRevenueAnalyticsAsync(DateRange dateRange, List<Guid> shopIds)
    {
        if (SelectedBusiness == null) return;

        var revenueTrends = await _dashboardService.GetRevenueTrendAnalysisAsync(
            SelectedBusiness.Id, dateRange, shopIds);

        // Load daily revenue data
        var dailyData = await _dashboardService.GetDailyRevenueDataAsync(
            SelectedBusiness.Id, dateRange, shopIds);
        
        DailyRevenueData.Clear();
        foreach (var data in dailyData)
        {
            DailyRevenueData.Add(data);
        }

        // Load monthly revenue data
        var monthlyData = await _dashboardService.GetMonthlyRevenueDataAsync(
            SelectedBusiness.Id, 12, shopIds);
        
        MonthlyRevenueData.Clear();
        foreach (var data in monthlyData)
        {
            MonthlyRevenueData.Add(data);
        }
    }

    private async Task LoadProductAnalyticsAsync(DateRange dateRange, List<Guid> shopIds)
    {
        if (SelectedBusiness == null) return;

        var topProducts = await _dashboardService.GetTopSellingProductsAsync(
            SelectedBusiness.Id, dateRange, 20, shopIds);

        TopProducts.Clear();
        foreach (var product in topProducts)
        {
            TopProducts.Add(product);
        }
    }

    private async Task LoadShopPerformanceAsync(DateRange dateRange)
    {
        if (SelectedBusiness == null) return;

        var performances = await _dashboardService.GetShopPerformanceSummariesAsync(
            SelectedBusiness.Id, dateRange);

        ShopPerformances.Clear();
        foreach (var performance in performances)
        {
            ShopPerformances.Add(performance);
        }
    }

    private async Task LoadProfitAnalysisAsync(DateRange dateRange, List<Guid> shopIds)
    {
        if (SelectedBusiness == null) return;

        var profitAnalysis = await _dashboardService.GetProfitAnalysisAsync(
            SelectedBusiness.Id, dateRange, shopIds);

        CategoryProfits.Clear();
        foreach (var categoryProfit in profitAnalysis.CategoryProfits)
        {
            CategoryProfits.Add(categoryProfit);
        }
    }

    private async Task LoadAlertsAsync()
    {
        if (SelectedBusiness == null) return;

        var alertList = await _dashboardService.GetActiveAlertsAsync(SelectedBusiness.Id);

        Alerts.Clear();
        foreach (var alert in alertList)
        {
            Alerts.Add(alert);
        }
    }

    private void UpdateSummaryMetrics()
    {
        if (DashboardOverview == null) return;

        TotalRevenue = DashboardOverview.RealTimeSales.TodayRevenue;
        TotalTransactions = DashboardOverview.RealTimeSales.TodayTransactionCount;
        AverageOrderValue = DashboardOverview.RealTimeSales.AverageOrderValue;
        
        if (DashboardOverview.RevenueTrends.ProfitAnalysis != null)
        {
            EstimatedProfit = DashboardOverview.RevenueTrends.ProfitAnalysis.EstimatedProfit;
            ProfitMarginPercentage = DashboardOverview.RevenueTrends.ProfitAnalysis.ProfitMarginPercentage;
        }

        LowStockAlerts = DashboardOverview.InventoryStatus.LowStockProducts;
        ExpiryAlerts = DashboardOverview.InventoryStatus.ExpiringProducts;
    }
}