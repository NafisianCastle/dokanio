using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using System.Collections.ObjectModel;

namespace Desktop.ViewModels;

/// <summary>
/// View model for AI-powered inventory management with recommendations
/// </summary>
public partial class AIInventoryViewModel : BaseViewModel
{
    private readonly IEnhancedInventoryService _enhancedInventoryService;
    private readonly IBusinessManagementService _businessManagementService;
    private readonly IAIAnalyticsEngine _aiAnalyticsEngine;
    private readonly ICurrentUserService _currentUserService;

    [ObservableProperty]
    private ObservableCollection<BusinessResponse> businesses = new();

    [ObservableProperty]
    private ObservableCollection<ShopResponse> shops = new();

    [ObservableProperty]
    private BusinessResponse? selectedBusiness;

    [ObservableProperty]
    private ShopResponse? selectedShop;

    [ObservableProperty]
    private bool isLoading;

    // AI Recommendations
    [ObservableProperty]
    private ObservableCollection<ReorderRecommendation> reorderRecommendations = new();

    [ObservableProperty]
    private ObservableCollection<OverstockAlert> overstockAlerts = new();

    [ObservableProperty]
    private ObservableCollection<ExpiryRiskAlert> expiryRiskAlerts = new();

    [ObservableProperty]
    private ObservableCollection<SeasonalRecommendation> seasonalRecommendations = new();

    // Inventory Analysis
    [ObservableProperty]
    private InventoryTurnoverAnalysis? turnoverAnalysis;

    [ObservableProperty]
    private InventoryValueAnalysis? valueAnalysis;

    [ObservableProperty]
    private ObservableCollection<ProductTurnoverInsight> productInsights = new();

    [ObservableProperty]
    private ObservableCollection<CategoryValueInsight> categoryInsights = new();

    // Summary metrics
    [ObservableProperty]
    private int totalProducts;

    [ObservableProperty]
    private int lowStockProducts;

    [ObservableProperty]
    private int overstockProducts;

    [ObservableProperty]
    private int expiringProducts;

    [ObservableProperty]
    private decimal totalInventoryValue;

    [ObservableProperty]
    private decimal deadStockValue;

    [ObservableProperty]
    private double averageTurnoverRate;

    [ObservableProperty]
    private int criticalReorders;

    [ObservableProperty]
    private int highPriorityReorders;

    public AIInventoryViewModel(
        IEnhancedInventoryService enhancedInventoryService,
        IBusinessManagementService businessManagementService,
        IAIAnalyticsEngine aiAnalyticsEngine,
        ICurrentUserService currentUserService)
    {
        _enhancedInventoryService = enhancedInventoryService;
        _businessManagementService = businessManagementService;
        _aiAnalyticsEngine = aiAnalyticsEngine;
        _currentUserService = currentUserService;
        Title = "AI Inventory Management";
    }

    public bool CanManageInventory => _currentUserService.CurrentUser?.Role == UserRole.BusinessOwner ||
                                      _currentUserService.CurrentUser?.Role == UserRole.ShopManager ||
                                      _currentUserService.CurrentUser?.Role == UserRole.InventoryStaff;

    [RelayCommand]
    private async Task LoadBusinessesAsync()
    {
        if (!CanManageInventory)
        {
            SetError("You don't have permission to manage inventory");
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
            
            foreach (var shop in shopList)
            {
                Shops.Add(shop);
            }

            // Auto-select first shop
            if (Shops.Any())
            {
                SelectedShop = Shops.First();
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
    private async Task GenerateRecommendationsAsync()
    {
        if (SelectedShop == null)
        {
            SetError("Please select a shop");
            return;
        }

        IsLoading = true;
        ClearError();

        try
        {
            // Get comprehensive inventory recommendations
            var recommendations = await _enhancedInventoryService.GetComprehensiveInventoryRecommendationsAsync(SelectedShop.Id);

            // Load reorder recommendations
            ReorderRecommendations.Clear();
            foreach (var reorder in recommendations.ReorderSuggestions)
            {
                ReorderRecommendations.Add(reorder);
            }

            // Load overstock alerts
            OverstockAlerts.Clear();
            foreach (var overstock in recommendations.OverstockAlerts)
            {
                OverstockAlerts.Add(overstock);
            }

            // Load expiry risk alerts
            ExpiryRiskAlerts.Clear();
            foreach (var expiry in recommendations.ExpiryRisks)
            {
                ExpiryRiskAlerts.Add(expiry);
            }

            // Load seasonal recommendations
            SeasonalRecommendations.Clear();
            foreach (var seasonal in recommendations.SeasonalRecommendations)
            {
                SeasonalRecommendations.Add(seasonal);
            }

            // Load inventory analysis
            await LoadInventoryAnalysisAsync();

            // Update summary metrics
            UpdateSummaryMetrics();
        }
        catch (Exception ex)
        {
            SetError($"Error generating recommendations: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadInventoryAnalysisAsync()
    {
        if (SelectedShop == null) return;

        try
        {
            // Load turnover analysis
            TurnoverAnalysis = await _enhancedInventoryService.AnalyzeInventoryTurnoverAsync(SelectedShop.Id);
            
            ProductInsights.Clear();
            foreach (var insight in TurnoverAnalysis.ProductInsights)
            {
                ProductInsights.Add(insight);
            }

            // Load value analysis
            ValueAnalysis = await _enhancedInventoryService.AnalyzeInventoryValueAsync(SelectedShop.Id);
            
            CategoryInsights.Clear();
            foreach (var insight in ValueAnalysis.CategoryBreakdown)
            {
                CategoryInsights.Add(insight);
            }
        }
        catch (Exception ex)
        {
            SetError($"Error loading inventory analysis: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PredictLowStockAsync(int daysAhead = 30)
    {
        if (SelectedShop == null)
        {
            SetError("Please select a shop");
            return;
        }

        IsLoading = true;
        ClearError();

        try
        {
            var predictions = await _enhancedInventoryService.PredictLowStockAsync(SelectedShop.Id, daysAhead);
            
            ReorderRecommendations.Clear();
            foreach (var prediction in predictions)
            {
                ReorderRecommendations.Add(prediction);
            }

            SuccessMessage = $"Generated {predictions.Count} low stock predictions for the next {daysAhead} days";
        }
        catch (Exception ex)
        {
            SetError($"Error predicting low stock: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task GetOverstockAlertsAsync(double monthsThreshold = 6.0)
    {
        if (SelectedShop == null)
        {
            SetError("Please select a shop");
            return;
        }

        IsLoading = true;
        ClearError();

        try
        {
            var alerts = await _enhancedInventoryService.GetOverstockAlertsAsync(SelectedShop.Id, monthsThreshold);
            
            OverstockAlerts.Clear();
            foreach (var alert in alerts)
            {
                OverstockAlerts.Add(alert);
            }

            SuccessMessage = $"Found {alerts.Count} overstock situations with more than {monthsThreshold} months of supply";
        }
        catch (Exception ex)
        {
            SetError($"Error getting overstock alerts: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task GetExpiryRiskAlertsAsync(int daysAhead = 60)
    {
        if (SelectedShop == null)
        {
            SetError("Please select a shop");
            return;
        }

        IsLoading = true;
        ClearError();

        try
        {
            var alerts = await _enhancedInventoryService.GetExpiryRiskAlertsAsync(SelectedShop.Id, daysAhead);
            
            ExpiryRiskAlerts.Clear();
            foreach (var alert in alerts)
            {
                ExpiryRiskAlerts.Add(alert);
            }

            SuccessMessage = $"Found {alerts.Length} products at risk of expiry within {daysAhead} days";
        }
        catch (Exception ex)
        {
            SetError($"Error getting expiry risk alerts: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task GetSeasonalRecommendationsAsync(int monthsAhead = 1)
    {
        if (SelectedShop == null)
        {
            SetError("Please select a shop");
            return;
        }

        IsLoading = true;
        ClearError();

        try
        {
            var recommendations = await _enhancedInventoryService.GetSeasonalRecommendationsAsync(SelectedShop.Id, monthsAhead);
            
            SeasonalRecommendations.Clear();
            foreach (var recommendation in recommendations)
            {
                SeasonalRecommendations.Add(recommendation);
            }

            SuccessMessage = $"Generated {recommendations.Count} seasonal recommendations for {monthsAhead} months ahead";
        }
        catch (Exception ex)
        {
            SetError($"Error getting seasonal recommendations: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CalculateSafetyStockAsync(Guid productId)
    {
        if (SelectedShop == null)
        {
            SetError("Please select a shop");
            return;
        }

        IsLoading = true;
        ClearError();

        try
        {
            var recommendation = await _enhancedInventoryService.CalculateSafetyStockAsync(SelectedShop.Id, productId);
            
            SuccessMessage = $"Safety stock for {recommendation.ProductName}: {recommendation.RecommendedSafetyStock} units " +
                           $"(Service Level: {recommendation.ServiceLevel:P0})";
        }
        catch (Exception ex)
        {
            SetError($"Error calculating safety stock: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearMessages()
    {
        ClearError();
        SuccessMessage = null;
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
            SelectedShop = null;
        }
    }

    partial void OnSelectedShopChanged(ShopResponse? value)
    {
        if (value != null)
        {
            // Clear previous data when shop changes
            ClearRecommendationData();
        }
    }

    private void UpdateSummaryMetrics()
    {
        TotalProducts = ProductInsights.Count;
        LowStockProducts = ReorderRecommendations.Count;
        OverstockProducts = OverstockAlerts.Count;
        ExpiringProducts = ExpiryRiskAlerts.Count;

        if (ValueAnalysis != null)
        {
            TotalInventoryValue = ValueAnalysis.TotalInventoryValue;
            DeadStockValue = ValueAnalysis.DeadStockValue;
        }

        if (TurnoverAnalysis != null)
        {
            AverageTurnoverRate = TurnoverAnalysis.AverageTurnoverRate;
        }

        CriticalReorders = ReorderRecommendations.Count(r => r.Priority == ReorderPriority.Critical);
        HighPriorityReorders = ReorderRecommendations.Count(r => r.Priority == ReorderPriority.High);
    }

    private void ClearRecommendationData()
    {
        ReorderRecommendations.Clear();
        OverstockAlerts.Clear();
        ExpiryRiskAlerts.Clear();
        SeasonalRecommendations.Clear();
        ProductInsights.Clear();
        CategoryInsights.Clear();
        TurnoverAnalysis = null;
        ValueAnalysis = null;
        
        // Reset metrics
        TotalProducts = 0;
        LowStockProducts = 0;
        OverstockProducts = 0;
        ExpiringProducts = 0;
        TotalInventoryValue = 0;
        DeadStockValue = 0;
        AverageTurnoverRate = 0;
        CriticalReorders = 0;
        HighPriorityReorders = 0;
    }
}