using Shared.Core.DTOs;

namespace Shared.Core.Services;

/// <summary>
/// Interface for data preprocessing and cleaning services
/// </summary>
public interface IDataPreprocessingService
{
    /// <summary>
    /// Configures data preprocessing pipeline for a business
    /// </summary>
    /// <param name="businessId">The business to configure preprocessing for</param>
    /// <param name="config">Preprocessing configuration</param>
    /// <returns>Configuration result</returns>
    Task<DataPreprocessingResult> ConfigureAsync(Guid businessId, DataPreprocessingConfig config);

    /// <summary>
    /// Preprocesses sales data for machine learning training
    /// </summary>
    /// <param name="salesData">Raw sales data</param>
    /// <param name="config">Training configuration</param>
    /// <returns>Preprocessed sales data</returns>
    Task<PreprocessedSalesData> PreprocessSalesDataAsync(List<SalesDataPoint> salesData, SalesForecastingTrainingConfig config);

    /// <summary>
    /// Preprocesses interaction data for recommendation model training
    /// </summary>
    /// <param name="interactionData">Raw interaction data</param>
    /// <param name="config">Training configuration</param>
    /// <returns>Preprocessed interaction data</returns>
    Task<PreprocessedInteractionData> PreprocessInteractionDataAsync(List<InteractionDataPoint> interactionData, RecommendationTrainingConfig config);

    /// <summary>
    /// Handles missing values in dataset using configured strategy
    /// </summary>
    /// <param name="data">Dataset with missing values</param>
    /// <param name="strategy">Missing value handling strategy</param>
    /// <returns>Dataset with missing values handled</returns>
    Task<List<Dictionary<string, object>>> HandleMissingValuesAsync(List<Dictionary<string, object>> data, MissingValueStrategy strategy);

    /// <summary>
    /// Detects and removes outliers from dataset
    /// </summary>
    /// <param name="data">Dataset potentially containing outliers</param>
    /// <param name="method">Outlier detection method</param>
    /// <returns>Dataset with outliers removed</returns>
    Task<List<Dictionary<string, object>>> RemoveOutliersAsync(List<Dictionary<string, object>> data, OutlierDetectionMethod method);

    /// <summary>
    /// Normalizes numerical features in dataset
    /// </summary>
    /// <param name="data">Dataset to normalize</param>
    /// <param name="method">Normalization method</param>
    /// <returns>Normalized dataset</returns>
    Task<List<Dictionary<string, object>>> NormalizeFeaturesAsync(List<Dictionary<string, object>> data, NormalizationMethod method);

    /// <summary>
    /// Validates data quality and generates quality report
    /// </summary>
    /// <param name="data">Dataset to validate</param>
    /// <returns>Data quality metrics and report</returns>
    Task<DataQualityReport> ValidateDataQualityAsync(List<Dictionary<string, object>> data);
}

/// <summary>
/// Data preprocessing operation result
/// </summary>
public class DataPreprocessingResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid BusinessId { get; set; }
    public DateTime ConfiguredAt { get; set; } = DateTime.UtcNow;
    public DataPreprocessingConfig Configuration { get; set; } = new();
    public List<string> EnabledOperations { get; set; } = new();
}

/// <summary>
/// Preprocessed sales data for training
/// </summary>
public class PreprocessedSalesData
{
    public List<Dictionary<string, object>> TrainingSet { get; set; } = new();
    public List<Dictionary<string, object>> ValidationSet { get; set; } = new();
    public List<Dictionary<string, object>> TestSet { get; set; } = new();
    public DataQualityMetrics QualityMetrics { get; set; } = new();
    public Dictionary<string, object> PreprocessingMetadata { get; set; } = new();
    public List<string> FeatureColumns { get; set; } = new();
    public string TargetColumn { get; set; } = string.Empty;
}

/// <summary>
/// Preprocessed interaction data for recommendation training
/// </summary>
public class PreprocessedInteractionData
{
    public List<Dictionary<string, object>> TrainingSet { get; set; } = new();
    public List<Dictionary<string, object>> ValidationSet { get; set; } = new();
    public List<Dictionary<string, object>> TestSet { get; set; } = new();
    public int UniqueUserCount { get; set; }
    public int UniqueItemCount { get; set; }
    public Dictionary<Guid, int> UserMapping { get; set; } = new();
    public Dictionary<Guid, int> ItemMapping { get; set; } = new();
    public DataQualityMetrics QualityMetrics { get; set; } = new();
    public Dictionary<string, object> PreprocessingMetadata { get; set; } = new();
}

/// <summary>
/// <summary>
/// Information about a data column
/// </summary>