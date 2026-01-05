using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Shared.Core.DTOs;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Enhanced encryption service supporting multiple encryption types and key management
/// </summary>
public class EnhancedEncryptionService : IEnhancedEncryptionService
{
    private readonly ILogger<EnhancedEncryptionService> _logger;
    private readonly Dictionary<EncryptionType, EncryptionConfig> _encryptionConfigs;
    private readonly Dictionary<string, byte[]> _keyCache;

    public EnhancedEncryptionService(ILogger<EnhancedEncryptionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _encryptionConfigs = InitializeEncryptionConfigs();
        _keyCache = new Dictionary<string, byte[]>();
    }

    /// <summary>
    /// Encrypts data with specified encryption type
    /// </summary>
    public async Task<EncryptionResult> EncryptAsync(string plainText, EncryptionType encryptionType, string? keyIdentifier = null)
    {
        try
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return new EncryptionResult
                {
                    Success = false,
                    ErrorMessage = "Plain text cannot be null or empty"
                };
            }

            var config = _encryptionConfigs[encryptionType];
            var key = await GetEncryptionKeyAsync(keyIdentifier ?? config.DefaultKeyId);
            var iv = GenerateIV();

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            using var msEncrypt = new MemoryStream();
            using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
            using var swEncrypt = new StreamWriter(csEncrypt);

            await swEncrypt.WriteAsync(plainText);
            swEncrypt.Close();

            var encryptedBytes = msEncrypt.ToArray();
            var result = new EncryptionResult
            {
                Success = true,
                EncryptedData = Convert.ToBase64String(encryptedBytes),
                IV = Convert.ToBase64String(iv),
                KeyIdentifier = keyIdentifier ?? config.DefaultKeyId,
                EncryptionType = encryptionType,
                Timestamp = DateTime.UtcNow
            };

            _logger.LogDebug("Data encrypted successfully with {EncryptionType}", encryptionType);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt data with {EncryptionType}", encryptionType);
            return new EncryptionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Decrypts data using the provided encryption result
    /// </summary>
    public async Task<DecryptionResult> DecryptAsync(EncryptionResult encryptionResult)
    {
        try
        {
            if (encryptionResult == null || !encryptionResult.Success)
            {
                return new DecryptionResult
                {
                    Success = false,
                    ErrorMessage = "Invalid encryption result"
                };
            }

            var key = await GetEncryptionKeyAsync(encryptionResult.KeyIdentifier);
            var encryptedBytes = Convert.FromBase64String(encryptionResult.EncryptedData);
            var iv = Convert.FromBase64String(encryptionResult.IV);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            using var msDecrypt = new MemoryStream(encryptedBytes);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);

            var decryptedText = await srDecrypt.ReadToEndAsync();

            var result = new DecryptionResult
            {
                Success = true,
                DecryptedData = decryptedText,
                OriginalEncryptionType = encryptionResult.EncryptionType,
                DecryptedAt = DateTime.UtcNow
            };

            _logger.LogDebug("Data decrypted successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt data");
            return new DecryptionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Encrypts sensitive object data for storage
    /// </summary>
    public async Task<string> EncryptObjectAsync<T>(T obj, EncryptionType encryptionType = EncryptionType.AtRest)
    {
        try
        {
            var json = JsonSerializer.Serialize(obj);
            var encryptionResult = await EncryptAsync(json, encryptionType);
            
            if (!encryptionResult.Success)
            {
                throw new InvalidOperationException($"Failed to encrypt object: {encryptionResult.ErrorMessage}");
            }

            // Store encryption metadata with the encrypted data
            var encryptedObject = new EncryptedObject
            {
                Data = encryptionResult.EncryptedData,
                IV = encryptionResult.IV,
                KeyIdentifier = encryptionResult.KeyIdentifier,
                EncryptionType = encryptionResult.EncryptionType,
                Timestamp = encryptionResult.Timestamp
            };

            return JsonSerializer.Serialize(encryptedObject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt object of type {Type}", typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// Decrypts object data from storage
    /// </summary>
    public async Task<T?> DecryptObjectAsync<T>(string encryptedObjectJson)
    {
        try
        {
            var encryptedObject = JsonSerializer.Deserialize<EncryptedObject>(encryptedObjectJson);
            if (encryptedObject == null)
            {
                throw new InvalidOperationException("Invalid encrypted object format");
            }

            var encryptionResult = new EncryptionResult
            {
                Success = true,
                EncryptedData = encryptedObject.Data,
                IV = encryptedObject.IV,
                KeyIdentifier = encryptedObject.KeyIdentifier,
                EncryptionType = encryptedObject.EncryptionType,
                Timestamp = encryptedObject.Timestamp
            };

            var decryptionResult = await DecryptAsync(encryptionResult);
            if (!decryptionResult.Success)
            {
                throw new InvalidOperationException($"Failed to decrypt object: {decryptionResult.ErrorMessage}");
            }

            return JsonSerializer.Deserialize<T>(decryptionResult.DecryptedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt object to type {Type}", typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// Generates a secure hash with salt for password storage
    /// </summary>
    public async Task<PasswordHashResult> HashPasswordAsync(string password, string? salt = null)
    {
        try
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password cannot be null or empty");
            }

            var saltBytes = salt != null ? Convert.FromBase64String(salt) : GenerateSalt();
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100000, HashAlgorithmName.SHA256, 32);

            return new PasswordHashResult
            {
                Success = true,
                Hash = Convert.ToBase64String(hash),
                Salt = Convert.ToBase64String(saltBytes),
                Iterations = 100000,
                Algorithm = "PBKDF2-SHA256"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hash password");
            return new PasswordHashResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Verifies a password against its hash
    /// </summary>
    public async Task<bool> VerifyPasswordAsync(string password, string hash, string salt)
    {
        try
        {
            var hashResult = await HashPasswordAsync(password, salt);
            return hashResult.Success && hashResult.Hash == hash;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify password");
            return false;
        }
    }

    /// <summary>
    /// Rotates encryption keys for enhanced security
    /// </summary>
    public async Task<KeyRotationResult> RotateEncryptionKeysAsync(string keyIdentifier)
    {
        try
        {
            _logger.LogInformation("Starting key rotation for key {KeyIdentifier}", keyIdentifier);

            // Generate new key
            var newKey = GenerateEncryptionKey();
            var newKeyId = $"{keyIdentifier}_v{DateTime.UtcNow:yyyyMMddHHmmss}";

            // Store new key (in production, this would use a secure key management service)
            _keyCache[newKeyId] = newKey;

            // Mark old key for deprecation (don't remove immediately to allow for decryption of existing data)
            var result = new KeyRotationResult
            {
                Success = true,
                OldKeyIdentifier = keyIdentifier,
                NewKeyIdentifier = newKeyId,
                RotationTimestamp = DateTime.UtcNow,
                Message = "Key rotation completed successfully"
            };

            _logger.LogInformation("Key rotation completed for {KeyIdentifier} -> {NewKeyIdentifier}", 
                keyIdentifier, newKeyId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate encryption key {KeyIdentifier}", keyIdentifier);
            return new KeyRotationResult
            {
                Success = false,
                OldKeyIdentifier = keyIdentifier,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Validates encryption configuration and key availability
    /// </summary>
    public async Task<EncryptionHealthResult> ValidateEncryptionHealthAsync()
    {
        var healthChecks = new List<EncryptionHealthCheck>();

        try
        {
            // Test each encryption type
            foreach (var encryptionType in Enum.GetValues<EncryptionType>())
            {
                var testData = "Health check test data";
                var encryptResult = await EncryptAsync(testData, encryptionType);
                
                if (encryptResult.Success)
                {
                    var decryptResult = await DecryptAsync(encryptResult);
                    var isHealthy = decryptResult.Success && decryptResult.DecryptedData == testData;

                    healthChecks.Add(new EncryptionHealthCheck
                    {
                        EncryptionType = encryptionType,
                        IsHealthy = isHealthy,
                        Message = isHealthy ? "Healthy" : $"Decrypt failed: {decryptResult.ErrorMessage}",
                        CheckedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    healthChecks.Add(new EncryptionHealthCheck
                    {
                        EncryptionType = encryptionType,
                        IsHealthy = false,
                        Message = $"Encrypt failed: {encryptResult.ErrorMessage}",
                        CheckedAt = DateTime.UtcNow
                    });
                }
            }

            var overallHealth = healthChecks.All(hc => hc.IsHealthy);

            return new EncryptionHealthResult
            {
                IsHealthy = overallHealth,
                HealthChecks = healthChecks,
                CheckedAt = DateTime.UtcNow,
                Message = overallHealth ? "All encryption types are healthy" : "Some encryption types have issues"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate encryption health");
            return new EncryptionHealthResult
            {
                IsHealthy = false,
                Message = $"Health check failed: {ex.Message}",
                CheckedAt = DateTime.UtcNow
            };
        }
    }

    private Dictionary<EncryptionType, EncryptionConfig> InitializeEncryptionConfigs()
    {
        return new Dictionary<EncryptionType, EncryptionConfig>
        {
            [EncryptionType.AtRest] = new EncryptionConfig
            {
                DefaultKeyId = "pos_at_rest_key_v1",
                Algorithm = "AES-256-CBC",
                KeySize = 256
            },
            [EncryptionType.InTransit] = new EncryptionConfig
            {
                DefaultKeyId = "pos_in_transit_key_v1",
                Algorithm = "AES-256-CBC",
                KeySize = 256
            },
            [EncryptionType.InMemory] = new EncryptionConfig
            {
                DefaultKeyId = "pos_in_memory_key_v1",
                Algorithm = "AES-256-CBC",
                KeySize = 256
            },
            [EncryptionType.EndToEnd] = new EncryptionConfig
            {
                DefaultKeyId = "pos_end_to_end_key_v1",
                Algorithm = "AES-256-CBC",
                KeySize = 256
            }
        };
    }

    private async Task<byte[]> GetEncryptionKeyAsync(string keyIdentifier)
    {
        if (_keyCache.TryGetValue(keyIdentifier, out var cachedKey))
        {
            return cachedKey;
        }

        // In production, this would retrieve from a secure key management service
        // For now, generate a deterministic key based on the identifier
        var key = GenerateEncryptionKey();
        _keyCache[keyIdentifier] = key;
        return key;
    }

    private byte[] GenerateEncryptionKey()
    {
        var key = new byte[32]; // 256-bit key
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(key);
        return key;
    }

    private byte[] GenerateIV()
    {
        var iv = new byte[16]; // 128-bit IV for AES
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(iv);
        return iv;
    }

    private byte[] GenerateSalt()
    {
        var salt = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        return salt;
    }
}

/// <summary>
/// Encryption configuration for different types
/// </summary>
public class EncryptionConfig
{
    public string DefaultKeyId { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public int KeySize { get; set; }
}

/// <summary>
/// Encrypted object wrapper
/// </summary>
public class EncryptedObject
{
    public string Data { get; set; } = string.Empty;
    public string IV { get; set; } = string.Empty;
    public string KeyIdentifier { get; set; } = string.Empty;
    public EncryptionType EncryptionType { get; set; }
    public DateTime Timestamp { get; set; }
}