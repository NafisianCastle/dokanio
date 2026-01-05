using Shared.Core.DTOs;

namespace Shared.Core.Integration;

/// <summary>
/// Interface for external AI service integration
/// </summary>
public interface IAIServiceClient
{
    /// <summary>
    /// Gets product recommendations from AI service
    /// </summary>
    /// <param name="request">Recommendation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<AIRecommendationResponse> GetProductRecommendationsAsync(AIRecommendationRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets inventory predictions from AI service
    /// </summary>
    /// <param name="request">Prediction request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<AIInventoryPredictionResponse> GetInventoryPredictionsAsync(AIInventoryPredictionRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets price optimization suggestions from AI service
    /// </summary>
    /// <param name="request">Price optimization request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<AIPriceOptimizationResponse> GetPriceOptimizationAsync(AIPriceOptimizationRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Trains AI model with business data
    /// </summary>
    /// <param name="request">Training data request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<AITrainingResponse> TrainModelAsync(AITrainingRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets AI model status and performance metrics
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<AIModelStatusResponse> GetModelStatusAsync(Guid businessId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for AI product recommendations
/// </summary>
public class AIRecommendationRequest
{
    public Guid BusinessId { get; set; }
    public Guid ShopId { get; set; }
    public Guid? CustomerId { get; set; }
    public List<Guid> CurrentCartItems { get; set; } = new();
    public List<PurchaseHistoryItem> PurchaseHistory { get; set; } = new();
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// Response containing AI product recommendations
/// </summary>
public class AIRecommendationResponse
{
    public bool Success { get; set; }
    public List<ProductRecommendation> Recommendations { get; set; } = new();
    public decimal ConfidenceScore { get; set; }
    public string? ReasoningExplanation { get; set; }
}

/// <summary>
/// Individual product recommendation
/// </summary>
public class ProductRecommendation
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal RecommendationScore { get; set; }
    public string RecommendationType { get; set; } = string.Empty; // CrossSell, UpSell, Bundle, etc.
    public string Reasoning { get; set; } = string.Empty;
}

/// <summary>
/// Purchase history item for AI analysis
/// </summary>
public class PurchaseHistoryItem
{
    public Guid ProductId { get; set; }
    public DateTime PurchaseDate { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

/// <summary>
/// Request for AI inventory predictions
/// </summary>
public class AIInventoryPredictionRequest
{
    public Guid BusinessId { get; set; }
    public Guid ShopId { get; set; }
    public List<InventoryItem> CurrentInventory { get; set; } = new();
    public List<SalesHistoryItem> SalesHistory { get; set; } = new();
    public int PredictionDays { get; set; } = 30;
}

/// <summary>
/// Response containing AI inventory predictions
/// </summary>
public class AIInventoryPredictionResponse
{
    public bool Success { get; set; }
    public List<InventoryPrediction> Predictions { get; set; } = new();
    public decimal OverallAccuracy { get; set; }
}

/// <summary>
/// Individual inventory prediction
/// </summary>
public class InventoryPrediction
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int PredictedDemand { get; set; }
    public int RecommendedReorderQuantity { get; set; }
    public DateTime? PredictedStockoutDate { get; set; }
    public decimal ConfidenceLevel { get; set; }
}

/// <summary>
/// Inventory item for AI analysis
/// </summary>
public class InventoryItem
{
    public Guid ProductId { get; set; }
    public int CurrentStock { get; set; }
    public int MinimumStock { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime? LastRestocked { get; set; }
}

/// <summary>
/// Sales history item for AI analysis
/// </summary>
public class SalesHistoryItem
{
    public Guid ProductId { get; set; }
    public DateTime SaleDate { get; set; }
    public int QuantitySold { get; set; }
    public decimal SalePrice { get; set; }
}

/// <summary>
/// Request for AI price optimization
/// </summary>
public class AIPriceOptimizationRequest
{
    public Guid BusinessId { get; set; }
    public Guid ShopId { get; set; }
    public List<ProductPriceData> Products { get; set; } = new();
    public MarketConditions MarketConditions { get; set; } = new();
}

/// <summary>
/// Response containing AI price optimization suggestions
/// </summary>
public class AIPriceOptimizationResponse
{
    public bool Success { get; set; }
    public List<PriceOptimizationSuggestion> Suggestions { get; set; } = new();
    public decimal ExpectedRevenueImpact { get; set; }
}

/// <summary>
/// Individual price optimization suggestion
/// </summary>
public class PriceOptimizationSuggestion
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal SuggestedPrice { get; set; }
    public decimal ExpectedDemandChange { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

/// <summary>
/// Product price data for AI analysis
/// </summary>
public class ProductPriceData
{
    public Guid ProductId { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal Cost { get; set; }
    public List<PriceHistoryItem> PriceHistory { get; set; } = new();
    public List<SalesHistoryItem> SalesHistory { get; set; } = new();
}

/// <summary>
/// Price history item
/// </summary>
public class PriceHistoryItem
{
    public DateTime Date { get; set; }
    public decimal Price { get; set; }
}

/// <summary>
/// Market conditions for price optimization
/// </summary>
public class MarketConditions
{
    public decimal CompetitorPriceIndex { get; set; }
    public decimal SeasonalityFactor { get; set; }
    public decimal DemandTrend { get; set; }
    public Dictionary<string, object> AdditionalFactors { get; set; } = new();
}

/// <summary>
/// Request for AI model training
/// </summary>
public class AITrainingRequest
{
    public Guid BusinessId { get; set; }
    public List<TrainingDataSet> TrainingData { get; set; } = new();
    public string ModelType { get; set; } = string.Empty;
    public Dictionary<string, object> TrainingParameters { get; set; } = new();
}

/// <summary>
/// Response from AI model training
/// </summary>
public class AITrainingResponse
{
    public bool Success { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public decimal TrainingAccuracy { get; set; }
    public DateTime TrainingCompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Training data set for AI model
/// </summary>
public class TrainingDataSet
{
    public string DataType { get; set; } = string.Empty;
    public List<Dictionary<string, object>> Data { get; set; } = new();
}

/// <summary>
/// Response containing AI model status
/// </summary>
public class AIModelStatusResponse
{
    public bool Success { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Accuracy { get; set; }
    public DateTime LastTrainedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public Dictionary<string, object> PerformanceMetrics { get; set; } = new();
}