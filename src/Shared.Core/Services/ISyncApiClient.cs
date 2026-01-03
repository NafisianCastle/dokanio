namespace Shared.Core.Services;

/// <summary>
/// Client for communicating with the sync API server
/// Handles authentication and data transfer for synchronization
/// </summary>
public interface ISyncApiClient
{
    /// <summary>
    /// Uploads local changes to the server
    /// </summary>
    /// <param name="request">The sync upload request containing local changes</param>
    /// <returns>Result of the upload operation</returns>
    Task<SyncApiResult> UploadChangesAsync(SyncUploadRequest request);
    
    /// <summary>
    /// Downloads changes from the server
    /// </summary>
    /// <param name="deviceId">The device requesting changes</param>
    /// <param name="lastSyncTimestamp">Timestamp of the last successful sync</param>
    /// <returns>Changes from the server since the last sync</returns>
    Task<SyncApiResult<SyncDownloadResponse>> DownloadChangesAsync(Guid deviceId, DateTime lastSyncTimestamp);
    
    /// <summary>
    /// Registers a device with the server for sync operations
    /// </summary>
    /// <param name="deviceId">The device ID to register</param>
    /// <param name="deviceName">Human-readable device name</param>
    /// <returns>Result of the registration operation</returns>
    Task<SyncApiResult> RegisterDeviceAsync(Guid deviceId, string deviceName);
    
    /// <summary>
    /// Authenticates with the server and obtains access tokens
    /// </summary>
    /// <param name="deviceId">The device ID for authentication</param>
    /// <param name="apiKey">The API key for authentication</param>
    /// <returns>Result of the authentication operation</returns>
    Task<SyncApiResult<AuthenticationResponse>> AuthenticateAsync(Guid deviceId, string apiKey);
}

/// <summary>
/// Result of a sync API operation
/// </summary>
public class SyncApiResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Generic result of a sync API operation with data
/// </summary>
/// <typeparam name="T">Type of the result data</typeparam>
public class SyncApiResult<T> : SyncApiResult
{
    public T? Data { get; set; }
}

/// <summary>
/// Response from authentication endpoint
/// </summary>
public class AuthenticationResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public Guid DeviceId { get; set; }
}