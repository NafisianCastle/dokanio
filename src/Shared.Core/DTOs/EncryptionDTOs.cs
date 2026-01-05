using Shared.Core.Enums;

namespace Shared.Core.DTOs;

/// <summary>
/// Result of encryption operation
/// </summary>
public class EncryptionResult
{
    public bool Success { get; set; }
    public string EncryptedData { get; set; } = string.Empty;
    public string IV { get; set; } = string.Empty;
    public string KeyIdentifier { get; set; } = string.Empty;
    public EncryptionType EncryptionType { get; set; }
    public DateTime Timestamp { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of decryption operation
/// </summary>
public class DecryptionResult
{
    public bool Success { get; set; }
    public string DecryptedData { get; set; } = string.Empty;
    public EncryptionType OriginalEncryptionType { get; set; }
    public DateTime DecryptedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of password hashing operation
/// </summary>
public class PasswordHashResult
{
    public bool Success { get; set; }
    public string Hash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public int Iterations { get; set; }
    public string Algorithm { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of key rotation operation
/// </summary>
public class KeyRotationResult
{
    public bool Success { get; set; }
    public string OldKeyIdentifier { get; set; } = string.Empty;
    public string NewKeyIdentifier { get; set; } = string.Empty;
    public DateTime RotationTimestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of encryption health validation
/// </summary>
public class EncryptionHealthResult
{
    public bool IsHealthy { get; set; }
    public List<EncryptionHealthCheck> HealthChecks { get; set; } = new();
    public DateTime CheckedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Individual encryption health check
/// </summary>
public class EncryptionHealthCheck
{
    public EncryptionType EncryptionType { get; set; }
    public bool IsHealthy { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; }
}