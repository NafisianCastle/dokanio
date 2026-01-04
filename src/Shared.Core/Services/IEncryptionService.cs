namespace Shared.Core.Services;

/// <summary>
/// Service for encrypting and decrypting sensitive data in local storage
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts sensitive data for storage
    /// </summary>
    /// <param name="plainText">The data to encrypt</param>
    /// <returns>Encrypted data as base64 string</returns>
    string Encrypt(string plainText);
    
    /// <summary>
    /// Decrypts data from storage
    /// </summary>
    /// <param name="encryptedText">The encrypted data as base64 string</param>
    /// <returns>Decrypted plain text</returns>
    string Decrypt(string encryptedText);
    
    /// <summary>
    /// Generates a secure hash for passwords
    /// </summary>
    /// <param name="password">The password to hash</param>
    /// <param name="salt">The salt to use for hashing</param>
    /// <returns>Password hash</returns>
    string HashPassword(string password, string salt);
    
    /// <summary>
    /// Generates a cryptographically secure salt
    /// </summary>
    /// <returns>Base64 encoded salt</returns>
    string GenerateSalt();
    
    /// <summary>
    /// Verifies a password against its hash
    /// </summary>
    /// <param name="password">The password to verify</param>
    /// <param name="hash">The stored hash</param>
    /// <param name="salt">The salt used for hashing</param>
    /// <returns>True if password matches</returns>
    bool VerifyPassword(string password, string hash, string salt);
}