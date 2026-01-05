using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Service for system integration and end-to-end workflow validation
/// </summary>
public interface ISystemIntegrationService
{
    /// <summary>
    /// Validates complete system integration and component wiring
    /// </summary>
    /// <returns>Integration validation result</returns>
    Task<SystemIntegrationResult> ValidateSystemIntegrationAsync();
    
    /// <summary>
    /// Tests complete business creation workflow
    /// </summary>
    /// <param name="request">Business creation test request</param>
    /// <returns>Workflow test result</returns>
    Task<WorkflowTestResult> TestBusinessCreationWorkflowAsync(BusinessCreationTestRequest request);
    
    /// <summary>
    /// Tests complete sales workflow from product selection to reporting
    /// </summary>
    /// <param name="request">Sales workflow test request</param>
    /// <returns>Workflow test result</returns>
    Task<WorkflowTestResult> TestSalesWorkflowAsync(SalesWorkflowTestRequest request);
    
    /// <summary>
    /// Tests AI analytics pipeline with production-like data volumes
    /// </summary>
    /// <param name="request">AI pipeline test request</param>
    /// <returns>AI pipeline test result</returns>
    Task<AIPipelineTestResult> TestAIAnalyticsPipelineAsync(AIPipelineTestRequest request);
    
    /// <summary>
    /// Tests multi-tenant data synchronization workflows
    /// </summary>
    /// <param name="request">Sync test request</param>
    /// <returns>Sync test result</returns>
    Task<SyncTestResult> TestMultiTenantSyncWorkflowAsync(SyncTestRequest request);
    
    /// <summary>
    /// Tests role-based access control across all user types
    /// </summary>
    /// <param name="request">RBAC test request</param>
    /// <returns>RBAC test result</returns>
    Task<RBACTestResult> TestRoleBasedAccessControlAsync(RBACTestRequest request);
    
    /// <summary>
    /// Validates cross-platform service compatibility
    /// </summary>
    /// <returns>Cross-platform validation result</returns>
    Task<CrossPlatformValidationResult> ValidateCrossPlatformCompatibilityAsync();
    
    /// <summary>
    /// Performs comprehensive system health check
    /// </summary>
    /// <returns>System health status</returns>
    Task<SystemHealthStatus> PerformSystemHealthCheckAsync();
}

/// <summary>
/// Result of system integration validation
/// </summary>
public class SystemIntegrationResult
{
    public bool IsSuccess { get; set; }
    public List<string> ValidatedComponents { get; set; } = new();
    public List<string> FailedComponents { get; set; } = new();
    public List<string> MissingDependencies { get; set; } = new();
    public Dictionary<string, object> ComponentMetrics { get; set; } = new();
    public TimeSpan ValidationDuration { get; set; }
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Base class for workflow test results
/// </summary>
public abstract class WorkflowTestResult
{
    public bool IsSuccess { get; set; }
    public List<string> CompletedSteps { get; set; } = new();
    public List<string> FailedSteps { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Request for business creation workflow test
/// </summary>
public class BusinessCreationTestRequest
{
    public string BusinessName { get; set; } = "Test Business";
    public BusinessType BusinessType { get; set; } = BusinessType.GeneralRetail;
    public string OwnerUsername { get; set; } = "testowner";
    public int NumberOfShops { get; set; } = 2;
    public bool TestWithCustomAttributes { get; set; } = true;
}

/// <summary>
/// Request for sales workflow test
/// </summary>
public class SalesWorkflowTestRequest
{
    public Guid BusinessId { get; set; }
    public Guid ShopId { get; set; }
    public Guid UserId { get; set; }
    public int NumberOfProducts { get; set; } = 10;
    public int NumberOfSales { get; set; } = 5;
    public bool TestDiscounts { get; set; } = true;
    public bool TestRecommendations { get; set; } = true;
}

/// <summary>
/// Request for AI pipeline test
/// </summary>
public class AIPipelineTestRequest
{
    public Guid BusinessId { get; set; }
    public int DataVolumeMultiplier { get; set; } = 1000; // Production-like volume
    public bool TestSalesAnalytics { get; set; } = true;
    public bool TestInventoryRecommendations { get; set; } = true;
    public bool TestPriceOptimization { get; set; } = true;
    public DateRange AnalysisPeriod { get; set; } = new();
}

/// <summary>
/// Result of AI pipeline test
/// </summary>
public class AIPipelineTestResult : WorkflowTestResult
{
    public int ProcessedRecords { get; set; }
    public TimeSpan DataProcessingTime { get; set; }
    public TimeSpan ModelInferenceTime { get; set; }
    public Dictionary<string, double> AccuracyMetrics { get; set; } = new();
    public List<string> GeneratedRecommendations { get; set; } = new();
}

/// <summary>
/// Request for sync test
/// </summary>
public class SyncTestRequest
{
    public List<Guid> BusinessIds { get; set; } = new();
    public List<Guid> ShopIds { get; set; } = new();
    public bool TestConflictResolution { get; set; } = true;
    public bool TestTenantIsolation { get; set; } = true;
    public int ConcurrentSyncCount { get; set; } = 5;
}

/// <summary>
/// Result of sync test
/// </summary>
public class SyncTestResult : WorkflowTestResult
{
    public int SyncedBusinesses { get; set; }
    public int SyncedShops { get; set; }
    public int ResolvedConflicts { get; set; }
    public List<string> IsolationViolations { get; set; } = new();
    public Dictionary<string, TimeSpan> SyncPerformanceMetrics { get; set; } = new();
}

/// <summary>
/// Request for RBAC test
/// </summary>
public class RBACTestRequest
{
    public Guid BusinessId { get; set; }
    public List<Guid> ShopIds { get; set; } = new();
    public bool TestAllRoles { get; set; } = true;
    public bool TestCrossShopAccess { get; set; } = true;
    public bool TestPermissionInheritance { get; set; } = true;
}

/// <summary>
/// Result of RBAC test
/// </summary>
public class RBACTestResult : WorkflowTestResult
{
    public int TestedRoles { get; set; }
    public int TestedPermissions { get; set; }
    public List<string> AccessViolations { get; set; } = new();
    public Dictionary<UserRole, List<string>> RolePermissionMap { get; set; } = new();
}

/// <summary>
/// Result of cross-platform validation
/// </summary>
public class CrossPlatformValidationResult
{
    public bool IsSuccess { get; set; }
    public List<string> SupportedPlatforms { get; set; } = new();
    public List<string> UnsupportedPlatforms { get; set; } = new();
    public Dictionary<string, List<string>> PlatformSpecificIssues { get; set; } = new();
    public Dictionary<string, object> CompatibilityMetrics { get; set; } = new();
}

/// <summary>
/// System health status
/// </summary>
public class SystemHealthStatus
{
    public bool IsHealthy { get; set; }
    public List<ComponentHealth> ComponentHealths { get; set; } = new();
    public Dictionary<string, object> SystemMetrics { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Health status of individual component
/// </summary>
public class ComponentHealth
{
    public string ComponentName { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public TimeSpan ResponseTime { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = new();
    public List<string> Issues { get; set; } = new();
}