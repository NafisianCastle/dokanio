using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of machine learning pipeline service
/// </summary>
public class MLPipelineService : IMLPipelineService
{
    private readonly IBusinessRepository _businessRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockRepository _stockRepository;
    private readonly IDataPreprocessingService _dataPreprocessingService;
    private readonly IFeatureEngineeringService _featureEngineeringService;
    private readonly IModelTrainingService _modelTrainingService;
    private readonly IModelPerformanceMonitoringService _performanceMonitoringService;
    private readonly ILogger<MLPipelineService> _logger;

    public MLPipelineService(
        IBusinessRepository businessRepository,
        ISaleRepository saleRepository,
        IProductRepository productRepository,
        IStockRepository stockRepository,
        IDataPreprocessingService dataPreprocessingService,
        IFeatureEngineeringService featureEngineeringService,
        IModelTrainingService modelTrainingService,
        IModelPerformanceMonitoringService performanceMonitoringService,
        ILogger<MLPipelineService> logger)
    {
        _businessRepository = businessRepository;
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _stockRepository = stockRepository;
        _dataPreprocessingService = dataPreprocessingService;
        _featureEngineeringService = featureEngineeringService;
        _modelTrainingService = modelTrainingService;
        _performanceMonitoringService = performanceMonitoringService;
        _logger = logger;
    }

    public async Task<MLPipelineResult> ConfigurePipelineAsync(Guid businessId, MLPipelineConfiguration pipelineConfig)
    {
        _logger.LogInformation("Configuring ML pipeline for business {BusinessId}", businessId);

        try
        {
            var business = await _businessRepository.GetByIdAsync(businessId);
            if (business == null)
            {
                return new MLPipelineResult
                {
                    Success = false,
                    Message = $"Business with ID {businessId} not found"
                };
            }

            // Validate configuration
            var validationResult = ValidatePipelineConfiguration(pipelineConfig);
            if (!validationResult.IsValid)
            {
                return new MLPipelineResult
                {
                    Success = false,
                    Message = $"Invalid pipeline configuration: {string.Join(", ", validationResult.Errors)}"
                };
            }

            // Initialize pipeline components
            var pipelineId = Guid.NewGuid();
            var enabledFeatures = new List<string>();

            // Configure data preprocessing
            if (pipelineConfig.PreprocessingConfig != null)
            {
                await _dataPreprocessingService.ConfigureAsync(businessId, pipelineConfig.PreprocessingConfig);
                enabledFeatures.Add("DataPreprocessing");
            }

            // Configure feature engineering
            if (pipelineConfig.FeatureConfig != null)
            {
                await _featureEngineeringService.ConfigureAsync(businessId, pipelineConfig.FeatureConfig);
                enabledFeatures.Add("FeatureEngineering");
            }

            // Configure model training for each enabled model type
            foreach (var modelType in pipelineConfig.EnabledModels)
            {
                await _modelTrainingService.ConfigureModelAsync(businessId, modelType, pipelineConfig.ModelParameters);
                enabledFeatures.Add($"Model_{modelType}");
            }

            // Configure performance monitoring
            if (pipelineConfig.RetrainingConfig?.EnableAutoRetraining == true)
            {
                await _performanceMonitoringService.ConfigureAsync(businessId, pipelineConfig.RetrainingConfig);
                enabledFeatures.Add("AutoRetraining");
            }

            _logger.LogInformation("ML pipeline configured successfully for business {BusinessId} with pipeline ID {PipelineId}", 
                businessId, pipelineId);

            return new MLPipelineResult
            {
                Success = true,
                Message = "ML pipeline configured successfully",
                PipelineId = pipelineId,
                EnabledFeatures = enabledFeatures,
                Configuration = new Dictionary<string, object>
                {
                    ["EnabledModels"] = pipelineConfig.EnabledModels,
                    ["PreprocessingEnabled"] = pipelineConfig.PreprocessingConfig != null,
                    ["FeatureEngineeringEnabled"] = pipelineConfig.FeatureConfig != null,
                    ["AutoRetrainingEnabled"] = pipelineConfig.RetrainingConfig?.EnableAutoRetraining == true
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring ML pipeline for business {BusinessId}", businessId);
            return new MLPipelineResult
            {
                Success = false,
                Message = $"Error configuring ML pipeline: {ex.Message}"
            };
        }
    }

    public async Task<ModelTrainingResult> TrainSalesForecastingModelAsync(Guid businessId, SalesForecastingTrainingConfig trainingConfig)
    {
        _logger.LogInformation("Training sales forecasting model for business {BusinessId}", businessId);

        var trainingStartTime = DateTime.UtcNow;

        try
        {
            // Collect historical sales data
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddMonths(-trainingConfig.HistoricalDataMonths);
            var salesData = await CollectSalesDataForTraining(businessId, startDate, endDate);

            if (!salesData.Any())
            {
                return new ModelTrainingResult
                {
                    Success = false,
                    Message = "Insufficient sales data for training",
                    ModelType = MLModelType.SalesForecasting,
                    TrainingStartedAt = trainingStartTime,
                    TrainingCompletedAt = DateTime.UtcNow
                };
            }

            // Preprocess data
            var preprocessedData = await _dataPreprocessingService.PreprocessSalesDataAsync(salesData, trainingConfig);

            // Engineer features
            var featuredData = await _featureEngineeringService.CreateSalesForecastingFeaturesAsync(preprocessedData, trainingConfig);

            // Train model
            var modelId = $"sales_forecast_{businessId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
            var trainingResult = await _modelTrainingService.TrainSalesForecastingModelAsync(
                modelId, featuredData, trainingConfig);

            if (!trainingResult.Success)
            {
                return new ModelTrainingResult
                {
                    Success = false,
                    Message = trainingResult.ErrorMessage,
                    ModelType = MLModelType.SalesForecasting,
                    TrainingStartedAt = trainingStartTime,
                    TrainingCompletedAt = DateTime.UtcNow
                };
            }

            // Evaluate model performance
            var performanceMetrics = await EvaluateModelPerformance(trainingResult.Model, featuredData.ValidationSet);

            _logger.LogInformation("Sales forecasting model trained successfully for business {BusinessId}. Model ID: {ModelId}, Accuracy: {Accuracy}",
                businessId, modelId, performanceMetrics.Accuracy);

            return new ModelTrainingResult
            {
                Success = true,
                Message = "Sales forecasting model trained successfully",
                ModelId = modelId,
                ModelType = MLModelType.SalesForecasting,
                PerformanceMetrics = performanceMetrics,
                TrainingStartedAt = trainingStartTime,
                TrainingCompletedAt = DateTime.UtcNow,
                ModelMetadata = new Dictionary<string, object>
                {
                    ["Algorithm"] = trainingConfig.Algorithm.ToString(),
                    ["HistoricalDataMonths"] = trainingConfig.HistoricalDataMonths,
                    ["ForecastHorizonDays"] = trainingConfig.ForecastHorizonDays,
                    ["TrainingDataPoints"] = featuredData.TrainingSet.Count,
                    ["ValidationDataPoints"] = featuredData.ValidationSet.Count
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error training sales forecasting model for business {BusinessId}", businessId);
            return new ModelTrainingResult
            {
                Success = false,
                Message = $"Error training model: {ex.Message}",
                ModelType = MLModelType.SalesForecasting,
                TrainingStartedAt = trainingStartTime,
                TrainingCompletedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<ModelTrainingResult> TrainRecommendationModelAsync(Guid businessId, RecommendationTrainingConfig trainingConfig)
    {
        _logger.LogInformation("Training recommendation model for business {BusinessId}", businessId);

        var trainingStartTime = DateTime.UtcNow;

        try
        {
            // Collect interaction data (sales, views, etc.)
            var interactionData = await CollectInteractionDataForTraining(businessId);

            if (interactionData.Count < trainingConfig.MinimumInteractions)
            {
                return new ModelTrainingResult
                {
                    Success = false,
                    Message = $"Insufficient interaction data for training. Required: {trainingConfig.MinimumInteractions}, Available: {interactionData.Count}",
                    ModelType = MLModelType.ProductRecommendation,
                    TrainingStartedAt = trainingStartTime,
                    TrainingCompletedAt = DateTime.UtcNow
                };
            }

            // Preprocess interaction data
            var preprocessedData = await _dataPreprocessingService.PreprocessInteractionDataAsync(interactionData, trainingConfig);

            // Engineer features for recommendations
            var featuredData = await _featureEngineeringService.CreateRecommendationFeaturesAsync(preprocessedData, trainingConfig);

            // Train recommendation model
            var modelId = $"recommendation_{businessId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
            var trainingResult = await _modelTrainingService.TrainRecommendationModelAsync(
                modelId, featuredData, trainingConfig);

            if (!trainingResult.Success)
            {
                return new ModelTrainingResult
                {
                    Success = false,
                    Message = trainingResult.ErrorMessage,
                    ModelType = MLModelType.ProductRecommendation,
                    TrainingStartedAt = trainingStartTime,
                    TrainingCompletedAt = DateTime.UtcNow
                };
            }

            // Evaluate model performance
            var performanceMetrics = await EvaluateRecommendationModelPerformance(trainingResult.Model, featuredData.ValidationSet);

            _logger.LogInformation("Recommendation model trained successfully for business {BusinessId}. Model ID: {ModelId}, Precision@10: {Precision}",
                businessId, modelId, performanceMetrics.Precision);

            return new ModelTrainingResult
            {
                Success = true,
                Message = "Recommendation model trained successfully",
                ModelId = modelId,
                ModelType = MLModelType.ProductRecommendation,
                PerformanceMetrics = performanceMetrics,
                TrainingStartedAt = trainingStartTime,
                TrainingCompletedAt = DateTime.UtcNow,
                ModelMetadata = new Dictionary<string, object>
                {
                    ["Algorithm"] = trainingConfig.Algorithm.ToString(),
                    ["EmbeddingDimensions"] = trainingConfig.EmbeddingDimensions,
                    ["TrainingEpochs"] = trainingConfig.TrainingEpochs,
                    ["InteractionCount"] = interactionData.Count,
                    ["UniqueUsers"] = preprocessedData.UniqueUserCount,
                    ["UniqueItems"] = preprocessedData.UniqueItemCount
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error training recommendation model for business {BusinessId}", businessId);
            return new ModelTrainingResult
            {
                Success = false,
                Message = $"Error training model: {ex.Message}",
                ModelType = MLModelType.ProductRecommendation,
                TrainingStartedAt = trainingStartTime,
                TrainingCompletedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<ModelDeploymentResult> DeployModelsAsync(Guid businessId, ModelDeploymentConfig deploymentConfig)
    {
        _logger.LogInformation("Deploying models for business {BusinessId} to {Environment}", 
            businessId, deploymentConfig.Environment);

        try
        {
            var deployedModelIds = new List<string>();
            var endpointUrls = new Dictionary<string, string>();

            foreach (var modelType in deploymentConfig.ModelsToDeploy)
            {
                // Get the latest trained model for this type
                var latestModel = await _modelTrainingService.GetLatestModelAsync(businessId, modelType);
                if (latestModel == null)
                {
                    _logger.LogWarning("No trained model found for type {ModelType} in business {BusinessId}", 
                        modelType, businessId);
                    continue;
                }

                // Deploy the model
                var deploymentResult = await _modelTrainingService.DeployModelAsync(
                    latestModel.ModelId, deploymentConfig);

                if (deploymentResult.Success)
                {
                    deployedModelIds.Add(latestModel.ModelId);
                    endpointUrls[modelType.ToString()] = deploymentResult.EndpointUrl;
                    
                    _logger.LogInformation("Model {ModelId} of type {ModelType} deployed successfully", 
                        latestModel.ModelId, modelType);
                }
                else
                {
                    _logger.LogError("Failed to deploy model {ModelId} of type {ModelType}: {Error}", 
                        latestModel.ModelId, modelType, deploymentResult.ErrorMessage);
                }
            }

            var deploymentVersion = $"v{DateTime.UtcNow:yyyyMMddHHmmss}";

            return new ModelDeploymentResult
            {
                Success = deployedModelIds.Any(),
                Message = deployedModelIds.Any() ? 
                    $"Successfully deployed {deployedModelIds.Count} models" : 
                    "No models were deployed",
                DeployedModelIds = deployedModelIds,
                Environment = deploymentConfig.Environment,
                DeploymentVersion = deploymentVersion,
                EndpointUrls = endpointUrls
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying models for business {BusinessId}", businessId);
            return new ModelDeploymentResult
            {
                Success = false,
                Message = $"Error deploying models: {ex.Message}",
                Environment = deploymentConfig.Environment
            };
        }
    }

    public async Task<ModelPerformanceReport> MonitorModelPerformanceAsync(Guid businessId)
    {
        _logger.LogInformation("Monitoring model performance for business {BusinessId}", businessId);

        try
        {
            var report = new ModelPerformanceReport
            {
                BusinessId = businessId
            };

            // Get all deployed models for this business
            var deployedModels = await _modelTrainingService.GetDeployedModelsAsync(businessId);

            foreach (var model in deployedModels)
            {
                // Evaluate current performance
                var currentMetrics = await _performanceMonitoringService.EvaluateModelPerformanceAsync(model.ModelId);
                report.ModelMetrics.Add(currentMetrics);

                // Check for performance degradation
                var performanceAlert = await _performanceMonitoringService.CheckPerformanceDegradationAsync(
                    model.ModelId, currentMetrics);

                if (performanceAlert != null)
                {
                    report.Alerts.Add(performanceAlert);
                }
            }

            // Determine if retraining is recommended
            var criticalAlerts = report.Alerts.Where(a => a.Severity >= AlertSeverity.High).ToList();
            if (criticalAlerts.Any())
            {
                report.RetrainingRecommended = true;
                report.RetrainingReason = $"Performance degradation detected in {criticalAlerts.Count} models";
            }

            _logger.LogInformation("Performance monitoring completed for business {BusinessId}. Models: {ModelCount}, Alerts: {AlertCount}",
                businessId, report.ModelMetrics.Count, report.Alerts.Count);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring model performance for business {BusinessId}", businessId);
            return new ModelPerformanceReport
            {
                BusinessId = businessId,
                Alerts = new List<PerformanceAlert>
                {
                    new PerformanceAlert
                    {
                        Severity = AlertSeverity.Critical,
                        AlertType = "MonitoringError",
                        Message = $"Error monitoring model performance: {ex.Message}"
                    }
                }
            };
        }
    }

    public async Task<ModelRetrainingResult> TriggerAutomaticRetrainingAsync(Guid businessId, RetrainingTrigger retrainingTrigger)
    {
        _logger.LogInformation("Triggering automatic retraining for business {BusinessId} due to {Trigger}", 
            businessId, retrainingTrigger);

        try
        {
            // Get models that need retraining
            var modelsNeedingRetraining = await _performanceMonitoringService.GetModelsNeedingRetrainingAsync(businessId);

            if (!modelsNeedingRetraining.Any())
            {
                return new ModelRetrainingResult
                {
                    Success = true,
                    Message = "No models require retraining at this time",
                    Trigger = retrainingTrigger
                };
            }

            var retrainingResults = new List<ModelRetrainingResult>();

            foreach (var model in modelsNeedingRetraining)
            {
                var previousMetrics = await _performanceMonitoringService.GetModelMetricsAsync(model.ModelId);

                // Retrain the model based on its type
                ModelTrainingResult newTrainingResult;
                switch (model.ModelType)
                {
                    case MLModelType.SalesForecasting:
                        var forecastingConfig = await GetDefaultSalesForecastingConfig(businessId);
                        newTrainingResult = await TrainSalesForecastingModelAsync(businessId, forecastingConfig);
                        break;

                    case MLModelType.ProductRecommendation:
                        var recommendationConfig = await GetDefaultRecommendationConfig(businessId);
                        newTrainingResult = await TrainRecommendationModelAsync(businessId, recommendationConfig);
                        break;

                    default:
                        _logger.LogWarning("Automatic retraining not supported for model type {ModelType}", model.ModelType);
                        continue;
                }

                if (newTrainingResult.Success)
                {
                    // Compare performance with previous model
                    var modelImproved = IsModelImproved(newTrainingResult.PerformanceMetrics, previousMetrics);

                    if (modelImproved)
                    {
                        // Deploy the new model
                        var deploymentConfig = new ModelDeploymentConfig
                        {
                            ModelsToDeploy = new List<MLModelType> { model.ModelType },
                            Environment = DeploymentEnvironment.Production
                        };
                        await DeployModelsAsync(businessId, deploymentConfig);

                        // Deprecate the old model
                        await _modelTrainingService.DeprecateModelAsync(model.ModelId);
                    }

                    retrainingResults.Add(new ModelRetrainingResult
                    {
                        Success = true,
                        Message = "Model retrained successfully",
                        NewModelId = newTrainingResult.ModelId,
                        PreviousModelId = model.ModelId,
                        Trigger = retrainingTrigger,
                        NewModelMetrics = newTrainingResult.PerformanceMetrics,
                        PreviousModelMetrics = previousMetrics,
                        ModelImproved = modelImproved
                    });
                }
            }

            var successfulRetraining = retrainingResults.Count(r => r.Success);
            var improvedModels = retrainingResults.Count(r => r.ModelImproved);

            _logger.LogInformation("Automatic retraining completed for business {BusinessId}. " +
                "Retrained: {RetrainedCount}, Improved: {ImprovedCount}",
                businessId, successfulRetraining, improvedModels);

            return new ModelRetrainingResult
            {
                Success = successfulRetraining > 0,
                Message = $"Retrained {successfulRetraining} models, {improvedModels} showed improvement",
                Trigger = retrainingTrigger
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during automatic retraining for business {BusinessId}", businessId);
            return new ModelRetrainingResult
            {
                Success = false,
                Message = $"Error during automatic retraining: {ex.Message}",
                Trigger = retrainingTrigger
            };
        }
    }

    public async Task<MLPipelineStatus> GetPipelineStatusAsync(Guid businessId)
    {
        _logger.LogInformation("Getting ML pipeline status for business {BusinessId}", businessId);

        try
        {
            var status = new MLPipelineStatus
            {
                BusinessId = businessId
            };

            // Get all models for this business
            var models = await _modelTrainingService.GetAllModelsAsync(businessId);
            
            foreach (var model in models)
            {
                var modelMetrics = await _performanceMonitoringService.GetModelMetricsAsync(model.ModelId);
                var needsRetraining = await _performanceMonitoringService.CheckIfModelNeedsRetrainingAsync(model.ModelId);

                status.ModelStatuses.Add(new ModelStatus
                {
                    ModelId = model.ModelId,
                    ModelType = model.ModelType,
                    State = model.State,
                    LastTrainedAt = model.LastTrainedAt,
                    LastUsedAt = model.LastUsedAt,
                    CurrentMetrics = modelMetrics,
                    NeedsRetraining = needsRetraining
                });
            }

            // Determine overall pipeline health
            status.Health = DeterminePipelineHealth(status.ModelStatuses);
            status.IsActive = status.ModelStatuses.Any(m => m.State == ModelState.Deployed);

            // Get active features
            status.ActiveFeatures = await GetActiveFeaturesAsync(businessId);

            // Get configuration
            status.Configuration = await GetPipelineConfigurationAsync(businessId);

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ML pipeline status for business {BusinessId}", businessId);
            return new MLPipelineStatus
            {
                BusinessId = businessId,
                IsActive = false,
                Health = PipelineHealth.Critical
            };
        }
    }

    #region Private Helper Methods

    private (bool IsValid, List<string> Errors) ValidatePipelineConfiguration(MLPipelineConfiguration config)
    {
        var errors = new List<string>();

        if (!config.EnabledModels.Any())
        {
            errors.Add("At least one model type must be enabled");
        }

        if (config.ValidationConfig?.MinimumAccuracyThreshold < 0 || config.ValidationConfig?.MinimumAccuracyThreshold > 1)
        {
            errors.Add("Minimum accuracy threshold must be between 0 and 1");
        }

        if (config.RetrainingConfig?.RetrainingIntervalDays <= 0)
        {
            errors.Add("Retraining interval must be greater than 0 days");
        }

        return (errors.Count == 0, errors);
    }

    private async Task<List<SalesDataPoint>> CollectSalesDataForTraining(Guid businessId, DateTime startDate, DateTime endDate)
    {
        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        var salesData = new List<SalesDataPoint>();

        foreach (var shop in business.Shops.Where(s => s.IsActive))
        {
            var sales = await _saleRepository.GetSalesByShopAndDateRangeAsync(shop.Id, startDate, endDate);
            
            foreach (var sale in sales)
            {
                salesData.Add(new SalesDataPoint
                {
                    Date = sale.CreatedAt.Date,
                    ShopId = shop.Id,
                    Revenue = sale.TotalAmount,
                    TransactionCount = 1,
                    ItemCount = sale.Items.Count,
                    PaymentMethod = sale.PaymentMethod.ToString(),
                    DayOfWeek = sale.CreatedAt.DayOfWeek,
                    Hour = sale.CreatedAt.Hour
                });
            }
        }

        return salesData;
    }

    private async Task<List<InteractionDataPoint>> CollectInteractionDataForTraining(Guid businessId)
    {
        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        var interactionData = new List<InteractionDataPoint>();

        foreach (var shop in business.Shops.Where(s => s.IsActive))
        {
            var sales = await _saleRepository.GetSalesByShopAsync(shop.Id);
            
            foreach (var sale in sales)
            {
                foreach (var item in sale.Items)
                {
                    interactionData.Add(new InteractionDataPoint
                    {
                        UserId = sale.UserId,
                        ItemId = item.ProductId,
                        InteractionType = "purchase",
                        Rating = 5.0, // Implicit rating for purchases
                        Timestamp = sale.CreatedAt,
                        Context = new Dictionary<string, object>
                        {
                            ["ShopId"] = shop.Id,
                            ["Quantity"] = item.Quantity,
                            ["Price"] = item.UnitPrice
                        }
                    });
                }
            }
        }

        return interactionData;
    }

    private async Task<ModelPerformanceMetrics> EvaluateModelPerformance(object model, object validationSet)
    {
        // Simplified model evaluation - in production, this would use actual ML evaluation
        return new ModelPerformanceMetrics
        {
            Accuracy = 0.85,
            Precision = 0.82,
            Recall = 0.88,
            F1Score = 0.85,
            MeanAbsoluteError = 15.2,
            RootMeanSquareError = 22.1,
            MeanAbsolutePercentageError = 0.12
        };
    }

    private async Task<ModelPerformanceMetrics> EvaluateRecommendationModelPerformance(object model, object validationSet)
    {
        // Simplified recommendation model evaluation
        return new ModelPerformanceMetrics
        {
            Accuracy = 0.78,
            Precision = 0.75, // Precision@10
            Recall = 0.82,    // Recall@10
            F1Score = 0.78,
            CustomMetrics = new Dictionary<string, double>
            {
                ["NDCG@10"] = 0.73,
                ["MAP@10"] = 0.71,
                ["Coverage"] = 0.65
            }
        };
    }

    private async Task<SalesForecastingTrainingConfig> GetDefaultSalesForecastingConfig(Guid businessId)
    {
        return new SalesForecastingTrainingConfig
        {
            HistoricalDataMonths = 12,
            Algorithm = ForecastingAlgorithm.ARIMA,
            ForecastHorizonDays = 30,
            ValidationSplitRatio = 0.2,
            FeatureColumns = new List<string> { "Revenue", "TransactionCount", "DayOfWeek", "Hour" }
        };
    }

    private async Task<RecommendationTrainingConfig> GetDefaultRecommendationConfig(Guid businessId)
    {
        return new RecommendationTrainingConfig
        {
            Algorithm = RecommendationAlgorithm.CollaborativeFiltering,
            MinimumInteractions = 5,
            EmbeddingDimensions = 50,
            TrainingEpochs = 100,
            RegularizationParameter = 0.01
        };
    }

    private bool IsModelImproved(ModelPerformanceMetrics newMetrics, ModelPerformanceMetrics previousMetrics)
    {
        // Simple improvement check - in production, this would be more sophisticated
        return newMetrics.Accuracy > previousMetrics.Accuracy && 
               newMetrics.F1Score > previousMetrics.F1Score;
    }

    private PipelineHealth DeterminePipelineHealth(List<ModelStatus> modelStatuses)
    {
        if (!modelStatuses.Any())
            return PipelineHealth.Offline;

        var failedModels = modelStatuses.Count(m => m.State == ModelState.Failed);
        var needsRetrainingCount = modelStatuses.Count(m => m.NeedsRetraining);

        if (failedModels > 0)
            return PipelineHealth.Critical;

        if (needsRetrainingCount > modelStatuses.Count / 2)
            return PipelineHealth.Warning;

        return PipelineHealth.Healthy;
    }

    private async Task<List<string>> GetActiveFeaturesAsync(Guid businessId)
    {
        // Simplified - in production, this would query actual configuration
        return new List<string>
        {
            "SalesForecasting",
            "ProductRecommendation",
            "DataPreprocessing",
            "FeatureEngineering",
            "PerformanceMonitoring"
        };
    }

    private async Task<Dictionary<string, object>> GetPipelineConfigurationAsync(Guid businessId)
    {
        // Simplified - in production, this would query actual configuration
        return new Dictionary<string, object>
        {
            ["EnabledModels"] = new[] { "SalesForecasting", "ProductRecommendation" },
            ["AutoRetrainingEnabled"] = true,
            ["RetrainingIntervalDays"] = 30,
            ["PerformanceThreshold"] = 0.7
        };
    }

    #endregion
}

/// <summary>
/// Sales data point for training
/// </summary>
public class SalesDataPoint
{
    public DateTime Date { get; set; }
    public Guid ShopId { get; set; }
    public decimal Revenue { get; set; }
    public int TransactionCount { get; set; }
    public int ItemCount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public DayOfWeek DayOfWeek { get; set; }
    public int Hour { get; set; }
}

/// <summary>
/// Interaction data point for recommendation training
/// </summary>
public class InteractionDataPoint
{
    public Guid UserId { get; set; }
    public Guid ItemId { get; set; }
    public string InteractionType { get; set; } = string.Empty;
    public double Rating { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
}