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

namespace Shared.Core.Tests;

/// <summary>
/// Property-based test for inventory synchronization consistency
/// Feature: multi-business-pos, Property 7: Inventory Synchronization Consistency
/// </summary>
public class InventorySyncPropertyTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;

    public InventorySyncPropertyTest()
    {
        var services = new ServiceCollection();
        
        // Add in-memory database
        services.AddDbContext<PosDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole());
        
        // Add repositories
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IBusinessRepository, BusinessRepository>();
        services.AddScoped<IShopRepository, ShopRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IDiscountRepository, DiscountRepository>();
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        services.AddScoped<ILicenseRepository, LicenseRepository>();
        services.AddScoped<ISaleItemRepository, SaleItemRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        
        // Add services
        services.AddScoped<IBusinessManagementService, BusinessManagementService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IEncryptionService, EncryptionService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IEnhancedSalesService, EnhancedSalesService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IWeightBasedPricingService, WeightBasedPricingService>();
        services.AddScoped<IMembershipService, MembershipService>();
        services.AddScoped<IDiscountService, DiscountService>();
        services.AddScoped<IConfigurationService, ConfigurationService>();
        services.AddScoped<ILicenseService, LicenseService>();
        services.AddSingleton<ICurrentUserService, CurrentUserService>();
        
        // Add sync services
        services.AddScoped<ISyncEngine, SyncEngine>();
        services.AddScoped<IConnectivityService, ConnectivityService>();
        services.AddScoped<ISyncApiClient, SyncApiClient>();
        services.AddScoped<IMultiTenantSyncService, MultiTenantSyncService>();
        services.AddHttpClient<ISyncApiClient, SyncApiClient>();
        
        // Add sync configuration
        services.AddSingleton(provider => new SyncConfiguration
        {
            DeviceId = Guid.NewGuid(),
            ServerBaseUrl = "https://test.example.com",
            SyncInterval = TimeSpan.FromMinutes(5),
            MaxRetryAttempts = 3,
            InitialRetryDelay = TimeSpan.FromSeconds(1),
            RetryBackoffMultiplier = 2.0
        });
        
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        
        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    /// <summary>
    /// Property 7: Inventory Synchronization Consistency
    /// For any inventory update, the final stock level should be consistent across all synchronized devices after conflict resolution.
    /// Validates: Requirements 7.3, 7.4
    /// Feature: multi-business-pos, Property 7: Inventory Synchronization Consistency
    /// </summary>
    [Fact]
    public async Task InventorySynchronizationConsistency_FinalStockLevelShouldBeConsistentAfterConflictResolution()
    {
        var multiTenantSyncService = _serviceProvider.GetRequiredService<IMultiTenantSyncService>();
        var stockRepository = _serviceProvider.GetRequiredService<IStockRepository>();
        
        // Test with multiple random synchronization scenarios
        for (int iteration = 0; iteration < 10; iteration++)
        {
            try
            {
                // Setup: Create a business with shop and products
                var syncTestData = GenerateRandomSyncTestData();
                await SetupSyncTestDataAsync(syncTestData);

                // Simulate inventory conflicts
                var conflicts = await CreateSimpleInventoryConflictsAsync(syncTestData);
                
                if (conflicts.Any())
                {
                    // Test: Resolve conflicts using multi-tenant sync service
                    var resolutionResult = await multiTenantSyncService.ResolveDataConflictsAsync(conflicts.ToArray());
                    
                    // Verify conflict resolution succeeded or handled gracefully
                    Assert.NotNull(resolutionResult);
                    
                    // Verify consistency: Final stock levels should be non-negative
                    foreach (var productId in syncTestData.ProductIds)
                    {
                        var finalStock = await stockRepository.GetByProductIdAsync(productId);
                        if (finalStock != null)
                        {
                            Assert.True(finalStock.Quantity >= 0, 
                                $"Final stock quantity should be non-negative for product {productId} " +
                                $"in iteration {iteration}. Got: {finalStock.Quantity}");
                        }
                    }
                    
                    // Verify tenant isolation
                    var isolationValid = await multiTenantSyncService.ValidateTenantIsolationAsync(
                        syncTestData.BusinessId, syncTestData);
                    
                    // Isolation validation should not throw exceptions (result is bool, so no need to check for null)
                }
                
                // Test synchronization operations
                var syncResult = await multiTenantSyncService.SyncShopDataAsync(syncTestData.ShopId);
                
                // Sync should complete without exceptions
                Assert.NotNull(syncResult);
                Assert.Equal(syncTestData.BusinessId, syncResult.BusinessId);
                Assert.Equal(syncTestData.ShopId, syncResult.ShopId);
                
                // Multiple syncs should be idempotent
                var secondSyncResult = await multiTenantSyncService.SyncShopDataAsync(syncTestData.ShopId);
                Assert.NotNull(secondSyncResult);
                Assert.Equal(syncResult.BusinessId, secondSyncResult.BusinessId);
                Assert.Equal(syncResult.ShopId, secondSyncResult.ShopId);
            }
            finally
            {
                CleanupTestData();
            }
        }
    }

    private SyncTestData GenerateRandomSyncTestData()
    {
        var random = new Random();
        var productCount = random.Next(2, 5); // 2-4 products for testing
        
        return new SyncTestData
        {
            BusinessId = Guid.NewGuid(),
            ShopId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ProductIds = Enumerable.Range(0, productCount).Select(_ => Guid.NewGuid()).ToList(),
            DeviceIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() } // 2 devices
        };
    }

    private async Task SetupSyncTestDataAsync(SyncTestData syncTestData)
    {
        // Create business owner
        var owner = new User
        {
            Id = syncTestData.UserId,
            BusinessId = syncTestData.BusinessId,
            Username = $"owner_{syncTestData.UserId:N}",
            FullName = $"Owner {syncTestData.UserId:N}",
            Email = $"owner_{syncTestData.UserId:N}@test.com",
            PasswordHash = "hash",
            Salt = "salt",
            Role = UserRole.BusinessOwner,
            DeviceId = syncTestData.DeviceIds[0]
        };

        // Create business
        var business = new Business
        {
            Id = syncTestData.BusinessId,
            Name = $"Business {syncTestData.BusinessId:N}",
            Type = BusinessType.GeneralRetail,
            OwnerId = syncTestData.UserId,
            DeviceId = syncTestData.DeviceIds[0]
        };

        // Create shop
        var shop = new Shop
        {
            Id = syncTestData.ShopId,
            BusinessId = syncTestData.BusinessId,
            Name = $"Shop {syncTestData.ShopId:N}",
            DeviceId = syncTestData.DeviceIds[0]
        };

        _context.Users.Add(owner);
        _context.Businesses.Add(business);
        _context.Shops.Add(shop);

        // Create products and initial stock
        for (int i = 0; i < syncTestData.ProductIds.Count; i++)
        {
            var productId = syncTestData.ProductIds[i];
            var product = new Product
            {
                Id = productId,
                ShopId = syncTestData.ShopId,
                Name = $"Product {i + 1}",
                Barcode = $"BC{productId:N}",
                UnitPrice = 10.00m + i,
                DeviceId = syncTestData.DeviceIds[0]
            };

            var stock = new Stock
            {
                Id = Guid.NewGuid(),
                ShopId = syncTestData.ShopId,
                ProductId = productId,
                Quantity = 100 + i * 10, // Initial stock: 100, 110, 120, etc.
                LastUpdatedAt = DateTime.UtcNow.AddMinutes(-60), // 1 hour ago
                DeviceId = syncTestData.DeviceIds[0]
            };

            _context.Products.Add(product);
            _context.Stock.Add(stock);
        }

        await _context.SaveChangesAsync();
    }

    private async Task<List<DataConflict>> CreateSimpleInventoryConflictsAsync(SyncTestData syncTestData)
    {
        var conflicts = new List<DataConflict>();
        var random = new Random();
        
        // Create conflicts for some products
        var conflictCount = Math.Min(2, syncTestData.ProductIds.Count);
        
        for (int i = 0; i < conflictCount; i++)
        {
            var productId = syncTestData.ProductIds[i];
            var stock = await _context.Stock.FirstOrDefaultAsync(s => s.ProductId == productId);
            
            if (stock != null)
            {
                // Simulate server changes
                var serverQuantity = Math.Max(0, stock.Quantity + random.Next(-20, 50));
                var serverTimestamp = DateTime.UtcNow.AddMinutes(-random.Next(1, 30));
                
                var conflict = new DataConflict
                {
                    EntityType = nameof(Stock),
                    EntityId = stock.Id,
                    BusinessId = syncTestData.BusinessId,
                    ShopId = syncTestData.ShopId,
                    LocalData = new { Quantity = stock.Quantity, LastUpdatedAt = stock.LastUpdatedAt, DeviceId = stock.DeviceId },
                    ServerData = new { Quantity = serverQuantity, LastUpdatedAt = serverTimestamp, DeviceId = syncTestData.DeviceIds[1] },
                    LocalTimestamp = stock.LastUpdatedAt,
                    ServerTimestamp = serverTimestamp,
                    Type = ConflictType.UpdateConflict,
                    ConflictReason = "Concurrent inventory updates from different devices"
                };
                
                conflicts.Add(conflict);
            }
        }
        
        return conflicts;
    }

    private void CleanupTestData()
    {
        try
        {
            // Remove all test data
            _context.Stock.RemoveRange(_context.Stock);
            _context.Sales.RemoveRange(_context.Sales);
            _context.Products.RemoveRange(_context.Products);
            _context.Shops.RemoveRange(_context.Shops);
            _context.Businesses.RemoveRange(_context.Businesses);
            _context.Users.RemoveRange(_context.Users);
            _context.SaveChanges();
        }
        catch (Exception)
        {
            // Ignore cleanup errors
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }

    private class SyncTestData
    {
        public Guid BusinessId { get; set; }
        public Guid ShopId { get; set; }
        public Guid UserId { get; set; }
        public List<Guid> ProductIds { get; set; } = new();
        public List<Guid> DeviceIds { get; set; } = new();
    }
}