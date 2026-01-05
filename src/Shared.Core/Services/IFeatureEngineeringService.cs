using Shared.Core.DTOs;

namespace Shared.Core.Services;

/// <summary>
/// Interface for feature engineering and transformation services
/// </summary>
public interface IFeatureEngineeringService
{
    /// <summary>
    /// Configures feature engineering pipeline for a business
    /// </summary>
    /// <param name="businessId">The business to configure feature engineering for</param>
    /// <param name="config">Feature engineering configuration</param>
    /// <returns>Configuration result</returns>
    Task<FeatureEngineeringResult> ConfigureAsync(Guid businessId, FeatureEngineeringConfig config);

    /// <summary>
    /// Creates features for sales forecasting models
    /// </summary>
    /// <param name="preprocessedData">Preprocessed sales data</param>
    /// <param name="config">Training configuration</param>
    /// <returns>Feature-engineered data for forecasting</returns>
    Task<ForecastingFeatureData> CreateSalesForecastingFeaturesAsync(PreprocessedSalesData preprocessedData, SalesForecastingTrainingConfig config);

    /// <summary>
    /// Creates features for recommendation models
    /// </summary>
    /// <param name="preprocessedData">Preprocessed interaction data</param>
    /// <param name="config">Training configuration</param>
    /// <returns>Feature-engineered data for recommendations</returns>
    Task<RecommendationFeatureData> CreateRecommendationFeaturesAsync(PreprocessedInteractionData preprocessedData, RecommendationTrainingConfig config);

    /// <summary>
    /// Creates time-based features from datetime columns
    /// </summary>
    /// <param name="data">Dataset with datetime columns</param>
    /// <param name="dateTimeColumns">List of datetime column names</param>
    /// <returns>Dataset with additional time features</returns>
    Task<List<Dictionary<string, object>>> CreateTimeFeaturesAsync(List<Dictionary<string, object>> data, List<string> dateTimeColumns);

    /// <summary>
    /// Creates seasonal features for time series analysis
    /// </summary>
    /// <param name="data">Dataset with date information</param>
    /// <param name="dateColumn">Name of the date column</param>
    /// <returns>Dataset with seasonal features</returns>
    Task<List<Dictionary<string, object>>> CreateSeasonalFeaturesAsync(List<Dictionary<string, object>> data, string dateColumn);

    /// <summary>
    /// Creates lag features for time series forecasting
    /// </summary>
    /// <param name="data">Time series dataset</param>
    /// <param name="targetColumn">Target column to create lags for</param>
    /// <param name="lagPeriods">List of lag periods to create</param>
    /// <returns>Dataset with lag features</returns>
    Task<List<Dictionary<string, object>>> CreateLagFeaturesAsync(List<Dictionary<string, object>> data, string targetColumn, List<int> lagPeriods);

    /// <summary>
    /// Creates rolling window features for time series analysis
    /// </summary>
    /// <param name="data">Time series dataset</param>
    /// <param name="targetColumn">Target column to create rolling features for</param>
    /// <param name="windowSizes">List of window sizes</param>
    /// <returns>Dataset with rolling features</returns>
    Task<List<Dictionary<string, object>>> CreateRollingFeaturesAsync(List<Dictionary<string, object>> data, string targetColumn, List<int> windowSizes);

    /// <summary>
    /// Creates categorical features using encoding techniques
    /// </summary>
    /// <param name="data">Dataset with categorical columns</param>
    /// <param name="categoricalColumns">List of categorical column names</param>
    /// <param name="encodingMethod">Encoding method to use</param>
    /// <returns>Dataset with encoded categorical features</returns>
    Task<List<Dictionary<string, object>>> CreateCategoricalFeaturesAsync(List<Dictionary<string, object>> data, List<string> categoricalColumns, CategoricalEncodingMethod encodingMethod);

    /// <summary>
    /// Creates interaction features between columns
    /// </summary>
    /// <param name="data">Dataset</param>
    /// <param name="columnPairs">Pairs of columns to create interactions for</param>
    /// <returns>Dataset with interaction features</returns>
    Task<List<Dictionary<string, object>>> CreateInteractionFeaturesAsync(List<Dictionary<string, object>> data, List<(string Column1, string Column2)> columnPairs);

    /// <summary>
    /// Selects the most important features using various selection methods
    /// </summary>
    /// <param name="data">Dataset with features</param>
    /// <param name="targetColumn">Target column name</param>
    /// <param name="selectionMethod">Feature selection method</param>
    /// <param name="maxFeatures">Maximum number of features to select</param>
    /// <returns>Dataset with selected features and feature importance scores</returns>
    Task<FeatureSelectionResult> SelectFeaturesAsync(List<Dictionary<string, object>> data, string targetColumn, FeatureSelectionMethod selectionMethod, int maxFeatures);
}

/// <summary>
/// Feature engineering operation result
/// </summary>
public class FeatureEngineeringResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid BusinessId { get; set; }
    public DateTime ConfiguredAt { get; set; } = DateTime.UtcNow;
    public FeatureEngineeringConfig Configuration { get; set; } = new();
    public List<string> EnabledFeatureTypes { get; set; } = new();
}

/// <summary>
/// Feature-engineered data for forecasting models
/// </summary>
public class ForecastingFeatureData
{
    public List<Dictionary<string, object>> TrainingSet { get; set; } = new();
    public List<Dictionary<string, object>> ValidationSet { get; set; } = new();
    public List<Dictionary<string, object>> TestSet { get; set; } = new();
    public List<string> FeatureColumns { get; set; } = new();
    public string TargetColumn { get; set; } = string.Empty;
    public Dictionary<string, FeatureInfo> FeatureMetadata { get; set; } = new();
    public FeatureImportanceScores ImportanceScores { get; set; } = new();
}

/// <summary>
/// Feature-engineered data for recommendation models
/// </summary>
public class RecommendationFeatureData
{
    public List<Dictionary<string, object>> TrainingSet { get; set; } = new();
    public List<Dictionary<string, object>> ValidationSet { get; set; } = new();
    public List<Dictionary<string, object>> TestSet { get; set; } = new();
    public List<string> UserFeatures { get; set; } = new();
    public List<string> ItemFeatures { get; set; } = new();
    public List<string> InteractionFeatures { get; set; } = new();
    public Dictionary<string, FeatureInfo> FeatureMetadata { get; set; } = new();
    public UserItemEmbeddings Embeddings { get; set; } = new();
}

/// <summary>
/// Information about a feature
/// </summary>
public class FeatureInfo
{
    public string FeatureName { get; set; } = string.Empty;
    public FeatureType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public double? Mean { get; set; }
    public double? StandardDeviation { get; set; }
    public int UniqueValueCount { get; set; }
    public List<object> SampleValues { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Feature importance scores
/// </summary>
public class FeatureImportanceScores
{
    public Dictionary<string, double> Scores { get; set; } = new();
    public FeatureSelectionMethod Method { get; set; }
    public List<string> TopFeatures { get; set; } = new();
    public double TotalImportance { get; set; }
}

/// <summary>
/// User and item embeddings for recommendation models
/// </summary>
public class UserItemEmbeddings
{
    public Dictionary<int, List<double>> UserEmbeddings { get; set; } = new();
    public Dictionary<int, List<double>> ItemEmbeddings { get; set; } = new();
    public int EmbeddingDimensions { get; set; }
    public string EmbeddingMethod { get; set; } = string.Empty;
}

/// <summary>
/// Feature selection result
/// </summary>
public class FeatureSelectionResult
{
    public List<Dictionary<string, object>> SelectedData { get; set; } = new();
    public List<string> SelectedFeatures { get; set; } = new();
    public FeatureImportanceScores ImportanceScores { get; set; } = new();
    public int OriginalFeatureCount { get; set; }
    public int SelectedFeatureCount { get; set; }
    public double SelectionRatio => OriginalFeatureCount > 0 ? (double)SelectedFeatureCount / OriginalFeatureCount : 0;
}

/// <summary>
/// Feature types
/// </summary>
public enum FeatureType
{
    Numerical,
    Categorical,
    DateTime,
    Text,
    Boolean,
    Derived,
    Interaction,
    Lag,
    Rolling,
    Seasonal
}

/// <summary>
/// Categorical encoding methods
/// </summary>
public enum CategoricalEncodingMethod
{
    OneHot,
    Label,
    Target,
    Binary,
    Frequency,
    Ordinal
}

/// <summary>
/// Feature selection methods
/// </summary>
public enum FeatureSelectionMethod
{
    Correlation,
    MutualInformation,
    ChiSquare,
    ANOVA,
    RecursiveFeatureElimination,
    Lasso,
    RandomForest
}