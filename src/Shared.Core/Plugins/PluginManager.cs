using Microsoft.Extensions.Logging;
using Shared.Core.Enums;
using System.Reflection;

namespace Shared.Core.Plugins;

/// <summary>
/// Implementation of plugin manager for business type plugins
/// </summary>
public class PluginManager : IPluginManager
{
    private readonly Dictionary<BusinessType, IBusinessTypePlugin> _plugins = new();
    private readonly ILogger<PluginManager> _logger;

    public PluginManager(ILogger<PluginManager> logger)
    {
        _logger = logger;
    }

    public async Task RegisterPluginAsync(IBusinessTypePlugin plugin)
    {
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));

        if (_plugins.ContainsKey(plugin.BusinessType))
        {
            _logger.LogWarning("Plugin for business type {BusinessType} is already registered. Replacing existing plugin.", plugin.BusinessType);
        }

        try
        {
            await plugin.InitializeAsync(new Dictionary<string, object>());
            _plugins[plugin.BusinessType] = plugin;
            _logger.LogInformation("Successfully registered plugin for business type {BusinessType} (Version: {Version})", 
                plugin.BusinessType, plugin.Version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize plugin for business type {BusinessType}", plugin.BusinessType);
            throw;
        }
    }

    public async Task UnregisterPluginAsync(BusinessType businessType)
    {
        if (_plugins.TryGetValue(businessType, out var plugin))
        {
            _plugins.Remove(businessType);
            _logger.LogInformation("Unregistered plugin for business type {BusinessType}", businessType);
        }
        else
        {
            _logger.LogWarning("No plugin found for business type {BusinessType} to unregister", businessType);
        }
        
        await Task.CompletedTask;
    }

    public IBusinessTypePlugin? GetPlugin(BusinessType businessType)
    {
        _plugins.TryGetValue(businessType, out var plugin);
        return plugin;
    }

    public IEnumerable<IBusinessTypePlugin> GetAllPlugins()
    {
        return _plugins.Values.ToList();
    }

    public bool IsPluginRegistered(BusinessType businessType)
    {
        return _plugins.ContainsKey(businessType);
    }

    public async Task LoadPluginsFromDirectoryAsync(string pluginDirectory)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            _logger.LogWarning("Plugin directory {PluginDirectory} does not exist", pluginDirectory);
            return;
        }

        var assemblyFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly);
        
        foreach (var assemblyFile in assemblyFiles)
        {
            try
            {
                var assembly = Assembly.LoadFrom(assemblyFile);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IBusinessTypePlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var pluginType in pluginTypes)
                {
                    var plugin = Activator.CreateInstance(pluginType) as IBusinessTypePlugin;
                    if (plugin != null)
                    {
                        await RegisterPluginAsync(plugin);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from assembly {AssemblyFile}", assemblyFile);
            }
        }
    }

    public async Task<Dictionary<BusinessType, ValidationResult>> ValidateAllPluginsAsync()
    {
        var results = new Dictionary<BusinessType, ValidationResult>();

        foreach (var kvp in _plugins)
        {
            try
            {
                // Basic validation - check if plugin can be instantiated and has required properties
                var validationResult = new ValidationResult { IsValid = true };
                
                if (string.IsNullOrEmpty(kvp.Value.DisplayName))
                {
                    validationResult.IsValid = false;
                    validationResult.Errors.Add("Plugin DisplayName is required");
                }
                
                if (kvp.Value.Version == null)
                {
                    validationResult.IsValid = false;
                    validationResult.Errors.Add("Plugin Version is required");
                }

                results[kvp.Key] = validationResult;
            }
            catch (Exception ex)
            {
                results[kvp.Key] = new ValidationResult
                {
                    IsValid = false,
                    Errors = { $"Plugin validation failed: {ex.Message}" }
                };
            }
        }

        return results;
    }
}