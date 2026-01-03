using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;

namespace Shared.Core.Services;

/// <summary>
/// Basic implementation of connectivity service for testing
/// In production, this would be platform-specific (Android/Windows)
/// </summary>
public class ConnectivityService : IConnectivityService, IDisposable
{
    private readonly ILogger<ConnectivityService> _logger;
    private readonly Timer _monitoringTimer;
    private bool _isConnected;
    private bool _isMonitoring;
    private bool _isDisposed;

    public bool IsConnected => _isConnected;

    public event EventHandler<ConnectivityChangedEventArgs>? ConnectivityChanged;

    public ConnectivityService(ILogger<ConnectivityService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _monitoringTimer = new Timer(CheckConnectivity, null, Timeout.Infinite, Timeout.Infinite);
        
        // Initialize connectivity status
        _isConnected = CheckNetworkConnectivity();
    }

    public async Task<bool> IsServerReachableAsync(string serverUrl, TimeSpan timeout = default)
    {
        if (string.IsNullOrEmpty(serverUrl))
            return false;

        if (timeout == default)
            timeout = TimeSpan.FromSeconds(5);

        try
        {
            // Extract hostname from URL
            var uri = new Uri(serverUrl);
            var hostname = uri.Host;

            using var ping = new Ping();
            var reply = await ping.SendPingAsync(hostname, (int)timeout.TotalMilliseconds);
            
            var isReachable = reply.Status == IPStatus.Success;
            _logger.LogDebug("Server {ServerUrl} reachability check: {IsReachable}", serverUrl, isReachable);
            
            return isReachable;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking server reachability for {ServerUrl}", serverUrl);
            return false;
        }
    }

    public Task StartMonitoringAsync()
    {
        if (_isMonitoring)
            return Task.CompletedTask;

        _logger.LogInformation("Starting connectivity monitoring");
        _isMonitoring = true;
        
        // Check every 10 seconds
        _monitoringTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(10));
        
        return Task.CompletedTask;
    }

    public Task StopMonitoringAsync()
    {
        if (!_isMonitoring)
            return Task.CompletedTask;

        _logger.LogInformation("Stopping connectivity monitoring");
        _isMonitoring = false;
        
        _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
        
        return Task.CompletedTask;
    }

    private void CheckConnectivity(object? state)
    {
        if (!_isMonitoring)
            return;

        try
        {
            var wasConnected = _isConnected;
            _isConnected = CheckNetworkConnectivity();

            if (wasConnected != _isConnected)
            {
                _logger.LogInformation("Connectivity status changed: {IsConnected}", _isConnected);
                ConnectivityChanged?.Invoke(this, new ConnectivityChangedEventArgs { IsConnected = _isConnected });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking connectivity");
        }
    }

    private bool CheckNetworkConnectivity()
    {
        try
        {
            // Simple check using NetworkInterface
            return NetworkInterface.GetIsNetworkAvailable();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking network availability");
            return false;
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _monitoringTimer?.Dispose();
            _isDisposed = true;
        }
    }
}