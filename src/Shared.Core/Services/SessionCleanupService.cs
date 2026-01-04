using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Shared.Core.Services;

/// <summary>
/// Background service for automatic session cleanup and inactivity timeout
/// </summary>
public class SessionCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);
    private readonly int _inactivityTimeoutMinutes = 30;

    public SessionCleanupService(IServiceProvider serviceProvider, ILogger<SessionCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Session cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredSessionsAsync();
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session cleanup");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Wait before retrying
            }
        }

        _logger.LogInformation("Session cleanup service stopped");
    }

    private async Task CleanupExpiredSessionsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();

        try
        {
            var expiredCount = await sessionService.EndExpiredSessionsAsync(_inactivityTimeoutMinutes);
            if (expiredCount > 0)
            {
                _logger.LogInformation("Cleaned up {ExpiredCount} expired sessions", expiredCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired sessions");
        }
    }
}