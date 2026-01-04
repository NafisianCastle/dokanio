using System.ComponentModel.DataAnnotations;

namespace Shared.Core.Entities;

/// <summary>
/// Represents business type-specific attributes for products
/// </summary>
public class BusinessTypeAttributes
{
    /// <summary>
    /// Expiry date for pharmacy products
    /// </summary>
    public DateTime? ExpiryDate { get; set; }
    
    /// <summary>
    /// Weight for grocery products (in kilograms)
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? Weight { get; set; }
    
    /// <summary>
    /// Volume for grocery products (e.g., "500ml", "2L")
    /// </summary>
    [MaxLength(50)]
    public string? Volume { get; set; }
    
    /// <summary>
    /// Manufacturer for pharmacy products
    /// </summary>
    [MaxLength(200)]
    public string? Manufacturer { get; set; }
    
    /// <summary>
    /// Batch number for pharmacy products
    /// </summary>
    [MaxLength(50)]
    public string? BatchNumber { get; set; }
    
    /// <summary>
    /// Generic name for pharmacy products
    /// </summary>
    [MaxLength(200)]
    public string? GenericName { get; set; }
    
    /// <summary>
    /// Dosage information for pharmacy products
    /// </summary>
    [MaxLength(100)]
    public string? Dosage { get; set; }
    
    /// <summary>
    /// Unit of measurement for grocery products (e.g., "kg", "piece", "liter")
    /// </summary>
    [MaxLength(20)]
    public string? Unit { get; set; }
    
    /// <summary>
    /// Custom attributes as JSON for extensibility
    /// </summary>
    public string? CustomAttributes { get; set; }
    
    /// <summary>
    /// Indicates if the product requires special handling (e.g., refrigeration)
    /// </summary>
    public bool RequiresSpecialHandling { get; set; } = false;
    
    /// <summary>
    /// Special handling instructions
    /// </summary>
    [MaxLength(500)]
    public string? SpecialHandlingInstructions { get; set; }
}