namespace Shared.Core.Enums;

/// <summary>
/// Represents the type of configuration value
/// </summary>
public enum ConfigurationType
{
    /// <summary>
    /// String configuration value
    /// </summary>
    String = 0,
    
    /// <summary>
    /// Numeric configuration value
    /// </summary>
    Number = 1,
    
    /// <summary>
    /// Boolean configuration value
    /// </summary>
    Boolean = 2,
    
    /// <summary>
    /// Currency configuration value
    /// </summary>
    Currency = 3,
    
    /// <summary>
    /// Percentage configuration value
    /// </summary>
    Percentage = 4
}