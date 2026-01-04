using Server.Models;

namespace Server.Services;

/// <summary>
/// Service for JWT token generation and validation
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Generates a JWT token for a device
    /// </summary>
    /// <param name="device">The device to generate token for</param>
    /// <returns>JWT token string</returns>
    string GenerateToken(Device device);
    
    /// <summary>
    /// Validates a JWT token and extracts device information
    /// </summary>
    /// <param name="token">The JWT token to validate</param>
    /// <returns>Device ID if valid, null otherwise</returns>
    Guid? ValidateToken(string token);
    
    /// <summary>
    /// Generates a refresh token
    /// </summary>
    /// <returns>Refresh token string</returns>
    string GenerateRefreshToken();
}