using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Shared.Core.Data;
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
        
        // Register repositories
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        
        // Register transaction logging service for offline-first persistence
        services.AddScoped<ITransactionLogService, TransactionLogService>();
        
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

        // Register test repository implementations
        services.AddScoped<IProductRepository, InMemoryProductRepository>();

        // Register business logic services
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IProductService, ProductService>();

        return services;
    }
}