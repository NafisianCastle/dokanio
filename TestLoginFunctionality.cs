using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Services;
using System;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// Simple test to verify login functionality works after fixing password hashing
/// </summary>
class TestLoginFunctionality
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🔍 Testing Login Functionality...\n");

        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OfflinePOS");
        var connectionString = $"Data Source={Path.Combine(appDataPath, "pos_seed.db")}";

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<PosDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IEncryptionService, EncryptionService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditRepository, AuditRepository>();

        var serviceProvider = services.BuildServiceProvider();

        try
        {
            using var scope = serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

            // Test credentials from the seeded database
            var testCredentials = new[]
            {
                new { Username = "admin", Password = "admin123", Role = "Administrator" },
                new { Username = "manager", Password = "manager123", Role = "ShopManager" },
                new { Username = "cashier", Password = "cashier123", Role = "Cashier" }
            };

            Console.WriteLine("Testing authentication with seeded credentials:\n");

            foreach (var cred in testCredentials)
            {
                Console.Write($"🔐 Testing {cred.Role} login ({cred.Username}/{cred.Password})... ");
                
                try
                {
                    var user = await userService.AuthenticateAsync(cred.Username, cred.Password);
                    
                    if (user != null)
                    {
                        Console.WriteLine($"✅ SUCCESS - Authenticated as {user.FullName} ({user.Role})");
                    }
                    else
                    {
                        Console.WriteLine("❌ FAILED - Authentication returned null");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ ERROR - {ex.Message}");
                }
            }

            // Test invalid credentials
            Console.WriteLine("\n🔍 Testing invalid credentials:");
            Console.Write("🔐 Testing invalid password... ");
            
            try
            {
                var invalidUser = await userService.AuthenticateAsync("admin", "wrongpassword");
                if (invalidUser == null)
                {
                    Console.WriteLine("✅ SUCCESS - Invalid password correctly rejected");
                }
                else
                {
                    Console.WriteLine("❌ FAILED - Invalid password was accepted");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR - {ex.Message}");
            }

            Console.WriteLine("\n🎉 Login functionality test completed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}