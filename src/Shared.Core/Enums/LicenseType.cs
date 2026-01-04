namespace Shared.Core.Enums;

/// <summary>
/// Represents the type of license
/// </summary>
public enum LicenseType
{
    /// <summary>
    /// Trial license with limited time
    /// </summary>
    Trial = 0,
    
    /// <summary>
    /// Basic paid license
    /// </summary>
    Basic = 1,
    
    /// <summary>
    /// Professional paid license with advanced features
    /// </summary>
    Professional = 2,
    
    /// <summary>
    /// Enterprise license with all features
    /// </summary>
    Enterprise = 3
}