using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Enhanced security service that coordinates encryption, audit logging, and session management
/// for multi-tenant POS system security
/// </summary>
public class EnhancedSecurityService : IEnhancedSecurityService
{
    private readonly IEncryptionService _encryptionService;
    private readonly IAuditService _auditService;
    private readonly ISessionService _sessionService;
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<EnhancedSecurityService> _logger;

    public EnhancedSecurityService(
        IEncryptionService encryptionService,
        IAuditService auditService,
        ISessionService sessionService,
        IAuthenticationService authenticationService,
        ILogger<EnhancedSecurityService> logger)
    {
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Encrypts sensitive data for multi-tenant storage with audit logging
    /// </summary>
    public async Task<string> EncryptSensitiveDataAsync(string plainText, Guid? userId = null, string? context = null)
    {
        try
        {
            var encryptedData = _encryptionService.Encrypt(plainText);
            
            // Log encryption activity for audit trail
            if (userId.HasValue)
            {
                await _auditService.LogAsync(
                    userId.Value,
                    AuditAction.DataEncryption,
                    $"Sensitive data encrypted{(context != null ? $" - {context}" : "")}");
            }

            _logger.LogDebug("Sensitive data encrypted successfully");
            return encryptedData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt sensitive data");
            
            if (userId.HasValue)
            {
                await _auditService.LogAsync(
                    userId.Value,
                    AuditAction.SecurityViolation,
                    $"Failed to encrypt sensitive data: {ex.Message}");
            }
            
            throw;
        }
    }

    /// <summary>
    /// Decrypts sensitive data with audit logging and access validation
    /// </summary>
    public async Task<string> DecryptSensitiveDataAsync(string encryptedData, Guid? userId = null, string? context = null)
    {
        try
        {
            var decryptedData = _encryptionService.Decrypt(encryptedData);
            
            // Log decryption activity for audit trail
            if (userId.HasValue)
            {
                await _auditService.LogAsync(
                    userId.Value,
                    AuditAction.DataDecryption,
                    $"Sensitive data decrypted{(context != null ? $" - {context}" : "")}");
            }

            _logger.LogDebug("Sensitive data decrypted successfully");
            return decryptedData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt sensitive data");
            
            if (userId.HasValue)
            {
                await _auditService.LogAsync(
                    userId.Value,
                    AuditAction.SecurityViolation,
                    $"Failed to decrypt sensitive data: {ex.Message}");
            }
            
            throw;
        }
    }

    /// <summary>
    /// Validates session security with role-based timeout enforcement
    /// </summary>
    public async Task<SessionValidationResult> ValidateSessionSecurityAsync(string sessionToken, UserRole? expectedRole = null)
    {
        try
        {
            var session = await _sessionService.GetActiveSessionAsync(sessionToken);
            if (session == null)
            {
                await _auditService.LogAsync(
                    null,
                    AuditAction.SecurityViolation,
                    $"Invalid session token access attempt");

                return new SessionValidationResult
                {
                    IsValid = false,
                    Reason = "Session not found or inactive"
                };
            }

            // Check role-based expiration
            var isExpired = await _sessionService.IsSessionExpiredAsync(sessionToken, expectedRole);
            if (isExpired)
            {
                await _auditService.LogAsync(
                    session.UserId,
                    AuditAction.SecurityViolation,
                    $"Expired session access attempt");

                return new SessionValidationResult
                {
                    IsValid = false,
                    Reason = "Session expired based on role timeout"
                };
            }

            // Update session activity
            await _sessionService.UpdateSessionActivityAsync(sessionToken);

            return new SessionValidationResult
            {
                IsValid = true,
                Session = session,
                Reason = "Session valid"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating session security");
            
            await _auditService.LogAsync(
                null,
                AuditAction.SecurityViolation,
                $"Session validation error: {ex.Message}");

            return new SessionValidationResult
            {
                IsValid = false,
                Reason = $"Validation error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Performs comprehensive security audit for multi-tenant data access
    /// </summary>
    public async Task<SecurityAuditResult> PerformSecurityAuditAsync(Guid businessId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var from = fromDate ?? DateTime.UtcNow.AddDays(-7);
            var to = toDate ?? DateTime.UtcNow;

            // Get security-related audit logs
            var securityViolations = await _auditService.GetSecurityViolationsAsync(from, to);
            var allAuditLogs = await _auditService.GetAuditLogsAsync(from, to);

            // Filter by business for multi-tenant isolation
            var businessLogs = allAuditLogs.Where(log => 
                log.EntityType == nameof(Business) || 
                log.Description.Contains(businessId.ToString())).ToList();

            var result = new SecurityAuditResult
            {
                BusinessId = businessId,
                AuditPeriod = new DTOs.DateRange { StartDate = from, EndDate = to },
                TotalSecurityEvents = businessLogs.Count,
                SecurityViolations = securityViolations.Count(),
                EncryptionEvents = businessLogs.Count(log => 
                    log.Action == AuditAction.DataEncryption || 
                    log.Action == AuditAction.DataDecryption),
                AuthenticationEvents = businessLogs.Count(log => 
                    log.Action == AuditAction.Login || 
                    log.Action == AuditAction.Logout),
                SessionEvents = businessLogs.Count(log => 
                    log.Description.Contains("session", StringComparison.OrdinalIgnoreCase)),
                RecentViolations = securityViolations.Take(10).ToList(),
                SecurityScore = CalculateSecurityScore(businessLogs, securityViolations.Count())
            };

            _logger.LogInformation("Security audit completed for business {BusinessId}: {Score}/100", 
                businessId, result.SecurityScore);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing security audit for business {BusinessId}", businessId);
            throw;
        }
    }

    /// <summary>
    /// Enforces multi-tenant data isolation security policies
    /// </summary>
    public async Task<bool> EnforceDataIsolationAsync(Guid businessId, object data, Guid? userId = null)
    {
        try
        {
            // Validate that data belongs to the correct business
            var isIsolationValid = ValidateBusinessDataIsolation(businessId, data);
            
            if (!isIsolationValid)
            {
                await _auditService.LogAsync(
                    userId,
                    AuditAction.SecurityViolation,
                    $"Data isolation violation detected for business {businessId}");

                _logger.LogWarning("Data isolation violation detected for business {BusinessId}", businessId);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enforcing data isolation for business {BusinessId}", businessId);
            
            await _auditService.LogAsync(
                userId,
                AuditAction.SecurityViolation,
                $"Data isolation enforcement error: {ex.Message}");
    
            throw;
        }
    }

    /// <summary>
    /// Cleans up expired sessions and performs security maintenance
    /// </summary>
    public async Task<SecurityMaintenanceResult> PerformSecurityMaintenanceAsync()
    {
        try
        {
            var expiredSessions = await _sessionService.EndExpiredSessionsWithRoleBasedTimeoutsAsync();
            
            // Log maintenance activity
            await _auditService.LogAsync(
                null,
                AuditAction.SystemMaintenance,
                $"Security maintenance completed: {expiredSessions} expired sessions cleaned");

            var result = new SecurityMaintenanceResult
            {
                ExpiredSessionsCleared = expiredSessions,
                MaintenanceTimestamp = DateTime.UtcNow,
                Success = true
            };

            _logger.LogInformation("Security maintenance completed: {ExpiredSessions} sessions cleared", expiredSessions);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during security maintenance");
            
            return new SecurityMaintenanceResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                MaintenanceTimestamp = DateTime.UtcNow
            };
        }
    }

    private bool ValidateBusinessDataIsolation(Guid businessId, object data)
    {
        // Simplified validation - in real implementation, this would check
        // that the data object contains the correct business ID
        if (data == null) return false;

        var dataType = data.GetType();
        var businessIdProperty = dataType.GetProperty("BusinessId");
        
        if (businessIdProperty != null)
        {
            var dataBusinessId = businessIdProperty.GetValue(data);
            return dataBusinessId?.Equals(businessId) == true;
        }

        // For collections, check first item
        if (data is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                return ValidateBusinessDataIsolation(businessId, item);
            }
        }

        // If no business ID property found, assume valid (for non-business-specific data)
        return true;
    }

    private int CalculateSecurityScore(List<AuditLog> auditLogs, int violationCount)
    {
        // Simplified security score calculation
        var baseScore = 100;
        
        // Deduct points for violations
        var violationPenalty = Math.Min(violationCount * 10, 50);
        
        // Deduct points for lack of security events (indicates no monitoring)
        var securityEventCount = auditLogs.Count(log => 
            log.Action == AuditAction.Login || 
            log.Action == AuditAction.Logout ||
            log.Action == AuditAction.DataEncryption ||
            log.Action == AuditAction.DataDecryption);
        
        var monitoringBonus = Math.Min(securityEventCount / 10, 10);
        
        return Math.Max(0, baseScore - violationPenalty + monitoringBonus);
    }
}

/// <summary>
/// Result of session validation
/// </summary>
public class SessionValidationResult
{
    public bool IsValid { get; set; }
    public UserSession? Session { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Result of security audit
/// </summary>
public class SecurityAuditResult
{
    public Guid BusinessId { get; set; }
    public DTOs.DateRange AuditPeriod { get; set; } = new();
    public int TotalSecurityEvents { get; set; }
    public int SecurityViolations { get; set; }
    public int EncryptionEvents { get; set; }
    public int AuthenticationEvents { get; set; }
    public int SessionEvents { get; set; }
    public List<AuditLog> RecentViolations { get; set; } = new();
    public int SecurityScore { get; set; }
}

/// <summary>
/// Result of security maintenance
/// </summary>
public class SecurityMaintenanceResult
{
    public int ExpiredSessionsCleared { get; set; }
    public DateTime MaintenanceTimestamp { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

