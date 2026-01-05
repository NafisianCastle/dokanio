using Shared.Core.DTOs;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Interface for machine learning pipeline management
/// </summary>
public interface IMLPipelineService
{
    /// <summary>
    /// Configures and initializes the ML pipeline for a business
    /// </summary>
    /// <param name="businessId">The business to configure pipeline for</param>
    /// <param name="pipelineConfig">Pipeline configuration settings</param>
    /// <returns>Pipeline configuration result</returns>
    Task<MLPipelineResult> ConfigurePipelineAsync(Guid businessId, MLPipelineConfiguration pipelineConfig);

    /// <summary>
    /// Trains sales forecasting models using historical data
    /// </summary>
    /// <param name="businessId">The business to train models for</param>
    /// <param name="trainingConfig">Training configuration parameters</param>
    /// <returns>Training result with model metrics</returns>
    Task<ModelTrainingResult> TrainSalesForecastingModelAsync(Guid businessId, SalesForecastingTrainingConfig trainingConfig);

    /// <summary>
    /// Trains recommendation models for product suggestions
    /// </summary>
    /// <param name="businessId">The business to train models for</param>
    /// <param name="trainingConfig">Training configuration parameters</param>
    /// <returns>Training result with model metrics</returns>
    Task<ModelTrainingResult> TrainRecommendationModelAsync(Guid businessId, RecommendationTrainingConfig trainingConfig);

    /// <summary>
    /// Deploys trained models to production environment
    /// </summary>
    /// <param name="businessId">The business to deploy models for</param>
    /// <param name="deploymentConfig">Deployment configuration</param>
    /// <returns>Deployment result</returns>
    Task<ModelDeploymentResult> DeployModelsAsync(Guid businessId, ModelDeploymentConfig deploymentConfig);

    /// <summary>
    /// Monitors model performance and triggers retraining if needed
    /// </summary>
    /// <param name="businessId">The business to monitor models for</param>
    /// <returns>Performance monitoring result</returns>
    Task<ModelPerformanceReport> MonitorModelPerformanceAsync(Guid businessId);

    /// <summary>
    /// Triggers automatic model retraining based on performance degradation
    /// </summary>
    /// <param name="businessId">The business to retrain models for</param>
    /// <param name="retrainingTrigger">Trigger that initiated retraining</param>
    /// <returns>Retraining result</returns>
    Task<ModelRetrainingResult> TriggerAutomaticRetrainingAsync(Guid businessId, RetrainingTrigger retrainingTrigger);

    /// <summary>
    /// Gets the current status of ML pipeline for a business
    /// </summary>
    /// <param name="businessId">The business to check pipeline status for</param>
    /// <returns>Pipeline status information</returns>
    Task<MLPipelineStatus> GetPipelineStatusAsync(Guid businessId);
}

/// <summary>
/// ML Pipeline configuration settings
/// </summary>
public class MLPipelineConfiguration
{
    public List<MLModelType> EnabledModels { get; set; } = new();
    public Dictionary<string, object> ModelParameters { get; set; } = new();
    public DataPreprocessingConfig PreprocessingConfig { get; set; } = new();
    public FeatureEngineeringConfig FeatureConfig { get; set; } = new();
    public ModelValidationConfig ValidationConfig { get; set; } = new();
    public AutoRetrainingConfig RetrainingConfig { get; set; } = new();
}

/// <summary>
/// Sales forecasting training configuration
/// </summary>
public class SalesForecastingTrainingConfig
{
    public int HistoricalDataMonths { get; set; } = 12;
    public List<string> FeatureColumns { get; set; } = new();
    public ForecastingAlgorithm Algorithm { get; set; } = ForecastingAlgorithm.ARIMA;
    public Dictionary<string, object> AlgorithmParameters { get; set; } = new();
    public int ForecastHorizonDays { get; set; } = 30;
    public double ValidationSplitRatio { get; set; } = 0.2;
}

/// <summary>
/// Recommendation model training configuration
/// </summary>
public class RecommendationTrainingConfig
{
    public RecommendationAlgorithm Algorithm { get; set; } = RecommendationAlgorithm.CollaborativeFiltering;
    public int MinimumInteractions { get; set; } = 5;
    public double RegularizationParameter { get; set; } = 0.01;
    public int EmbeddingDimensions { get; set; } = 50;
    public int TrainingEpochs { get; set; } = 100;
    public Dictionary<string, object> HyperParameters { get; set; } = new();
}

/// <summary>
/// Model deployment configuration
/// </summary>
public class ModelDeploymentConfig
{
    public List<MLModelType> ModelsToDeploy { get; set; } = new();
    public DeploymentEnvironment Environment { get; set; } = DeploymentEnvironment.Production;
    public bool EnableABTesting { get; set; } = false;
    public double TrafficSplitPercentage { get; set; } = 100.0;
    public ModelServingConfig ServingConfig { get; set; } = new();
}

/// <summary>
/// Data preprocessing configuration
/// </summary>
public class DataPreprocessingConfig
{
    public bool HandleMissingValues { get; set; } = true;
    public MissingValueStrategy MissingValueStrategy { get; set; } = MissingValueStrategy.Mean;
    public bool RemoveOutliers { get; set; } = true;
    public OutlierDetectionMethod OutlierMethod { get; set; } = OutlierDetectionMethod.IQR;
    public bool NormalizeFeatures { get; set; } = true;
    public NormalizationMethod NormalizationMethod { get; set; } = NormalizationMethod.StandardScaling;
}

/// <summary>
/// Feature engineering configuration
/// </summary>
public class FeatureEngineeringConfig
{
    public bool CreateTimeFeatures { get; set; } = true;
    public bool CreateSeasonalFeatures { get; set; } = true;
    public bool CreateLagFeatures { get; set; } = true;
    public int MaxLagDays { get; set; } = 7;
    public bool CreateRollingFeatures { get; set; } = true;
    public List<int> RollingWindows { get; set; } = new() { 7, 14, 30 };
    public bool CreateCategoricalFeatures { get; set; } = true;
}

/// <summary>
/// Model validation configuration
/// </summary>
public class ModelValidationConfig
{
    public ValidationMethod ValidationMethod { get; set; } = ValidationMethod.TimeSeriesSplit;
    public int CrossValidationFolds { get; set; } = 5;
    public double TestSizeRatio { get; set; } = 0.2;
    public List<string> EvaluationMetrics { get; set; } = new() { "MAE", "RMSE", "MAPE" };
    public double MinimumAccuracyThreshold { get; set; } = 0.7;
}

/// <summary>
/// Automatic retraining configuration
/// </summary>
public class AutoRetrainingConfig
{
    public bool EnableAutoRetraining { get; set; } = true;
    public int RetrainingIntervalDays { get; set; } = 30;
    public double PerformanceDegradationThreshold { get; set; } = 0.1;
    public int MinimumNewDataPoints { get; set; } = 100;
    public List<RetrainingTrigger> RetrainingTriggers { get; set; } = new();
}

/// <summary>
/// Model serving configuration
/// </summary>
public class ModelServingConfig
{
    public int MaxConcurrentRequests { get; set; } = 100;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public bool EnableCaching { get; set; } = true;
    public int CacheTTLMinutes { get; set; } = 60;
    public bool EnableLogging { get; set; } = true;
}

/// <summary>
/// ML Pipeline operation result
/// </summary>
public class MLPipelineResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid PipelineId { get; set; }
    public DateTime ConfiguredAt { get; set; } = DateTime.UtcNow;
    public List<string> EnabledFeatures { get; set; } = new();
    public Dictionary<string, object> Configuration { get; set; } = new();
}

/// <summary>
/// Model training result
/// </summary>
public class ModelTrainingResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public MLModelType ModelType { get; set; }
    public ModelPerformanceMetrics PerformanceMetrics { get; set; } = new();
    public DateTime TrainingStartedAt { get; set; }
    public DateTime TrainingCompletedAt { get; set; }
    public TimeSpan TrainingDuration => TrainingCompletedAt - TrainingStartedAt;
    public Dictionary<string, object> ModelMetadata { get; set; } = new();
}

/// <summary>
/// Model deployment result
/// </summary>
public class ModelDeploymentResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> DeployedModelIds { get; set; } = new();
    public DeploymentEnvironment Environment { get; set; }
    public DateTime DeployedAt { get; set; } = DateTime.UtcNow;
    public string DeploymentVersion { get; set; } = string.Empty;
    public Dictionary<string, string> EndpointUrls { get; set; } = new();
}

/// <summary>
/// Model performance monitoring report
/// </summary>
public class ModelPerformanceReport
{
    public Guid BusinessId { get; set; }
    public DateTime ReportGeneratedAt { get; set; } = DateTime.UtcNow;
    public List<ModelPerformanceMetrics> ModelMetrics { get; set; } = new();
    public List<PerformanceAlert> Alerts { get; set; } = new();
    public bool RetrainingRecommended { get; set; }
    public string RetrainingReason { get; set; } = string.Empty;
}

/// <summary>
/// Model performance metrics
/// </summary>
public class ModelPerformanceMetrics
{
    public string ModelId { get; set; } = string.Empty;
    public MLModelType ModelType { get; set; }
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
    public double MeanAbsoluteError { get; set; }
    public double RootMeanSquareError { get; set; }
    public double MeanAbsolutePercentageError { get; set; }
    public Dictionary<string, double> CustomMetrics { get; set; } = new();
    public DateTime LastEvaluatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Performance alert for model degradation
/// </summary>
public class PerformanceAlert
{
    public AlertSeverity Severity { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public double CurrentValue { get; set; }
    public double ThresholdValue { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Model retraining result
/// </summary>
public class ModelRetrainingResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string NewModelId { get; set; } = string.Empty;
    public string PreviousModelId { get; set; } = string.Empty;
    public RetrainingTrigger Trigger { get; set; }
    public ModelPerformanceMetrics NewModelMetrics { get; set; } = new();
    public ModelPerformanceMetrics PreviousModelMetrics { get; set; } = new();
    public bool ModelImproved { get; set; }
    public DateTime RetrainingCompletedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// ML Pipeline status information
/// </summary>
public class MLPipelineStatus
{
    public Guid BusinessId { get; set; }
    public bool IsActive { get; set; }
    public List<ModelStatus> ModelStatuses { get; set; } = new();
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    public PipelineHealth Health { get; set; }
    public List<string> ActiveFeatures { get; set; } = new();
    public Dictionary<string, object> Configuration { get; set; } = new();
}

/// <summary>
/// Individual model status
/// </summary>
public class ModelStatus
{
    public string ModelId { get; set; } = string.Empty;
    public MLModelType ModelType { get; set; }
    public ModelState State { get; set; }
    public DateTime LastTrainedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public ModelPerformanceMetrics CurrentMetrics { get; set; } = new();
    public bool NeedsRetraining { get; set; }
}

/// <summary>
/// Enums for ML Pipeline
/// </summary>
public enum MLModelType
{
    SalesForecasting,
    ProductRecommendation,
    InventoryOptimization,
    PriceOptimization,
    CustomerSegmentation,
    DemandPrediction
}

public enum ForecastingAlgorithm
{
    ARIMA,
    LSTM,
    Prophet,
    LinearRegression,
    RandomForest,
    XGBoost
}

public enum RecommendationAlgorithm
{
    CollaborativeFiltering,
    ContentBased,
    MatrixFactorization,
    DeepLearning,
    Hybrid
}

public enum DeploymentEnvironment
{
    Development,
    Staging,
    Production
}

public enum MissingValueStrategy
{
    Mean,
    Median,
    Mode,
    Forward,
    Backward,
    Interpolation,
    Remove
}

public enum OutlierDetectionMethod
{
    IQR,
    ZScore,
    IsolationForest,
    LocalOutlierFactor
}

public enum NormalizationMethod
{
    StandardScaling,
    MinMaxScaling,
    RobustScaling,
    Normalization
}

public enum ValidationMethod
{
    HoldOut,
    CrossValidation,
    TimeSeriesSplit,
    StratifiedSplit
}

public enum RetrainingTrigger
{
    ScheduledInterval,
    PerformanceDegradation,
    DataDrift,
    NewDataAvailable,
    ManualTrigger
}

public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum PipelineHealth
{
    Healthy,
    Warning,
    Critical,
    Offline
}

public enum ModelState
{
    Training,
    Trained,
    Deployed,
    Deprecated,
    Failed
}