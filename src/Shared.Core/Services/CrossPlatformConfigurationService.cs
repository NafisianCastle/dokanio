using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;

namespace Shared.Core.Services;

/// <summary>
/// Service for managing cross-platform configuration
/// </summary>
public interface ICrossPlatformConfigurationService
{
    /// <summary>
    /// Gets the current device configuration
    /// </summary>
    Task<DeviceConfiguration> GetDeviceConfigurationAsync();
    
    /// <summary>
    /// Updates the device configuration
    /// </summary>
    Task UpdateDeviceConfigurationAsync(DeviceConfiguration configuration);
    
    /// <summary>
    /// Gets the sync configuration for the current device
    /// </summary>
    Task<SyncConfiguration> GetSyncConfigurationAsync();
    
    /// <summary>
    /// Updates the sync configuration
    /// </summary>
    Task UpdateSyncConfigurationAsync(SyncConfiguration configuration);
}

/// <summary>
/// Cross-platform configuration service implementation
/// </summary>
public class CrossPlatformConfigurationService : ICrossPlatformConfigurationService
{
    private readonly ILogger<CrossPlatformConfigurationService> _logger;
    private DeviceConfiguration? _deviceConfiguration;
    private SyncConfiguration? _syncConfiguration;

    public CrossPlatformConfigurationService(ILogger<CrossPlatformConfigurationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the current device configuration
    /// </summary>
    public async Task<DeviceConfiguration> GetDeviceConfigurationAsync()
    {
        if (_deviceConfiguration == null)
        {
            _logger.LogInformation("Initializing device configuration");
            
            _deviceConfiguration = new DeviceConfiguration
            {
                DeviceId = await GetOrCreateDeviceIdAsync(),
                DeviceName = GetDeviceName(),
                Platform = GetCurrentPlatform(),
                Version = GetApplicationVersion(),
                LastSyncTimestamp = DateTime.MinValue,
                IsRegistered = false
            };
        }

        return _deviceConfiguration;
    }

    /// <summary>
    /// Updates the device configuration
    /// </summary>
    public async Task UpdateDeviceConfigurationAsync(DeviceConfiguration configuration)
    {
        _deviceConfiguration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger.LogInformation("Device configuration updated for device {DeviceId}", configuration.DeviceId);
        
        // In a real implementation, this would persist to local storage
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the sync configuration for the current device
    /// </summary>
    public async Task<SyncConfiguration> GetSyncConfigurationAsync()
    {
        if (_syncConfiguration == null)
        {
            var deviceConfig = await GetDeviceConfigurationAsync();
            
            _syncConfiguration = new SyncConfiguration
            {
                DeviceId = deviceConfig.DeviceId,
                ServerBaseUrl = GetDefaultServerUrl(),
                SyncInterval = TimeSpan.FromMinutes(5),
                MaxRetryAttempts = 3,
                InitialRetryDelay = TimeSpan.FromSeconds(1),
                RetryBackoffMultiplier = 2.0
            };
        }

        return _syncConfiguration;
    }

    /// <summary>
    /// Updates the sync configuration
    /// </summary>
    public async Task UpdateSyncConfigurationAsync(SyncConfiguration configuration)
    {
        _syncConfiguration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger.LogInformation("Sync configuration updated for device {DeviceId}", configuration.DeviceId);
        
        // In a real implementation, this would persist to local storage
        await Task.CompletedTask;
    }

    private async Task<Guid> GetOrCreateDeviceIdAsync()
    {
        // In a real implementation, this would check local storage first
        // For now, generate a new one each time
        var deviceId = Guid.NewGuid();
        _logger.LogInformation("Generated new device ID: {DeviceId}", deviceId);
        return deviceId;
    }

    private string GetDeviceName()
    {
        try
        {
            return Environment.MachineName ?? "Unknown Device";
        }
        catch
        {
            return "Unknown Device";
        }
    }

    private string GetCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
            return "Windows";
        else if (OperatingSystem.IsAndroid())
            return "Android";
        else if (OperatingSystem.IsIOS())
            return "iOS";
        else if (OperatingSystem.IsMacOS())
            return "macOS";
        else if (OperatingSystem.IsLinux())
            return "Linux";
        else
            return "Unknown";
    }

    private string GetApplicationVersion()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "1.0.0.0";
        }
        catch
        {
            return "1.0.0.0";
        }
    }

    private string GetDefaultServerUrl()
    {
        // This should be configurable per environment
        return "https://api.offlinepos.local";
    }
}

/// <summary>
/// Device configuration data
/// </summary>
public class DeviceConfiguration
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime LastSyncTimestamp { get; set; }
    public bool IsRegistered { get; set; }
}