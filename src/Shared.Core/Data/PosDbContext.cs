using Microsoft.EntityFrameworkCore;
using Shared.Core.Entities;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Product configuration
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Barcode).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Barcode).HasMaxLength(50);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.BatchNumber).HasMaxLength(50);
            entity.Property(e => e.UnitPrice).HasPrecision(10, 2);
            entity.Property(e => e.PurchasePrice).HasPrecision(10, 2);
            entity.Property(e => e.SellingPrice).HasPrecision(10, 2);
        });

        // Sale configuration
        modelBuilder.Entity<Sale>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
            entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TotalAmount).HasPrecision(10, 2);
        });

        // SaleItem configuration
        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UnitPrice).HasPrecision(10, 2);
            entity.Property(e => e.BatchNumber).HasMaxLength(50);
            
            // Foreign key relationships
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
            
            // Foreign key relationship
            entity.HasOne(e => e.Product)
                  .WithMany(p => p.StockEntries)
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}