using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using System.Collections.ObjectModel;
using Shared.Core.DTOs;
using Shared.Core.Enums;
using Shared.Core.Services;

namespace Desktop.ViewModels;

public class AIInventoryViewModel : BaseViewModel
{
    private readonly IEnhancedInventoryService _enhancedInventoryService;
    private readonly IBusinessManagementService _businessManagementService;
    private readonly IAIAnalyticsEngine _aiAnalyticsEngine;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;

    [Reactive] public ObservableCollection<BusinessResponse> Businesses { get; set; } = new();
    [Reactive] public ObservableCollection<ShopResponse> Shops { get; set; } = new();
    [Reactive] public BusinessResponse? SelectedBusiness { get; set; }
    [Reactive] public ShopResponse? SelectedShop { get; set; }
    [Reactive] public bool IsLoading { get; set; }

    [Reactive] public ObservableCollection<ReorderRecommendation> ReorderRecommendations { get; set; } = new();
    [Reactive] public ObservableCollection<OverstockAlert> OverstockAlerts { get; set; } = new();
    [Reactive] public ObservableCollection<ExpiryRiskAlert> ExpiryRiskAlerts { get; set; } = new();
    [Reactive] public ObservableCollection<SeasonalRecommendation> SeasonalRecommendations { get; set; } = new();
    [Reactive] public InventoryTurnoverAnalysis? TurnoverAnalysis { get; set; }
    [Reactive] public InventoryValueAnalysis? ValueAnalysis { get; set; }
    [Reactive] public ObservableCollection<ProductTurnoverInsight> ProductInsights { get; set; } = new();
    [Reactive] public ObservableCollection<CategoryValueInsight> CategoryInsights { get; set; } = new();

    [Reactive] public int TotalProducts { get; set; }
    [Reactive] public int LowStockProducts { get; set; }
    [Reactive] public int OverstockProducts { get; set; }
    [Reactive] public int ExpiringProducts { get; set; }
    [Reactive] public decimal TotalInventoryValue { get; set; }
    [Reactive] public decimal DeadStockValue { get; set; }
    [Reactive] public double AverageTurnoverRate { get; set; }
    [Reactive] public int CriticalReorders { get; set; }
    [Reactive] public int HighPriorityReorders { get; set; }

    public bool CanManageInventory => _currentUserService.CurrentUser != null &&
                                      _authorizationService.CanManageInventory(_currentUserService.CurrentUser);

    public ReactiveCommand<Unit, Unit> LoadBusinessesCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadShopsForBusinessCommand { get; }
    public ReactiveCommand<Unit, Unit> GenerateRecommendationsCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadInventoryAnalysisCommand { get; }
    public ReactiveCommand<int, Unit> PredictLowStockCommand { get; }
    public ReactiveCommand<double, Unit> GetOverstockAlertsCommand { get; }
    public ReactiveCommand<int, Unit> GetExpiryRiskAlertsCommand { get; }
    public ReactiveCommand<int, Unit> GetSeasonalRecommendationsCommand { get; }
    public ReactiveCommand<Guid, Unit> CalculateSafetyStockCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearMessagesCommand { get; }

    public AIInventoryViewModel(
        IAuthorizationService authorizationService,
        IEnhancedInventoryService enhancedInventoryService,
        IBusinessManagementService businessManagementService,
        IAIAnalyticsEngine aiAnalyticsEngine,
        ICurrentUserService currentUserService)
    {
        _authorizationService      = authorizationService;
        _enhancedInventoryService  = enhancedInventoryService;
        _businessManagementService = businessManagementService;
        _aiAnalyticsEngine         = aiAnalyticsEngine;
        _currentUserService        = currentUserService;
        Title = "AI Inventory Management";

        LoadBusinessesCommand          = ReactiveCommand.CreateFromTask(LoadBusinessesAsync);
        LoadShopsForBusinessCommand    = ReactiveCommand.CreateFromTask(LoadShopsForBusinessAsync);
        GenerateRecommendationsCommand = ReactiveCommand.CreateFromTask(GenerateRecommendationsAsync);
        LoadInventoryAnalysisCommand   = ReactiveCommand.CreateFromTask(LoadInventoryAnalysisAsync);
        PredictLowStockCommand         = ReactiveCommand.CreateFromTask<int>(PredictLowStockAsync);
        GetOverstockAlertsCommand      = ReactiveCommand.CreateFromTask<double>(GetOverstockAlertsAsync);
        GetExpiryRiskAlertsCommand     = ReactiveCommand.CreateFromTask<int>(GetExpiryRiskAlertsAsync);
        GetSeasonalRecommendationsCommand = ReactiveCommand.CreateFromTask<int>(GetSeasonalRecommendationsAsync);
        CalculateSafetyStockCommand    = ReactiveCommand.CreateFromTask<Guid>(CalculateSafetyStockAsync);
        ClearMessagesCommand           = ReactiveCommand.Create(() => { ClearError(); SuccessMessage = null; });

        this.WhenAnyValue(x => x.SelectedBusiness).Subscribe(b =>
        {
            if (b != null) _ = LoadShopsForBusinessAsync();
            else { Shops.Clear(); SelectedShop = null; }
        });

        this.WhenAnyValue(x => x.SelectedShop).Subscribe(s =>
        {
            if (s != null) ClearRecommendationData();
        });
    }

    private async Task LoadBusinessesAsync()
    {
        if (!CanManageInventory) { SetError("You don't have permission to manage inventory"); return; }
        IsLoading = true; ClearError();
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
        IsLoading = true; ClearError();
        try
        {
            var list = await _businessManagementService.GetShopsByBusinessAsync(SelectedBusiness.Id);
            Shops.Clear();
            foreach (var s in list) Shops.Add(s);
            if (Shops.Any()) SelectedShop = Shops.First();
        }
        catch (Exception ex) { SetError($"Error loading shops: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private async Task GenerateRecommendationsAsync()
    {
        if (SelectedShop == null) { SetError("Please select a shop"); return; }
        IsLoading = true; ClearError();
        try
        {
            var recs = await _enhancedInventoryService.GetComprehensiveInventoryRecommendationsAsync(SelectedShop.Id);
            ReorderRecommendations.Clear();  foreach (var r in recs.ReorderSuggestions)      ReorderRecommendations.Add(r);
            OverstockAlerts.Clear();         foreach (var o in recs.OverstockAlerts)          OverstockAlerts.Add(o);
            ExpiryRiskAlerts.Clear();        foreach (var e in recs.ExpiryRisks)              ExpiryRiskAlerts.Add(e);
            SeasonalRecommendations.Clear(); foreach (var s in recs.SeasonalRecommendations)  SeasonalRecommendations.Add(s);
            await LoadInventoryAnalysisAsync();
            UpdateSummaryMetrics();
        }
        catch (Exception ex) { SetError($"Error generating recommendations: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private async Task LoadInventoryAnalysisAsync()
    {
        if (SelectedShop == null) return;
        try
        {
            TurnoverAnalysis = await _enhancedInventoryService.AnalyzeInventoryTurnoverAsync(SelectedShop.Id);
            ProductInsights.Clear();
            foreach (var i in TurnoverAnalysis.ProductInsights) ProductInsights.Add(i);

            ValueAnalysis = await _enhancedInventoryService.AnalyzeInventoryValueAsync(SelectedShop.Id);
            CategoryInsights.Clear();
            foreach (var i in ValueAnalysis.CategoryBreakdown) CategoryInsights.Add(i);
        }
        catch (Exception ex) { SetError($"Error loading inventory analysis: {ex.Message}"); }
    }

    private async Task PredictLowStockAsync(int daysAhead = 30)
    {
        if (SelectedShop == null) { SetError("Please select a shop"); return; }
        IsLoading = true; ClearError();
        try
        {
            var preds = await _enhancedInventoryService.PredictLowStockAsync(SelectedShop.Id, daysAhead);
            ReorderRecommendations.Clear();
            foreach (var p in preds) ReorderRecommendations.Add(p);
            SuccessMessage = $"Generated {preds.Count} low stock predictions for the next {daysAhead} days";
        }
        catch (Exception ex) { SetError($"Error predicting low stock: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private async Task GetOverstockAlertsAsync(double monthsThreshold = 6.0)
    {
        if (SelectedShop == null) { SetError("Please select a shop"); return; }
        IsLoading = true; ClearError();
        try
        {
            var alerts = await _enhancedInventoryService.GetOverstockAlertsAsync(SelectedShop.Id, monthsThreshold);
            OverstockAlerts.Clear();
            foreach (var a in alerts) OverstockAlerts.Add(a);
            SuccessMessage = $"Found {alerts.Count} overstock situations with more than {monthsThreshold} months of supply";
        }
        catch (Exception ex) { SetError($"Error getting overstock alerts: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private async Task GetExpiryRiskAlertsAsync(int daysAhead = 60)
    {
        if (SelectedShop == null) { SetError("Please select a shop"); return; }
        IsLoading = true; ClearError();
        try
        {
            var alerts = await _enhancedInventoryService.GetExpiryRiskAlertsAsync(SelectedShop.Id, daysAhead);
            ExpiryRiskAlerts.Clear();
            foreach (var a in alerts) ExpiryRiskAlerts.Add(a);
            SuccessMessage = $"Found {alerts.Count()} products at risk of expiry within {daysAhead} days";
        }
        catch (Exception ex) { SetError($"Error getting expiry risk alerts: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private async Task GetSeasonalRecommendationsAsync(int monthsAhead = 1)
    {
        if (SelectedShop == null) { SetError("Please select a shop"); return; }
        IsLoading = true; ClearError();
        try
        {
            var recs = await _enhancedInventoryService.GetSeasonalRecommendationsAsync(SelectedShop.Id, monthsAhead);
            SeasonalRecommendations.Clear();
            foreach (var r in recs) SeasonalRecommendations.Add(r);
            SuccessMessage = $"Generated {recs.Count} seasonal recommendations for {monthsAhead} months ahead";
        }
        catch (Exception ex) { SetError($"Error getting seasonal recommendations: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private async Task CalculateSafetyStockAsync(Guid productId)
    {
        if (SelectedShop == null) { SetError("Please select a shop"); return; }
        IsLoading = true; ClearError();
        try
        {
            var rec = await _enhancedInventoryService.CalculateSafetyStockAsync(SelectedShop.Id, productId);
            SuccessMessage = $"Safety stock for {rec.ProductName}: {rec.RecommendedSafetyStock} units " +
                             $"(Service Level: {rec.ServiceLevel:P0})";
        }
        catch (Exception ex) { SetError($"Error calculating safety stock: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private void UpdateSummaryMetrics()
    {
        TotalProducts        = ProductInsights.Count;
        LowStockProducts     = ReorderRecommendations.Count;
        OverstockProducts    = OverstockAlerts.Count;
        ExpiringProducts     = ExpiryRiskAlerts.Count;
        if (ValueAnalysis != null)    { TotalInventoryValue = ValueAnalysis.TotalInventoryValue; DeadStockValue = ValueAnalysis.DeadStockValue; }
        if (TurnoverAnalysis != null) { AverageTurnoverRate = TurnoverAnalysis.AverageTurnoverRate; }
        CriticalReorders     = ReorderRecommendations.Count(r => r.Priority == ReorderPriority.Critical);
        HighPriorityReorders = ReorderRecommendations.Count(r => r.Priority == ReorderPriority.High);
    }

    private void ClearRecommendationData()
    {
        ReorderRecommendations.Clear(); OverstockAlerts.Clear();
        ExpiryRiskAlerts.Clear(); SeasonalRecommendations.Clear();
        ProductInsights.Clear(); CategoryInsights.Clear();
        TurnoverAnalysis = null; ValueAnalysis = null;
        TotalProducts = LowStockProducts = OverstockProducts = ExpiringProducts = 0;
        TotalInventoryValue = DeadStockValue = 0; AverageTurnoverRate = 0;
        CriticalReorders = HighPriorityReorders = 0;
    }
}
