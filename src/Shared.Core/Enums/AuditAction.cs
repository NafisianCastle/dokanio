namespace Shared.Core.Enums;

/// <summary>
/// Defines audit actions for security logging
/// </summary>
public enum AuditAction
{
    Login = 0,
    Logout = 1,
    CreateSale = 2,
    RefundSale = 3,
    CreateProduct = 4,
    UpdateProduct = 5,
    DeleteProduct = 6,
    UpdateInventory = 7,
    AccessReports = 8,
    ChangeUserRole = 9,
    SystemConfiguration = 10,
    DataExport = 11,
    DataImport = 12,
    SecurityViolation = 13
}