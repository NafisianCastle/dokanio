using Shared.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Shared.Core.Entities;

public class Product : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// The shop this product belongs to
    /// </summary>
    [Required]
    public Guid ShopId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? Barcode { get; set; }
    
    [MaxLength(100)]
    public string? Category { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }
    
    /// <summary>
    /// Business type-specific attributes (stored as JSON)
    /// </summary>
    public string? BusinessTypeAttributesJson { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public Guid DeviceId { get; set; }
    
    public DateTime? ServerSyncedAt { get; set; }
    
    public SyncStatus SyncStatus { get; set; } = SyncStatus.NotSynced;
    
    // Medicine-specific properties (legacy - will be moved to BusinessTypeAttributes)
    [MaxLength(50)]
    public string? BatchNumber { get; set; }
    
    public DateTime? ExpiryDate { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal? PurchasePrice { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal? SellingPrice { get; set; }
    
    // Weight-based pricing properties (legacy - will be moved to BusinessTypeAttributes)
    public bool IsWeightBased { get; set; } = false;
    
    [Range(0, double.MaxValue)]
    public decimal? RatePerKilogram { get; set; }
    
    [Range(0, 6)]
    public int WeightPrecision { get; set; } = 3; // Decimal places for weight (default 3 for grams precision)
    
    // Soft delete properties
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    
    // Navigation properties
    public virtual Shop Shop { get; set; } = null!;
    public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    public virtual ICollection<Stock> StockEntries { get; set; } = new List<Stock>();
}