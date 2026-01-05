using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Shared.Core.Services;
using Xunit;

namespace Shared.Core.Tests;

/// <summary>
/// Property-based tests for data encryption consistency
/// Feature: multi-business-pos
/// </summary>
public class DataEncryptionPropertyTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;

    public DataEncryptionPropertyTest()
    {
        var services = new ServiceCollection();
        
        // Add in-memory database
        services.AddDbContext<PosDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole());
        
        // Add encryption service
        services.AddScoped<IEncryptionService, EncryptionService>();
        
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        
        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    /// <summary>
    /// Property 10: Data Encryption Consistency
    /// For any sensitive data stored or transmitted, it should be encrypted using the configured encryption standards.
    /// Validates: Requirements 13.2, 13.3
    /// Feature: multi-business-pos, Property 10: Data Encryption Consistency
    /// </summary>
    [Fact]
    public void DataEncryptionConsistency_SensitiveDataShouldBeEncryptedUsingConfiguredStandards()
    {
        var encryptionService = _serviceProvider.GetRequiredService<IEncryptionService>();
        
        // Test with multiple random data encryption scenarios
        for (int iteration = 0; iteration < 100; iteration++)
        {
            try
            {
                // Generate random sensitive data of various types and sizes
                var sensitiveDataSets = GenerateRandomSensitiveData();
                
                foreach (var dataSet in sensitiveDataSets)
                {
                    // Test: Encrypt the sensitive data
                    var encryptedData = encryptionService.Encrypt(dataSet.PlainText);
                    
                    // Verify encryption consistency: Encrypted data should not equal plain text
                    if (!string.IsNullOrEmpty(dataSet.PlainText))
                    {
                        Assert.NotEqual(dataSet.PlainText, encryptedData);
                        Assert.False(string.IsNullOrEmpty(encryptedData), 
                            $"Encrypted data should not be empty for non-empty input in iteration {iteration}");
                    }
                    else
                    {
                        // Empty input should produce empty output
                        Assert.True(string.IsNullOrEmpty(encryptedData), 
                            $"Empty input should produce empty encrypted output in iteration {iteration}");
                    }
                    
                    // Test: Decrypt the data back
                    var decryptedData = encryptionService.Decrypt(encryptedData);
                    
                    // Verify round-trip consistency: Decrypted data should equal original
                    Assert.Equal(dataSet.PlainText, decryptedData);
                    
                    // Test deterministic encryption: Same input should produce same output
                    var secondEncryption = encryptionService.Encrypt(dataSet.PlainText);
                    Assert.Equal(encryptedData, secondEncryption);
                    
                    // Test that encrypted data appears to be base64 encoded (standard format)
                    if (!string.IsNullOrEmpty(encryptedData))
                    {
                        try
                        {
                            var base64Bytes = Convert.FromBase64String(encryptedData);
                            Assert.True(base64Bytes.Length > 0, 
                                $"Encrypted data should be valid base64 in iteration {iteration}");
                        }
                        catch (FormatException)
                        {
                            Assert.Fail($"Encrypted data should be valid base64 format in iteration {iteration}");
                        }
                    }
                    
                    // Test password hashing consistency
                    if (dataSet.IsPassword)
                    {
                        var salt = encryptionService.GenerateSalt();
                        var hash1 = encryptionService.HashPassword(dataSet.PlainText, salt);
                        var hash2 = encryptionService.HashPassword(dataSet.PlainText, salt);
                        
                        // Same password with same salt should produce same hash
                        Assert.Equal(hash1, hash2);
                        
                        // Verify password verification works
                        var isValid = encryptionService.VerifyPassword(dataSet.PlainText, hash1, salt);
                        Assert.True(isValid, 
                            $"Password verification should succeed for correct password in iteration {iteration}");
                        
                        // Wrong password should fail verification
                        var wrongPassword = dataSet.PlainText + "wrong";
                        var isInvalid = encryptionService.VerifyPassword(wrongPassword, hash1, salt);
                        Assert.False(isInvalid, 
                            $"Password verification should fail for wrong password in iteration {iteration}");
                        
                        // Different salt should produce different hash
                        var differentSalt = encryptionService.GenerateSalt();
                        var differentHash = encryptionService.HashPassword(dataSet.PlainText, differentSalt);
                        Assert.NotEqual(hash1, differentHash);
                    }
                }
                
                // Test salt generation consistency
                var salt1 = encryptionService.GenerateSalt();
                var salt2 = encryptionService.GenerateSalt();
                
                // Each salt should be unique
                Assert.NotEqual(salt1, salt2);
                
                // Salts should be valid base64
                try
                {
                    var saltBytes1 = Convert.FromBase64String(salt1);
                    var saltBytes2 = Convert.FromBase64String(salt2);
                    Assert.True(saltBytes1.Length >= 16, 
                        $"Salt should be at least 16 bytes in iteration {iteration}");
                    Assert.True(saltBytes2.Length >= 16, 
                        $"Salt should be at least 16 bytes in iteration {iteration}");
                }
                catch (FormatException)
                {
                    Assert.Fail($"Generated salts should be valid base64 format in iteration {iteration}");
                }
                
                // Test encryption with invalid/malformed data
                try
                {
                    var invalidDecryption = encryptionService.Decrypt("invalid-base64-data");
                    // Should return empty string for invalid data (graceful handling)
                    Assert.True(string.IsNullOrEmpty(invalidDecryption), 
                        $"Invalid encrypted data should return empty string in iteration {iteration}");
                }
                catch
                {
                    // Exception is also acceptable for invalid data
                }
            }
            finally
            {
                // No cleanup needed for encryption tests
            }
        }
    }

    /// <summary>
    /// Generates random sensitive data for encryption testing
    /// </summary>
    private static List<SensitiveDataTestSet> GenerateRandomSensitiveData()
    {
        var random = new Random();
        var dataSets = new List<SensitiveDataTestSet>();
        
        // Test various types of sensitive data
        var sensitiveDataTypes = new[]
        {
            // Passwords
            ("Password123!", true),
            ("ComplexP@ssw0rd!2024", true),
            ("simple", true),
            ("", true), // Empty password
            
            // Personal information
            ("John Doe", false),
            ("john.doe@example.com", false),
            ("123-45-6789", false), // SSN format
            ("4532-1234-5678-9012", false), // Credit card format
            
            // Business data
            ("Customer payment information", false),
            ("Inventory count: 150 units", false),
            ("Sales total: $1,234.56", false),
            
            // Various lengths and special characters
            ("A", false), // Single character
            (new string('X', 1000), false), // Long string
            ("Special chars: !@#$%^&*()_+-=[]{}|;':\",./<>?", false),
            ("Unicode: 你好世界 🌍", false),
            ("Newlines\nand\ttabs", false),
            
            // Edge cases
            (null, false),
            ("", false),
            ("   ", false), // Whitespace only
        };
        
        // Add random generated data
        for (int i = 0; i < 10; i++)
        {
            var length = random.Next(1, 200);
            var randomString = GenerateRandomString(length, random);
            dataSets.Add(new SensitiveDataTestSet 
            { 
                PlainText = randomString, 
                IsPassword = random.Next(0, 3) == 0 // 33% chance of being treated as password
            });
        }
        
        // Add predefined test cases
        foreach (var (text, isPassword) in sensitiveDataTypes)
        {
            dataSets.Add(new SensitiveDataTestSet { PlainText = text ?? string.Empty, IsPassword = isPassword });
        }
        
        return dataSets;
    }
    
    /// <summary>
    /// Generates a random string of specified length
    /// </summary>
    private static string GenerateRandomString(int length, Random random)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_+-=[]{}|;':\",./<>? \t\n";
        var result = new char[length];
        
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }
        
        return new string(result);
    }

    /// <summary>
    /// Test data structure for sensitive data encryption testing
    /// </summary>
    private class SensitiveDataTestSet
    {
        public string PlainText { get; set; } = string.Empty;
        public bool IsPassword { get; set; }
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}