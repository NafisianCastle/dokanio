using Shared.Core.DTOs;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Interface for enhanced encryption service supporting multiple encryption types
/// </summary>
public interface IEnhancedEncryptionService
{
    /// <summary>
    /// Encrypts data with specified encryption type
    /// </summary>
    /// <param name="plainText">Data to encrypt</param>
    /// <param name="encryptionType">Type of encryption to use</param>
    /// <param name="keyIdentifier">Optional key identifier</param>
    /// <returns>Encryption result</returns>
    Task<EncryptionResult> EncryptAsync(string plainText, EncryptionType encryptionType, string? keyIdentifier = null);

    /// <summary>
    /// Decrypts data using the provided encryption result
    /// </summary>
    /// <param name="encryptionResult">Result from previous encryption</param>
    /// <returns>Decryption result</returns>
    Task<DecryptionResult> DecryptAsync(EncryptionResult encryptionResult);

    /// <summary>
    /// Encrypts sensitive object data for storage
    /// </summary>
    /// <typeparam name="T">Type of object to encrypt</typeparam>
    /// <param name="obj">Object to encrypt</param>
    /// <param name="encryptionType">Type of encryption to use</param>
    /// <returns>Encrypted object as JSON string</returns>
    Task<string> EncryptObjectAsync<T>(T obj, EncryptionType encryptionType = EncryptionType.AtRest);

    /// <summary>
    /// Decrypts object data from storage
    /// </summary>
    /// <typeparam name="T">Type of object to decrypt to</typeparam>
    /// <param name="encryptedObjectJson">Encrypted object JSON</param>
    /// <returns>Decrypted object</returns>
    Task<T?> DecryptObjectAsync<T>(string encryptedObjectJson);

    /// <summary>
    /// Generates a secure hash with salt for password storage
    /// </summary>
    /// <param name="password">Password to hash</param>
    /// <param name="salt">Optional salt (will generate if not provided)</param>
    /// <returns>Password hash result</returns>
    Task<PasswordHashResult> HashPasswordAsync(string password, string? salt = null);

    /// <summary>
    /// Verifies a password against its hash
    /// </summary>
    /// <param name="password">Password to verify</param>
    /// <param name="hash">Stored hash</param>
    /// <param name="salt">Salt used for hashing</param>
    /// <returns>True if password matches</returns>
    Task<bool> VerifyPasswordAsync(string password, string hash, string salt);

    /// <summary>
    /// Rotates encryption keys for enhanced security
    /// </summary>
    /// <param name="keyIdentifier">Key identifier to rotate</param>
    /// <returns>Key rotation result</returns>
    Task<KeyRotationResult> RotateEncryptionKeysAsync(string keyIdentifier);

    /// <summary>
    /// Validates encryption configuration and key availability
    /// </summary>
    /// <returns>Encryption health result</returns>
    Task<EncryptionHealthResult> ValidateEncryptionHealthAsync();
}