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
    
    /// <summary>
    /// Forces a complete re-seed of the database (clears existing data first)
    /// </summary>
    Task ForceReseedDatabaseAsync();
}

/// <summary>
/// Implementation of database migration and seeding service
/// </summary>
public class DatabaseMigrationService : IDatabaseMigrationService
{
    private readonly PosDbContext _context;
    private readonly ILogger<DatabaseMigrationService> _logger;
    private readonly IEncryptionService _encryptionService;

    public DatabaseMigrationService(PosDbContext context, ILogger<DatabaseMigrationService> logger, IEncryptionService encryptionService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
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

            // Check if we already have comprehensive data (users, products, and customers)
            var hasUsers = await _context.Users.AnyAsync();
            var hasProducts = await _context.Products.AnyAsync();
            var hasCustomers = await _context.Customers.AnyAsync();
            
            if (hasUsers && hasProducts && hasCustomers)
            {
                _logger.LogInformation("Database already contains comprehensive data, skipping seeding");
                return;
            }

            _logger.LogInformation("Database is missing data. Current counts - Users: {UserCount}, Products: {ProductCount}, Customers: {CustomerCount}", 
                await _context.Users.CountAsync(), 
                await _context.Products.CountAsync(), 
                await _context.Customers.CountAsync());

            _logger.LogInformation("Seeding comprehensive initial data...");

            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            // 1. First seed businesses with placeholder owner IDs
            var businessIds = new[]
            {
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid()
            };

            var adminUserId = Guid.NewGuid();

            // 2. Seed Businesses with admin as owner (FK constraints disabled)
            var businesses = new List<Business>
            {
                new Business
                {
                    Id = businessIds[0],
                    Name = "TechMart Electronics",
                    Type = BusinessType.GeneralRetail,
                    OwnerId = adminUserId, // Will be valid once we create the admin user
                    Address = "123 Tech Street, Silicon Valley",
                    Phone = "+1-555-0101",
                    Email = "info@techmart.com",
                    TaxId = "TAX123456789",
                    IsActive = true,
                    CreatedAt = now,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                },
                new Business
                {
                    Id = businessIds[1],
                    Name = "HealthPlus Pharmacy",
                    Type = BusinessType.Pharmacy,
                    OwnerId = adminUserId,
                    Address = "456 Health Avenue, Medical District",
                    Phone = "+1-555-0102",
                    Email = "info@healthplus.com",
                    TaxId = "TAX987654321",
                    IsActive = true,
                    CreatedAt = now,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                },
                new Business
                {
                    Id = businessIds[2],
                    Name = "FreshMart Grocery",
                    Type = BusinessType.Grocery,
                    OwnerId = adminUserId,
                    Address = "789 Fresh Street, Downtown",
                    Phone = "+1-555-0103",
                    Email = "info@freshmart.com",
                    TaxId = "TAX456789123",
                    IsActive = true,
                    CreatedAt = now,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                }
            };

            await _context.Businesses.AddRangeAsync(businesses);
            await _context.SaveChangesAsync(); // Save businesses first

            // 3. Seed Shops for each business
            var shops = new List<Shop>
            {
                // TechMart shops
                new Shop
                {
                    Id = Guid.NewGuid(),
                    Name = "TechMart Downtown",
                    BusinessId = businessIds[0],
                    Address = "123 Tech Street, Downtown Branch",
                    Phone = "+1-555-0111",
                    IsActive = true,
                    CreatedAt = now,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                },
                new Shop
                {
                    Id = Guid.NewGuid(),
                    Name = "TechMart Mall",
                    BusinessId = businessIds[0],
                    Address = "Shopping Mall, Level 2",
                    Phone = "+1-555-0112",
                    IsActive = true,
                    CreatedAt = now,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                },
                // HealthPlus shops
                new Shop
                {
                    Id = Guid.NewGuid(),
                    Name = "HealthPlus Central",
                    BusinessId = businessIds[1],
                    Address = "456 Health Avenue, Central Branch",
                    Phone = "+1-555-0121",
                    IsActive = true,
                    CreatedAt = now,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                },
                // FreshMart shops
                new Shop
                {
                    Id = Guid.NewGuid(),
                    Name = "FreshMart Express",
                    BusinessId = businessIds[2],
                    Address = "789 Fresh Street, Express Store",
                    Phone = "+1-555-0131",
                    IsActive = true,
                    CreatedAt = now,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                }
            };

            await _context.Shops.AddRangeAsync(shops);
            await _context.SaveChangesAsync(); // Save shops before users

            // 4. Now seed Users with proper BusinessId and ShopId references
            // Create users with proper PBKDF2 hashing to match EncryptionService
            var adminSalt = _encryptionService.GenerateSalt();
            var managerSalt = _encryptionService.GenerateSalt();
            var cashierSalt = _encryptionService.GenerateSalt();

            var users = new List<User>
            {
                new User
                {
                    Id = adminUserId, // Use the same ID we used for business owner
                    BusinessId = businessIds[0], // TechMart
                    ShopId = shops[0].Id, // TechMart Downtown
                    Username = "admin",
                    Email = "admin@pos.local",
                    PasswordHash = _encryptionService.HashPassword("admin123", adminSalt),
                    Salt = adminSalt,
                    Role = UserRole.Administrator,
                    FullName = "System Administrator",
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessIds[0], // TechMart
                    ShopId = shops[0].Id, // TechMart Downtown
                    Username = "manager",
                    Email = "manager@pos.local",
                    PasswordHash = _encryptionService.HashPassword("manager123", managerSalt),
                    Salt = managerSalt,
                    Role = UserRole.ShopManager,
                    FullName = "Shop Manager",
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessIds[0], // TechMart
                    ShopId = shops[0].Id, // TechMart Downtown
                    Username = "cashier",
                    Email = "cashier@pos.local",
                    PasswordHash = _encryptionService.HashPassword("cashier123", cashierSalt),
                    Salt = cashierSalt,
                    Role = UserRole.Cashier,
                    FullName = "Store Cashier",
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                }
            };

            await _context.Users.AddRangeAsync(users);
            await _context.SaveChangesAsync(); // Save users - now the foreign key references are valid

            // 6. Seed comprehensive product catalog
            var products = new List<Product>();

            // Electronics products (assign to first shop)
            var electronicsProducts = new[]
            {
                new { Name = "iPhone 15 Pro", Barcode = "1001001001001", Price = 999.99m, Cost = 750.00m },
                new { Name = "Samsung Galaxy S24", Barcode = "1001001001002", Price = 899.99m, Cost = 680.00m },
                new { Name = "MacBook Air M3", Barcode = "1001001001003", Price = 1299.99m, Cost = 980.00m },
                new { Name = "Dell XPS 13", Barcode = "1001001001004", Price = 1099.99m, Cost = 850.00m },
                new { Name = "iPad Pro 12.9", Barcode = "1001001001005", Price = 799.99m, Cost = 600.00m },
                new { Name = "AirPods Pro", Barcode = "1001001001006", Price = 249.99m, Cost = 180.00m },
                new { Name = "Sony WH-1000XM5", Barcode = "1001001001007", Price = 399.99m, Cost = 280.00m },
                new { Name = "Nintendo Switch OLED", Barcode = "1001001001008", Price = 349.99m, Cost = 260.00m },
                new { Name = "LG OLED 55\" TV", Barcode = "1001001001009", Price = 1499.99m, Cost = 1100.00m },
                new { Name = "Canon EOS R6", Barcode = "1001001001010", Price = 2499.99m, Cost = 1800.00m }
            };

            foreach (var item in electronicsProducts)
            {
                products.Add(new Product
                {
                    Id = Guid.NewGuid(),
                    ShopId = shops[0].Id, // TechMart Downtown
                    Name = item.Name,
                    Barcode = item.Barcode,
                    Category = "Electronics",
                    UnitPrice = item.Price,
                    PurchasePrice = item.Cost,
                    SellingPrice = item.Price,
                    IsActive = true,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            // Pharmacy products (assign to pharmacy shop)
            var pharmacyProducts = new[]
            {
                new { Name = "Paracetamol 500mg", Barcode = "2001001001001", Price = 5.99m, Cost = 3.50m, Batch = "PAR2024001", ExpiryMonths = 24 },
                new { Name = "Ibuprofen 400mg", Barcode = "2001001001002", Price = 8.99m, Cost = 5.20m, Batch = "IBU2024001", ExpiryMonths = 18 },
                new { Name = "Amoxicillin 250mg", Barcode = "2001001001003", Price = 12.99m, Cost = 8.50m, Batch = "AMX2024001", ExpiryMonths = 12 },
                new { Name = "Vitamin D3 1000IU", Barcode = "2001001001004", Price = 15.99m, Cost = 10.00m, Batch = "VIT2024001", ExpiryMonths = 36 },
                new { Name = "Omega-3 Fish Oil", Barcode = "2001001001005", Price = 24.99m, Cost = 16.00m, Batch = "OMG2024001", ExpiryMonths = 24 },
                new { Name = "Multivitamin Complex", Barcode = "2001001001006", Price = 19.99m, Cost = 12.50m, Batch = "MUL2024001", ExpiryMonths = 30 },
                new { Name = "Cough Syrup 100ml", Barcode = "2001001001007", Price = 9.99m, Cost = 6.00m, Batch = "COU2024001", ExpiryMonths = 18 },
                new { Name = "Antiseptic Cream", Barcode = "2001001001008", Price = 7.99m, Cost = 4.50m, Batch = "ANT2024001", ExpiryMonths = 24 },
                new { Name = "Blood Pressure Monitor", Barcode = "2001001001009", Price = 89.99m, Cost = 60.00m, Batch = "BPM2024001", ExpiryMonths = 60 },
                new { Name = "Digital Thermometer", Barcode = "2001001001010", Price = 29.99m, Cost = 18.00m, Batch = "THM2024001", ExpiryMonths = 60 }
            };

            foreach (var item in pharmacyProducts)
            {
                products.Add(new Product
                {
                    Id = Guid.NewGuid(),
                    ShopId = shops[2].Id, // HealthPlus Central
                    Name = item.Name,
                    Barcode = item.Barcode,
                    Category = "Medicine",
                    UnitPrice = item.Price,
                    PurchasePrice = item.Cost,
                    SellingPrice = item.Price,
                    BatchNumber = item.Batch,
                    ExpiryDate = now.AddMonths(item.ExpiryMonths),
                    IsActive = true,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            // Grocery products (assign to grocery shop)
            var groceryProducts = new[]
            {
                new { Name = "Organic Bananas (1kg)", Barcode = "3001001001001", Price = 3.99m, Cost = 2.20m, ExpiryDays = 7 },
                new { Name = "Fresh Milk (1L)", Barcode = "3001001001002", Price = 2.99m, Cost = 1.80m, ExpiryDays = 5 },
                new { Name = "Whole Wheat Bread", Barcode = "3001001001003", Price = 2.49m, Cost = 1.50m, ExpiryDays = 3 },
                new { Name = "Free Range Eggs (12)", Barcode = "3001001001004", Price = 4.99m, Cost = 3.20m, ExpiryDays = 14 },
                new { Name = "Greek Yogurt (500g)", Barcode = "3001001001005", Price = 5.99m, Cost = 3.80m, ExpiryDays = 10 },
                new { Name = "Olive Oil (500ml)", Barcode = "3001001001006", Price = 12.99m, Cost = 8.50m, ExpiryDays = 365 },
                new { Name = "Basmati Rice (2kg)", Barcode = "3001001001007", Price = 8.99m, Cost = 5.50m, ExpiryDays = 730 },
                new { Name = "Chicken Breast (1kg)", Barcode = "3001001001008", Price = 15.99m, Cost = 11.00m, ExpiryDays = 3 },
                new { Name = "Fresh Salmon (500g)", Barcode = "3001001001009", Price = 24.99m, Cost = 18.00m, ExpiryDays = 2 },
                new { Name = "Organic Spinach", Barcode = "3001001001010", Price = 3.49m, Cost = 2.00m, ExpiryDays = 5 }
            };

            foreach (var item in groceryProducts)
            {
                products.Add(new Product
                {
                    Id = Guid.NewGuid(),
                    ShopId = shops[3].Id, // FreshMart Express
                    Name = item.Name,
                    Barcode = item.Barcode,
                    Category = "Food",
                    UnitPrice = item.Price,
                    PurchasePrice = item.Cost,
                    SellingPrice = item.Price,
                    ExpiryDate = now.AddDays(item.ExpiryDays),
                    IsActive = true,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await _context.Products.AddRangeAsync(products);
            await _context.SaveChangesAsync(); // Save products before stock

            // 7. Seed stock with varying quantities (some low stock scenarios)
            var stockEntries = new List<Stock>();
            var random = new Random(42); // Fixed seed for reproducibility

            foreach (var product in products)
            {
                var baseQuantity = product.Category switch
                {
                    "Electronics" => random.Next(5, 25), // Lower quantities for expensive items
                    "Medicine" => random.Next(20, 100),  // Medium quantities
                    "Food" => random.Next(50, 200),      // Higher quantities for consumables
                    _ => random.Next(10, 50)
                };

                // Create some low stock scenarios (10% of products)
                var isLowStock = random.NextDouble() < 0.1;
                var quantity = isLowStock ? random.Next(1, 5) : baseQuantity;

                stockEntries.Add(new Stock
                {
                    Id = Guid.NewGuid(),
                    ShopId = shops[0].Id, // Assign to first shop for simplicity
                    ProductId = product.Id,
                    Quantity = quantity,
                    LastUpdatedAt = now,
                    UpdatedAt = now,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                });
            }

            await _context.Stock.AddRangeAsync(stockEntries);
            await _context.SaveChangesAsync(); // Save stock before customers

            // 8. Seed customers with different membership tiers
            var customers = new List<Customer>
            {
                new Customer
                {
                    Id = Guid.NewGuid(),
                    MembershipNumber = "GOLD001",
                    Name = "John Smith",
                    Email = "john.smith@email.com",
                    Phone = "+1-555-1001",
                    Tier = MembershipTier.Gold,
                    TotalSpent = 2500.00m,
                    VisitCount = 45,
                    LastVisit = now.AddDays(-2),
                    JoinDate = now.AddMonths(-6), // Changed from CreatedAt to JoinDate
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                },
                new Customer
                {
                    Id = Guid.NewGuid(),
                    MembershipNumber = "SILVER001",
                    Name = "Sarah Johnson",
                    Email = "sarah.johnson@email.com",
                    Phone = "+1-555-1002",
                    Tier = MembershipTier.Silver,
                    TotalSpent = 1200.00m,
                    VisitCount = 28,
                    LastVisit = now.AddDays(-5),
                    JoinDate = now.AddMonths(-4), // Changed from CreatedAt to JoinDate
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                },
                new Customer
                {
                    Id = Guid.NewGuid(),
                    MembershipNumber = "BRONZE001",
                    Name = "Mike Wilson",
                    Email = "mike.wilson@email.com",
                    Phone = "+1-555-1003",
                    Tier = MembershipTier.Bronze,
                    TotalSpent = 450.00m,
                    VisitCount = 12,
                    LastVisit = now.AddDays(-10),
                    JoinDate = now.AddMonths(-2), // Changed from CreatedAt to JoinDate
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                },
                new Customer
                {
                    Id = Guid.NewGuid(),
                    MembershipNumber = "REG001",
                    Name = "Emily Davis",
                    Email = "emily.davis@email.com",
                    Phone = "+1-555-1004",
                    Tier = MembershipTier.None, // Changed from Regular to None
                    TotalSpent = 150.00m,
                    VisitCount = 5,
                    LastVisit = now.AddDays(-15),
                    JoinDate = now.AddMonths(-1), // Changed from CreatedAt to JoinDate
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                }
            };

            await _context.Customers.AddRangeAsync(customers);
            await _context.SaveChangesAsync(); // Save customers before suppliers

            // 9. Seed suppliers
            var suppliers = new List<Supplier>
            {
                new Supplier
                {
                    Id = Guid.NewGuid(),
                    Name = "TechDistributor Inc.",
                    ContactPerson = "Robert Tech",
                    Email = "orders@techdistributor.com",
                    Phone = "+1-555-2001",
                    Address = "100 Tech Industrial Park",
                    IsActive = true,
                    CreatedAt = now,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                },
                new Supplier
                {
                    Id = Guid.NewGuid(),
                    Name = "PharmaCorp Ltd.",
                    ContactPerson = "Dr. Lisa Pharma",
                    Email = "supply@pharmacorp.com",
                    Phone = "+1-555-2002",
                    Address = "200 Medical Supply District",
                    IsActive = true,
                    CreatedAt = now,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                },
                new Supplier
                {
                    Id = Guid.NewGuid(),
                    Name = "Fresh Foods Wholesale",
                    ContactPerson = "Mark Fresh",
                    Email = "orders@freshfoods.com",
                    Phone = "+1-555-2003",
                    Address = "300 Agricultural Center",
                    IsActive = true,
                    CreatedAt = now,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                }
            };

            await _context.Suppliers.AddRangeAsync(suppliers);
            await _context.SaveChangesAsync(); // Save suppliers before sales

            // 10. Seed some historical sales data
            var sales = new List<Sale>();
            var saleItems = new List<SaleItem>();

            for (int i = 0; i < 20; i++)
            {
                var saleDate = now.AddDays(-random.Next(1, 30));
                var customer = random.NextDouble() < 0.7 ? customers[random.Next(customers.Count)] : null; // 70% chance of having customer
                
                var sale = new Sale
                {
                    Id = Guid.NewGuid(),
                    InvoiceNumber = $"INV-{DateTime.Now:yyyyMM}-{(i + 1):D4}",
                    CustomerId = customer?.Id,
                    UserId = users[random.Next(users.Count)].Id,
                    ShopId = shops[random.Next(shops.Count)].Id,
                    PaymentMethod = (PaymentMethod)random.Next(0, 4),
                    CreatedAt = saleDate,
                    UpdatedAt = saleDate,
                    DeviceId = deviceId,
                    SyncStatus = SyncStatus.NotSynced
                };

                // Add 1-5 items per sale
                var itemCount = random.Next(1, 6);
                var saleTotal = 0m;

                for (int j = 0; j < itemCount; j++)
                {
                    var product = products[random.Next(products.Count)];
                    var quantity = random.Next(1, 4);
                    var unitPrice = product.UnitPrice;
                    var totalPrice = quantity * unitPrice;

                    saleItems.Add(new SaleItem
                    {
                        Id = Guid.NewGuid(),
                        SaleId = sale.Id,
                        ProductId = product.Id,
                        Quantity = quantity,
                        UnitPrice = unitPrice,
                        TotalPrice = totalPrice, // Changed from Total to TotalPrice
                        // Removed DeviceId and SyncStatus as they don't exist in SaleItem
                    });

                    saleTotal += totalPrice;
                }

                sale.TotalAmount = saleTotal;
                sale.TaxAmount = saleTotal * 0.18m; // 18% tax
                // Removed properties that don't exist: Subtotal, AmountReceived, ChangeAmount, Status

                sales.Add(sale);
            }

            await _context.Sales.AddRangeAsync(sales);
            await _context.SaleItems.AddRangeAsync(saleItems);

            await _context.SaveChangesAsync(); // Final save for sales and sale items

            _logger.LogInformation("Comprehensive data seeding completed successfully. Added: {UserCount} users, {BusinessCount} businesses, {ShopCount} shops, {ProductCount} products, {StockCount} stock entries, {CustomerCount} customers, {SupplierCount} suppliers, {SaleCount} sales with {SaleItemCount} items", 
                users.Count, businesses.Count, shops.Count, products.Count, stockEntries.Count, customers.Count, suppliers.Count, sales.Count, saleItems.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while seeding comprehensive initial data");
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

    /// <summary>
    /// Forces a complete re-seed of the database (deletes and recreates database)
    /// </summary>
    public async Task ForceReseedDatabaseAsync()
    {
        try
        {
            _logger.LogInformation("Force re-seeding database - deleting and recreating database...");
            
            // Delete and recreate the database to ensure clean schema
            await _context.Database.EnsureDeletedAsync();
            await _context.Database.EnsureCreatedAsync();
            
            _logger.LogInformation("Database recreated, proceeding with fresh seed data...");
            
            // Temporarily disable foreign key constraints for seeding
            await _context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
            
            try
            {
                // Now seed fresh data
                await SeedInitialDataAsync();
            }
            finally
            {
                // Re-enable foreign key constraints
                await _context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
            }
            
            _logger.LogInformation("Force re-seed completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during force re-seed");
            throw;
        }
    }
}