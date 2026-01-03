using System.ComponentModel.DataAnnotations;

namespace Shared.Core.Entities;

public class SaleItem : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid SaleId { get; set; }
    
    public Guid ProductId { get; set; }
    
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }
    
    [MaxLength(50)]
    public string? BatchNumber { get; set; }
    
    // Soft delete properties
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    
    // Navigation properties
    public virtual Sale Sale { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}