using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Service interface for license management operations
/// </summary>
public interface ILicenseService
{
    /// <summary>
    /// Validates the current license for the device
    /// </summary>
    /// <returns>License validation result</returns>
    Task<LicenseValidationResult> ValidateLicenseAsync();
    
    /// <summary>
    /// Activates a license using a license key
    /// </summary>
    /// <param name="licenseKey">The license key to activate</param>
    /// <returns>True if activation was successful</returns>
    Task<bool> ActivateLicenseAsync(string licenseKey);
    
    /// <summary>
    /// Gets the current license for the device
    /// </summary>
    /// <returns>Current license or null if none exists</returns>
    Task<License?> GetCurrentLicenseAsync();
    
    /// <summary>
    /// Checks if a specific feature is enabled in the current license
    /// </summary>
    /// <param name="featureName">Name of the feature to check</param>
    /// <returns>True if feature is enabled</returns>
    Task<bool> IsFeatureEnabledAsync(string featureName);
    
    /// <summary>
    /// Gets the remaining trial time for trial licenses
    /// </summary>
    /// <returns>Remaining trial time, or TimeSpan.Zero if not a trial or expired</returns>
    Task<TimeSpan> GetRemainingTrialTimeAsync();
    
    /// <summary>
    /// Checks the current license status
    /// </summary>
    /// <returns>Current license status</returns>
    Task<LicenseStatus> CheckLicenseStatusAsync();
    
    /// <summary>
    /// Creates a new trial license for a device
    /// </summary>
    /// <param name="request">Trial license request</param>
    /// <returns>License activation result</returns>
    Task<LicenseActivationResult> CreateTrialLicenseAsync(TrialLicenseRequest request);
    
    /// <summary>
    /// Activates a license with full activation details
    /// </summary>
    /// <param name="request">License activation request</param>
    /// <returns>License activation result</returns>
    Task<LicenseActivationResult> ActivateLicenseAsync(LicenseActivationRequest request);
    
    /// <summary>
    /// Gets license information for the current device
    /// </summary>
    /// <returns>License information or null if no license</returns>
    Task<LicenseInfo?> GetLicenseInfoAsync();
    
    /// <summary>
    /// Checks if the license allows the current number of devices
    /// </summary>
    /// <returns>True if device limit is not exceeded</returns>
    Task<bool> IsDeviceLimitValidAsync();
    
    /// <summary>
    /// Updates license status (for administrative purposes)
    /// </summary>
    /// <param name="licenseId">License ID</param>
    /// <param name="status">New status</param>
    /// <returns>True if update was successful</returns>
    Task<bool> UpdateLicenseStatusAsync(Guid licenseId, LicenseStatus status);
}