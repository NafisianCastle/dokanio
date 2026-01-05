using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Shared.Core.Services;

/// <summary>
/// Service for managing device context information
/// </summary>
public class DeviceContextService : IDeviceContextService
{
    private readonly ILogger<DeviceContextService> _logger;
    private Guid _currentDeviceId;
    private DeviceInfo? _deviceInfo;

    public DeviceContextService(ILogger<DeviceContextService> logger)
    {
        _logger = logger;
        _currentDeviceId = GenerateDeviceId();
        InitializeDeviceInfo();
    }

    public Guid GetCurrentDeviceId()
    {
        return _currentDeviceId;
    }

    public void SetCurrentDeviceId(Guid deviceId)
    {
        _currentDeviceId = deviceId;
        _logger.LogInformation("Device ID set to {DeviceId}", deviceId);
    }

    public DeviceInfo GetDeviceInfo()
    {
        if (_deviceInfo == null)
        {
            InitializeDeviceInfo();
        }
        
        return _deviceInfo!;
    }

    private void InitializeDeviceInfo()
    {
        try
        {
            _deviceInfo = new DeviceInfo
            {
                DeviceId = _currentDeviceId,
                DeviceName = Environment.MachineName,
                Platform = GetPlatformName(),
                Version = Environment.OSVersion.ToString(),
                LastSeen = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing device info");
            _deviceInfo = new DeviceInfo
            {
                DeviceId = _currentDeviceId,
                DeviceName = "Unknown",
                Platform = "Unknown",
                Version = "Unknown",
                LastSeen = DateTime.UtcNow
            };
        }
    }

    private Guid GenerateDeviceId()
    {
        try
        {
            // Try to generate a consistent device ID based on machine characteristics
            var machineId = Environment.MachineName + Environment.UserName + Environment.OSVersion.ToString();
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(machineId));
            
            // Use first 16 bytes to create a GUID
            var guidBytes = new byte[16];
            Array.Copy(hash, guidBytes, 16);
            
            return new Guid(guidBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not generate consistent device ID, using random GUID");
            return Guid.NewGuid();
        }
    }

    private string GetPlatformName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "macOS";
        
        return "Unknown";
    }
}