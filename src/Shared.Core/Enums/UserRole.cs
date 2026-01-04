namespace Shared.Core.Enums;

/// <summary>
/// Defines user roles for role-based access control
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Basic cashier with limited permissions
    /// </summary>
    Cashier = 0,
    
    /// <summary>
    /// Supervisor with additional permissions
    /// </summary>
    Supervisor = 1,
    
    /// <summary>
    /// Manager with full operational permissions
    /// </summary>
    Manager = 2,
    
    /// <summary>
    /// Administrator with system-level permissions
    /// </summary>
    Administrator = 3
}