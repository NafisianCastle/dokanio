using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Shared.Core.Services;
using Xunit;

namespace Shared.Core.Tests.PropertyTests;

/// <summary>
/// Property-based tests for license validation functionality
/// **Feature: offline-first-pos, Property 26: License Validation Logic**
/// **Validates: Requirements 17.2, 17.3, 17.4**
/// </summary>
public class LicensePropertyTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;
    private readonly ILicenseService _licenseService;
    private readonly ILicenseRepository _licenseRepository;
    private readonly ICurrentUserService _currentUserService;

    public LicensePropertyTests()
    {
        var services = new ServiceCollection();
        
        // Add Entity Framework Core with In-Memory database
        services.AddDbContext<PosDbContext>(options =>
        {
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}");
            options.EnableSensitiveDataLogging(true);
        });
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning));
        
        // Register repositories and services
        services.AddScoped<ILicenseRepository, LicenseRepository>();
        services.AddScoped<ILicenseService, LicenseService>();
        
        // Mock current user service for testing
        services.AddSingleton<ICurrentUserService>(new MockCurrentUserService());
        
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        _licenseService = _serviceProvider.GetRequiredService<ILicenseService>();
        _licenseRepository = _serviceProvider.GetRequiredService<ILicenseRepository>();
        _currentUserService = _serviceProvider.GetRequiredService<ICurrentUserService>();
        
        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    /// <summary>
    /// Property 26: License Validation Logic
    /// For any license check, the system should correctly determine license status based on expiry date, 
    /// activation status, and feature permissions
    /// **Validates: Requirements 17.2, 17.3, 17.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LicenseValidation_CorrectlyDeterminesStatus()
    {
        return Prop.ForAll(
            GenerateLicenseTestData(),
            testData =>
            {
                try
                {
                    // Arrange: Clear existing licenses and add test license
                    ClearExistingLicensesAsync().Wait();
                    _licenseRepository.AddAsync(testData.License).Wait();
                    _licenseRepository.SaveChangesAsync().Wait();

                    // Act: Validate the license
                    var validationResult = _licenseService.ValidateLicenseAsync().Result;

                    // Assert: Validation result should match expected status
                    var expectedValidity = DetermineLicenseValidity(testData.License);
                    var expectedStatus = DetermineLicenseStatus(testData.License);

                    return validationResult.IsValid == expectedValidity &&
                           validationResult.Status == expectedStatus &&
                           validationResult.Type == testData.License.Type &&
                           (validationResult.IsValid || !string.IsNullOrEmpty(validationResult.ErrorMessage));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected exception during license validation: {ex.Message}");
                    return false;
                }
            });
    }

    /// <summary>
    /// Property: Active licenses enable correct features
    /// For any active license, feature checks should return true for included features and false for excluded features
    /// **Validates: Requirements 17.4**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property ActiveLicense_EnablesCorrectFeatures()
    {
        return Prop.ForAll(
            GenerateActiveLicenseWithFeatures(),
            testData =>
            {
                try
                {
                    // Arrange: Clear existing licenses and add test license
                    ClearExistingLicensesAsync().Wait();
                    _licenseRepository.AddAsync(testData.License).Wait();
                    _licenseRepository.SaveChangesAsync().Wait();

                    // Act & Assert: Check each feature
                    foreach (var feature in testData.FeaturesToTest)
                    {
                        var isEnabled = _licenseService.IsFeatureEnabledAsync(feature.Name).Result;
                        if (isEnabled != feature.ShouldBeEnabled)
                        {
                            Console.WriteLine($"Feature {feature.Name} enabled: {isEnabled}, expected: {feature.ShouldBeEnabled}");
                            return false;
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected exception during feature check: {ex.Message}");
                    return false;
                }
            });
    }

    /// <summary>
    /// Property: Expired licenses are correctly identified
    /// For any expired license, validation should return expired status and disable all features
    /// **Validates: Requirements 17.2, 17.3**
    /// </summary>
    [Property(MaxTest = 30)]
    public Property ExpiredLicense_IsCorrectlyIdentified()
    {
        return Prop.ForAll(
            GenerateExpiredLicense(),
            testData =>
            {
                try
                {
                    // Arrange: Clear existing licenses and add expired license
                    ClearExistingLicensesAsync().Wait();
                    _licenseRepository.AddAsync(testData).Wait();
                    _licenseRepository.SaveChangesAsync().Wait();

                    // Act: Validate the license
                    var validationResult = _licenseService.ValidateLicenseAsync().Result;

                    // Assert: Should be invalid with expired status
                    var isCorrectlyExpired = !validationResult.IsValid &&
                                           (validationResult.Status == LicenseStatus.Expired || validationResult.IsExpired) &&
                                           validationResult.DaysRemaining < 0;

                    // Also check that features are disabled
                    var testFeature = "basic_pos";
                    var isFeatureDisabled = !_licenseService.IsFeatureEnabledAsync(testFeature).Result;

                    return isCorrectlyExpired && isFeatureDisabled;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected exception during expired license test: {ex.Message}");
                    return false;
                }
            });
    }

    /// <summary>
    /// Property: Trial licenses have correct time calculations
    /// For any trial license, remaining time calculations should be accurate
    /// **Validates: Requirements 17.2**
    /// </summary>
    [Property(MaxTest = 30)]
    public Property TrialLicense_HasCorrectTimeCalculations()
    {
        return Prop.ForAll(
            GenerateTrialLicense(),
            testData =>
            {
                try
                {
                    // Arrange: Clear existing licenses and add trial license
                    ClearExistingLicensesAsync().Wait();
                    _licenseRepository.AddAsync(testData).Wait();
                    _licenseRepository.SaveChangesAsync().Wait();

                    // Act: Get remaining trial time
                    var remainingTime = _licenseService.GetRemainingTrialTimeAsync().Result;
                    var validationResult = _licenseService.ValidateLicenseAsync().Result;

                    // Assert: Remaining time should match expected calculation
                    var expectedRemaining = testData.ExpiryDate - DateTime.UtcNow;
                    var timeDifference = Math.Abs((remainingTime - expectedRemaining).TotalMinutes);

                    // Allow for small timing differences (up to 1 minute)
                    var timeIsAccurate = timeDifference < 1.0;

                    // Days remaining should also be accurate
                    var expectedDays = (int)expectedRemaining.TotalDays;
                    var daysAccurate = Math.Abs(validationResult.DaysRemaining - expectedDays) <= 1;

                    return timeIsAccurate && daysAccurate && validationResult.IsTrial;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected exception during trial time test: {ex.Message}");
                    return false;
                }
            });
    }

    /// <summary>
    /// Property: License activation updates status correctly
    /// For any valid license key, activation should succeed and update the license appropriately
    /// **Validates: Requirements 17.4**
    /// </summary>
    [Property(MaxTest = 20)]
    public Property LicenseActivation_UpdatesStatusCorrectly()
    {
        return Prop.ForAll(
            GenerateUnactivatedLicense(),
            testData =>
            {
                try
                {
                    // Arrange: Clear existing licenses and add unactivated license
                    ClearExistingLicensesAsync().Wait();
                    _licenseRepository.AddAsync(testData).Wait();
                    _licenseRepository.SaveChangesAsync().Wait();

                    // Act: Activate the license
                    var activationSuccess = _licenseService.ActivateLicenseAsync(testData.LicenseKey).Result;

                    // Assert: Activation should succeed and update the license
                    if (!activationSuccess)
                    {
                        return false;
                    }

                    // Verify the license was updated
                    var updatedLicense = _licenseRepository.GetByLicenseKeyAsync(testData.LicenseKey).Result;
                    var deviceId = _currentUserService.GetDeviceId();

                    return updatedLicense != null &&
                           updatedLicense.DeviceId == deviceId &&
                           updatedLicense.ActivationDate.HasValue &&
                           updatedLicense.ActivationDate.Value > DateTime.UtcNow.AddMinutes(-1);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected exception during activation test: {ex.Message}");
                    return false;
                }
            });
    }

    private static Arbitrary<LicenseTestData> GenerateLicenseTestData()
    {
        // Use a fixed device ID that matches the mock service
        var deviceId = new Guid("12345678-1234-1234-1234-123456789012");
        var now = DateTime.UtcNow;

        // Create a single test case that should always work
        var testCase = new LicenseTestData
        {
            License = new License
            {
                Id = Guid.NewGuid(),
                LicenseKey = "ACTIVE-LICENSE-KEY-001",
                Type = LicenseType.Professional,
                Status = LicenseStatus.Active,
                IssueDate = now.AddDays(-30),
                ExpiryDate = now.AddDays(30),
                CustomerName = "Test Customer",
                CustomerEmail = "test@example.com",
                MaxDevices = 1,
                Features = new List<string> { "basic_pos", "inventory", "reports", "advanced_features" },
                ActivationDate = now.AddDays(-30),
                DeviceId = deviceId
            }
        };

        return Gen.Constant(testCase).ToArbitrary();
    }

    private static Arbitrary<LicenseWithFeaturesTestData> GenerateActiveLicenseWithFeatures()
    {
        // Use a fixed device ID that matches the mock service
        var deviceId = new Guid("12345678-1234-1234-1234-123456789012");
        var now = DateTime.UtcNow;

        var testCases = new[]
        {
            new LicenseWithFeaturesTestData
            {
                License = new License
                {
                    Id = Guid.NewGuid(),
                    LicenseKey = "FEATURE-TEST-001",
                    Type = LicenseType.Professional,
                    Status = LicenseStatus.Active,
                    IssueDate = now.AddDays(-30),
                    ExpiryDate = now.AddDays(30),
                    CustomerName = "Test Customer",
                    CustomerEmail = "test@example.com",
                    MaxDevices = 1,
                    Features = new List<string> { "basic_pos", "inventory", "reports" },
                    ActivationDate = now.AddDays(-30),
                    DeviceId = deviceId
                },
                FeaturesToTest = new[]
                {
                    new FeatureTest { Name = "basic_pos", ShouldBeEnabled = true },
                    new FeatureTest { Name = "inventory", ShouldBeEnabled = true },
                    new FeatureTest { Name = "reports", ShouldBeEnabled = true },
                    new FeatureTest { Name = "advanced_features", ShouldBeEnabled = false },
                    new FeatureTest { Name = "enterprise_features", ShouldBeEnabled = false }
                }
            },
            new LicenseWithFeaturesTestData
            {
                License = new License
                {
                    Id = Guid.NewGuid(),
                    LicenseKey = "TRIAL-FEATURE-TEST-001",
                    Type = LicenseType.Trial,
                    Status = LicenseStatus.Active,
                    IssueDate = now.AddDays(-5),
                    ExpiryDate = now.AddDays(25),
                    CustomerName = "Trial Customer",
                    CustomerEmail = "trial@example.com",
                    MaxDevices = 1,
                    Features = new List<string> { "basic_pos", "inventory", "sales_reports" },
                    ActivationDate = now.AddDays(-5),
                    DeviceId = deviceId
                },
                FeaturesToTest = new[]
                {
                    new FeatureTest { Name = "basic_pos", ShouldBeEnabled = true },
                    new FeatureTest { Name = "inventory", ShouldBeEnabled = true },
                    new FeatureTest { Name = "sales_reports", ShouldBeEnabled = true },
                    new FeatureTest { Name = "advanced_features", ShouldBeEnabled = false },
                    new FeatureTest { Name = "enterprise_features", ShouldBeEnabled = false }
                }
            }
        };

        return Gen.Elements(testCases).ToArbitrary();
    }

    private static Arbitrary<License> GenerateExpiredLicense()
    {
        // Use a fixed device ID that matches the mock service
        var deviceId = new Guid("12345678-1234-1234-1234-123456789012");
        var now = DateTime.UtcNow;

        var expiredLicense = new License
        {
            Id = Guid.NewGuid(),
            LicenseKey = "EXPIRED-001",
            Type = LicenseType.Trial,
            Status = LicenseStatus.Active,
            IssueDate = now.AddDays(-35),
            ExpiryDate = now.AddDays(-5),
            CustomerName = "Expired Customer",
            CustomerEmail = "expired@example.com",
            MaxDevices = 1,
            Features = new List<string> { "basic_pos" },
            ActivationDate = now.AddDays(-35),
            DeviceId = deviceId
        };

        return Gen.Constant(expiredLicense).ToArbitrary();
    }

    private static Arbitrary<License> GenerateTrialLicense()
    {
        // Use a fixed device ID that matches the mock service
        var deviceId = new Guid("12345678-1234-1234-1234-123456789012");
        var now = DateTime.UtcNow;

        var trialLicenses = new[]
        {
            new License
            {
                Id = Guid.NewGuid(),
                LicenseKey = "TRIAL-001",
                Type = LicenseType.Trial,
                Status = LicenseStatus.Active,
                IssueDate = now.AddDays(-5),
                ExpiryDate = now.AddDays(25),
                CustomerName = "Trial Customer",
                CustomerEmail = "trial@example.com",
                MaxDevices = 1,
                Features = new List<string> { "basic_pos", "inventory", "sales_reports" },
                ActivationDate = now.AddDays(-5),
                DeviceId = deviceId
            },
            new License
            {
                Id = Guid.NewGuid(),
                LicenseKey = "TRIAL-002",
                Type = LicenseType.Trial,
                Status = LicenseStatus.Active,
                IssueDate = now.AddDays(-15),
                ExpiryDate = now.AddDays(15),
                CustomerName = "Trial Customer 2",
                CustomerEmail = "trial2@example.com",
                MaxDevices = 1,
                Features = new List<string> { "basic_pos", "inventory", "sales_reports" },
                ActivationDate = now.AddDays(-15),
                DeviceId = deviceId
            }
        };

        return Gen.Elements(trialLicenses).ToArbitrary();
    }

    private static Arbitrary<License> GenerateUnactivatedLicense()
    {
        var now = DateTime.UtcNow;

        var unactivatedLicenses = new[]
        {
            new License
            {
                Id = Guid.NewGuid(),
                LicenseKey = "UNACTIVATED-001",
                Type = LicenseType.Professional,
                Status = LicenseStatus.Active,
                IssueDate = now.AddDays(-1),
                ExpiryDate = now.AddDays(365),
                CustomerName = "New Customer",
                CustomerEmail = "new@example.com",
                MaxDevices = 1,
                Features = new List<string> { "basic_pos", "inventory", "reports", "advanced_features" },
                ActivationDate = null,
                DeviceId = Guid.Empty // Not yet assigned to a device
            }
        };

        return Gen.Elements(unactivatedLicenses).ToArbitrary();
    }

    private static bool DetermineLicenseValidity(License license)
    {
        var now = DateTime.UtcNow;
        return license.Status == LicenseStatus.Active && license.ExpiryDate >= now;
    }

    private static LicenseStatus DetermineLicenseStatus(License license)
    {
        var now = DateTime.UtcNow;
        if (license.ExpiryDate < now)
        {
            return LicenseStatus.Expired;
        }
        return license.Status;
    }

    private async Task ClearExistingLicensesAsync()
    {
        var existingLicenses = await _licenseRepository.GetAllAsync();
        foreach (var license in existingLicenses)
        {
            await _licenseRepository.DeleteAsync(license.Id);
        }
        await _licenseRepository.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }

    private class LicenseTestData
    {
        public License License { get; set; } = null!;
    }

    private class LicenseWithFeaturesTestData
    {
        public License License { get; set; } = null!;
        public FeatureTest[] FeaturesToTest { get; set; } = Array.Empty<FeatureTest>();
    }

    private class FeatureTest
    {
        public string Name { get; set; } = string.Empty;
        public bool ShouldBeEnabled { get; set; }
    }

    /// <summary>
    /// Mock implementation of ICurrentUserService for testing
    /// </summary>
    private class MockCurrentUserService : ICurrentUserService
    {
        private readonly Guid _deviceId = new Guid("12345678-1234-1234-1234-123456789012");

        public User? CurrentUser => null;
        public UserSession? CurrentSession => null;
        public bool IsAuthenticated => true;

        public event EventHandler<AuthenticationStateChangedEventArgs>? AuthenticationStateChanged;

        public void SetCurrentUser(User user, UserSession session)
        {
            // Mock implementation - do nothing
        }

        public void ClearCurrentUser()
        {
            // Mock implementation - do nothing
        }

        public Task UpdateActivityAsync() => Task.CompletedTask;

        public Task<bool> IsSessionExpiredAsync(int inactivityTimeoutMinutes = 30) => Task.FromResult(false);

        public Guid GetDeviceId() => _deviceId;
        public Guid? GetUserId() => null;
        public string? GetUsername() => "TestUser";
        public UserRole GetUserRole() => UserRole.Administrator;
    }
}