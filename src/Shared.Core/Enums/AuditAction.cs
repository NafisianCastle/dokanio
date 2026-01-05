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
    SecurityViolation = 13,
    DataEncryption = 14,
    DataDecryption = 15,
    SystemMaintenance = 16,
    DataAccess = 17,
    SecurityAlert = 18,
    ComplianceCheck = 19,
    ThreatDetection = 20,
    SessionTimeout = 21,
    PasswordChange = 22,
    AccountLocked = 23,
    AccountUnlocked = 24
}