using Shared.Core.Services;
using Microsoft.Extensions.Logging;

namespace Mobile.Services;

public class BackgroundSyncService
{
    private readonly ISyncEngine _syncEngine;
    private readonly IConnectivityService _connectivityService;
    private readonly ILogger<BackgroundSyncService> _logger;
    private Timer? _syncTimer;
    private bool _isRunning;

    public BackgroundSyncService(
        ISyncEngine syncEngine,
        IConnectivityService connectivityService,
        ILogger<BackgroundSyncService> logger)
    {
        _syncEngine = syncEngine;
        _connectivityService = connectivityService;
        _logger = logger;
    }

    public async Task StartAsync()
    {
        if (_isRunning) return;

        _logger.LogInformation("Starting background sync service");
        _isRunning = true;

        // Start periodic sync every 5 minutes
        _syncTimer = new Timer(async _ => await PerformSync(), null, TimeSpan.Zero, TimeSpan.FromMinutes(5));

        // Subscribe to connectivity changes
        _connectivityService.ConnectivityChanged += OnConnectivityChanged;

        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!_isRunning) return;

        _logger.LogInformation("Stopping background sync service");
        _isRunning = false;

        _syncTimer?.Dispose();
        _syncTimer = null;

        _connectivityService.ConnectivityChanged -= OnConnectivityChanged;

        await Task.CompletedTask;
    }

    private async void OnConnectivityChanged(object? sender, Shared.Core.Services.ConnectivityChangedEventArgs e)
    {
        if (e.IsConnected)
        {
            _logger.LogInformation("Connectivity restored, triggering sync");
            await PerformSync();
        }
        else
        {
            _logger.LogInformation("Connectivity lost, sync will resume when connection is restored");
        }
    }

    private async Task PerformSync()
    {
        if (!_isRunning) return;

        try
        {
            var isConnected = _connectivityService.IsConnected;
            if (!isConnected)
            {
                _logger.LogDebug("No connectivity, skipping sync");
                return;
            }

            _logger.LogDebug("Performing background sync");
            var result = await _syncEngine.SyncAllAsync();
            
            if (result.Success)
            {
                _logger.LogDebug("Background sync completed successfully");
            }
            else
            {
                _logger.LogWarning("Background sync failed: {Error}", result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during background sync");
        }
    }
}