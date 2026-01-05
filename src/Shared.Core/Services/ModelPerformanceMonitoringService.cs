using Microsoft.Extensions.Logging;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of model performance monitoring service
/// </summary>
public class ModelPerformanceMonitoringService : IModelPerformanceMonitoringService
{
    private readonly ILogger<ModelPerformanceMonitoringService> _logger;
    private readonly IModelTrainingService _modelTrainingService;
    private readonly Dictionary<Guid, AutoRetrainingConfig> _monitoringConfigurations = new();
    private readonly Dictionary<string, List<ModelPerformanceMetrics>> _performanceHistory = new();
    private readonly Dictionary<string, List<PredictionOutcome>> _predictionHistory = new();
    private readonly Dictionary<string, DateTime> _lastHealthChecks = new();

    public ModelPerformanceMonitoringService(
        ILogger<ModelPerformanceMonitoringService> logger,
        IModelTrainingService modelTrainingService)
    {
        _logger = logger;
        _modelTrainingService = modelTrainingService;
    }

    public async Task<MonitoringConfigurationResult> ConfigureAsync(Guid businessId, AutoRetrainingConfig config)
    {
        _logger.LogInformation("Configuring performance monitoring for business {BusinessId}", businessId);

        try
        {
            _monitoringConfigurations[businessId] = config;

            var enabledFeatures = new List<string>();
            if (config.EnableAutoRetraining) enabledFeatures.Add("AutoRetraining");
            if (config.RetrainingTriggers.Contains(RetrainingTrigger.PerformanceDegradation)) enabledFeatures.Add("PerformanceMonitoring");
            if (config.RetrainingTriggers.Contains(RetrainingTrigger.DataDrift)) enabledFeatures.Add("DataDriftDetection");

            _logger.LogInformation("Performance monitoring configured for business {BusinessId} with {FeatureCount} features",
                businessId, enabledFeatures.Count);

            return new MonitoringConfigurationResult
            {
                Success = true,
                Message = "Performance monitoring configured successfully",
                BusinessId = businessId,
                Configuration = config,
                EnabledMonitoringFeatures = enabledFeatures
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring performance monitoring for business {BusinessId}", businessId);
            return new MonitoringConfigurationResult
            {
                Success = false,
                Message = $"Error configuring monitoring: {ex.Message}",
                BusinessId = businessId
            };
        }
    }

    public async Task<ModelPerformanceMetrics> EvaluateModelPerformanceAsync(string modelId)
    {
        _logger.LogDebug("Evaluating performance for model {ModelId}", modelId);

        try
        {
            // In production, this would evaluate the model against recent data
            // For now, we'll simulate performance evaluation
            var currentMetrics = await SimulatePerformanceEvaluation(modelId);

            // Store metrics in history
            if (!_performanceHistory.ContainsKey(modelId))
            {
                _performanceHistory[modelId] = new List<ModelPerformanceMetrics>();
            }
            _performanceHistory[modelId].Add(currentMetrics);

            // Keep only last 30 evaluations
            if (_performanceHistory[modelId].Count > 30)
            {
                _performanceHistory[modelId] = _performanceHistory[modelId].TakeLast(30).ToList();
            }

            _logger.LogDebug("Performance evaluation completed for model {ModelId}. Accuracy: {Accuracy}",
                modelId, currentMetrics.Accuracy);

            return currentMetrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating performance for model {ModelId}", modelId);
            return new ModelPerformanceMetrics
            {
                ModelId = modelId,
                LastEvaluatedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<PerformanceAlert?> CheckPerformanceDegradationAsync(string modelId, ModelPerformanceMetrics currentMetrics)
    {
        _logger.LogDebug("Checking performance degradation for model {ModelId}", modelId);

        try
        {
            // Get baseline metrics (first recorded metrics or average of first few)
            var baselineMetrics = await GetBaselineMetrics(modelId);
            if (baselineMetrics == null)
            {
                _logger.LogDebug("No baseline metrics found for model {ModelId}", modelId);
                return null;
            }

            // Check for significant degradation
            var accuracyDrop = baselineMetrics.Accuracy - currentMetrics.Accuracy;
            var threshold = 0.05; // 5% degradation threshold

            if (accuracyDrop > threshold)
            {
                var severity = accuracyDrop switch
                {
                    > 0.15 => AlertSeverity.Critical,
                    > 0.10 => AlertSeverity.High,
                    > 0.05 => AlertSeverity.Medium,
                    _ => AlertSeverity.Low
                };

                _logger.LogWarning("Performance degradation detected for model {ModelId}. Accuracy dropped by {AccuracyDrop:P2}",
                    modelId, accuracyDrop);

                return new PerformanceAlert
                {
                    Severity = severity,
                    ModelId = modelId,
                    AlertType = "PerformanceDegradation",
                    Message = $"Model accuracy dropped by {accuracyDrop:P2} from baseline",
                    CurrentValue = currentMetrics.Accuracy,
                    ThresholdValue = baselineMetrics.Accuracy - threshold
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking performance degradation for model {ModelId}", modelId);
            return new PerformanceAlert
            {
                Severity = AlertSeverity.Critical,
                ModelId = modelId,
                AlertType = "MonitoringError",
                Message = $"Error checking performance degradation: {ex.Message}"
            };
        }
    }

    public async Task<List<ModelStatus>> GetModelsNeedingRetrainingAsync(Guid businessId)
    {
        _logger.LogDebug("Getting models needing retraining for business {BusinessId}", businessId);

        try
        {
            var allModels = await _modelTrainingService.GetAllModelsAsync(businessId);
            var modelsNeedingRetraining = new List<ModelStatus>();

            foreach (var model in allModels)
            {
                var needsRetraining = await CheckIfModelNeedsRetrainingAsync(model.ModelId);
                if (needsRetraining)
                {
                    modelsNeedingRetraining.Add(new ModelStatus
                    {
                        ModelId = model.ModelId,
                        ModelType = model.ModelType,
                        State = model.State,
                        LastTrainedAt = model.LastTrainedAt,
                        LastUsedAt = model.LastUsedAt,
                        CurrentMetrics = model.PerformanceMetrics,
                        NeedsRetraining = true
                    });
                }
            }

            _logger.LogInformation("Found {Count} models needing retraining for business {BusinessId}",
                modelsNeedingRetraining.Count, businessId);

            return modelsNeedingRetraining;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting models needing retraining for business {BusinessId}", businessId);
            return new List<ModelStatus>();
        }
    }

    public async Task<ModelPerformanceMetrics> GetModelMetricsAsync(string modelId)
    {
        if (_performanceHistory.ContainsKey(modelId) && _performanceHistory[modelId].Any())
        {
            return _performanceHistory[modelId].Last();
        }

        // Return default metrics if no history exists
        return new ModelPerformanceMetrics
        {
            ModelId = modelId,
            LastEvaluatedAt = DateTime.UtcNow
        };
    }

    public async Task<bool> CheckIfModelNeedsRetrainingAsync(string modelId)
    {
        try
        {
            // Check various criteria for retraining
            var currentMetrics = await GetModelMetricsAsync(modelId);
            var baselineMetrics = await GetBaselineMetrics(modelId);

            if (baselineMetrics == null) return false;

            // Check performance degradation
            var accuracyDrop = baselineMetrics.Accuracy - currentMetrics.Accuracy;
            if (accuracyDrop > 0.1) // 10% degradation threshold
            {
                return true;
            }

            // Check model age (simplified)
            var daysSinceLastEvaluation = (DateTime.UtcNow - currentMetrics.LastEvaluatedAt).TotalDays;
            if (daysSinceLastEvaluation > 30) // 30 days threshold
            {
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if model {ModelId} needs retraining", modelId);
            return false;
        }
    }

    public async Task<DataDriftAnalysis> MonitorDataDriftAsync(string modelId, List<Dictionary<string, object>> recentData)
    {
        _logger.LogDebug("Monitoring data drift for model {ModelId} with {DataCount} recent samples", modelId, recentData.Count);

        try
        {
            var analysis = new DataDriftAnalysis
            {
                ModelId = modelId
            };

            if (!recentData.Any())
            {
                analysis.DriftDetected = false;
                analysis.Severity = DriftSeverity.None;
                return analysis;
            }

            // Simplified drift detection - in production, this would use statistical tests
            var driftScore = await CalculateDataDriftScore(modelId, recentData);
            analysis.OverallDriftScore = driftScore;

            // Determine drift severity
            analysis.Severity = driftScore switch
            {
                > 0.8 => DriftSeverity.Critical,
                > 0.6 => DriftSeverity.High,
                > 0.4 => DriftSeverity.Medium,
                > 0.2 => DriftSeverity.Low,
                _ => DriftSeverity.None
            };

            analysis.DriftDetected = analysis.Severity > DriftSeverity.None;

            if (analysis.DriftDetected)
            {
                analysis.RecommendedActions.Add("Consider retraining the model with recent data");
                analysis.RecommendedActions.Add("Investigate changes in data sources");
                
                _logger.LogWarning("Data drift detected for model {ModelId}. Drift score: {DriftScore}, Severity: {Severity}",
                    modelId, driftScore, analysis.Severity);
            }

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring data drift for model {ModelId}", modelId);
            return new DataDriftAnalysis
            {
                ModelId = modelId,
                DriftDetected = false,
                Severity = DriftSeverity.None
            };
        }
    }

    public async Task<AccuracyTrackingResult> TrackPredictionAccuracyAsync(string modelId, List<PredictionOutcome> predictions)
    {
        _logger.LogDebug("Tracking prediction accuracy for model {ModelId} with {PredictionCount} predictions", modelId, predictions.Count);

        try
        {
            // Store prediction outcomes
            if (!_predictionHistory.ContainsKey(modelId))
            {
                _predictionHistory[modelId] = new List<PredictionOutcome>();
            }
            _predictionHistory[modelId].AddRange(predictions);

            // Keep only last 1000 predictions
            if (_predictionHistory[modelId].Count > 1000)
            {
                _predictionHistory[modelId] = _predictionHistory[modelId].TakeLast(1000).ToList();
            }

            var result = new AccuracyTrackingResult
            {
                ModelId = modelId,
                TrackingPeriodStart = predictions.Min(p => p.PredictionTime),
                TrackingPeriodEnd = predictions.Max(p => p.PredictionTime),
                TotalPredictions = predictions.Count
            };

            // Calculate current accuracy (simplified)
            result.CurrentAccuracy = await CalculateAccuracy(predictions);

            // Get baseline accuracy
            var baselineMetrics = await GetBaselineMetrics(modelId);
            result.BaselineAccuracy = baselineMetrics?.Accuracy ?? result.CurrentAccuracy;

            result.AccuracyChange = result.CurrentAccuracy - result.BaselineAccuracy;
            result.AccuracyDegraded = result.AccuracyChange < -0.05; // 5% degradation threshold

            // Generate accuracy trends (simplified)
            result.AccuracyTrends = await GenerateAccuracyTrends(modelId, predictions);

            _logger.LogDebug("Accuracy tracking completed for model {ModelId}. Current accuracy: {Accuracy:P2}",
                modelId, result.CurrentAccuracy);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking prediction accuracy for model {ModelId}", modelId);
            return new AccuracyTrackingResult
            {
                ModelId = modelId,
                TotalPredictions = predictions.Count,
                CurrentAccuracy = 0.0
            };
        }
    }

    public async Task<ModelHealthReport> GenerateModelHealthReportAsync(Guid businessId)
    {
        _logger.LogInformation("Generating model health report for business {BusinessId}", businessId);

        try
        {
            var report = new ModelHealthReport
            {
                BusinessId = businessId
            };

            var allModels = await _modelTrainingService.GetAllModelsAsync(businessId);
            
            foreach (var model in allModels)
            {
                var healthStatus = await EvaluateModelHealth(model);
                report.ModelHealthStatuses.Add(healthStatus);

                // Collect critical alerts
                if (healthStatus.HealthLevel == ModelHealthLevel.Critical)
                {
                    foreach (var issue in healthStatus.Issues.Where(i => i.Severity >= HealthSeverity.High))
                    {
                        report.CriticalAlerts.Add(new PerformanceAlert
                        {
                            Severity = issue.Severity == HealthSeverity.Critical ? AlertSeverity.Critical : AlertSeverity.High,
                            ModelId = model.ModelId,
                            AlertType = issue.Type.ToString(),
                            Message = issue.Description
                        });
                    }
                }
            }

            // Calculate overall health summary
            report.OverallHealth = CalculateOverallHealthSummary(report.ModelHealthStatuses);

            // Generate recommended actions
            report.RecommendedActions = GenerateRecommendedActions(report);

            _logger.LogInformation("Model health report generated for business {BusinessId}. Overall health: {HealthLevel}",
                businessId, report.OverallHealth.OverallHealthLevel);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating model health report for business {BusinessId}", businessId);
            return new ModelHealthReport
            {
                BusinessId = businessId,
                OverallHealth = new ModelHealthSummary
                {
                    OverallHealthLevel = ModelHealthLevel.Critical
                }
            };
        }
    }

    public async Task<MonitoringScheduleResult> SetupAutomatedMonitoringAsync(Guid businessId, MonitoringScheduleConfig scheduleConfig)
    {
        _logger.LogInformation("Setting up automated monitoring for business {BusinessId}", businessId);

        try
        {
            var scheduledJobs = new List<string>();

            if (scheduleConfig.EnableDailyHealthChecks)
            {
                scheduledJobs.Add("DailyHealthCheck");
            }

            if (scheduleConfig.EnableWeeklyPerformanceReports)
            {
                scheduledJobs.Add("WeeklyPerformanceReport");
            }

            if (scheduleConfig.EnableRealTimeAlerting)
            {
                scheduledJobs.Add("RealTimeAlerting");
            }

            // In production, this would set up actual scheduled jobs using a job scheduler
            var nextCheck = DateTime.UtcNow.AddHours(scheduleConfig.PerformanceCheckIntervalHours);

            _logger.LogInformation("Automated monitoring setup completed for business {BusinessId} with {JobCount} scheduled jobs",
                businessId, scheduledJobs.Count);

            return new MonitoringScheduleResult
            {
                Success = true,
                Message = "Automated monitoring setup successfully",
                BusinessId = businessId,
                ScheduledJobs = scheduledJobs,
                NextScheduledCheck = nextCheck,
                Configuration = scheduleConfig
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up automated monitoring for business {BusinessId}", businessId);
            return new MonitoringScheduleResult
            {
                Success = false,
                Message = $"Error setting up monitoring: {ex.Message}",
                BusinessId = businessId
            };
        }
    }

    #region Private Helper Methods

    private async Task<ModelPerformanceMetrics> SimulatePerformanceEvaluation(string modelId)
    {
        // Simulate performance evaluation with some randomness to show degradation over time
        var random = new Random(modelId.GetHashCode() + DateTime.UtcNow.Millisecond);
        
        // Get previous metrics to simulate gradual degradation
        var previousMetrics = await GetModelMetricsAsync(modelId);
        var baseAccuracy = previousMetrics.Accuracy > 0 ? previousMetrics.Accuracy : 0.85;
        
        // Simulate slight degradation over time
        var degradationFactor = random.NextDouble() * 0.02; // Up to 2% degradation
        var currentAccuracy = Math.Max(0.5, baseAccuracy - degradationFactor);

        return new ModelPerformanceMetrics
        {
            ModelId = modelId,
            Accuracy = currentAccuracy,
            Precision = currentAccuracy * 0.95,
            Recall = currentAccuracy * 0.98,
            F1Score = currentAccuracy * 0.96,
            MeanAbsoluteError = 15.0 + (random.NextDouble() * 10),
            RootMeanSquareError = 22.0 + (random.NextDouble() * 15),
            MeanAbsolutePercentageError = 0.12 + (random.NextDouble() * 0.08),
            LastEvaluatedAt = DateTime.UtcNow
        };
    }

    private async Task<ModelPerformanceMetrics?> GetBaselineMetrics(string modelId)
    {
        if (_performanceHistory.ContainsKey(modelId) && _performanceHistory[modelId].Any())
        {
            // Return the first recorded metrics as baseline
            return _performanceHistory[modelId].First();
        }

        return null;
    }

    private async Task<double> CalculateDataDriftScore(string modelId, List<Dictionary<string, object>> recentData)
    {
        // Simplified drift calculation - in production, this would use statistical tests
        // like Kolmogorov-Smirnov, Population Stability Index, etc.
        
        var random = new Random(modelId.GetHashCode());
        return random.NextDouble(); // Random drift score between 0 and 1
    }

    private async Task<double> CalculateAccuracy(List<PredictionOutcome> predictions)
    {
        if (!predictions.Any()) return 0.0;

        // Simplified accuracy calculation
        var correctPredictions = 0;
        
        foreach (var prediction in predictions)
        {
            // Simple comparison - in production, this would be more sophisticated
            if (prediction.PredictedValues.ContainsKey("value") && prediction.ActualValues.ContainsKey("value"))
            {
                var predicted = Convert.ToDouble(prediction.PredictedValues["value"]);
                var actual = Convert.ToDouble(prediction.ActualValues["value"]);
                
                // Consider prediction correct if within 10% of actual value
                if (Math.Abs(predicted - actual) / Math.Max(Math.Abs(actual), 1) <= 0.1)
                {
                    correctPredictions++;
                }
            }
        }

        return (double)correctPredictions / predictions.Count;
    }

    private async Task<List<AccuracyTrend>> GenerateAccuracyTrends(string modelId, List<PredictionOutcome> predictions)
    {
        var trends = new List<AccuracyTrend>();
        
        // Group predictions by day and calculate daily accuracy
        var dailyGroups = predictions.GroupBy(p => p.PredictionTime.Date).OrderBy(g => g.Key);
        
        foreach (var group in dailyGroups)
        {
            var dailyPredictions = group.ToList();
            var dailyAccuracy = await CalculateAccuracy(dailyPredictions);
            
            trends.Add(new AccuracyTrend
            {
                Date = group.Key,
                Accuracy = dailyAccuracy,
                PredictionCount = dailyPredictions.Count,
                ConfidenceInterval = 0.95 // Simplified confidence interval
            });
        }

        return trends;
    }

    private async Task<ModelHealthStatus> EvaluateModelHealth(TrainedModelInfo model)
    {
        var healthStatus = new ModelHealthStatus
        {
            ModelId = model.ModelId,
            ModelType = model.ModelType,
            CurrentMetrics = model.PerformanceMetrics,
            LastHealthCheck = DateTime.UtcNow,
            DaysSinceLastRetraining = (int)(DateTime.UtcNow - model.LastTrainedAt).TotalDays
        };

        var issues = new List<HealthIssue>();

        // Check for performance degradation
        var baselineMetrics = await GetBaselineMetrics(model.ModelId);
        if (baselineMetrics != null)
        {
            var accuracyDrop = baselineMetrics.Accuracy - model.PerformanceMetrics.Accuracy;
            if (accuracyDrop > 0.1)
            {
                issues.Add(new HealthIssue
                {
                    Type = HealthIssueType.PerformanceDegradation.ToString(),
                    Description = $"Model accuracy dropped by {accuracyDrop:P2} from baseline",
                    Severity = accuracyDrop > 0.2 ? HealthSeverity.Critical : HealthSeverity.High,
                    RecommendedAction = "Consider retraining the model with recent data"
                });
            }
        }

        // Check model staleness
        if (healthStatus.DaysSinceLastRetraining > 60)
        {
            issues.Add(new HealthIssue
            {
                Type = HealthIssueType.ModelStaleness.ToString(),
                Description = $"Model hasn't been retrained for {healthStatus.DaysSinceLastRetraining} days",
                Severity = healthStatus.DaysSinceLastRetraining > 90 ? HealthSeverity.High : HealthSeverity.Medium,
                RecommendedAction = "Schedule model retraining"
            });
        }

        healthStatus.Issues = issues;

        // Determine overall health level
        healthStatus.HealthLevel = issues.Any() switch
        {
            true when issues.Any(i => i.Severity == HealthSeverity.Critical) => ModelHealthLevel.Critical,
            true when issues.Any(i => i.Severity == HealthSeverity.High) => ModelHealthLevel.Warning,
            true => ModelHealthLevel.Warning,
            false => ModelHealthLevel.Healthy
        };

        healthStatus.RetrainingRecommended = issues.Any(i => 
            i.Type == HealthIssueType.PerformanceDegradation.ToString() || 
            i.Type == HealthIssueType.ModelStaleness.ToString());

        return healthStatus;
    }

    private ModelHealthSummary CalculateOverallHealthSummary(List<ModelHealthStatus> modelHealthStatuses)
    {
        var summary = new ModelHealthSummary
        {
            TotalModels = modelHealthStatuses.Count,
            HealthyModels = modelHealthStatuses.Count(m => m.HealthLevel == ModelHealthLevel.Healthy),
            ModelsWithWarnings = modelHealthStatuses.Count(m => m.HealthLevel == ModelHealthLevel.Warning),
            CriticalModels = modelHealthStatuses.Count(m => m.HealthLevel == ModelHealthLevel.Critical),
            ModelsNeedingRetraining = modelHealthStatuses.Count(m => m.RetrainingRecommended)
        };

        // Calculate overall health score (0-1)
        if (summary.TotalModels == 0)
        {
            summary.OverallHealthScore = 1.0;
            summary.OverallHealthLevel = ModelHealthLevel.Healthy;
        }
        else
        {
            summary.OverallHealthScore = (double)summary.HealthyModels / summary.TotalModels;
            
            summary.OverallHealthLevel = summary.CriticalModels > 0 ? ModelHealthLevel.Critical :
                                       summary.ModelsWithWarnings > summary.HealthyModels ? ModelHealthLevel.Warning :
                                       ModelHealthLevel.Healthy;
        }

        return summary;
    }

    private List<string> GenerateRecommendedActions(ModelHealthReport report)
    {
        var actions = new List<string>();

        if (report.OverallHealth.CriticalModels > 0)
        {
            actions.Add($"Immediate attention required for {report.OverallHealth.CriticalModels} critical models");
        }

        if (report.OverallHealth.ModelsNeedingRetraining > 0)
        {
            actions.Add($"Schedule retraining for {report.OverallHealth.ModelsNeedingRetraining} models");
        }

        if (report.OverallHealth.ModelsWithWarnings > report.OverallHealth.HealthyModels)
        {
            actions.Add("Review monitoring thresholds and alert configurations");
        }

        if (!actions.Any())
        {
            actions.Add("All models are healthy - continue regular monitoring");
        }

        return actions;
    }

    #endregion
}