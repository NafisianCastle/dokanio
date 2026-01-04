namespace Shared.Core.Enums;

/// <summary>
/// Represents the status of a license
/// </summary>
public enum LicenseStatus
{
    /// <summary>
    /// License is active and valid
    /// </summary>
    Active = 0,
    
    /// <summary>
    /// License has expired
    /// </summary>
    Expired = 1,
    
    /// <summary>
    /// License has been revoked
    /// </summary>
    Revoked = 2,
    
    /// <summary>
    /// License is temporarily suspended
    /// </summary>
    Suspended = 3
}