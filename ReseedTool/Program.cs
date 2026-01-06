using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Shared.Core.Data;
using Shared.Core.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

Console.WriteLine("🔄 Force re-seeding database with comprehensive test data...");

var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OfflinePOS");
Directory.CreateDirectory(appDataPath);
var connectionString = $"Data Source={Path.Combine(appDataPath, "pos_seed.db")}";

var services = new ServiceCollection();

// Add logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
});

// Add database context
services.AddDbContext<PosDbContext>(options =>
    options.UseSqlite(connectionString));

// Add encryption service
services.AddScoped<IEncryptionService, EncryptionService>();

// Add migration service
services.AddScoped<IDatabaseMigrationService, DatabaseMigrationService>();

var serviceProvider = services.BuildServiceProvider();

try
{
    using var scope = serviceProvider.CreateScope();
    var migrationService = scope.ServiceProvider.GetRequiredService<IDatabaseMigrationService>();
    
    // Ensure database exists
    await migrationService.EnsureDatabaseCreatedAsync();
    
    // Force re-seed with comprehensive data
    await migrationService.ForceReseedDatabaseAsync();
    
    // Verify the data
    var context = scope.ServiceProvider.GetRequiredService<PosDbContext>();
    
    var userCount = await context.Users.CountAsync();
    var businessCount = await context.Businesses.CountAsync();
    var shopCount = await context.Shops.CountAsync();
    var productCount = await context.Products.CountAsync();
    var stockCount = await context.Stock.CountAsync();
    var customerCount = await context.Customers.CountAsync();
    var supplierCount = await context.Suppliers.CountAsync();
    var salesCount = await context.Sales.CountAsync();
    var saleItemCount = await context.SaleItems.CountAsync();
    
    Console.WriteLine("\n✅ Database re-seeded successfully!");
    Console.WriteLine($"📊 Data Summary:");
    Console.WriteLine($"   👥 Users: {userCount}");
    Console.WriteLine($"   🏢 Businesses: {businessCount}");
    Console.WriteLine($"   🏪 Shops: {shopCount}");
    Console.WriteLine($"   📦 Products: {productCount}");
    Console.WriteLine($"   📋 Stock entries: {stockCount}");
    Console.WriteLine($"   👤 Customers: {customerCount}");
    Console.WriteLine($"   🚚 Suppliers: {supplierCount}");
    Console.WriteLine($"   🧾 Sales: {salesCount}");
    Console.WriteLine($"   📝 Sale items: {saleItemCount}");
    
    // Show login credentials
    Console.WriteLine("\n🔐 Login Credentials:");
    var users = await context.Users.Select(u => new { u.Username, u.FullName, u.Role }).ToListAsync();
    foreach (var user in users)
    {
        var password = user.Username + "123"; // Based on our seed data pattern
        Console.WriteLine($"   {user.Role}: {user.Username} / {password}");
    }
    
    Console.WriteLine("\n🎉 Ready for testing! You can now login to the POS application.");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    Environment.Exit(1);
}