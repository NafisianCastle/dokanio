using Shared.Core.Enums;

namespace Shared.Core.Plugins;

/// <summary>
/// Interface for managing business type plugins
/// </summary>
public interface IPluginManager
{
    /// <summary>
    /// Registers a business type plugin
    /// </summary>
    /// <param name="plugin">Plugin to register</param>
    Task RegisterPluginAsync(IBusinessTypePlugin plugin);
    
    /// <summary>
    /// Unregisters a business type plugin
    /// </summary>
    /// <param name="businessType">Business type to unregister</param>
    Task UnregisterPluginAsync(BusinessType businessType);
    
    /// <summary>
    /// Gets a plugin for a specific business type
    /// </summary>
    /// <param name="businessType">Business type</param>
    /// <returns>Plugin instance or null if not found</returns>
    IBusinessTypePlugin? GetPlugin(BusinessType businessType);
    
    /// <summary>
    /// Gets all registered plugins
    /// </summary>
    /// <returns>Collection of registered plugins</returns>
    IEnumerable<IBusinessTypePlugin> GetAllPlugins();
    
    /// <summary>
    /// Checks if a plugin is registered for a business type
    /// </summary>
    /// <param name="businessType">Business type to check</param>
    /// <returns>True if plugin is registered</returns>
    bool IsPluginRegistered(BusinessType businessType);
    
    /// <summary>
    /// Loads plugins from a directory
    /// </summary>
    /// <param name="pluginDirectory">Directory containing plugin assemblies</param>
    Task LoadPluginsFromDirectoryAsync(string pluginDirectory);
    
    /// <summary>
    /// Validates all registered plugins
    /// </summary>
    /// <returns>Validation results for all plugins</returns>
    Task<Dictionary<BusinessType, ValidationResult>> ValidateAllPluginsAsync();
}