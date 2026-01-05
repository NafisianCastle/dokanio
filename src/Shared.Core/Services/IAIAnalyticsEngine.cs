using Shared.Core.DTOs;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Interface for AI-powered analytics and recommendation engine
/// </summary>
public interface IAIAnalyticsEngine
{
    /// <summary>
    /// Analyzes sales trends and patterns for a business over a specified period
    /// </summary>
    /// <param name="businessId">The business to analyze</param>
    /// <param name="period">The date range for analysis</param>
    /// <returns>Sales insights including trends, patterns, and recommendations</returns>
    Task<SalesInsights> AnalyzeSalesTrendsAsync(Guid businessId, DateRange period);

    /// <summary>
    /// Generates inventory recommendations based on sales patterns and business type
    /// </summary>
    /// <param name="shopId">The shop to generate recommendations for</param>
    /// <returns>Inventory recommendations including reorder suggestions and stock optimization</returns>
    Task<InventoryRecommendations> GenerateInventoryRecommendationsAsync(Guid shopId);

    /// <summary>
    /// Provides product recommendations for cross-sell and up-sell opportunities
    /// </summary>
    /// <param name="shopId">The shop context for recommendations</param>
    /// <param name="customerId">Optional customer ID for personalized recommendations</param>
    /// <returns>Product recommendations with relevance scores</returns>
    Task<ProductRecommendations> GetProductRecommendationsAsync(Guid shopId, Guid? customerId = null);

    /// <summary>
    /// Analyzes pricing opportunities and suggests optimizations
    /// </summary>
    /// <param name="businessId">The business to analyze</param>
    /// <returns>Price optimization suggestions based on demand trends</returns>
    Task<PriceOptimizationSuggestions> AnalyzePricingOpportunitiesAsync(Guid businessId);

    /// <summary>
    /// Gets expiry risk alerts for products approaching expiration (pharmacy-specific)
    /// </summary>
    /// <param name="shopId">The shop to check for expiry risks</param>
    /// <returns>Array of expiry risk alerts with recommendations</returns>
    Task<ExpiryRiskAlert[]> GetExpiryRiskAlertsAsync(Guid shopId);

    /// <summary>
    /// Collects and preprocesses data for AI model training
    /// </summary>
    /// <param name="businessId">The business to collect data for</param>
    /// <param name="dataTypes">Types of data to collect</param>
    /// <returns>Preprocessed data ready for model training</returns>
    Task<AIModelData> CollectAndPreprocessDataAsync(Guid businessId, AIDataType[] dataTypes);

    /// <summary>
    /// Predicts future sales based on historical patterns
    /// </summary>
    /// <param name="shopId">The shop to predict sales for</param>
    /// <param name="forecastPeriod">The period to forecast</param>
    /// <returns>Sales forecast with confidence intervals</returns>
    Task<SalesForecast> PredictSalesAsync(Guid shopId, DateRange forecastPeriod);

    /// <summary>
    /// Identifies best-selling and low-performing products
    /// </summary>
    /// <param name="businessId">The business to analyze</param>
    /// <param name="period">The analysis period</param>
    /// <returns>Product performance analysis</returns>
    Task<ProductPerformanceAnalysis> AnalyzeProductPerformanceAsync(Guid businessId, DateRange period);

    /// <summary>
    /// Analyzes peak sales times and patterns
    /// </summary>
    /// <param name="businessId">The business to analyze</param>
    /// <param name="period">The analysis period</param>
    /// <returns>Peak time analysis with recommendations</returns>
    Task<PeakTimeAnalysis> AnalyzePeakTimesAsync(Guid businessId, DateRange period);
}