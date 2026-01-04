using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Shared.Core.Data;
using Shared.Core.DTOs;
using Shared.Core.Repositories;
using Shared.Core.Services;
using Shared.Core.Tests.TestImplementations;

namespace Shared.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedCore(this IServiceCollection services, string connectionString)
    {
        // Add Entity Framework Core with SQLite
        services.AddDbContext<PosDbContext>(options =>
        {
            options.UseSqlite(connectionString, sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(30);
            });
            options.EnableSensitiveDataLogging(false);
        });

        // Register business logic services
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IWeightBasedPricingService, WeightBasedPricingService>();
        services.AddScoped<IDiscountService, DiscountService>();
        services.AddScoped<IMembershipService, MembershipService>();
        services.AddScoped<IConfigurationService, ConfigurationService>();
        
        // Register repositories
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<ISaleItemRepository, SaleItemRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IDiscountRepository, DiscountRepository>();
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        
        // Register transaction logging service for offline-first persistence
        services.AddScoped<ITransactionLogService, TransactionLogService>();
        
        // Register security and authentication services
        services.AddScoped<IEncryptionService, EncryptionService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddSingleton<ICurrentUserService, CurrentUserService>();
        
        // Register database migration service
        services.AddScoped<IDatabaseMigrationService, DatabaseMigrationService>();
        
        // Register cross-platform configuration service
        services.AddSingleton<ICrossPlatformConfigurationService, CrossPlatformConfigurationService>();
        
        // Register background services
        services.AddHostedService<SessionCleanupService>();
        
        // Register sync services
        services.AddScoped<ISyncEngine, SyncEngine>();
        services.AddScoped<IConnectivityService, ConnectivityService>();
        services.AddScoped<ISyncApiClient, SyncApiClient>();
        services.AddHttpClient<ISyncApiClient, SyncApiClient>();
        
        // Register hardware integration services
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<IPrinterService, PrinterService>();
        services.AddScoped<IBarcodeScanner, BarcodeScanner>();
        services.AddScoped<ICashDrawerService, CashDrawerService>();
        
        // Register hardware configurations (should be configured by the consuming application)
        services.AddSingleton(provider => new ReceiptConfiguration
        {
            ShopName = "POS Shop",
            PaperWidth = 48,
            PrintBarcode = true,
            FooterMessage = "Thank you for your business!"
        });
        
        services.AddSingleton(provider => new ScannerConfiguration
        {
            ScanTimeout = TimeSpan.FromSeconds(30),
            EnableBeep = true,
            EnableVibration = true
        });
        
        services.AddSingleton(provider => new CashDrawerConfiguration
        {
            Port = "COM1",
            BaudRate = 9600,
            OpenTimeout = TimeSpan.FromSeconds(5)
        });
        
        // Register sync configuration (should be configured by the consuming application)
        services.AddSingleton(provider => new SyncConfiguration
        {
            DeviceId = Guid.NewGuid(), // Should be set by the application
            ServerBaseUrl = "https://api.example.com", // Should be configured
            SyncInterval = TimeSpan.FromMinutes(5),
            MaxRetryAttempts = 3,
            InitialRetryDelay = TimeSpan.FromSeconds(1),
            RetryBackoffMultiplier = 2.0
        });

        return services;
    }
    
    public static IServiceCollection AddSharedCoreInMemory(this IServiceCollection services)
    {
        // Add Entity Framework Core with In-Memory database for testing
        services.AddDbContext<PosDbContext>(options =>
        {
            options.UseInMemoryDatabase("TestDatabase");
            options.EnableSensitiveDataLogging(true);
        });

        // Register business logic services
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IWeightBasedPricingService, WeightBasedPricingService>();
        services.AddScoped<IDiscountService, DiscountService>();
        services.AddScoped<IMembershipService, MembershipService>();
        
        // Register repositories
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<ISaleItemRepository, SaleItemRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IDiscountRepository, DiscountRepository>();
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();

        // Register additional services for testing
        services.AddScoped<IDiscountManagementService, DiscountManagementService>();
        services.AddScoped<IConfigurationService, ConfigurationService>();

        return services;
    }
}