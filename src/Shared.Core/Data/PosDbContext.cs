using Microsoft.EntityFrameworkCore;
using Shared.Core.Entities;
using System.Linq.Expressions;

namespace Shared.Core.Data;

public class PosDbContext : DbContext
{
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Sale> Sales { get; set; } = null!;
    public DbSet<SaleItem> SaleItems { get; set; } = null!;
    public DbSet<Stock> Stock { get; set; } = null!;

    public PosDbContext(DbContextOptions<PosDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Default configuration for SQLite with WAL mode
            optionsBuilder.UseSqlite("Data Source=pos.db", options =>
            {
                options.CommandTimeout(30);
            });
        }
        
        // Enable sensitive data logging only in development
        optionsBuilder.EnableSensitiveDataLogging(false);
        optionsBuilder.EnableDetailedErrors(true);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure soft delete for all entities
        ConfigureSoftDelete<Product>(modelBuilder);
        ConfigureSoftDelete<Sale>(modelBuilder);
        ConfigureSoftDelete<SaleItem>(modelBuilder);
        ConfigureSoftDelete<Stock>(modelBuilder);

        // Product configuration
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Barcode).IsUnique();
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.ExpiryDate);
            entity.HasIndex(e => e.SyncStatus);
            entity.HasIndex(e => e.DeviceId);
            
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Barcode).HasMaxLength(50);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.BatchNumber).HasMaxLength(50);
            entity.Property(e => e.UnitPrice).HasPrecision(10, 2);
            entity.Property(e => e.PurchasePrice).HasPrecision(10, 2);
            entity.Property(e => e.SellingPrice).HasPrecision(10, 2);
            
            // Convert enums to integers for SQLite
            entity.Property(e => e.SyncStatus).HasConversion<int>();
        });

        // Sale configuration
        modelBuilder.Entity<Sale>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.SyncStatus);
            entity.HasIndex(e => e.DeviceId);
            
            entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TotalAmount).HasPrecision(10, 2);
            
            // Convert enums to integers for SQLite
            entity.Property(e => e.PaymentMethod).HasConversion<int>();
            entity.Property(e => e.SyncStatus).HasConversion<int>();
        });

        // SaleItem configuration
        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SaleId);
            entity.HasIndex(e => e.ProductId);
            
            entity.Property(e => e.UnitPrice).HasPrecision(10, 2);
            entity.Property(e => e.BatchNumber).HasMaxLength(50);
            
            // Foreign key relationships with proper constraints
            entity.HasOne(e => e.Sale)
                  .WithMany(s => s.Items)
                  .HasForeignKey(e => e.SaleId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(e => e.Product)
                  .WithMany(p => p.SaleItems)
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Stock configuration
        modelBuilder.Entity<Stock>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.SyncStatus);
            entity.HasIndex(e => e.DeviceId);
            
            // Convert enums to integers for SQLite
            entity.Property(e => e.SyncStatus).HasConversion<int>();
            
            // Foreign key relationship
            entity.HasOne(e => e.Product)
                  .WithMany(p => p.StockEntries)
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private void ConfigureSoftDelete<T>(ModelBuilder modelBuilder) where T : class, ISoftDeletable
    {
        modelBuilder.Entity<T>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<T>().HasIndex(e => e.IsDeleted);
    }

    public override int SaveChanges()
    {
        HandleSoftDelete();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        HandleSoftDelete();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void HandleSoftDelete()
    {
        var deletedEntries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Deleted && e.Entity is ISoftDeletable)
            .ToList();

        foreach (var entry in deletedEntries)
        {
            entry.State = EntityState.Modified;
            var entity = (ISoftDeletable)entry.Entity;
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
        }
    }
}