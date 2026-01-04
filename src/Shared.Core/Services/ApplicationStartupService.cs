using Shared.Core.DTOs;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Service for handling application startup tasks and validations
/// </summary>
public class ApplicationStartupService : IApplicationStartupService
{
    private readonly ILicenseService _licenseService;
    private readonly IConfigurationService _configurationService;
    private readonly IIntegratedPosService _integratedPosService;
    private readonly IDatabaseMigrationService _databaseMigrationService;

    public ApplicationStartupService(
        ILicenseService licenseService,
        IConfigurationService configurationService,
        IIntegratedPosService integratedPosService,
        IDatabaseMigrationService databaseMigrationService)
    {
        _licenseService = licenseService;
        _configurationService = configurationService;
        _integratedPosService = integratedPosService;
        _databaseMigrationService = databaseMigrationService;
    }

    public async Task<ApplicationStartupResult> InitializeApplicationAsync()
    {
        var result = new ApplicationStartupResult();

        try
        {
            result.InitializationMessages.Add("Starting application initialization...");

            // 1. Run database migrations
            try
            {
                await _databaseMigrationService.InitializeDatabaseAsync();
                result.InitializationMessages.Add("Database initialization completed successfully");
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Database initialization failed: {ex.Message}");
                result.IsSuccessful = false;
                return result;
            }

            // 2. Initialize default configurations
            try
            {
                await InitializeDefaultConfigurationsAsync();
                result.InitializationMessages.Add("Default configurations initialized");
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Configuration initialization warning: {ex.Message}");
            }

            // 3. Validate system requirements
            result.SystemValidation = await ValidateSystemRequirementsAsync();
            if (!result.SystemValidation.IsValid)
            {
                result.IsSuccessful = false;
                result.Errors.AddRange(result.SystemValidation.ValidationErrors);
            }

            // 4. Get license information
            try
            {
                result.LicenseInfo = await _licenseService.GetLicenseInfoAsync();
                if (result.LicenseInfo != null)
                {
                    result.InitializationMessages.Add($"License loaded: {result.LicenseInfo.Type} - {result.LicenseInfo.Status}");
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"License information warning: {ex.Message}");
            }

            // 5. Get configuration summary
            try
            {
                result.Configuration = await _integratedPosService.GetPosConfigurationAsync();
                result.InitializationMessages.Add("Configuration summary loaded");
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Configuration summary warning: {ex.Message}");
            }

            // 6. Add warnings from system validation
            result.Warnings.AddRange(result.SystemValidation.Warnings);

            if (result.IsSuccessful)
            {
                result.InitializationMessages.Add("Application initialization completed successfully");
            }
        }
        catch (Exception ex)
        {
            result.IsSuccessful = false;
            result.Errors.Add($"Application initialization failed: {ex.Message}");
        }

        return result;
    }

    public async Task<SystemValidationResult> ValidateSystemRequirementsAsync()
    {
        return await _integratedPosService.ValidateSystemForSaleAsync();
    }

    public async Task InitializeDefaultConfigurationsAsync()
    {
        await _configurationService.InitializeDefaultConfigurationsAsync();
    }

    public async Task<LicenseActivationResult> EnsureLicenseExistsAsync(Guid deviceId, string customerName, string customerEmail)
    {
        try
        {
            // Check if license already exists
            var existingLicense = await _licenseService.GetCurrentLicenseAsync();
            if (existingLicense != null)
            {
                return new LicenseActivationResult
                {
                    Success = true,
                    ErrorMessage = null,
                    License = new LicenseInfo
                    {
                        LicenseKey = existingLicense.LicenseKey,
                        Type = existingLicense.Type,
                        Status = existingLicense.Status,
                        ExpiryDate = existingLicense.ExpiryDate,
                        Features = existingLicense.Features
                    }
                };
            }

            // Create trial license
            var trialRequest = new TrialLicenseRequest
            {
                DeviceId = deviceId,
                CustomerName = customerName,
                CustomerEmail = customerEmail
            };

            return await _licenseService.CreateTrialLicenseAsync(trialRequest);
        }
        catch (Exception ex)
        {
            return new LicenseActivationResult
            {
                Success = false,
                ErrorMessage = $"Failed to ensure license exists: {ex.Message}"
            };
        }
    }
}