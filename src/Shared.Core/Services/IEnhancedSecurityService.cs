using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Enhanced security service interface for multi-tenant POS system security
/// </summary>
public interface IEnhancedSecurityService
{
    /// <summary>
    /// Encrypts sensitive data for multi-tenant storage with audit logging
    /// </summary>
    /// <param name="plainText">Data to encrypt</param>
    /// <param name="userId">User performing the encryption</param>
    /// <param name="context">Context for audit logging</param>
    /// <returns>Encrypted data</returns>
    Task<string> EncryptSensitiveDataAsync(string plainText, Guid? userId = null, string? context = null);

    /// <summary>
    /// Decrypts sensitive data with audit logging and access validation
    /// </summary>
    /// <param name="encryptedData">Data to decrypt</param>
    /// <param name="userId">User performing the decryption</param>
    /// <param name="context">Context for audit logging</param>
    /// <returns>Decrypted data</returns>
    Task<string> DecryptSensitiveDataAsync(string encryptedData, Guid? userId = null, string? context = null);

    /// <summary>
    /// Validates session security with role-based timeout enforcement
    /// </summary>
    /// <param name="sessionToken">Session token to validate</param>
    /// <param name="expectedRole">Expected user role for timeout calculation</param>
    /// <returns>Session validation result</returns>
    Task<SessionValidationResult> ValidateSessionSecurityAsync(string sessionToken, UserRole? expectedRole = null);

    /// <summary>
    /// Performs comprehensive security audit for multi-tenant data access
    /// </summary>
    /// <param name="businessId">Business ID for audit scope</param>
    /// <param name="fromDate">Start date for audit period</param>
    /// <param name="toDate">End date for audit period</param>
    /// <returns>Security audit result</returns>
    Task<SecurityAuditResult> PerformSecurityAuditAsync(Guid businessId, DateTime? fromDate = null, DateTime? toDate = null);

    /// <summary>
    /// Enforces multi-tenant data isolation security policies
    /// </summary>
    /// <param name="businessId">Business ID for isolation validation</param>
    /// <param name="data">Data to validate</param>
    /// <param name="userId">User accessing the data</param>
    /// <returns>True if data isolation is valid</returns>
    Task<bool> EnforceDataIsolationAsync(Guid businessId, object data, Guid? userId = null);

    /// <summary>
    /// Cleans up expired sessions and performs security maintenance
    /// </summary>
    /// <returns>Security maintenance result</returns>
    Task<SecurityMaintenanceResult> PerformSecurityMaintenanceAsync();
}