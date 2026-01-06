using Microsoft.AspNetCore.Mvc;
using Shared.Core.DTOs;
using Shared.Core.Services;

namespace Server.Controllers;

/// <summary>
/// API controller for managing system configurations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : ControllerBase
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<ConfigurationController> _logger;

    public ConfigurationController(
        IConfigurationService configurationService,
        ILogger<ConfigurationController> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all system configurations
    /// </summary>
    /// <returns>List of system configurations</returns>
    [HttpGet("system")]
    public async Task<ActionResult<IEnumerable<ConfigurationDto>>> GetSystemConfigurations()
    {
        try
        {
            var configurations = await _configurationService.GetSystemConfigurationsAsync();
            return Ok(configurations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system configurations");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets shop-level pricing settings
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Shop pricing settings</returns>
    [HttpGet("shop/{shopId}/pricing")]
    public async Task<ActionResult<ShopPricingSettings>> GetShopPricingSettings(Guid shopId)
    {
        try
        {
            var settings = await _configurationService.GetShopPricingSettingsAsync(shopId);
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shop pricing settings for shop {ShopId}", shopId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Updates shop-level pricing settings
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="settings">Pricing settings</param>
    /// <returns>Success result</returns>
    [HttpPut("shop/{shopId}/pricing")]
    public async Task<ActionResult> UpdateShopPricingSettings(Guid shopId, [FromBody] ShopPricingSettings settings)
    {
        try
        {
            await _configurationService.SetShopPricingSettingsAsync(shopId, settings);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating shop pricing settings for shop {ShopId}", shopId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets shop-level tax settings
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Shop tax settings</returns>
    [HttpGet("shop/{shopId}/tax")]
    public async Task<ActionResult<ShopTaxSettings>> GetShopTaxSettings(Guid shopId)
    {
        try
        {
            var settings = await _configurationService.GetShopTaxSettingsAsync(shopId);
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shop tax settings for shop {ShopId}", shopId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Updates shop-level tax settings
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="settings">Tax settings</param>
    /// <returns>Success result</returns>
    [HttpPut("shop/{shopId}/tax")]
    public async Task<ActionResult> UpdateShopTaxSettings(Guid shopId, [FromBody] ShopTaxSettings settings)
    {
        try
        {
            await _configurationService.SetShopTaxSettingsAsync(shopId, settings);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating shop tax settings for shop {ShopId}", shopId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets user preferences
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>User preferences</returns>
    [HttpGet("user/{userId}/preferences")]
    public async Task<ActionResult<UserPreferences>> GetUserPreferences(Guid userId)
    {
        try
        {
            var preferences = await _configurationService.GetUserPreferencesAsync(userId);
            return Ok(preferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user preferences for user {UserId}", userId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Updates user preferences
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="preferences">User preferences</param>
    /// <returns>Success result</returns>
    [HttpPut("user/{userId}/preferences")]
    public async Task<ActionResult> UpdateUserPreferences(Guid userId, [FromBody] UserPreferences preferences)
    {
        try
        {
            preferences.UserId = userId; // Ensure consistency
            await _configurationService.SetUserPreferencesAsync(userId, preferences);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user preferences for user {UserId}", userId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets barcode scanner settings
    /// </summary>
    /// <param name="deviceId">Device identifier (optional)</param>
    /// <returns>Barcode scanner settings</returns>
    [HttpGet("barcode-scanner")]
    public async Task<ActionResult<BarcodeScannerSettings>> GetBarcodeScannerSettings([FromQuery] Guid? deviceId = null)
    {
        try
        {
            var settings = await _configurationService.GetBarcodeScannerSettingsAsync(deviceId);
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting barcode scanner settings");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Updates barcode scanner settings
    /// </summary>
    /// <param name="settings">Barcode scanner settings</param>
    /// <param name="deviceId">Device identifier (optional)</param>
    /// <returns>Success result</returns>
    [HttpPut("barcode-scanner")]
    public async Task<ActionResult> UpdateBarcodeScannerSettings([FromBody] BarcodeScannerSettings settings, [FromQuery] Guid? deviceId = null)
    {
        try
        {
            await _configurationService.SetBarcodeScannerSettingsAsync(settings, deviceId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating barcode scanner settings");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets performance settings
    /// </summary>
    /// <returns>Performance settings</returns>
    [HttpGet("performance")]
    public async Task<ActionResult<PerformanceSettings>> GetPerformanceSettings()
    {
        try
        {
            var settings = await _configurationService.GetPerformanceSettingsAsync();
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance settings");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Updates performance settings
    /// </summary>
    /// <param name="settings">Performance settings</param>
    /// <returns>Success result</returns>
    [HttpPut("performance")]
    public async Task<ActionResult> UpdatePerformanceSettings([FromBody] PerformanceSettings settings)
    {
        try
        {
            await _configurationService.SetPerformanceSettingsAsync(settings);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance settings");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets business settings
    /// </summary>
    /// <returns>Business settings</returns>
    [HttpGet("business")]
    public async Task<ActionResult<BusinessSettings>> GetBusinessSettings()
    {
        try
        {
            var settings = await _configurationService.GetBusinessSettingsAsync();
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting business settings");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets currency settings
    /// </summary>
    /// <returns>Currency settings</returns>
    [HttpGet("currency")]
    public async Task<ActionResult<CurrencySettings>> GetCurrencySettings()
    {
        try
        {
            var settings = await _configurationService.GetCurrencySettingsAsync();
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting currency settings");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets localization settings
    /// </summary>
    /// <returns>Localization settings</returns>
    [HttpGet("localization")]
    public async Task<ActionResult<LocalizationSettings>> GetLocalizationSettings()
    {
        try
        {
            var settings = await _configurationService.GetLocalizationSettingsAsync();
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting localization settings");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Resets a configuration to its default value
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <returns>Success result</returns>
    [HttpPost("reset/{key}")]
    public async Task<ActionResult> ResetConfiguration(string key)
    {
        try
        {
            await _configurationService.ResetConfigurationAsync(key);
            return Ok();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid configuration key: {Key}", key);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting configuration {Key}", key);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Initializes default configurations
    /// </summary>
    /// <returns>Success result</returns>
    [HttpPost("initialize-defaults")]
    public async Task<ActionResult> InitializeDefaultConfigurations()
    {
        try
        {
            await _configurationService.InitializeDefaultConfigurationsAsync();
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing default configurations");
            return StatusCode(500, "Internal server error");
        }
    }
}