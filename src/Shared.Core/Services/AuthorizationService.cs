using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of role-based authorization service
/// </summary>
public class AuthorizationService : IAuthorizationService
{
    private readonly Dictionary<UserRole, HashSet<AuditAction>> _rolePermissions;

    public AuthorizationService()
    {
        _rolePermissions = InitializeRolePermissions();
    }

    public bool HasPermission(User user, AuditAction action)
    {
        if (!user.IsActive)
            return false;

        return HasPermission(user.Role, action);
    }

    public bool HasPermission(UserRole role, AuditAction action)
    {
        return _rolePermissions.ContainsKey(role) && _rolePermissions[role].Contains(action);
    }

    public IEnumerable<AuditAction> GetRolePermissions(UserRole role)
    {
        return _rolePermissions.ContainsKey(role) ? _rolePermissions[role] : Enumerable.Empty<AuditAction>();
    }

    public bool CanAccessReports(User user)
    {
        return HasPermission(user, AuditAction.AccessReports);
    }

    public bool CanManageInventory(User user)
    {
        return HasPermission(user, AuditAction.UpdateInventory) || 
               HasPermission(user, AuditAction.CreateProduct) || 
               HasPermission(user, AuditAction.UpdateProduct);
    }

    public bool CanManageUsers(User user)
    {
        return HasPermission(user, AuditAction.ChangeUserRole);
    }

    public bool CanProcessRefunds(User user)
    {
        return HasPermission(user, AuditAction.RefundSale);
    }

    private Dictionary<UserRole, HashSet<AuditAction>> InitializeRolePermissions()
    {
        return new Dictionary<UserRole, HashSet<AuditAction>>
        {
            [UserRole.Cashier] = new HashSet<AuditAction>
            {
                AuditAction.Login,
                AuditAction.Logout,
                AuditAction.CreateSale
            },
            
            [UserRole.InventoryStaff] = new HashSet<AuditAction>
            {
                AuditAction.Login,
                AuditAction.Logout,
                AuditAction.CreateProduct,
                AuditAction.UpdateProduct,
                AuditAction.UpdateInventory
            },
            
            [UserRole.ShopManager] = new HashSet<AuditAction>
            {
                AuditAction.Login,
                AuditAction.Logout,
                AuditAction.CreateSale,
                AuditAction.RefundSale,
                AuditAction.CreateProduct,
                AuditAction.UpdateProduct,
                AuditAction.DeleteProduct,
                AuditAction.UpdateInventory,
                AuditAction.AccessReports,
                AuditAction.DataExport,
                AuditAction.DataImport
            },
            
            [UserRole.BusinessOwner] = new HashSet<AuditAction>
            {
                AuditAction.Login,
                AuditAction.Logout,
                AuditAction.CreateSale,
                AuditAction.RefundSale,
                AuditAction.CreateProduct,
                AuditAction.UpdateProduct,
                AuditAction.DeleteProduct,
                AuditAction.UpdateInventory,
                AuditAction.AccessReports,
                AuditAction.ChangeUserRole,
                AuditAction.SystemConfiguration,
                AuditAction.DataExport,
                AuditAction.DataImport
            },
            
            // Legacy roles for backward compatibility
            [UserRole.Supervisor] = new HashSet<AuditAction>
            {
                AuditAction.Login,
                AuditAction.Logout,
                AuditAction.CreateSale,
                AuditAction.RefundSale,
                AuditAction.AccessReports,
                AuditAction.UpdateInventory
            },
            
            [UserRole.Manager] = new HashSet<AuditAction>
            {
                AuditAction.Login,
                AuditAction.Logout,
                AuditAction.CreateSale,
                AuditAction.RefundSale,
                AuditAction.CreateProduct,
                AuditAction.UpdateProduct,
                AuditAction.DeleteProduct,
                AuditAction.UpdateInventory,
                AuditAction.AccessReports,
                AuditAction.DataExport,
                AuditAction.DataImport
            },
            
            [UserRole.Administrator] = new HashSet<AuditAction>
            {
                AuditAction.Login,
                AuditAction.Logout,
                AuditAction.CreateSale,
                AuditAction.RefundSale,
                AuditAction.CreateProduct,
                AuditAction.UpdateProduct,
                AuditAction.DeleteProduct,
                AuditAction.UpdateInventory,
                AuditAction.AccessReports,
                AuditAction.ChangeUserRole,
                AuditAction.SystemConfiguration,
                AuditAction.DataExport,
                AuditAction.DataImport,
                AuditAction.SecurityViolation
            }
        };
    }
}