using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Service for role-based authorization
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Checks if user has permission to perform an action
    /// </summary>
    /// <param name="user">User to check</param>
    /// <param name="action">Action to check</param>
    /// <returns>True if user has permission</returns>
    bool HasPermission(User user, AuditAction action);
    
    /// <summary>
    /// Checks if user role has permission to perform an action
    /// </summary>
    /// <param name="role">User role</param>
    /// <param name="action">Action to check</param>
    /// <returns>True if role has permission</returns>
    bool HasPermission(UserRole role, AuditAction action);
    
    /// <summary>
    /// Gets all permissions for a user role
    /// </summary>
    /// <param name="role">User role</param>
    /// <returns>List of allowed actions</returns>
    IEnumerable<AuditAction> GetRolePermissions(UserRole role);
    
    /// <summary>
    /// Checks if user can access reports
    /// </summary>
    /// <param name="user">User to check</param>
    /// <returns>True if user can access reports</returns>
    bool CanAccessReports(User user);
    
    /// <summary>
    /// Checks if user can manage inventory
    /// </summary>
    /// <param name="user">User to check</param>
    /// <returns>True if user can manage inventory</returns>
    bool CanManageInventory(User user);
    
    /// <summary>
    /// Checks if user can manage users
    /// </summary>
    /// <param name="user">User to check</param>
    /// <returns>True if user can manage users</returns>
    bool CanManageUsers(User user);
    
    /// <summary>
    /// Checks if user can process refunds
    /// </summary>
    /// <param name="user">User to check</param>
    /// <returns>True if user can process refunds</returns>
    bool CanProcessRefunds(User user);
}