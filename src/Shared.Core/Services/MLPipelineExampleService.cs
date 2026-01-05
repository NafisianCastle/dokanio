using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Example service demonstrating how to use the ML Pipeline infrastructure
/// </summary>
public class MLPipelineExampleService
{
    private readonly IMLPipelineService _mlPipelineService;
    private readonly ILogger<MLPipelineExampleService> _logger;

    public MLPipelineExampleService(
        IMLPipelineService mlPipelineService,
        ILogger<MLPipelineExampleService> logger)
    {
        _mlPipelineService = mlPipelineService;
        _logger = logger;
    }

    /// <summary>
    /// Example: Set up complete ML pipeline for a business
    /// </summary>
    public async Task<bool> SetupMLPipelineForBusinessAsync(Guid businessId)
    {
        _logger.LogInformation("Setting up ML pipeline for business {BusinessId}", businessId);

        try
        {
            // 1. Configure ML Pipeline
            var pipelineConfig = new MLPipelineConfiguration
            {
                EnabledModels = new List<MLModelType>
                {
                    MLModelType.SalesForecasting,
                    MLModelType.ProductRecommendation,
                    MLModelType.InventoryOptimization
                },
                PreprocessingConfig = new DataPreprocessingConfig
                {
                    HandleMissingValues = true,
                    MissingValueStrategy = MissingValueStrategy.Mean,
                    RemoveOutliers = true,
                    OutlierMethod = OutlierDetectionMethod.IQR,
                    NormalizeFeatures = true,
                    NormalizationMethod = NormalizationMethod.StandardScaling
                },
                FeatureConfig = new FeatureEngineeringConfig
                {
                    CreateTimeFeatures = true,
                    CreateSeasonalFeatures = true,
                    CreateLagFeatures = true,
                    MaxLagDays = 7,
                    CreateRollingFeatures = true,
                    RollingWindows = new List<int> { 7, 14, 30 },
                    CreateCategoricalFeatures = true
                },
                ValidationConfig = new ModelValidationConfig
                {
                    ValidationMethod = ValidationMethod.TimeSeriesSplit,
                    TestSizeRatio = 0.2,
                    EvaluationMetrics = new List<string> { "MAE", "RMSE", "MAPE", "R2" },
                    MinimumAccuracyThreshold = 0.7
                },
                RetrainingConfig = new AutoRetrainingConfig
                {
                    EnableAutoRetraining = true,
                    RetrainingIntervalDays = 30,
                    PerformanceDegradationThreshold = 0.1,
                    MinimumNewDataPoints = 100,
                    RetrainingTriggers = new List<RetrainingTrigger>
                    {
                        RetrainingTrigger.ScheduledInterval,
                        RetrainingTrigger.PerformanceDegradation,
                        RetrainingTrigger.DataDrift
                    }
                }
            };

            var configResult = await _mlPipelineService.ConfigurePipelineAsync(businessId, pipelineConfig);
            if (!configResult.Success)
            {
                _logger.LogError("Failed to configure ML pipeline: {Message}", configResult.Message);
                return false;
            }

            _logger.LogInformation("ML pipeline configured successfully with {FeatureCount} features enabled",
                configResult.EnabledFeatures.Count);

            // 2. Train Sales Forecasting Model
            var forecastingConfig = new SalesForecastingTrainingConfig
            {
                HistoricalDataMonths = 12,
                Algorithm = ForecastingAlgorithm.ARIMA,
                ForecastHorizonDays = 30,
                ValidationSplitRatio = 0.2,
                FeatureColumns = new List<string> { "Revenue", "TransactionCount", "DayOfWeek", "Hour" }
            };

            var forecastingResult = await _mlPipelineService.TrainSalesForecastingModelAsync(businessId, forecastingConfig);
            if (forecastingResult.Success)
            {
                _logger.LogInformation("Sales forecasting model trained successfully. Model ID: {ModelId}, Accuracy: {Accuracy}",
                    forecastingResult.ModelId, forecastingResult.PerformanceMetrics.Accuracy);
            }

            // 3. Train Recommendation Model
            var recommendationConfig = new RecommendationTrainingConfig
            {
                Algorithm = RecommendationAlgorithm.CollaborativeFiltering,
                MinimumInteractions = 5,
                EmbeddingDimensions = 50,
                TrainingEpochs = 100,
                RegularizationParameter = 0.01
            };

            var recommendationResult = await _mlPipelineService.TrainRecommendationModelAsync(businessId, recommendationConfig);
            if (recommendationResult.Success)
            {
                _logger.LogInformation("Recommendation model trained successfully. Model ID: {ModelId}, Precision: {Precision}",
                    recommendationResult.ModelId, recommendationResult.PerformanceMetrics.Precision);
            }

            // 4. Deploy Models to Production
            var deploymentConfig = new ModelDeploymentConfig
            {
                ModelsToDeploy = new List<MLModelType>
                {
                    MLModelType.SalesForecasting,
                    MLModelType.ProductRecommendation
                },
                Environment = DeploymentEnvironment.Production,
                EnableABTesting = false,
                TrafficSplitPercentage = 100.0,
                ServingConfig = new ModelServingConfig
                {
                    MaxConcurrentRequests = 100,
                    RequestTimeoutSeconds = 30,
                    EnableCaching = true,
                    CacheTTLMinutes = 60,
                    EnableLogging = true
                }
            };

            var deploymentResult = await _mlPipelineService.DeployModelsAsync(businessId, deploymentConfig);
            if (deploymentResult.Success)
            {
                _logger.LogInformation("Models deployed successfully. Deployed models: {ModelCount}",
                    deploymentResult.DeployedModelIds.Count);
            }

            // 5. Set up Performance Monitoring
            var monitoringResult = await _mlPipelineService.MonitorModelPerformanceAsync(businessId);
            if (monitoringResult.RetrainingRecommended)
            {
                _logger.LogWarning("Model retraining recommended: {Reason}", monitoringResult.RetrainingReason);
            }

            _logger.LogInformation("ML pipeline setup completed successfully for business {BusinessId}", businessId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up ML pipeline for business {BusinessId}", businessId);
            return false;
        }
    }

    /// <summary>
    /// Example: Trigger automatic retraining based on performance degradation
    /// </summary>
    public async Task<bool> TriggerAutomaticRetrainingAsync(Guid businessId)
    {
        _logger.LogInformation("Triggering automatic retraining for business {BusinessId}", businessId);

        try
        {
            var retrainingResult = await _mlPipelineService.TriggerAutomaticRetrainingAsync(
                businessId, RetrainingTrigger.PerformanceDegradation);

            if (retrainingResult.Success)
            {
                _logger.LogInformation("Automatic retraining completed successfully: {Message}", retrainingResult.Message);
                return true;
            }
            else
            {
                _logger.LogWarning("Automatic retraining failed: {Message}", retrainingResult.Message);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during automatic retraining for business {BusinessId}", businessId);
            return false;
        }
    }

    /// <summary>
    /// Example: Get pipeline status and health information
    /// </summary>
    public async Task<MLPipelineStatus?> GetPipelineHealthAsync(Guid businessId)
    {
        _logger.LogInformation("Getting pipeline health for business {BusinessId}", businessId);

        try
        {
            var status = await _mlPipelineService.GetPipelineStatusAsync(businessId);
            
            _logger.LogInformation("Pipeline status retrieved. Health: {Health}, Active models: {ModelCount}",
                status.Health, status.ModelStatuses.Count);

            // Log any models that need attention
            var modelsNeedingRetraining = status.ModelStatuses.Where(m => m.NeedsRetraining).ToList();
            if (modelsNeedingRetraining.Any())
            {
                _logger.LogWarning("Models needing retraining: {ModelIds}",
                    string.Join(", ", modelsNeedingRetraining.Select(m => m.ModelId)));
            }

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pipeline health for business {BusinessId}", businessId);
            return null;
        }
    }

    /// <summary>
    /// Example: Demonstrate complete ML workflow from data to predictions
    /// </summary>
    public async Task<bool> DemonstrateCompleteMLWorkflowAsync(Guid businessId)
    {
        _logger.LogInformation("Demonstrating complete ML workflow for business {BusinessId}", businessId);

        try
        {
            // 1. Setup pipeline
            var setupSuccess = await SetupMLPipelineForBusinessAsync(businessId);
            if (!setupSuccess)
            {
                _logger.LogError("Failed to setup ML pipeline");
                return false;
            }

            // 2. Wait for models to be ready (in production, this would be event-driven)
            await Task.Delay(2000);

            // 3. Check pipeline status
            var status = await GetPipelineHealthAsync(businessId);
            if (status?.Health != PipelineHealth.Healthy)
            {
                _logger.LogWarning("Pipeline health is not optimal: {Health}", status?.Health);
            }

            // 4. Monitor performance
            var performanceReport = await _mlPipelineService.MonitorModelPerformanceAsync(businessId);
            _logger.LogInformation("Performance monitoring completed. Models monitored: {ModelCount}, Alerts: {AlertCount}",
                performanceReport.ModelMetrics.Count, performanceReport.Alerts.Count);

            // 5. Trigger retraining if needed
            if (performanceReport.RetrainingRecommended)
            {
                var retrainingSuccess = await TriggerAutomaticRetrainingAsync(businessId);
                if (retrainingSuccess)
                {
                    _logger.LogInformation("Automatic retraining completed successfully");
                }
            }

            _logger.LogInformation("Complete ML workflow demonstration completed successfully for business {BusinessId}", businessId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ML workflow demonstration for business {BusinessId}", businessId);
            return false;
        }
    }
}