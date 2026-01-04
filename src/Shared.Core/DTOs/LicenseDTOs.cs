using Shared.Core.Enums;

namespace Shared.Core.DTOs;

/// <summary>
/// DTO for license validation results
/// </summary>
public class LicenseValidationResult
{
    /// <summary>
    /// Whether the license is valid
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// Current license status
    /// </summary>
    public LicenseStatus Status { get; set; }
    
    /// <summary>
    /// License type
    /// </summary>
    public LicenseType Type { get; set; }
    
    /// <summary>
    /// Days remaining until expiry (negative if expired)
    /// </summary>
    public int DaysRemaining { get; set; }
    
    /// <summary>
    /// List of enabled features
    /// </summary>
    public List<string> EnabledFeatures { get; set; } = new();
    
    /// <summary>
    /// Validation error message if invalid
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Whether this is a trial license
    /// </summary>
    public bool IsTrial => Type == LicenseType.Trial;
    
    /// <summary>
    /// Whether the license has expired
    /// </summary>
    public bool IsExpired => Status == LicenseStatus.Expired || DaysRemaining < 0;
}

/// <summary>
/// DTO for license activation requests
/// </summary>
public class LicenseActivationRequest
{
    /// <summary>
    /// License key to activate
    /// </summary>
    public string LicenseKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Device ID requesting activation
    /// </summary>
    public Guid DeviceId { get; set; }
    
    /// <summary>
    /// Customer name
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Customer email
    /// </summary>
    public string CustomerEmail { get; set; } = string.Empty;
}

/// <summary>
/// DTO for license activation results
/// </summary>
public class LicenseActivationResult
{
    /// <summary>
    /// Whether activation was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if activation failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Activated license information
    /// </summary>
    public LicenseInfo? License { get; set; }
}

/// <summary>
/// DTO for license information
/// </summary>
public class LicenseInfo
{
    /// <summary>
    /// License ID
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// License key
    /// </summary>
    public string LicenseKey { get; set; } = string.Empty;
    
    /// <summary>
    /// License type
    /// </summary>
    public LicenseType Type { get; set; }
    
    /// <summary>
    /// License status
    /// </summary>
    public LicenseStatus Status { get; set; }
    
    /// <summary>
    /// Issue date
    /// </summary>
    public DateTime IssueDate { get; set; }
    
    /// <summary>
    /// Expiry date
    /// </summary>
    public DateTime ExpiryDate { get; set; }
    
    /// <summary>
    /// Customer name
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Customer email
    /// </summary>
    public string CustomerEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// Maximum devices allowed
    /// </summary>
    public int MaxDevices { get; set; }
    
    /// <summary>
    /// Enabled features
    /// </summary>
    public List<string> Features { get; set; } = new();
    
    /// <summary>
    /// Activation date
    /// </summary>
    public DateTime? ActivationDate { get; set; }
}

/// <summary>
/// DTO for trial license creation
/// </summary>
public class TrialLicenseRequest
{
    /// <summary>
    /// Customer name
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Customer email
    /// </summary>
    public string CustomerEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// Device ID for the trial
    /// </summary>
    public Guid DeviceId { get; set; }
    
    /// <summary>
    /// Trial duration in days (default 30)
    /// </summary>
    public int TrialDays { get; set; } = 30;
}