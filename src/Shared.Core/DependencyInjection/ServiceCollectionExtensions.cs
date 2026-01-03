using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Core.Data;
using Shared.Core.Repositories;
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

        return services;
    }
}