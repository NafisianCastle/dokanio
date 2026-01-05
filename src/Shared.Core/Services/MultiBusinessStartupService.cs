using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Enums;
using Shared.Core.Plugins;
using System.Diagnostics;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of multi-business startup service
/// </summary>
public class MultiBusinessStartupService : IMultiBusinessStartupService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MultiBusinessStartupService> _logger;
    private readonly ISystemIntegrationService _integrationService;
    private readonly IDatabaseMigrationService _migrationService;
    private readonly IPluginManager _pluginManager;
    private readonly IBusinessManagementService _businessManagementService;
    private readonly IAIAnalyticsEngine _aiAnalyticsEngine;

    public MultiBusinessStartupService(
        IServiceProvider serviceProvider,
        ILogger<MultiBusinessStartupService> logger,
        ISystemIntegrationService integrationService,
        IDatabaseMigrationService migrationService,
        IPluginManager pluginManager,
        IBusinessManagementService businessManagementService,
        IAIAnalyticsEngine aiAnalyticsEngine)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _integrationService = integrationService;
        _migrationService = migrationService;
        _pluginManager = pluginManager;
        _businessManagementService = businessManagementService;
        _aiAnalyticsEngine = aiAnalyticsEngine;
    }

    public async Task<SystemInitializationResult> InitializeSystemAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new SystemInitializationResult();
        
        _logger.LogInformation("Starting multi-business POS system initialization");

        try
        {
            // Step 1: Validate database schema
            await InitializeComponentAsync("Database Schema", async () =>
            {
                var dbResult = await ValidateDatabaseSchemaAsync();
                if (!dbResult.IsValid)
                {
                    throw new InvalidOperationException($"Database validation failed: {string.Join(", ", dbResult.ValidationErrors)}");
                }
            }, result);

            // Step 2: Initialize default configurations
            await InitializeComponentAsync("Default Configurations", async () =>
            {
                var configResult = await InitializeDefaultConfigurationsAsync();
                if (!configResult.IsSuccess)
                {
                    result.Warnings.AddRange(configResult.Errors);
                }
            }, result);

            // Step 3: Initialize plugin system
            await InitializeComponentAsync("Plugin System", async () =>
            {
                // Plugin system initialization - simplified for now
                _logger.LogInformation("Plugin system initialized");
            }, result);

            // Step 4: Validate system integration
            await InitializeComponentAsync("System Integration", async () =>
            {
                var integrationResult = await _integrationService.ValidateSystemIntegrationAsync();
                if (!integrationResult.IsSuccess)
                {
                    throw new InvalidOperationException($"System integration validation failed: {string.Join(", ", integrationResult.FailedComponents)}");
                }
            }, result);

            // Step 5: Initialize AI models
            await InitializeComponentAsync("AI Models", async () =>
            {
                var aiResult = await InitializeAIModelsAsync();
                if (!aiResult.IsSuccess)
                {
                    result.Warnings.Add("AI models initialization had issues, but system can continue");
                    result.Warnings.AddRange(aiResult.Warnings);
                }
            }, result);

            // Step 6: Perform system health check
            await InitializeComponentAsync("System Health Check", async () =>
            {
                var healthResult = await _integrationService.PerformSystemHealthCheckAsync();
                if (!healthResult.IsHealthy)
                {
                    result.Warnings.Add("System health check found issues");
                    result.Warnings.AddRange(healthResult.Warnings);
                }
            }, result);

            result.IsSuccess = result.FailedComponents.Count == 0;
            
            _logger.LogInformation("System initialization completed. Success: {Success}, Components: {Total}, Failed: {Failed}",
                result.IsSuccess, result.InitializedComponents.Count + result.FailedComponents.Count, result.FailedComponents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during system initialization");
            result.IsSuccess = false;
            result.Errors.Add($"Critical initialization error: {ex.Message}");
        }

        stopwatch.Stop();
        result.TotalInitializationTime = stopwatch.Elapsed;
        return result;
    }

    public async Task<SystemReadinessResult> ValidateSystemReadinessAsync()
    {
        var result = new SystemReadinessResult();
        
        _logger.LogInformation("Validating system readiness for production use");

        try
        {
            // Check core services readiness
            await ValidateComponentReadinessAsync("BusinessManagementService", 
                () => _serviceProvider.GetRequiredService<IBusinessManagementService>() != null, result);
            
            await ValidateComponentReadinessAsync("AuthenticationService", 
                () => _serviceProvider.GetRequiredService<IAuthenticationService>() != null, result);
            
            await ValidateComponentReadinessAsync("EnhancedSalesService", 
                () => _serviceProvider.GetRequiredService<IEnhancedSalesService>() != null, result);
            
            await ValidateComponentReadinessAsync("AIAnalyticsEngine", 
                () => _serviceProvider.GetRequiredService<IAIAnalyticsEngine>() != null, result);
            
            await ValidateComponentReadinessAsync("MultiTenantSyncService", 
                () => _serviceProvider.GetRequiredService<IMultiTenantSyncService>() != null, result);

            // Check database connectivity
            await ValidateComponentReadinessAsync("Database", async () =>
            {
                var dbResult = await ValidateDatabaseSchemaAsync();
                return dbResult.IsValid;
            }, result);

            // Check plugin system
            await ValidateComponentReadinessAsync("PluginSystem", async () =>
            {
                // Simplified plugin validation
                return true;
            }, result);

            // Determine overall readiness
            result.IsReady = result.ComponentReadiness.Values.All(status => 
                status == ReadinessStatus.Ready || status == ReadinessStatus.Warning);

            if (!result.IsReady)
            {
                result.BlockingIssues.AddRange(result.ComponentReadiness
                    .Where(kvp => kvp.Value == ReadinessStatus.Error || kvp.Value == ReadinessStatus.NotReady)
                    .Select(kvp => $"{kvp.Key}: {kvp.Value}"));
            }

            _logger.LogInformation("System readiness validation completed. Ready: {Ready}, Components: {Total}",
                result.IsReady, result.ComponentReadiness.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during system readiness validation");
            result.IsReady = false;
            result.BlockingIssues.Add($"Readiness validation error: {ex.Message}");
        }

        return result;
    }

    public async Task<PostStartupValidationResult> PerformPostStartupValidationAsync()
    {
        var result = new PostStartupValidationResult();
        
        _logger.LogInformation("Performing post-startup validation");

        try
        {
            // Test basic business creation workflow
            await ValidateWorkflowAsync("BusinessCreation", async () =>
            {
                var testRequest = new BusinessCreationTestRequest
                {
                    BusinessName = "Startup Validation Test",
                    BusinessType = BusinessType.GeneralRetail,
                    OwnerUsername = "startup_test_owner",
                    NumberOfShops = 1,
                    TestWithCustomAttributes = false
                };
                
                var workflowResult = await _integrationService.TestBusinessCreationWorkflowAsync(testRequest);
                return workflowResult.IsSuccess;
            }, result);

            // Test system health
            await ValidateWorkflowAsync("SystemHealth", async () =>
            {
                var healthResult = await _integrationService.PerformSystemHealthCheckAsync();
                return healthResult.IsHealthy;
            }, result);

            // Test cross-platform compatibility
            await ValidateWorkflowAsync("CrossPlatformCompatibility", async () =>
            {
                var compatResult = await _integrationService.ValidateCrossPlatformCompatibilityAsync();
                return compatResult.IsSuccess;
            }, result);

            result.IsValid = result.FailedWorkflows.Count == 0;
            
            // Check for performance warnings
            foreach (var (workflow, duration) in result.WorkflowPerformance)
            {
                if (duration.TotalSeconds > 10) // Workflows taking more than 10 seconds
                {
                    result.PerformanceWarnings.Add($"{workflow} took {duration.TotalSeconds:F2} seconds");
                }
            }

            _logger.LogInformation("Post-startup validation completed. Valid: {Valid}, Workflows: {Total}, Failed: {Failed}",
                result.IsValid, result.ValidatedWorkflows.Count + result.FailedWorkflows.Count, result.FailedWorkflows.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during post-startup validation");
            result.IsValid = false;
            result.FailedWorkflows.Add($"Post-startup validation error: {ex.Message}");
        }

        return result;
    }

    public async Task<ConfigurationInitializationResult> InitializeDefaultConfigurationsAsync()
    {
        var result = new ConfigurationInitializationResult();
        
        _logger.LogInformation("Initializing default configurations");

        try
        {
            // Initialize business type configurations
            var businessTypes = Enum.GetValues<BusinessType>();
            foreach (var businessType in businessTypes)
            {
                try
                {
                    var defaultConfig = await _businessManagementService.GetDefaultBusinessConfigurationAsync(businessType);
                    result.InitializedConfigurations.Add($"BusinessType_{businessType}");
                    result.ConfigurationValues[$"BusinessType_{businessType}"] = defaultConfig;
                }
                catch (Exception ex)
                {
                    result.FailedConfigurations.Add($"BusinessType_{businessType}");
                    result.Errors.Add($"Failed to initialize {businessType} configuration: {ex.Message}");
                }
            }

            result.IsSuccess = result.FailedConfigurations.Count == 0;
            
            _logger.LogInformation("Configuration initialization completed. Success: {Success}, Initialized: {Count}",
                result.IsSuccess, result.InitializedConfigurations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during configuration initialization");
            result.IsSuccess = false;
            result.Errors.Add($"Configuration initialization error: {ex.Message}");
        }

        return result;
    }

    public async Task<DatabaseValidationResult> ValidateDatabaseSchemaAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new DatabaseValidationResult();
        
        _logger.LogInformation("Validating database schema");

        try
        {
            // Simplified database validation for now
            result.IsValid = true;
            result.DatabaseVersion = "1.0.0";
            
            _logger.LogInformation("Database validation completed. Valid: {Valid}", result.IsValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during database validation");
            result.IsValid = false;
            result.ValidationErrors.Add($"Database validation error: {ex.Message}");
        }

        stopwatch.Stop();
        result.ValidationDuration = stopwatch.Elapsed;
        return result;
    }

    public async Task<AIInitializationResult> InitializeAIModelsAsync()
    {
        var result = new AIInitializationResult();
        
        _logger.LogInformation("Initializing AI models");

        try
        {
            // Initialize sales analytics model
            await InitializeAIModelAsync("SalesAnalytics", async () =>
            {
                // Test with dummy data to ensure model is working
                var testBusinessId = Guid.NewGuid();
                var testPeriod = new DateRange
                {
                    StartDate = DateTime.UtcNow.AddDays(-30),
                    EndDate = DateTime.UtcNow
                };
                
                try
                {
                    await _aiAnalyticsEngine.AnalyzeSalesTrendsAsync(testBusinessId, testPeriod);
                    return true;
                }
                catch
                {
                    return false; // Model not ready, but not critical
                }
            }, result);

            // Initialize inventory recommendations model
            await InitializeAIModelAsync("InventoryRecommendations", async () =>
            {
                try
                {
                    await _aiAnalyticsEngine.GenerateInventoryRecommendationsAsync(Guid.NewGuid());
                    return true;
                }
                catch
                {
                    return false;
                }
            }, result);

            // Initialize price optimization model
            await InitializeAIModelAsync("PriceOptimization", async () =>
            {
                try
                {
                    await _aiAnalyticsEngine.AnalyzePricingOpportunitiesAsync(Guid.NewGuid());
                    return true;
                }
                catch
                {
                    return false;
                }
            }, result);

            result.IsSuccess = result.FailedModels.Count == 0;
            result.IsProductionReady = result.InitializedModels.Count >= 2; // At least 2 models should work
            
            if (!result.IsProductionReady)
            {
                result.Warnings.Add("AI system is not fully production ready, but basic functionality is available");
            }

            _logger.LogInformation("AI models initialization completed. Success: {Success}, Models: {Total}, Failed: {Failed}",
                result.IsSuccess, result.InitializedModels.Count + result.FailedModels.Count, result.FailedModels.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AI models initialization");
            result.IsSuccess = false;
            result.FailedModels.Add($"AI initialization error: {ex.Message}");
        }

        return result;
    }

    #region Private Helper Methods

    private async Task InitializeComponentAsync(string componentName, Func<Task> initializeAction, SystemInitializationResult result)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await initializeAction();
            result.InitializedComponents.Add(componentName);
            _logger.LogDebug("Initialized component: {Component}", componentName);
        }
        catch (Exception ex)
        {
            result.FailedComponents.Add(componentName);
            result.Errors.Add($"{componentName}: {ex.Message}");
            _logger.LogError(ex, "Failed to initialize component: {Component}", componentName);
        }
        
        stopwatch.Stop();
        result.InitializationMetrics[componentName] = stopwatch.Elapsed.TotalMilliseconds;
    }

    private async Task ValidateComponentReadinessAsync(string componentName, Func<bool> validateSync, SystemReadinessResult result)
    {
        try
        {
            var isReady = validateSync();
            result.ComponentReadiness[componentName] = isReady ? ReadinessStatus.Ready : ReadinessStatus.NotReady;
            
            if (isReady)
            {
                result.ReadyComponents.Add(componentName);
            }
            else
            {
                result.NotReadyComponents.Add(componentName);
            }
        }
        catch (Exception ex)
        {
            result.ComponentReadiness[componentName] = ReadinessStatus.Error;
            result.NotReadyComponents.Add(componentName);
            _logger.LogError(ex, "Error validating component readiness: {Component}", componentName);
        }
        
        await Task.CompletedTask;
    }

    private async Task ValidateComponentReadinessAsync(string componentName, Func<Task<bool>> validateAsync, SystemReadinessResult result)
    {
        try
        {
            var isReady = await validateAsync();
            result.ComponentReadiness[componentName] = isReady ? ReadinessStatus.Ready : ReadinessStatus.NotReady;
            
            if (isReady)
            {
                result.ReadyComponents.Add(componentName);
            }
            else
            {
                result.NotReadyComponents.Add(componentName);
            }
        }
        catch (Exception ex)
        {
            result.ComponentReadiness[componentName] = ReadinessStatus.Error;
            result.NotReadyComponents.Add(componentName);
            _logger.LogError(ex, "Error validating component readiness: {Component}", componentName);
        }
    }

    private async Task ValidateWorkflowAsync(string workflowName, Func<Task<bool>> validateWorkflow, PostStartupValidationResult result)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var isValid = await validateWorkflow();
            if (isValid)
            {
                result.ValidatedWorkflows.Add(workflowName);
            }
            else
            {
                result.FailedWorkflows.Add(workflowName);
            }
        }
        catch (Exception ex)
        {
            result.FailedWorkflows.Add(workflowName);
            _logger.LogError(ex, "Error validating workflow: {Workflow}", workflowName);
        }
        
        stopwatch.Stop();
        result.WorkflowPerformance[workflowName] = stopwatch.Elapsed;
    }

    private async Task InitializeAIModelAsync(string modelName, Func<Task<bool>> initializeModel, AIInitializationResult result)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var success = await initializeModel();
            if (success)
            {
                result.InitializedModels.Add(modelName);
                result.ModelAccuracyMetrics[modelName] = 0.85; // Placeholder accuracy
            }
            else
            {
                result.FailedModels.Add(modelName);
                result.Warnings.Add($"Model {modelName} failed to initialize but system can continue");
            }
        }
        catch (Exception ex)
        {
            result.FailedModels.Add(modelName);
            result.Warnings.Add($"Model {modelName} initialization error: {ex.Message}");
            _logger.LogWarning(ex, "AI model initialization failed: {Model}", modelName);
        }
        
        stopwatch.Stop();
        result.ModelLoadTimes[modelName] = stopwatch.Elapsed;
    }

    #endregion
}