namespace Shared.Core.Services;

/// <summary>
/// Service for managing device context information
/// </summary>
public interface IDeviceContextService
{
    /// <summary>
    /// Gets the current device ID
    /// </summary>
    Guid GetCurrentDeviceId();
    
    /// <summary>
    /// Sets the current device ID
    /// </summary>
    void SetCurrentDeviceId(Guid deviceId);
    
    /// <summary>
    /// Gets device information
    /// </summary>
    DeviceInfo GetDeviceInfo();
}

/// <summary>
/// Device information
/// </summary>
public class DeviceInfo
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime LastSeen { get; set; }
}