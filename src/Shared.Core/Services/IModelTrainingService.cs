using Shared.Core.DTOs;

namespace Shared.Core.Services;

/// <summary>
/// Interface for machine learning model training services
/// </summary>
public interface IModelTrainingService
{
    /// <summary>
    /// Configures model training for a specific model type
    /// </summary>
    /// <param name="businessId">The business to configure training for</param>
    /// <param name="modelType">Type of model to configure</param>
    /// <param name="parameters">Model-specific parameters</param>
    /// <returns>Configuration result</returns>
    Task<ModelConfigurationResult> ConfigureModelAsync(Guid businessId, MLModelType modelType, Dictionary<string, object> parameters);

    /// <summary>
    /// Trains a sales forecasting model
    /// </summary>
    /// <param name="modelId">Unique identifier for the model</param>
    /// <param name="trainingData">Feature-engineered training data</param>
    /// <param name="config">Training configuration</param>
    /// <returns>Training result with trained model</returns>
    Task<ModelTrainingResult> TrainSalesForecastingModelAsync(string modelId, ForecastingFeatureData trainingData, SalesForecastingTrainingConfig config);

    /// <summary>
    /// Trains a recommendation model
    /// </summary>
    /// <param name="modelId">Unique identifier for the model</param>
    /// <param name="trainingData">Feature-engineered training data</param>
    /// <param name="config">Training configuration</param>
    /// <returns>Training result with trained model</returns>
    Task<ModelTrainingResult> TrainRecommendationModelAsync(string modelId, RecommendationFeatureData trainingData, RecommendationTrainingConfig config);

    /// <summary>
    /// Deploys a trained model to the specified environment
    /// </summary>
    /// <param name="modelId">Model to deploy</param>
    /// <param name="deploymentConfig">Deployment configuration</param>
    /// <returns>Deployment result</returns>
    Task<ModelDeploymentResult> DeployModelAsync(string modelId, ModelDeploymentConfig deploymentConfig);

    /// <summary>
    /// Gets the latest trained model for a business and model type
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <param name="modelType">Type of model</param>
    /// <returns>Latest model information</returns>
    Task<TrainedModelInfo?> GetLatestModelAsync(Guid businessId, MLModelType modelType);

    /// <summary>
    /// Gets all deployed models for a business
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <returns>List of deployed models</returns>
    Task<List<DeployedModelInfo>> GetDeployedModelsAsync(Guid businessId);

    /// <summary>
    /// Gets all models for a business
    /// </summary>
    /// <param name="businessId">Business identifier</param>
    /// <returns>List of all models</returns>
    Task<List<TrainedModelInfo>> GetAllModelsAsync(Guid businessId);

    /// <summary>
    /// Deprecates an old model
    /// </summary>
    /// <param name="modelId">Model to deprecate</param>
    /// <returns>Operation result</returns>
    Task<bool> DeprecateModelAsync(string modelId);

    /// <summary>
    /// Makes predictions using a trained model
    /// </summary>
    /// <param name="modelId">Model to use for prediction</param>
    /// <param name="inputData">Input data for prediction</param>
    /// <returns>Prediction results</returns>
    Task<PredictionResult> PredictAsync(string modelId, Dictionary<string, object> inputData);

    /// <summary>
    /// Makes batch predictions using a trained model
    /// </summary>
    /// <param name="modelId">Model to use for prediction</param>
    /// <param name="inputData">Batch input data for prediction</param>
    /// <returns>Batch prediction results</returns>
    Task<BatchPredictionResult> PredictBatchAsync(string modelId, List<Dictionary<string, object>> inputData);
}

/// <summary>
/// Model configuration result
/// </summary>
public class ModelConfigurationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid BusinessId { get; set; }
    public MLModelType ModelType { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
    public DateTime ConfiguredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Model training operation result
/// </summary>
public class ModelTrainingOperationResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public object? Model { get; set; }
    public ModelPerformanceMetrics? PerformanceMetrics { get; set; }
    public Dictionary<string, object> TrainingMetadata { get; set; } = new();
}

/// <summary>
/// Model deployment operation result
/// </summary>
public class ModelDeploymentOperationResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string EndpointUrl { get; set; } = string.Empty;
    public Dictionary<string, object> DeploymentMetadata { get; set; } = new();
}

/// <summary>
/// Information about a trained model
/// </summary>
public class TrainedModelInfo
{
    public string ModelId { get; set; } = string.Empty;
    public Guid BusinessId { get; set; }
    public MLModelType ModelType { get; set; }
    public ModelState State { get; set; }
    public DateTime LastTrainedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public ModelPerformanceMetrics PerformanceMetrics { get; set; } = new();
    public Dictionary<string, object> ModelMetadata { get; set; } = new();
    public string ModelVersion { get; set; } = string.Empty;
    public long ModelSizeBytes { get; set; }
}

/// <summary>
/// Information about a deployed model
/// </summary>
public class DeployedModelInfo : TrainedModelInfo
{
    public DeploymentEnvironment Environment { get; set; }
    public string EndpointUrl { get; set; } = string.Empty;
    public DateTime DeployedAt { get; set; }
    public string DeploymentVersion { get; set; } = string.Empty;
    public ModelServingConfig ServingConfig { get; set; } = new();
    public ModelServingMetrics ServingMetrics { get; set; } = new();
}

/// <summary>
/// Model serving metrics
/// </summary>
public class ModelServingMetrics
{
    public long TotalRequests { get; set; }
    public long SuccessfulRequests { get; set; }
    public long FailedRequests { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public double P95ResponseTimeMs { get; set; }
    public double P99ResponseTimeMs { get; set; }
    public DateTime LastRequestAt { get; set; }
    public double RequestsPerSecond { get; set; }
    public double ErrorRate => TotalRequests > 0 ? (double)FailedRequests / TotalRequests : 0;
}

/// <summary>
/// Single prediction result
/// </summary>
public class PredictionResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public Dictionary<string, object> Predictions { get; set; } = new();
    public double ConfidenceScore { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime PredictedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan ProcessingTime { get; set; }
}

/// <summary>
/// Batch prediction result
/// </summary>
public class BatchPredictionResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<PredictionResult> Predictions { get; set; } = new();
    public int TotalPredictions { get; set; }
    public int SuccessfulPredictions { get; set; }
    public int FailedPredictions { get; set; }
    public DateTime PredictedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan TotalProcessingTime { get; set; }
    public double AverageProcessingTimeMs { get; set; }
}