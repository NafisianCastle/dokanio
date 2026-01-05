using Shared.Core.DTOs;

namespace Shared.Core.Services;

/// <summary>
/// Service for initializing and validating the multi-business POS system on startup
/// </summary>
public interface IMultiBusinessStartupService
{
    /// <summary>
    /// Initializes the complete multi-business system
    /// </summary>
    /// <returns>Initialization result</returns>
    Task<SystemInitializationResult> InitializeSystemAsync();
    
    /// <summary>
    /// Validates system readiness for production use
    /// </summary>
    /// <returns>Readiness validation result</returns>
    Task<SystemReadinessResult> ValidateSystemReadinessAsync();
    
    /// <summary>
    /// Performs post-startup validation and health checks
    /// </summary>
    /// <returns>Post-startup validation result</returns>
    Task<PostStartupValidationResult> PerformPostStartupValidationAsync();
    
    /// <summary>
    /// Initializes default business types and configurations
    /// </summary>
    /// <returns>Configuration initialization result</returns>
    Task<ConfigurationInitializationResult> InitializeDefaultConfigurationsAsync();
    
    /// <summary>
    /// Validates database schema and performs migrations if needed
    /// </summary>
    /// <returns>Database validation result</returns>
    Task<DatabaseValidationResult> ValidateDatabaseSchemaAsync();
    
    /// <summary>
    /// Initializes AI/ML models and validates their readiness
    /// </summary>
    /// <returns>AI initialization result</returns>
    Task<AIInitializationResult> InitializeAIModelsAsync();
}

/// <summary>
/// Result of system initialization
/// </summary>
public class SystemInitializationResult
{
    public bool IsSuccess { get; set; }
    public List<string> InitializedComponents { get; set; } = new();
    public List<string> FailedComponents { get; set; } = new();
    public Dictionary<string, object> InitializationMetrics { get; set; } = new();
    public TimeSpan TotalInitializationTime { get; set; }
    public DateTime InitializedAt { get; set; } = DateTime.UtcNow;
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Result of system readiness validation
/// </summary>
public class SystemReadinessResult
{
    public bool IsReady { get; set; }
    public List<string> ReadyComponents { get; set; } = new();
    public List<string> NotReadyComponents { get; set; } = new();
    public Dictionary<string, ReadinessStatus> ComponentReadiness { get; set; } = new();
    public List<string> BlockingIssues { get; set; } = new();
    public List<string> NonBlockingIssues { get; set; } = new();
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Readiness status of individual components
/// </summary>
public enum ReadinessStatus
{
    Ready,
    NotReady,
    Warning,
    Error
}

/// <summary>
/// Result of post-startup validation
/// </summary>
public class PostStartupValidationResult
{
    public bool IsValid { get; set; }
    public List<string> ValidatedWorkflows { get; set; } = new();
    public List<string> FailedWorkflows { get; set; } = new();
    public Dictionary<string, TimeSpan> WorkflowPerformance { get; set; } = new();
    public List<string> PerformanceWarnings { get; set; } = new();
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of configuration initialization
/// </summary>
public class ConfigurationInitializationResult
{
    public bool IsSuccess { get; set; }
    public List<string> InitializedConfigurations { get; set; } = new();
    public List<string> FailedConfigurations { get; set; } = new();
    public Dictionary<string, object> ConfigurationValues { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Result of database validation
/// </summary>
public class DatabaseValidationResult
{
    public bool IsValid { get; set; }
    public string DatabaseVersion { get; set; } = string.Empty;
    public List<string> AppliedMigrations { get; set; } = new();
    public List<string> PendingMigrations { get; set; } = new();
    public bool MigrationsApplied { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public TimeSpan ValidationDuration { get; set; }
}

/// <summary>
/// Result of AI initialization
/// </summary>
public class AIInitializationResult
{
    public bool IsSuccess { get; set; }
    public List<string> InitializedModels { get; set; } = new();
    public List<string> FailedModels { get; set; } = new();
    public Dictionary<string, double> ModelAccuracyMetrics { get; set; } = new();
    public Dictionary<string, TimeSpan> ModelLoadTimes { get; set; } = new();
    public bool IsProductionReady { get; set; }
    public List<string> Warnings { get; set; } = new();
}