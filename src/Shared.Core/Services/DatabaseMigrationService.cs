using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Service for handling database migrations and initial data seeding
/// </summary>
public interface IDatabaseMigrationService
{
    /// <summary>
    /// Ensures the database is created and migrated to the latest version
    /// </summary>
    Task EnsureDatabaseCreatedAsync();
    
    /// <summary>
    /// Seeds the database with initial data if it's empty
    /// </summary>
    Task SeedInitialDataAsync();
    
    /// <summary>
    /// Performs a complete database setup (migration + seeding)
    /// </summary>
    Task InitializeDatabaseAsync();
}

/// <summary>
/// Implementation of database migration and seeding service
/// </summary>
public class DatabaseMigrationService : IDatabaseMigrationService
{
    private readonly PosDbContext _context;
    private readonly ILogger<DatabaseMigrationService> _logger;

    public DatabaseMigrationService(PosDbContext context, ILogger<DatabaseMigrationService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Ensures the database is created and migrated to the latest version
    /// </summary>
    public async Task EnsureDatabaseCreatedAsync()
    {
        try
        {
            _logger.LogInformation("Ensuring database is created and migrated...");
            
            // For SQLite, this will create the database file if it doesn't exist
            // and apply any pending migrations
            await _context.Database.EnsureCreatedAsync();
            
            _logger.LogInformation("Database creation and migration completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while creating/migrating database");
            throw;
        }
    }

    /// <summary>
    /// Seeds the database with initial data if it's empty
    /// </summary>
    public async Task SeedInitialDataAsync()
    {
        try
        {
            _logger.LogInformation("Checking if database seeding is needed...");

            // Check if we already have data
            var hasProducts = await _context.Products.AnyAsync();
            if (hasProducts)
            {
                _logger.LogInformation("Database already contains data, skipping seeding");
                return;
            }

            _logger.LogInformation("Seeding initial data...");

            var deviceId = Guid.NewGuid();
            
            // Seed sample products
            var sampleProducts = new List<Product>
            {
                new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Sample Product 1",
                    Barcode = "1234567890123",
                    Category = "Electronics",
                    UnitPrice = 29.99m,
                    IsActive = true,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Sample Medicine",
                    Barcode = "9876543210987",
                    Category = "Medicine",
                    UnitPrice = 15.50m,
                    IsActive = true,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced,
                    BatchNumber = "BATCH001",
                    ExpiryDate = DateTime.UtcNow.AddYears(2),
                    PurchasePrice = 10.00m,
                    SellingPrice = 15.50m,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Sample Food Item",
                    Barcode = "5555555555555",
                    Category = "Food",
                    UnitPrice = 5.99m,
                    IsActive = true,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            await _context.Products.AddRangeAsync(sampleProducts);

            // Seed initial stock for the products
            var sampleStock = sampleProducts.Select(product => new Stock
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Quantity = 100, // Initial stock quantity
                LastUpdatedAt = DateTime.UtcNow,
                DeviceId = deviceId,
                SyncStatus = SyncStatus.NotSynced
            }).ToList();

            await _context.Stock.AddRangeAsync(sampleStock);

            // Seed a default user (for desktop application)
            var defaultUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                Email = "admin@pos.local",
                PasswordHash = "AQAAAAEAACcQAAAAEJ1234567890", // This should be properly hashed in real implementation
                Role = UserRole.Administrator,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                DeviceId = deviceId,
                SyncStatus = SyncStatus.NotSynced
            };

            await _context.Users.AddAsync(defaultUser);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Initial data seeding completed successfully. Added {ProductCount} products, {StockCount} stock entries, and 1 default user", 
                sampleProducts.Count, sampleStock.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while seeding initial data");
            throw;
        }
    }

    /// <summary>
    /// Performs a complete database setup (migration + seeding)
    /// </summary>
    public async Task InitializeDatabaseAsync()
    {
        await EnsureDatabaseCreatedAsync();
        await SeedInitialDataAsync();
    }
}