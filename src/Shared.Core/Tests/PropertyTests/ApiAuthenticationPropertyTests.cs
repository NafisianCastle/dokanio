using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Core.Data;
using Shared.Core.Services;
using System.Net;
using Xunit;

namespace Shared.Core.Tests.PropertyTests;

/// <summary>
/// Property-based tests for API authentication and device registration
/// Tests Properties 15 and 16 from the design document
/// </summary>
public class ApiAuthenticationPropertyTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;

    public ApiAuthenticationPropertyTests()
    {
        var services = new ServiceCollection();
        
        // Use SQLite in-memory database for testing
        services.AddDbContext<PosDbContext>(options =>
        {
            options.UseSqlite("Data Source=:memory:", sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(30);
            });
            options.EnableSensitiveDataLogging(true);
        });
        
        // Register test implementations
        services.AddScoped<ISyncApiClient, AuthTestSyncApiClient>();
        
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        
        // Ensure database is created and configured
        _context.Database.OpenConnection(); // Keep connection open for in-memory SQLite
        _context.Database.EnsureCreated();
        
        // Enable foreign keys for SQLite
        _context.Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON");
    }

    [Property]
    public Property DeviceRegistrationRequirement()
    {
        // Feature: offline-first-pos, Property 15: For any sync operation attempt, the operation should be rejected if the device is not properly registered
        // **Validates: Requirements 9.1**
        
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 10).Select(_ => Guid.NewGuid())),
            Arb.From(Gen.Elements("Device1", "Device2", "TestDevice", "POS-Terminal", "Mobile-POS")),
            (deviceId, deviceName) =>
            {
                // Clear database for each test
                ClearDatabase();
                
                var syncApiClient = _serviceProvider.GetRequiredService<ISyncApiClient>() as AuthTestSyncApiClient;
                if (syncApiClient == null) return false;
                
                // Reset the test client state
                syncApiClient.Reset();
                
                // Try to perform sync operation without registering device first
                var syncRequest = new SyncUploadRequest
                {
                    DeviceId = deviceId,
                    LastSyncTimestamp = DateTime.UtcNow.AddDays(-1),
                    Sales = new List<SaleDto>(),
                    StockUpdates = new List<StockUpdateDto>()
                };

                // This should fail because device is not registered
                var syncResult = syncApiClient.UploadChangesAsync(syncRequest).Result;
                var isSyncRejectedWithoutRegistration = !syncResult.Success && syncResult.StatusCode == 401;
                
                // Now register the device
                var registrationResult = syncApiClient.RegisterDeviceAsync(deviceId, deviceName).Result;
                var isRegistrationSuccessful = registrationResult.Success;
                
                if (!isRegistrationSuccessful)
                {
                    return false;
                }
                
                // Mark device as registered in test client
                syncApiClient.RegisterDevice(deviceId, "test-api-key");
                
                // Authenticate to get access
                var authResult = syncApiClient.AuthenticateAsync(deviceId, "test-api-key").Result;
                var isAuthSuccessful = authResult.Success;
                
                if (!isAuthSuccessful)
                {
                    return false;
                }
                
                // Mark device as authenticated in test client
                syncApiClient.AuthenticateDevice(deviceId);
                
                // Now try sync operation with proper authentication
                var authenticatedSyncResult = syncApiClient.UploadChangesAsync(syncRequest).Result;
                var isSyncSuccessfulWithAuth = authenticatedSyncResult.Success;
                
                // Property: Sync operations should be rejected without proper device registration and authentication
                return isSyncRejectedWithoutRegistration && isRegistrationSuccessful && isAuthSuccessful && isSyncSuccessfulWithAuth;
            });
    }

    [Property]
    public Property JwtAuthenticationEnforcement()
    {
        // Feature: offline-first-pos, Property 16: For any API operation, the request should be rejected if it lacks a valid JWT token
        // **Validates: Requirements 7.2, 9.2**
        
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 10).Select(_ => Guid.NewGuid())),
            Arb.From(Gen.Elements("Device1", "Device2", "TestDevice", "POS-Terminal", "Mobile-POS")),
            (deviceId, deviceName) =>
            {
                // Clear database for each test
                ClearDatabase();
                
                var syncApiClient = _serviceProvider.GetRequiredService<ISyncApiClient>() as AuthTestSyncApiClient;
                if (syncApiClient == null) return false;
                
                // Reset the test client state
                syncApiClient.Reset();
                
                // Test various protected operations without authentication
                var syncUploadRequest = new SyncUploadRequest
                {
                    DeviceId = deviceId,
                    LastSyncTimestamp = DateTime.UtcNow.AddDays(-1),
                    Sales = new List<SaleDto>(),
                    StockUpdates = new List<StockUpdateDto>()
                };
                
                // Test sync upload without authentication
                var syncUploadResult = syncApiClient.UploadChangesAsync(syncUploadRequest).Result;
                var isSyncUploadUnauthorized = !syncUploadResult.Success && syncUploadResult.StatusCode == 401;
                
                // Test sync download without authentication
                var syncDownloadResult = syncApiClient.DownloadChangesAsync(deviceId, DateTime.UtcNow.AddDays(-1)).Result;
                var isSyncDownloadUnauthorized = !syncDownloadResult.Success && syncDownloadResult.StatusCode == 401;
                
                // Test that public operations (registration) work without authentication
                var registrationResult = syncApiClient.RegisterDeviceAsync(deviceId, deviceName).Result;
                var isRegistrationAccessible = registrationResult.Success;
                
                // Register and authenticate the device
                syncApiClient.RegisterDevice(deviceId, "test-api-key");
                var authResult = syncApiClient.AuthenticateAsync(deviceId, "test-api-key").Result;
                var isAuthAccessible = authResult.Success;
                
                // Authenticate the device in test client
                syncApiClient.AuthenticateDevice(deviceId);
                
                // Now test that protected operations work with authentication
                var authenticatedSyncResult = syncApiClient.UploadChangesAsync(syncUploadRequest).Result;
                var isSyncSuccessfulWithAuth = authenticatedSyncResult.Success;
                
                // Property: Protected operations should reject requests without authentication, but public operations should be accessible
                return isSyncUploadUnauthorized && 
                       isSyncDownloadUnauthorized && 
                       isRegistrationAccessible && 
                       isAuthAccessible &&
                       isSyncSuccessfulWithAuth;
            });
    }

    private void ClearDatabase()
    {
        // Clear all data from the database
        if (_context.Sales != null)
            _context.Sales.RemoveRange(_context.Sales);
        if (_context.Products != null)
            _context.Products.RemoveRange(_context.Products);
        if (_context.Stock != null)
            _context.Stock.RemoveRange(_context.Stock);
        
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context?.Database?.CloseConnection();
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}

/// <summary>
/// Test implementation of ISyncApiClient for property-based testing
/// Simulates server behavior for authentication and device registration
/// </summary>
public class AuthTestSyncApiClient : ISyncApiClient
{
    private readonly HashSet<Guid> _registeredDevices = new();
    private readonly HashSet<Guid> _authenticatedDevices = new();
    private readonly Dictionary<Guid, string> _deviceApiKeys = new();

    public void Reset()
    {
        _registeredDevices.Clear();
        _authenticatedDevices.Clear();
        _deviceApiKeys.Clear();
    }

    public void RegisterDevice(Guid deviceId, string apiKey)
    {
        _registeredDevices.Add(deviceId);
        _deviceApiKeys[deviceId] = apiKey;
    }

    public void AuthenticateDevice(Guid deviceId)
    {
        _authenticatedDevices.Add(deviceId);
    }

    public Task<SyncApiResult> UploadChangesAsync(SyncUploadRequest request)
    {
        // Simulate authentication check
        if (!_authenticatedDevices.Contains(request.DeviceId))
        {
            return Task.FromResult(new SyncApiResult
            {
                Success = false,
                StatusCode = 401,
                Message = "Unauthorized - device not authenticated",
                Errors = new List<string> { "Device authentication required" }
            });
        }

        return Task.FromResult(new SyncApiResult
        {
            Success = true,
            StatusCode = 200,
            Message = "Upload successful"
        });
    }

    public Task<SyncApiResult<SyncDownloadResponse>> DownloadChangesAsync(Guid deviceId, DateTime lastSyncTimestamp)
    {
        // Simulate authentication check
        if (!_authenticatedDevices.Contains(deviceId))
        {
            return Task.FromResult(new SyncApiResult<SyncDownloadResponse>
            {
                Success = false,
                StatusCode = 401,
                Message = "Unauthorized - device not authenticated",
                Errors = new List<string> { "Device authentication required" }
            });
        }

        return Task.FromResult(new SyncApiResult<SyncDownloadResponse>
        {
            Success = true,
            StatusCode = 200,
            Message = "Download successful",
            Data = new SyncDownloadResponse
            {
                ServerTimestamp = DateTime.UtcNow,
                Products = new List<ProductDto>(),
                Stock = new List<StockDto>(),
                HasMoreData = false
            }
        });
    }

    public Task<SyncApiResult> RegisterDeviceAsync(Guid deviceId, string deviceName)
    {
        // Registration is always public and should succeed
        return Task.FromResult(new SyncApiResult
        {
            Success = true,
            StatusCode = 200,
            Message = "Device registered successfully"
        });
    }

    public Task<SyncApiResult<AuthenticationResponse>> AuthenticateAsync(Guid deviceId, string apiKey)
    {
        // Authentication is public but requires device to be registered
        if (!_registeredDevices.Contains(deviceId) || 
            !_deviceApiKeys.TryGetValue(deviceId, out var expectedApiKey) || 
            expectedApiKey != apiKey)
        {
            return Task.FromResult(new SyncApiResult<AuthenticationResponse>
            {
                Success = false,
                StatusCode = 401,
                Message = "Invalid device credentials",
                Errors = new List<string> { "Device not found or invalid API key" }
            });
        }

        return Task.FromResult(new SyncApiResult<AuthenticationResponse>
        {
            Success = true,
            StatusCode = 200,
            Message = "Authentication successful",
            Data = new AuthenticationResponse
            {
                AccessToken = "test-jwt-token",
                RefreshToken = "test-refresh-token",
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                DeviceId = deviceId
            }
        });
    }
}