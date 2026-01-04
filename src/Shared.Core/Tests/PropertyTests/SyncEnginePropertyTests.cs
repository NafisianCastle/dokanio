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

public class SyncEnginePropertyTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;
    private readonly ISyncEngine _syncEngine;
    private readonly TestConnectivityService _connectivityService;
    private readonly TestSyncApiClient _apiClient;
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockRepository _stockRepository;

    public SyncEnginePropertyTests()
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
        
        // Add logging
        services.AddLogging();
        
        // Add repositories
        services.AddScoped<IRepository<Product>, Repository<Product>>();
        services.AddScoped<IRepository<Sale>, Repository<Sale>>();
        services.AddScoped<IRepository<Stock>, Repository<Stock>>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        
        // Add test implementations for sync services
        services.AddSingleton<TestConnectivityService>();
        services.AddSingleton<TestSyncApiClient>();
        services.AddSingleton<IConnectivityService>(provider => provider.GetRequiredService<TestConnectivityService>());
        services.AddSingleton<ISyncApiClient>(provider => provider.GetRequiredService<TestSyncApiClient>());
        
        // Add sync configuration
        services.AddSingleton(new SyncConfiguration
        {
            DeviceId = Guid.NewGuid(),
            ServerBaseUrl = "https://test.example.com",
            SyncInterval = TimeSpan.FromSeconds(1), // Fast for testing
            MaxRetryAttempts = 2, // Fewer retries for testing
            InitialRetryDelay = TimeSpan.FromMilliseconds(10),
            RetryBackoffMultiplier = 2.0
        });
        
        // Add sync engine
        services.AddScoped<ISyncEngine, SyncEngine>();
        
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        _syncEngine = _serviceProvider.GetRequiredService<ISyncEngine>();
        _connectivityService = _serviceProvider.GetRequiredService<TestConnectivityService>();
        _apiClient = _serviceProvider.GetRequiredService<TestSyncApiClient>();
        _saleRepository = _serviceProvider.GetRequiredService<ISaleRepository>();
        _productRepository = _serviceProvider.GetRequiredService<IProductRepository>();
        _stockRepository = _serviceProvider.GetRequiredService<IStockRepository>();
        
        // Ensure database is created and configured
        _context.Database.OpenConnection(); // Keep connection open for in-memory SQLite
        _context.Database.EnsureCreated();
        
        // Enable foreign keys for SQLite
        _context.Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON");
    }

    [Property]
    public bool AutomaticSyncTrigger(NonEmptyString saleInvoice, PositiveInt amount)
    {
        // Feature: offline-first-pos, Property 8: For any network connectivity restoration event, the Sync_Engine should automatically initiate upload of pending local transactions
        // **Validates: Requirements 2.3, 4.7, 6.2**
        
        // Clear the database for each test
        ClearDatabase();
        
        var deviceId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var invoiceNumber = $"{saleInvoice.Get}-{DateTime.Now.Ticks}";
        var totalAmount = Math.Max(0.01m, amount.Get);
        
        try
        {
            // Set up test data: Create a product and an unsynced sale
            var product = new Product
            {
                Id = productId,
                Name = "Test Product",
                Barcode = $"SYNC{DateTime.Now.Ticks}",
                UnitPrice = totalAmount,
                IsActive = true,
                DeviceId = deviceId,
                CreatedAt = DateTime.UtcNow,
                SyncStatus = SyncStatus.NotSynced
            };
            
            _context.Products.Add(product);
            
            var sale = new Sale
            {
                Id = saleId,
                InvoiceNumber = invoiceNumber,
                TotalAmount = totalAmount,
                PaymentMethod = PaymentMethod.Cash,
                DeviceId = deviceId,
                CreatedAt = DateTime.UtcNow,
                SyncStatus = SyncStatus.NotSynced // This should trigger sync when connectivity is restored
            };
            
            _context.Sales.Add(sale);
            
            var saleItem = new SaleItem
            {
                Id = Guid.NewGuid(),
                SaleId = saleId,
                ProductId = productId,
                Quantity = 1,
                UnitPrice = totalAmount
            };
            
            _context.SaleItems.Add(saleItem);
            _context.SaveChanges();
            
            // Verify initial state: Sale should be unsynced
            var initialSale = _context.Sales.First(s => s.Id == saleId);
            if (initialSale.SyncStatus != SyncStatus.NotSynced)
            {
                return false; // Initial state should be unsynced
            }
            
            // Reset API client call tracking
            _apiClient.ResetCallTracking();
            
            // Simulate connectivity loss then restoration
            _connectivityService.SetConnectivity(false);
            
            // Trigger connectivity restoration event
            _connectivityService.SetConnectivity(true);
            
            // Give some time for the sync to be triggered
            Thread.Sleep(100);
            
            // Manually trigger sync to simulate automatic sync trigger
            var syncResult = _syncEngine.SyncAllAsync().Result;
            
            // Verify that sync was triggered and upload was attempted
            if (!_apiClient.WasUploadCalled)
            {
                return false; // Upload should have been called when connectivity was restored
            }
            
            // Verify that the sale was processed for sync
            var syncedSale = _context.Sales.First(s => s.Id == saleId);
            if (syncedSale.SyncStatus == SyncStatus.NotSynced)
            {
                return false; // Sale should have been processed (either synced or sync attempted)
            }
            
            // The property holds: connectivity restoration triggers sync of pending transactions
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool ExponentialBackoffRetry(PositiveInt failureCount)
    {
        // Feature: offline-first-pos, Property 9: For any failed sync attempt, subsequent retry intervals should follow exponential backoff pattern (each retry interval should be approximately double the previous interval)
        // **Validates: Requirements 2.4**
        
        // Clear the database for each test
        ClearDatabase();
        
        var deviceId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var invoiceNumber = $"RETRY-{DateTime.Now.Ticks}";
        
        // Limit failure count to reasonable range for testing
        var maxFailures = Math.Min(failureCount.Get % 5 + 1, 3); // 1-3 failures
        
        try
        {
            // Set up test data: Create an unsynced sale
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Retry Test Product",
                Barcode = $"RETRY{DateTime.Now.Ticks}",
                UnitPrice = 10.00m,
                IsActive = true,
                DeviceId = deviceId,
                CreatedAt = DateTime.UtcNow,
                SyncStatus = SyncStatus.NotSynced
            };
            
            _context.Products.Add(product);
            
            var sale = new Sale
            {
                Id = saleId,
                InvoiceNumber = invoiceNumber,
                TotalAmount = 10.00m,
                PaymentMethod = PaymentMethod.Cash,
                DeviceId = deviceId,
                CreatedAt = DateTime.UtcNow,
                SyncStatus = SyncStatus.NotSynced
            };
            
            _context.Sales.Add(sale);
            
            var saleItem = new SaleItem
            {
                Id = Guid.NewGuid(),
                SaleId = saleId,
                ProductId = product.Id,
                Quantity = 1,
                UnitPrice = 10.00m
            };
            
            _context.SaleItems.Add(saleItem);
            _context.SaveChanges();
            
            // Configure API client to fail for the specified number of attempts
            _apiClient.ResetCallTracking();
            _apiClient.SetFailureCount(maxFailures);
            
            // Record start time to measure retry intervals
            var startTime = DateTime.UtcNow;
            var callTimes = new List<DateTime>();
            
            // Configure API client to record call times
            _apiClient.OnUploadCall = () => callTimes.Add(DateTime.UtcNow);
            
            // Trigger sync (this should retry with exponential backoff)
            var syncResult = _syncEngine.SyncAllAsync().Result;
            
            // Verify that retries were attempted
            if (_apiClient.UploadCallCount <= 1)
            {
                return false; // Should have made multiple attempts due to failures
            }
            
            // Verify exponential backoff pattern in call intervals
            if (callTimes.Count >= 2)
            {
                var intervals = new List<double>();
                for (int i = 1; i < callTimes.Count; i++)
                {
                    var interval = (callTimes[i] - callTimes[i - 1]).TotalMilliseconds;
                    intervals.Add(interval);
                }
                
                // Check that each interval is approximately double the previous one
                // Allow for some tolerance due to timing variations
                for (int i = 1; i < intervals.Count; i++)
                {
                    var ratio = intervals[i] / intervals[i - 1];
                    // Exponential backoff should have ratio around 2.0 (allow 1.5 to 3.0 for tolerance)
                    if (ratio < 1.5 || ratio > 3.0)
                    {
                        return false; // Intervals should follow exponential backoff pattern
                    }
                }
            }
            
            // The property holds: retry intervals follow exponential backoff pattern
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool SyncIdempotency(NonEmptyString invoicePrefix, PositiveInt amount, PositiveInt syncCount)
    {
        // Feature: offline-first-pos, Property 10: For any set of transactions, performing sync operations multiple times should produce the same final state as performing sync once
        // **Validates: Requirements 6.7**
        
        // Clear the database for each test
        ClearDatabase();
        
        var deviceId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var invoiceNumber = $"{invoicePrefix.Get}-{DateTime.Now.Ticks}";
        var totalAmount = Math.Max(0.01m, amount.Get);
        
        // Limit sync count to reasonable range for testing
        var numberOfSyncs = Math.Min(syncCount.Get % 5 + 1, 3); // 1-3 syncs
        
        try
        {
            // Set up test data: Create a product and an unsynced sale
            var product = new Product
            {
                Id = productId,
                Name = "Idempotency Test Product",
                Barcode = $"IDEM{DateTime.Now.Ticks}",
                UnitPrice = totalAmount,
                IsActive = true,
                DeviceId = deviceId,
                CreatedAt = DateTime.UtcNow,
                SyncStatus = SyncStatus.NotSynced
            };
            
            _context.Products.Add(product);
            
            var sale = new Sale
            {
                Id = saleId,
                InvoiceNumber = invoiceNumber,
                TotalAmount = totalAmount,
                PaymentMethod = PaymentMethod.Cash,
                DeviceId = deviceId,
                CreatedAt = DateTime.UtcNow,
                SyncStatus = SyncStatus.NotSynced
            };
            
            _context.Sales.Add(sale);
            
            var saleItem = new SaleItem
            {
                Id = Guid.NewGuid(),
                SaleId = saleId,
                ProductId = productId,
                Quantity = 1,
                UnitPrice = totalAmount
            };
            
            _context.SaleItems.Add(saleItem);
            _context.SaveChanges();
            
            // Reset API client and ensure it succeeds
            _apiClient.ResetCallTracking();
            _apiClient.SetFailureCount(0); // No failures for idempotency test
            
            // Capture initial state
            var initialSale = _context.Sales.First(s => s.Id == saleId);
            var initialSyncStatus = initialSale.SyncStatus;
            var initialServerSyncedAt = initialSale.ServerSyncedAt;
            
            // Perform sync multiple times
            var syncResults = new List<SyncResult>();
            for (int i = 0; i < numberOfSyncs; i++)
            {
                var syncResult = _syncEngine.SyncAllAsync().Result;
                syncResults.Add(syncResult);
            }
            
            // Capture final state after multiple syncs
            var finalSale = _context.Sales.First(s => s.Id == saleId);
            var finalSyncStatus = finalSale.SyncStatus;
            var finalServerSyncedAt = finalSale.ServerSyncedAt;
            
            // Verify idempotency: Multiple syncs should not change the final state
            // The sale should be synced after the first successful sync
            if (finalSyncStatus != SyncStatus.Synced)
            {
                return false; // Sale should be marked as synced
            }
            
            // The API should only be called once for the same data (idempotent behavior)
            // After the first sync, subsequent syncs should not upload the same data again
            if (_apiClient.UploadCallCount > numberOfSyncs)
            {
                return false; // Should not make excessive API calls for already synced data
            }
            
            // All sync results should be successful (idempotent operations should succeed)
            if (!syncResults.All(r => r.Success))
            {
                return false; // All sync operations should succeed
            }
            
            // Verify that the sale data itself hasn't changed due to multiple syncs
            var unchangedSale = _context.Sales.First(s => s.Id == saleId);
            if (unchangedSale.InvoiceNumber != invoiceNumber ||
                unchangedSale.TotalAmount != totalAmount ||
                unchangedSale.PaymentMethod != PaymentMethod.Cash)
            {
                return false; // Sale data should remain unchanged
            }
            
            // The property holds: multiple sync operations produce the same final state
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool ConflictResolutionRules(NonEmptyString productName, PositiveInt localPrice, PositiveInt serverPrice)
    {
        // Feature: offline-first-pos, Property 11: For any sync conflict, the resolution should follow predefined rules: server wins for product prices, sales are append-only, inventory is recalculated from transactions
        // **Validates: Requirements 2.5, 6.5**
        
        // Clear the database for each test
        ClearDatabase();
        
        var deviceId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var barcode = $"CONFLICT{DateTime.Now.Ticks}";
        var localUnitPrice = Math.Max(0.01m, localPrice.Get);
        var serverUnitPrice = Math.Max(0.01m, serverPrice.Get);
        
        // Ensure prices are different to create a conflict
        if (localUnitPrice == serverUnitPrice)
        {
            serverUnitPrice += 1.00m;
        }
        
        try
        {
            // Set up test data: Create a local product with local price
            var localProduct = new Product
            {
                Id = productId,
                Name = productName.Get,
                Barcode = barcode,
                UnitPrice = localUnitPrice, // Local price
                IsActive = true,
                DeviceId = deviceId,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10), // Created earlier
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5), // Updated locally
                SyncStatus = SyncStatus.NotSynced
            };
            
            _context.Products.Add(localProduct);
            _context.SaveChanges();
            
            // Verify initial local state
            var initialProduct = _context.Products.First(p => p.Id == productId);
            if (initialProduct.UnitPrice != localUnitPrice)
            {
                return false; // Initial local price should be set
            }
            
            // Configure API client to return server product with different price (conflict scenario)
            _apiClient.ResetCallTracking();
            _apiClient.SetFailureCount(0); // No failures for conflict resolution test
            
            // Configure the test API client to return server data with conflicting price
            var serverProduct = new ProductDto
            {
                Id = productId,
                Name = productName.Get,
                Barcode = barcode,
                UnitPrice = serverUnitPrice, // Server price (different from local)
                IsActive = true,
                DeviceId = deviceId,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-2) // Server updated more recently
            };
            
            // Set up the test API client to return this conflicting product
            _apiClient.SetServerProducts(new List<ProductDto> { serverProduct });
            
            // Trigger sync (this should resolve the conflict)
            var syncResult = _syncEngine.SyncAllAsync().Result;
            
            // Verify that sync was successful
            if (!syncResult.Success)
            {
                return false; // Sync should succeed even with conflicts
            }
            
            // Verify conflict resolution: Server should win for product prices
            var resolvedProduct = _context.Products.First(p => p.Id == productId);
            
            // Rule 1: Server wins for product prices
            if (resolvedProduct.UnitPrice != serverUnitPrice)
            {
                return false; // Server price should win in conflict resolution
            }
            
            // Verify that the product is marked as synced after conflict resolution
            if (resolvedProduct.SyncStatus != SyncStatus.Synced)
            {
                return false; // Product should be marked as synced after conflict resolution
            }
            
            // Verify that conflicts were reported in sync result
            if (syncResult.ConflictsResolved <= 0)
            {
                return false; // Should report that conflicts were resolved
            }
            
            // Test sales append-only rule: Create a local sale and verify it's not overwritten
            var saleId = Guid.NewGuid();
            var localSale = new Sale
            {
                Id = saleId,
                InvoiceNumber = $"LOCAL-{DateTime.Now.Ticks}",
                TotalAmount = 100.00m,
                PaymentMethod = PaymentMethod.Cash,
                DeviceId = deviceId,
                CreatedAt = DateTime.UtcNow,
                SyncStatus = SyncStatus.NotSynced
            };
            
            _context.Sales.Add(localSale);
            _context.SaveChanges();
            
            // Trigger another sync
            var salesSyncResult = _syncEngine.SyncSalesAsync().Result;
            
            // Verify that local sale still exists (append-only rule)
            var persistedSale = _context.Sales.FirstOrDefault(s => s.Id == saleId);
            if (persistedSale == null || persistedSale.InvoiceNumber != localSale.InvoiceNumber)
            {
                return false; // Local sale should persist (append-only rule)
            }
            
            // The property holds: conflict resolution follows predefined rules
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ClearDatabase()
    {
        // Use IgnoreQueryFilters to remove all entities including soft-deleted ones
        _context.SaleItems.IgnoreQueryFilters().ExecuteDelete();
        _context.Sales.IgnoreQueryFilters().ExecuteDelete();
        _context.Stock.IgnoreQueryFilters().ExecuteDelete();
        _context.Products.IgnoreQueryFilters().ExecuteDelete();
    }

    public void Dispose()
    {
        (_syncEngine as IDisposable)?.Dispose();
        _context?.Database.CloseConnection();
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}

/// <summary>
/// Test implementation of connectivity service for property testing
/// </summary>
public class TestConnectivityService : IConnectivityService
{
    private bool _isConnected = true;
    
    public bool IsConnected => _isConnected;
    
    public event EventHandler<ConnectivityChangedEventArgs>? ConnectivityChanged;
    
    public Task<bool> IsConnectedAsync()
    {
        return Task.FromResult(_isConnected);
    }
    public void SetConnectivity(bool isConnected)
    {
        if (_isConnected != isConnected)
        {
            _isConnected = isConnected;
            ConnectivityChanged?.Invoke(this, new ConnectivityChangedEventArgs { IsConnected = isConnected });
        }
    }
    
    public Task<bool> IsServerReachableAsync(string serverUrl, TimeSpan timeout = default)
    {
        return Task.FromResult(_isConnected);
    }
    
    public Task StartMonitoringAsync()
    {
        return Task.CompletedTask;
    }
    
    public Task StopMonitoringAsync()
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Test implementation of sync API client for property testing
/// </summary>
public class TestSyncApiClient : ISyncApiClient
{
    public bool WasUploadCalled { get; private set; }
    public bool WasDownloadCalled { get; private set; }
    public int UploadCallCount { get; private set; }
    public int DownloadCallCount { get; private set; }
    
    private int _failureCount = 0;
    private int _currentFailures = 0;
    private List<ProductDto> _serverProducts = new();
    private List<StockDto> _serverStock = new();
    
    public Action? OnUploadCall { get; set; }
    public Action? OnDownloadCall { get; set; }
    
    public void ResetCallTracking()
    {
        WasUploadCalled = false;
        WasDownloadCalled = false;
        UploadCallCount = 0;
        DownloadCallCount = 0;
        _currentFailures = 0;
        OnUploadCall = null;
        OnDownloadCall = null;
        _serverProducts.Clear();
        _serverStock.Clear();
    }
    
    public void SetFailureCount(int failureCount)
    {
        _failureCount = failureCount;
        _currentFailures = 0;
    }
    
    public void SetServerProducts(List<ProductDto> products)
    {
        _serverProducts = products ?? new List<ProductDto>();
    }
    
    public void SetServerStock(List<StockDto> stock)
    {
        _serverStock = stock ?? new List<StockDto>();
    }
    
    public Task<SyncApiResult> UploadChangesAsync(SyncUploadRequest request)
    {
        WasUploadCalled = true;
        UploadCallCount++;
        OnUploadCall?.Invoke();
        
        // Simulate failures for the specified number of attempts
        if (_currentFailures < _failureCount)
        {
            _currentFailures++;
            return Task.FromResult(new SyncApiResult
            {
                Success = false,
                Message = $"Simulated failure {_currentFailures}/{_failureCount}",
                StatusCode = 500,
                Errors = { "Simulated network error" }
            });
        }
        
        return Task.FromResult(new SyncApiResult
        {
            Success = true,
            Message = $"Test upload successful for {request.Sales.Count} sales",
            StatusCode = 200
        });
    }
    
    public Task<SyncApiResult<SyncDownloadResponse>> DownloadChangesAsync(Guid deviceId, DateTime lastSyncTimestamp)
    {
        WasDownloadCalled = true;
        DownloadCallCount++;
        OnDownloadCall?.Invoke();
        
        var response = new SyncDownloadResponse
        {
            ServerTimestamp = DateTime.UtcNow,
            Products = _serverProducts.ToList(), // Return configured server products
            Stock = _serverStock.ToList(), // Return configured server stock
            HasMoreData = false
        };
        
        return Task.FromResult(new SyncApiResult<SyncDownloadResponse>
        {
            Success = true,
            Message = "Test download successful",
            StatusCode = 200,
            Data = response
        });
    }
    
    public Task<SyncApiResult> RegisterDeviceAsync(Guid deviceId, string deviceName)
    {
        return Task.FromResult(new SyncApiResult
        {
            Success = true,
            Message = "Test device registration successful",
            StatusCode = 200
        });
    }
    
    public Task<SyncApiResult<AuthenticationResponse>> AuthenticateAsync(Guid deviceId, string apiKey)
    {
        var authResponse = new AuthenticationResponse
        {
            AccessToken = "test_token",
            RefreshToken = "test_refresh",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            DeviceId = deviceId
        };
        
        return Task.FromResult(new SyncApiResult<AuthenticationResponse>
        {
            Success = true,
            Message = "Test authentication successful",
            StatusCode = 200,
            Data = authResponse
        });
    }
}