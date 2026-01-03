using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Shared.Core.Data;

public class PosDbContextFactory : IDesignTimeDbContextFactory<PosDbContext>
{
    public PosDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        
        // Use a default connection string for migrations
        optionsBuilder.UseSqlite("Data Source=pos.db", options =>
        {
            options.CommandTimeout(30);
        });

        return new PosDbContext(optionsBuilder.Options);
    }
}