namespace Shared.Core.Enums;

/// <summary>
/// Defines different types of businesses supported by the POS system
/// </summary>
public enum BusinessType
{
    /// <summary>
    /// Grocery store with weight-based products
    /// </summary>
    Grocery = 0,
    
    /// <summary>
    /// Large supermarket with diverse product categories
    /// </summary>
    SuperShop = 1,
    
    /// <summary>
    /// Pharmacy/medicine shop with expiry tracking
    /// </summary>
    Pharmacy = 2,
    
    /// <summary>
    /// General retail store
    /// </summary>
    GeneralRetail = 3,
    
    /// <summary>
    /// Custom business type with configurable attributes
    /// </summary>
    Custom = 4
}