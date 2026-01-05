using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Plugins;

/// <summary>
/// Interface for business type plugins that extend the system with new business types
/// </summary>
public interface IBusinessTypePlugin
{
    /// <summary>
    /// The business type this plugin supports
    /// </summary>
    BusinessType BusinessType { get; }
    
    /// <summary>
    /// Display name for the business type
    /// </summary>
    string DisplayName { get; }
    
    /// <summary>
    /// Description of the business type
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Version of the plugin
    /// </summary>
    Version Version { get; }
    
    /// <summary>
    /// Validates a product for this business type
    /// </summary>
    /// <param name="product">Product to validate</param>
    /// <returns>Validation result</returns>
    Task<ValidationResult> ValidateProductAsync(Product product);
    
    /// <summary>
    /// Validates a sale for this business type
    /// </summary>
    /// <param name="sale">Sale to validate</param>
    /// <returns>Validation result</returns>
    Task<ValidationResult> ValidateSaleAsync(Sale sale);
    
    /// <summary>
    /// Gets custom product attributes for this business type
    /// </summary>
    /// <returns>List of custom attribute definitions</returns>
    IEnumerable<CustomAttributeDefinition> GetCustomProductAttributes();
    
    /// <summary>
    /// Gets business-specific configuration schema
    /// </summary>
    /// <returns>Configuration schema</returns>
    BusinessConfigurationSchema GetConfigurationSchema();
    
    /// <summary>
    /// Initializes the plugin with configuration
    /// </summary>
    /// <param name="configuration">Plugin configuration</param>
    Task InitializeAsync(Dictionary<string, object> configuration);
}

/// <summary>
/// Validation result for business type operations
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Definition for custom product attributes
/// </summary>
public class CustomAttributeDefinition
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Type DataType { get; set; } = typeof(string);
    public bool IsRequired { get; set; }
    public object? DefaultValue { get; set; }
    public string? ValidationPattern { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Configuration schema for business types
/// </summary>
public class BusinessConfigurationSchema
{
    public List<ConfigurationField> Fields { get; set; } = new();
}

/// <summary>
/// Configuration field definition
/// </summary>
public class ConfigurationField
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Type DataType { get; set; } = typeof(string);
    public bool IsRequired { get; set; }
    public object? DefaultValue { get; set; }
    public string? Description { get; set; }
    public List<object>? AllowedValues { get; set; }
}