using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using System.Text.Json;
using System.Security.Cryptography;

namespace Shared.Core.Services;

/// <summary>
/// Service implementation for license management operations
/// </summary>
public class LicenseService : ILicenseService
{
    private readonly ILicenseRepository _licenseRepository;

    /// <summary>
    /// Computes a SHA-256 hash of the license key for safe logging and correlation.
    /// This avoids storing or exposing the raw license key in logs.
    /// </summary>
    /// <param name="licenseKey">The raw license key.</param>
    /// <returns>Hex-encoded SHA-256 hash of the license key.</returns>
    private static string HashLicenseKey(string licenseKey)
    {
        if (string.IsNullOrEmpty(licenseKey))
        {
            return string.Empty;
        }

        using var sha256 = SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(licenseKey);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes);
    }
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<LicenseService> _logger;

    public LicenseService(
        ILicenseRepository licenseRepository,
        ICurrentUserService currentUserService,
        ILogger<LicenseService> logger)
    {
        _licenseRepository = licenseRepository;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Validates the current license for the device
    /// </summary>
    /// <returns>License validation result</returns>
    public async Task<LicenseValidationResult> ValidateLicenseAsync()
    {
        try
        {
            var deviceId = _currentUserService.GetDeviceId();
            var license = await _licenseRepository.GetCurrentLicenseAsync(deviceId);

            if (license == null)
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    Status = LicenseStatus.Expired,
                    ErrorMessage = "No license found for this device"
                };
            }

            var now = DateTime.UtcNow;
            var daysRemaining = (int)(license.ExpiryDate - now).TotalDays;
            var isExpired = license.ExpiryDate < now;
            var status = isExpired ? LicenseStatus.Expired : license.Status;

            return new LicenseValidationResult
            {
                IsValid = !isExpired && license.Status == LicenseStatus.Active,
                Status = status,
                Type = license.Type,
                DaysRemaining = daysRemaining,
                EnabledFeatures = license.Features,
                ErrorMessage = isExpired ? "License has expired" : 
                              license.Status != LicenseStatus.Active ? $"License is {license.Status}" : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating license");
            return new LicenseValidationResult
            {
                IsValid = false,
                Status = LicenseStatus.Expired,
                ErrorMessage = "Error validating license"
            };
        }
    }

    /// <summary>
    /// Activates a license using a license key
    /// </summary>
    /// <param name="licenseKey">The license key to activate</param>
    /// <returns>True if activation was successful</returns>
    public async Task<bool> ActivateLicenseAsync(string licenseKey)
    {
        try
        {
            var license = await _licenseRepository.GetByLicenseKeyAsync(licenseKey);
            if (license == null)
            {
                _logger.LogWarning("License key not found: {LicenseKey}", licenseKey);
                return false;
            }

            if (license.Status != LicenseStatus.Active)
            {
                _logger.LogWarning("License is not active: {LicenseKey}, Status: {Status}", licenseKey, license.Status);
                return false;
            }

            if (license.ExpiryDate < DateTime.UtcNow)
            {
                _logger.LogWarning("License has expired: {LicenseKey}, ExpiryDate: {ExpiryDate}", licenseKey, license.ExpiryDate);
                return false;
            }

            var deviceId = _currentUserService.GetDeviceId();
            license.DeviceId = deviceId;
            license.ActivationDate = DateTime.UtcNow;

            await _licenseRepository.UpdateAsync(license);
            await _licenseRepository.SaveChangesAsync();

            _logger.LogInformation("License activated successfully: {LicenseKey} for device {DeviceId}", licenseKey, deviceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating license: {LicenseKey}", licenseKey);
            return false;
        }
    }

    /// <summary>
    /// Gets the current license for the device
    /// </summary>
    /// <returns>Current license or null if none exists</returns>
    public async Task<License?> GetCurrentLicenseAsync()
    {
        try
        {
            var deviceId = _currentUserService.GetDeviceId();
            return await _licenseRepository.GetCurrentLicenseAsync(deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current license");
            return null;
        }
    }

    /// <summary>
    /// Checks if a specific feature is enabled in the current license
    /// </summary>
    /// <param name="featureName">Name of the feature to check</param>
    /// <returns>True if feature is enabled</returns>
    public async Task<bool> IsFeatureEnabledAsync(string featureName)
    {
        try
        {
            var license = await GetCurrentLicenseAsync();
            if (license == null || license.Status != LicenseStatus.Active || license.ExpiryDate < DateTime.UtcNow)
            {
                return false;
            }

            // For trial licenses, use the features defined in the license
            // For other license types, also use the features defined in the license
            return license.Features.Contains(featureName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking feature: {FeatureName}", featureName);
            return false;
        }
    }

    /// <summary>
    /// Gets the remaining trial time for trial licenses
    /// </summary>
    /// <returns>Remaining trial time, or TimeSpan.Zero if not a trial or expired</returns>
    public async Task<TimeSpan> GetRemainingTrialTimeAsync()
    {
        try
        {
            var license = await GetCurrentLicenseAsync();
            if (license == null)
            {
                return TimeSpan.Zero;
            }

            var remaining = license.ExpiryDate - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting remaining trial time");
            return TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Checks the current license status
    /// </summary>
    /// <returns>Current license status</returns>
    public async Task<LicenseStatus> CheckLicenseStatusAsync()
    {
        try
        {
            var license = await GetCurrentLicenseAsync();
            if (license == null)
            {
                return LicenseStatus.Expired;
            }

            if (license.ExpiryDate < DateTime.UtcNow)
            {
                return LicenseStatus.Expired;
            }

            return license.Status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking license status");
            return LicenseStatus.Expired;
        }
    }

    /// <summary>
    /// Creates a new trial license for a device
    /// </summary>
    /// <param name="request">Trial license request</param>
    /// <returns>License activation result</returns>
    public async Task<LicenseActivationResult> CreateTrialLicenseAsync(TrialLicenseRequest request)
    {
        try
        {
            // Check if device already has a license
            var existingLicense = await _licenseRepository.GetCurrentLicenseAsync(request.DeviceId);
            if (existingLicense != null)
            {
                return new LicenseActivationResult
                {
                    Success = false,
                    ErrorMessage = "Device already has a license"
                };
            }

            var now = DateTime.UtcNow;
            var licenseKey = GenerateLicenseKey();
            
            var license = new License
            {
                Id = Guid.NewGuid(),
                LicenseKey = licenseKey,
                Type = LicenseType.Trial,
                Status = LicenseStatus.Active,
                IssueDate = now,
                ExpiryDate = now.AddDays(request.TrialDays),
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                MaxDevices = 1,
                Features = new List<string> { "basic_pos", "inventory", "sales_reports" },
                ActivationDate = now,
                DeviceId = request.DeviceId
            };

            await _licenseRepository.AddAsync(license);
            await _licenseRepository.SaveChangesAsync();

            var licenseKeyHash = HashLicenseKey(licenseKey);
            _logger.LogInformation("Trial license created for device {DeviceId}", request.DeviceId);

            return new LicenseActivationResult
            {
                Success = true,
                License = MapToLicenseInfo(license)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating trial license for device {DeviceId}", request.DeviceId);
            return new LicenseActivationResult
            {
                Success = false,
                ErrorMessage = "Error creating trial license"
            };
        }
    }

    /// <summary>
    /// Activates a license with full activation details
    /// </summary>
    /// <param name="request">License activation request</param>
    /// <returns>License activation result</returns>
    public async Task<LicenseActivationResult> ActivateLicenseAsync(LicenseActivationRequest request)
    {
        try
        {
            var license = await _licenseRepository.GetByLicenseKeyAsync(request.LicenseKey);
            if (license == null)
            {
                return new LicenseActivationResult
                {
                    Success = false,
                    ErrorMessage = "Invalid license key"
                };
            }

            if (license.Status != LicenseStatus.Active)
            {
                return new LicenseActivationResult
                {
                    Success = false,
                    ErrorMessage = $"License is {license.Status}"
                };
            }

            if (license.ExpiryDate < DateTime.UtcNow)
            {
                return new LicenseActivationResult
                {
                    Success = false,
                    ErrorMessage = "License has expired"
                };
            }

            license.DeviceId = request.DeviceId;
            license.ActivationDate = DateTime.UtcNow;
            license.CustomerName = request.CustomerName;
            license.CustomerEmail = request.CustomerEmail;

            await _licenseRepository.UpdateAsync(license);
            await _licenseRepository.SaveChangesAsync();

            _logger.LogInformation("License activated: {LicenseKey} for device {DeviceId}", request.LicenseKey, request.DeviceId);

            return new LicenseActivationResult
            {
                Success = true,
                License = MapToLicenseInfo(license)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating license: {LicenseKey}", request.LicenseKey);
            return new LicenseActivationResult
            {
                Success = false,
                ErrorMessage = "Error activating license"
            };
        }
    }

    /// <summary>
    /// Gets license information for the current device
    /// </summary>
    /// <returns>License information or null if no license</returns>
    public async Task<LicenseInfo?> GetLicenseInfoAsync()
    {
        try
        {
            var license = await GetCurrentLicenseAsync();
            return license != null ? MapToLicenseInfo(license) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting license info");
            return null;
        }
    }

    /// <summary>
    /// Checks if the license allows the current number of devices
    /// </summary>
    /// <returns>True if device limit is not exceeded</returns>
    public async Task<bool> IsDeviceLimitValidAsync()
    {
        try
        {
            var license = await GetCurrentLicenseAsync();
            if (license == null)
            {
                return false;
            }

            // For now, we assume one license per device
            // In a full implementation, you would count active devices for this license
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking device limit");
            return false;
        }
    }

    /// <summary>
    /// Updates license status (for administrative purposes)
    /// </summary>
    /// <param name="licenseId">License ID</param>
    /// <param name="status">New status</param>
    /// <returns>True if update was successful</returns>
    public async Task<bool> UpdateLicenseStatusAsync(Guid licenseId, LicenseStatus status)
    {
        try
        {
            var license = await _licenseRepository.GetByIdAsync(licenseId);
            if (license == null)
            {
                return false;
            }

            license.Status = status;
            await _licenseRepository.UpdateAsync(license);
            await _licenseRepository.SaveChangesAsync();

            _logger.LogInformation("License status updated: {LicenseId} to {Status}", licenseId, status);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating license status: {LicenseId}", licenseId);
            return false;
        }
    }

    /// <summary>
    /// Generates a unique license key
    /// </summary>
    /// <returns>Generated license key</returns>
    private static string GenerateLicenseKey()
    {
        var guid = Guid.NewGuid().ToString("N").ToUpper();
        return $"{guid[..8]}-{guid[8..16]}-{guid[16..24]}-{guid[24..]}";
    }

    /// <summary>
    /// Maps License entity to LicenseInfo DTO
    /// </summary>
    /// <param name="license">License entity</param>
    /// <returns>LicenseInfo DTO</returns>
    private static LicenseInfo MapToLicenseInfo(License license)
    {
        return new LicenseInfo
        {
            Id = license.Id,
            LicenseKey = license.LicenseKey,
            Type = license.Type,
            Status = license.Status,
            IssueDate = license.IssueDate,
            ExpiryDate = license.ExpiryDate,
            CustomerName = license.CustomerName,
            CustomerEmail = license.CustomerEmail,
            MaxDevices = license.MaxDevices,
            Features = license.Features,
            ActivationDate = license.ActivationDate
        };
    }
}