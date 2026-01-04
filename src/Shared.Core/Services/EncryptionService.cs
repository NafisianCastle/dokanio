using System.Security.Cryptography;
using System.Text;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of encryption service for sensitive data protection
/// </summary>
public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public EncryptionService(string? encryptionKey = null)
    {
        // In production, this should come from secure configuration
        var keyString = encryptionKey ?? "POS-Encryption-Key-32-Bytes-Long!";
        _key = Encoding.UTF8.GetBytes(keyString.PadRight(32).Substring(0, 32));
        _iv = Encoding.UTF8.GetBytes("POS-IV-16-Bytes!".PadRight(16).Substring(0, 16));
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        using var encryptor = aes.CreateEncryptor();
        using var msEncrypt = new MemoryStream();
        using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
        using var swEncrypt = new StreamWriter(csEncrypt);
        
        swEncrypt.Write(plainText);
        swEncrypt.Close();
        
        return Convert.ToBase64String(msEncrypt.ToArray());
    }

    public string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return string.Empty;

        try
        {
            var cipherBytes = Convert.FromBase64String(encryptedText);

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;

            using var decryptor = aes.CreateDecryptor();
            using var msDecrypt = new MemoryStream(cipherBytes);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);
            
            return srDecrypt.ReadToEnd();
        }
        catch
        {
            // Return empty string if decryption fails
            return string.Empty;
        }
    }

    public string HashPassword(string password, string salt)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(salt))
            throw new ArgumentException("Password and salt cannot be null or empty");

        var hash = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(salt), 10000, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(hash);
    }

    public string GenerateSalt()
    {
        var salt = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        return Convert.ToBase64String(salt);
    }

    public bool VerifyPassword(string password, string hash, string salt)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(salt))
            return false;

        try
        {
            var computedHash = HashPassword(password, salt);
            return computedHash == hash;
        }
        catch
        {
            return false;
        }
    }
}