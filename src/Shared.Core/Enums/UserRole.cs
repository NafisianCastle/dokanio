namespace Shared.Core.Enums;

/// <summary>
/// Defines user roles for role-based access control in multi-business environment
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Basic cashier with limited permissions for sales processing
    /// </summary>
    Cashier = 0,
    
    /// <summary>
    /// Inventory staff with permissions for stock management
    /// </summary>
    InventoryStaff = 1,
    
    /// <summary>
    /// Shop manager with full operational permissions for a specific shop
    /// </summary>
    ShopManager = 2,
    
    /// <summary>
    /// Business owner with full access to all shops, analytics, and configurations
    /// </summary>
    BusinessOwner = 3,
    
    /// <summary>
    /// System administrator with system-level permissions (legacy role)
    /// </summary>
    Administrator = 4,
    
    /// <summary>
    /// Legacy supervisor role (maintained for backward compatibility)
    /// </summary>
    Supervisor = 5,
    
    /// <summary>
    /// Legacy manager role (maintained for backward compatibility)
    /// </summary>
    Manager = 6
}