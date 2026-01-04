using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Entities;
using Shared.Core.Services;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared.Core.Data;

public class PosDbContext : DbContext
{
    private readonly ILogger<PosDbContext>? _logger;

    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Sale> Sales { get; set; } = null!;
    public DbSet<SaleItem> SaleItems { get; set; } = null!;
    public DbSet<Stock> Stock { get; set; } = null!;
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<Discount> Discounts { get; set; } = null!;
    public DbSet<SaleDiscount> SaleDiscounts { get; set; } = null!;
    public DbSet<TransactionLogEntry> TransactionLogs { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<UserSession> UserSessions { get; set; } = null!;
    public DbSet<SystemLogEntry> SystemLogs { get; set; } = null!;

    public PosDbContext(DbContextOptions<PosDbContext> options) : base(options)
    {
    }

    public PosDbContext(DbContextOptions<PosDbContext> options, ILogger<PosDbContext> logger) : base(options)
    {
        _logger = logger;
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
        ConfigureSoftDelete<Customer>(modelBuilder);
        ConfigureSoftDelete<Discount>(modelBuilder);
        ConfigureSoftDelete<User>(modelBuilder);

        // TransactionLogEntry configuration (not soft deletable)
        modelBuilder.Entity<TransactionLogEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IsProcessed);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.EntityType);
            entity.HasIndex(e => e.EntityId);
            
            entity.Property(e => e.Operation).IsRequired().HasMaxLength(20);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityData).IsRequired();
        });

        // Product configuration
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Barcode).IsUnique();
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.ExpiryDate);
            entity.HasIndex(e => e.SyncStatus);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.IsWeightBased);
            
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Barcode).HasMaxLength(50);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.BatchNumber).HasMaxLength(50);
            entity.Property(e => e.UnitPrice).HasPrecision(10, 2);
            entity.Property(e => e.PurchasePrice).HasPrecision(10, 2);
            entity.Property(e => e.SellingPrice).HasPrecision(10, 2);
            entity.Property(e => e.RatePerKilogram).HasPrecision(10, 2);
            
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
            entity.HasIndex(e => e.CustomerId);
            
            entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TotalAmount).HasPrecision(10, 2);
            entity.Property(e => e.DiscountAmount).HasPrecision(10, 2);
            entity.Property(e => e.TaxAmount).HasPrecision(10, 2);
            entity.Property(e => e.MembershipDiscountAmount).HasPrecision(10, 2);
            
            // Convert enums to integers for SQLite
            entity.Property(e => e.PaymentMethod).HasConversion<int>();
            entity.Property(e => e.SyncStatus).HasConversion<int>();
            
            // Foreign key relationship with Customer
            entity.HasOne(e => e.Customer)
                  .WithMany(c => c.Sales)
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // SaleItem configuration
        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SaleId);
            entity.HasIndex(e => e.ProductId);
            
            entity.Property(e => e.UnitPrice).HasPrecision(10, 2);
            entity.Property(e => e.TotalPrice).HasPrecision(10, 2);
            entity.Property(e => e.Weight).HasPrecision(10, 3);
            entity.Property(e => e.RatePerKilogram).HasPrecision(10, 2);
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

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Role);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.SyncStatus);
            entity.HasIndex(e => e.DeviceId);
            
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Salt).IsRequired();
            
            // Convert enums to integers for SQLite
            entity.Property(e => e.Role).HasConversion<int>();
            entity.Property(e => e.SyncStatus).HasConversion<int>();
        });

        // Customer configuration
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MembershipNumber).IsUnique();
            entity.HasIndex(e => e.Tier);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.JoinDate);
            entity.HasIndex(e => e.TotalSpent);
            entity.HasIndex(e => e.SyncStatus);
            entity.HasIndex(e => e.DeviceId);
            
            entity.Property(e => e.MembershipNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.TotalSpent).HasPrecision(10, 2);
            
            // Convert enums to integers for SQLite
            entity.Property(e => e.Tier).HasConversion<int>();
            entity.Property(e => e.SyncStatus).HasConversion<int>();
        });

        // Discount configuration
        modelBuilder.Entity<Discount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.Scope);
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.RequiredMembershipTier);
            entity.HasIndex(e => e.StartDate);
            entity.HasIndex(e => e.EndDate);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.SyncStatus);
            entity.HasIndex(e => e.DeviceId);
            
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Value).HasPrecision(10, 2);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.MinimumAmount).HasPrecision(10, 2);
            
            // Convert enums to integers for SQLite
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Scope).HasConversion<int>();
            entity.Property(e => e.RequiredMembershipTier).HasConversion<int>();
            entity.Property(e => e.SyncStatus).HasConversion<int>();
            
            // Foreign key relationship with Product
            entity.HasOne(e => e.Product)
                  .WithMany()
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // SaleDiscount configuration
        modelBuilder.Entity<SaleDiscount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SaleId);
            entity.HasIndex(e => e.DiscountId);
            entity.HasIndex(e => e.AppliedAt);
            
            entity.Property(e => e.DiscountAmount).HasPrecision(10, 2);
            entity.Property(e => e.DiscountReason).IsRequired().HasMaxLength(200);
            
            // Foreign key relationships
            entity.HasOne(e => e.Sale)
                  .WithMany(s => s.AppliedDiscounts)
                  .HasForeignKey(e => e.SaleId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(e => e.Discount)
                  .WithMany(d => d.SaleDiscounts)
                  .HasForeignKey(e => e.DiscountId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // AuditLog configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.EntityType);
            entity.HasIndex(e => e.EntityId);
            entity.HasIndex(e => e.SyncStatus);
            entity.HasIndex(e => e.DeviceId);
            
            entity.Property(e => e.Username).HasMaxLength(100);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(200);
            entity.Property(e => e.EntityType).HasMaxLength(50);
            entity.Property(e => e.OldValues).HasMaxLength(1000);
            entity.Property(e => e.NewValues).HasMaxLength(1000);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            
            // Convert enums to integers for SQLite
            entity.Property(e => e.Action).HasConversion<int>();
            entity.Property(e => e.SyncStatus).HasConversion<int>();
            
            // Foreign key relationship
            entity.HasOne(e => e.User)
                  .WithMany(u => u.AuditLogs)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // UserSession configuration
        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.SessionToken).IsUnique();
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.LastActivityAt);
            entity.HasIndex(e => e.DeviceId);
            
            entity.Property(e => e.SessionToken).IsRequired();
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            
            // Foreign key relationship
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // SystemLogEntry configuration (not soft deletable)
        modelBuilder.Entity<SystemLogEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Level);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.UserId);
            
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.ExceptionDetails).HasMaxLength(4000);
            entity.Property(e => e.AdditionalData).HasMaxLength(2000);
            
            // Convert enums to integers for SQLite
            entity.Property(e => e.Level).HasConversion<int>();
            entity.Property(e => e.Category).HasConversion<int>();
        });
    }

    private void ConfigureSoftDelete<T>(ModelBuilder modelBuilder) where T : class, ISoftDeletable
    {
        modelBuilder.Entity<T>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<T>().HasIndex(e => e.IsDeleted);
    }

    public override int SaveChanges()
    {
        LogTransactions();
        HandleSoftDelete();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await LogTransactionsAsync();
        HandleSoftDelete();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void LogTransactions()
    {
        LogTransactionsAsync().GetAwaiter().GetResult();
    }

    private async Task LogTransactionsAsync()
    {
        try
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || 
                           e.State == EntityState.Modified || 
                           e.State == EntityState.Deleted)
                .Where(e => e.Entity is not TransactionLogEntry && e.Entity is not SystemLogEntry) // Don't log transaction log entries or system log entries themselves
                .ToList();

            var transactionLogs = new List<TransactionLogEntry>();

            foreach (var entry in entries)
            {
                var operation = entry.State switch
                {
                    EntityState.Added => "INSERT",
                    EntityState.Modified => "UPDATE",
                    EntityState.Deleted => "DELETE",
                    _ => "UNKNOWN"
                };

                var entityType = entry.Entity.GetType().Name;
                var entityId = GetEntityId(entry.Entity);
                var entityData = JsonSerializer.Serialize(entry.Entity, new JsonSerializerOptions
                {
                    ReferenceHandler = ReferenceHandler.IgnoreCycles,
                    WriteIndented = false
                });
                var deviceId = GetDeviceId(entry.Entity);

                var logEntry = new TransactionLogEntry
                {
                    Operation = operation,
                    EntityType = entityType,
                    EntityId = entityId,
                    EntityData = entityData,
                    DeviceId = deviceId,
                    CreatedAt = DateTime.UtcNow,
                    IsProcessed = false
                };

                transactionLogs.Add(logEntry);
            }

            if (transactionLogs.Any())
            {
                _logger?.LogDebug("Logging {Count} transactions for durability", transactionLogs.Count);
                TransactionLogs.AddRange(transactionLogs);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error logging transactions for durability");
            // Don't throw - allow the main operation to continue
        }
    }

    private Guid GetEntityId(object entity)
    {
        // Use reflection to get the Id property
        var idProperty = entity.GetType().GetProperty("Id");
        if (idProperty != null && idProperty.PropertyType == typeof(Guid))
        {
            return (Guid)(idProperty.GetValue(entity) ?? Guid.Empty);
        }
        return Guid.Empty;
    }

    private Guid GetDeviceId(object entity)
    {
        // Use reflection to get the DeviceId property if it exists
        var deviceIdProperty = entity.GetType().GetProperty("DeviceId");
        if (deviceIdProperty != null && deviceIdProperty.PropertyType == typeof(Guid))
        {
            return (Guid)(deviceIdProperty.GetValue(entity) ?? Guid.Empty);
        }
        return Guid.Empty;
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