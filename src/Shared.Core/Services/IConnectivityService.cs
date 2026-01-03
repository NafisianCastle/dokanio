namespace Shared.Core.Services;

/// <summary>
/// Service for monitoring network connectivity status
/// Used by the sync engine to detect when to trigger synchronization
/// </summary>
public interface IConnectivityService
{
    /// <summary>
    /// Gets the current connectivity status
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// Checks connectivity to the sync server specifically
    /// </summary>
    /// <param name="serverUrl">The server URL to check</param>
    /// <param name="timeout">Timeout for the connectivity check</param>
    /// <returns>True if the server is reachable</returns>
    Task<bool> IsServerReachableAsync(string serverUrl, TimeSpan timeout = default);
    
    /// <summary>
    /// Event raised when connectivity status changes
    /// </summary>
    event EventHandler<ConnectivityChangedEventArgs> ConnectivityChanged;
    
    /// <summary>
    /// Starts monitoring connectivity changes
    /// </summary>
    Task StartMonitoringAsync();
    
    /// <summary>
    /// Stops monitoring connectivity changes
    /// </summary>
    Task StopMonitoringAsync();
}