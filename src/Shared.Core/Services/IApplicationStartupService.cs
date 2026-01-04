using Shared.Core.DTOs;

namespace Shared.Core.Services;

/// <summary>
/// Service for handling application startup tasks and validations
/// </summary>
public interface IApplicationStartupService
{
    /// <summary>
    /// Initializes the application with all required validations and setup
    /// </summary>
    /// <returns>Startup result with validation information</returns>
    Task<ApplicationStartupResult> InitializeApplicationAsync();
    
    /// <summary>
    /// Validates license and system requirements
    /// </summary>
    /// <returns>Validation result</returns>
    Task<SystemValidationResult> ValidateSystemRequirementsAsync();
    
    /// <summary>
    /// Initializes default configurations if not present
    /// </summary>
    /// <returns>Task</returns>
    Task InitializeDefaultConfigurationsAsync();
    
    /// <summary>
    /// Checks and creates trial license if no license exists
    /// </summary>
    /// <param name="deviceId">Device ID</param>
    /// <param name="customerName">Customer name for trial</param>
    /// <param name="customerEmail">Customer email for trial</param>
    /// <returns>License creation result</returns>
    Task<LicenseActivationResult> EnsureLicenseExistsAsync(Guid deviceId, string customerName, string customerEmail);
}

public class ApplicationStartupResult
{
    public bool IsSuccessful { get; set; } = true;
    public SystemValidationResult SystemValidation { get; set; } = new();
    public LicenseInfo? LicenseInfo { get; set; }
    public PosConfigurationSummary Configuration { get; set; } = new();
    public List<string> InitializationMessages { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}