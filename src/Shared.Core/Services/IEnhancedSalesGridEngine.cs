using Shared.Core.Entities;
using Shared.Core.DTOs;

namespace Shared.Core.Services;

/// <summary>
/// Enhanced sales grid engine for real-time calculations and inline editing
/// </summary>
public interface IEnhancedSalesGridEngine
{
    /// <summary>
    /// Add a product to the sales grid with default quantity
    /// </summary>
    Task<GridOperationResult> AddProductToGridAsync(Guid saleSessionId, Product product, decimal quantity = 1);
    
    /// <summary>
    /// Add a weight-based product to the sales grid
    /// </summary>
    Task<GridOperationResult> AddWeightBasedProductToGridAsync(Guid saleSessionId, Product product, decimal weight);
    
    /// <summary>
    /// Update the quantity of a specific line item
    /// </summary>
    Task<GridOperationResult> UpdateQuantityAsync(Guid saleSessionId, Guid saleItemId, decimal newQuantity);
    
    /// <summary>
    /// Update the weight of a weight-based line item
    /// </summary>
    Task<GridOperationResult> UpdateWeightAsync(Guid saleSessionId, Guid saleItemId, decimal newWeight);
    
    /// <summary>
    /// Update the discount amount for a specific line item
    /// </summary>
    Task<GridOperationResult> UpdateDiscountAsync(Guid saleSessionId, Guid saleItemId, decimal discountAmount);
    
    /// <summary>
    /// Remove an item from the sales grid
    /// </summary>
    Task<GridOperationResult> RemoveItemAsync(Guid saleSessionId, Guid saleItemId);
    
    /// <summary>
    /// Recalculate totals for a specific line item
    /// </summary>
    Task<GridCalculationResult> RecalculateLineItemAsync(Guid saleSessionId, Guid saleItemId);
    
    /// <summary>
    /// Recalculate all totals for the entire sales grid
    /// </summary>
    Task<GridCalculationResult> RecalculateAllTotalsAsync(Guid saleSessionId);
    
    /// <summary>
    /// Validate all data in the sales grid
    /// </summary>
    Task<GridValidationResult> ValidateGridDataAsync(Guid saleSessionId);
    
    /// <summary>
    /// Get the current state of the sales grid
    /// </summary>
    Task<SalesGridState> GetGridStateAsync(Guid saleSessionId);
    
    /// <summary>
    /// Clear all items from the sales grid
    /// </summary>
    Task<GridOperationResult> ClearGridAsync(Guid saleSessionId);
    
    /// <summary>
    /// Apply a discount to the entire sale
    /// </summary>
    Task<GridOperationResult> ApplySaleDiscountAsync(Guid saleSessionId, decimal discountAmount, string discountReason);
    
    /// <summary>
    /// Remove a sale-level discount
    /// </summary>
    Task<GridOperationResult> RemoveSaleDiscountAsync(Guid saleSessionId);
}