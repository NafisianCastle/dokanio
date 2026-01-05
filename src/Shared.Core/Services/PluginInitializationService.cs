using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Core.Plugins;

namespace Shared.Core.Services;

/// <summary>
/// Background service that initializes plugins on startup
/// </summary>
public class PluginInitializationService : BackgroundService
{
    private readonly IPluginManager _pluginManager;
    private readonly ILogger<PluginInitializationService> _logger;
    private readonly string _pluginDirectory;

    public PluginInitializationService(
        IPluginManager pluginManager,
        ILogger<PluginInitializationService> logger,
        string pluginDirectory)
    {
        _pluginManager = pluginManager;
        _logger = logger;
        _pluginDirectory = pluginDirectory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting plugin initialization from directory: {PluginDirectory}", _pluginDirectory);

            // Load plugins from directory
            await _pluginManager.LoadPluginsFromDirectoryAsync(_pluginDirectory);

            // Validate all loaded plugins
            var validationResults = await _pluginManager.ValidateAllPluginsAsync();
            
            foreach (var result in validationResults)
            {
                if (result.Value.IsValid)
                {
                    _logger.LogInformation("Plugin for business type {BusinessType} loaded and validated successfully", result.Key);
                }
                else
                {
                    _logger.LogWarning("Plugin for business type {BusinessType} validation failed: {Errors}", 
                        result.Key, string.Join(", ", result.Value.Errors));
                }
            }

            var loadedPlugins = _pluginManager.GetAllPlugins().ToList();
            _logger.LogInformation("Plugin initialization completed. Loaded {PluginCount} plugins", loadedPlugins.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during plugin initialization");
        }
    }
}