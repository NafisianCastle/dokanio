using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Moq;
using Shared.Core.Architecture;
using Shared.Core.Data;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Events;
using Shared.Core.Integration;
using Shared.Core.Plugins;
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
        services.AddScoped<IEnhancedSalesService, EnhancedSalesService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IEnhancedInventoryService, EnhancedInventoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IWeightBasedPricingService, WeightBasedPricingService>();
        services.AddScoped<IDiscountService, DiscountService>();
        services.AddScoped<IMembershipService, MembershipService>();
        services.AddScoped<IConfigurationService, ConfigurationService>();
        services.AddScoped<ILicenseService, LicenseService>();
        services.AddScoped<IIntegratedPosService, IntegratedPosService>();
        services.AddScoped<IApplicationStartupService, ApplicationStartupService>();
        services.AddScoped<IBusinessManagementService, BusinessManagementService>();
        
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
        services.AddScoped<ILicenseRepository, LicenseRepository>();
        services.AddScoped<IBusinessRepository, BusinessRepository>();
        services.AddScoped<IShopRepository, ShopRepository>();
        
        // Register transaction logging service for offline-first persistence
        services.AddScoped<ITransactionLogService, TransactionLogService>();
        
        // Register security and authentication services
        services.AddScoped<IEncryptionService, EncryptionService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthenticationService>(provider => new AuthenticationService(
            provider.GetRequiredService<IUserRepository>(),
            provider.GetRequiredService<IShopRepository>(),
            provider.GetRequiredService<ISessionService>(),
            provider.GetRequiredService<IAuthorizationService>(),
            provider.GetRequiredService<IEncryptionService>(),
            provider.GetRequiredService<IAuditService>()));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddSingleton<ICurrentUserService, CurrentUserService>();
        
        // Register database migration service
        services.AddScoped<IDatabaseMigrationService, DatabaseMigrationService>();
        
        // Register cross-platform configuration service
        services.AddSingleton<ICrossPlatformConfigurationService, CrossPlatformConfigurationService>();
        
        // Register AI Analytics Engine
        services.AddScoped<IAIAnalyticsEngine, AIAnalyticsEngine>();
        
        // Register ML Pipeline Services
        services.AddScoped<IMLPipelineService, MLPipelineService>();
        services.AddScoped<IDataPreprocessingService, DataPreprocessingService>();
        services.AddScoped<IFeatureEngineeringService, FeatureEngineeringService>();
        services.AddScoped<IModelTrainingService, ModelTrainingService>();
        services.AddScoped<IModelPerformanceMonitoringService, ModelPerformanceMonitoringService>();
        
        // Register background services
        services.AddHostedService<SessionCleanupService>();
        
        // Register sync services
        services.AddScoped<ISyncEngine, SyncEngine>();
        services.AddScoped<IConnectivityService, ConnectivityService>();
        services.AddScoped<ISyncApiClient, SyncApiClient>();
        services.AddScoped<IMultiTenantSyncService, MultiTenantSyncService>();
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

        // Add extensible architecture services
        services.AddExtensibleArchitecture(new MultiTenantServiceConfiguration
        {
            EnablePluginSystem = true,
            EnableEventBus = true,
            EnableAnalyticsIntegration = false, // Disabled by default
            EnableAIIntegration = false, // Disabled by default
            PluginDirectory = "plugins"
        });

        return services;
    }
    
    public static IServiceCollection AddSharedCoreInMemory(this IServiceCollection services)
    {
        // Add logging
        services.AddLogging();
        
        // Add Entity Framework Core with In-Memory database for testing
        services.AddDbContext<PosDbContext>(options =>
        {
            options.UseInMemoryDatabase("TestDatabase");
            options.EnableSensitiveDataLogging(true);
        });

        // Register business logic services
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IEnhancedSalesService, EnhancedSalesService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IEnhancedInventoryService, EnhancedInventoryService>();
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
        services.AddScoped<ILicenseRepository, LicenseRepository>();
        services.AddScoped<IBusinessRepository, BusinessRepository>();
        services.AddScoped<IShopRepository, ShopRepository>();

        // Register additional services for testing
        services.AddScoped<IDiscountManagementService, DiscountManagementService>();
        services.AddScoped<IConfigurationService, ConfigurationService>();
        services.AddScoped<ILicenseService, LicenseService>();
        services.AddScoped<IIntegratedPosService, IntegratedPosService>();
        services.AddScoped<IApplicationStartupService, ApplicationStartupService>();
        services.AddScoped<IDatabaseMigrationService, DatabaseMigrationService>();
        services.AddScoped<IBusinessManagementService, BusinessManagementService>();
        
        // Register sync services for testing
        services.AddScoped<ISyncEngine, SyncEngine>();
        services.AddScoped<IConnectivityService, ConnectivityService>();
        services.AddScoped<ISyncApiClient, SyncApiClient>();
        services.AddScoped<IMultiTenantSyncService, MultiTenantSyncService>();
        services.AddHttpClient<ISyncApiClient, SyncApiClient>();
        
        // Register hardware integration services for testing
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<IPrinterService, PrinterService>();
        services.AddScoped<IBarcodeScanner, BarcodeScanner>();
        services.AddScoped<ICashDrawerService, CashDrawerService>();
        
        // Register additional repositories and services
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<ITransactionLogService, TransactionLogService>();
        services.AddScoped<IEncryptionService, EncryptionService>();
        services.AddScoped<IUserService, UserService>();
        
        // Register AI Analytics Engine
        services.AddScoped<IAIAnalyticsEngine, AIAnalyticsEngine>();
        
        // Register ML Pipeline Services
        services.AddScoped<IMLPipelineService, MLPipelineService>();
        services.AddScoped<IDataPreprocessingService, DataPreprocessingService>();
        services.AddScoped<IFeatureEngineeringService, FeatureEngineeringService>();
        services.AddScoped<IModelTrainingService, ModelTrainingService>();
        services.AddScoped<IModelPerformanceMonitoringService, ModelPerformanceMonitoringService>();
        
        services.AddScoped<IAuthenticationService>(provider => new AuthenticationService(
            provider.GetRequiredService<IUserRepository>(),
            provider.GetRequiredService<IShopRepository>(),
            provider.GetRequiredService<ISessionService>(),
            provider.GetRequiredService<IAuthorizationService>(),
            provider.GetRequiredService<IEncryptionService>(),
            provider.GetRequiredService<IAuditService>()));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddSingleton<ICrossPlatformConfigurationService, CrossPlatformConfigurationService>();
        
        // Add test configurations
        services.AddSingleton(provider => new SyncConfiguration
        {
            DeviceId = Guid.NewGuid(),
            ServerUrl = "https://test.example.com",
            ServerBaseUrl = "https://test.example.com",
            ApiKey = "test-api-key",
            SyncInterval = TimeSpan.FromMinutes(5),
            ConnectivityCheckInterval = TimeSpan.FromMinutes(1),
            MaxRetryAttempts = 3,
            RetryDelay = TimeSpan.FromSeconds(30),
            InitialRetryDelay = TimeSpan.FromSeconds(5),
            RetryBackoffMultiplier = 2.0,
            BatchSize = 100
        });
        
        services.AddSingleton(provider => new ReceiptConfiguration
        {
            ShopName = "Test POS Shop",
            ShopAddress = "123 Test Street",
            ShopPhone = "555-0123",
            PaperWidth = 48,
            PrintLogo = false,
            PrintBarcode = true,
            FooterMessage = "Thank you for testing!"
        });
        
        services.AddSingleton(provider => new ScannerConfiguration
        {
            EnableContinuousMode = false,
            ScanTimeout = TimeSpan.FromSeconds(30),
            EnableBeep = true,
            EnableVibration = true,
            SupportedFormats = new List<string> { "EAN13", "EAN8", "Code128", "Code39" }
        });
        
        services.AddSingleton(provider => new CashDrawerConfiguration
        {
            Port = "COM1",
            BaudRate = 9600,
            OpenTimeout = TimeSpan.FromSeconds(5),
            AutoClose = false,
            AutoCloseDelay = TimeSpan.FromSeconds(30)
        });
        
        // Add mock current user service for testing
        services.AddSingleton<ICurrentUserService>(provider => 
        {
            var mockService = new Mock<ICurrentUserService>();
            var deviceId = Guid.NewGuid();
            var user = new User 
            { 
                Id = Guid.NewGuid(), 
                DeviceId = deviceId,
                Username = "TestUser",
                Role = UserRole.Administrator
            };
            mockService.Setup(x => x.CurrentUser).Returns(user);
            mockService.Setup(x => x.GetDeviceId()).Returns(deviceId);
            mockService.Setup(x => x.GetUserId()).Returns(user.Id);
            mockService.Setup(x => x.GetUsername()).Returns(user.Username);
            return mockService.Object;
        });

        // Add extensible architecture services for testing
        services.AddExtensibleArchitecture(new MultiTenantServiceConfiguration
        {
            EnablePluginSystem = false, // Disabled for testing
            EnableEventBus = true,
            EnableAnalyticsIntegration = false,
            EnableAIIntegration = false,
            PluginDirectory = "test-plugins"
        });

        return services;
    }
}