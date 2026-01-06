using Shared.Core.DTOs;
using Shared.Core.Entities;

namespace Shared.Core.Services;

/// <summary>
/// Interface for real-time calculation engine that handles immediate updates for sales calculations
/// </summary>
public interface IRealTimeCalculationEngine
{
    /// <summary>
    /// Calculates the total price for a single sale item including all applicable pricing rules
    /// </summary>
    /// <param name="item">The sale item to calculate</param>
    /// <param name="shopConfiguration">Shop-specific configuration for pricing rules</param>
    /// <returns>Detailed calculation result for the line item</returns>
    Task<LineItemCalculationResult> CalculateLineItemAsync(SaleItem item, ShopConfiguration shopConfiguration);
    
    /// <summary>
    /// Calculates all totals for an entire order including subtotal, discounts, taxes, and final total
    /// </summary>
    /// <param name="items">List of sale items in the order</param>
    /// <param name="shopConfiguration">Shop-specific configuration for pricing and tax rules</param>
    /// <param name="customer">Customer for membership-based calculations (optional)</param>
    /// <returns>Complete order calculation with breakdown</returns>
    Task<OrderTotalCalculation> CalculateOrderTotalsAsync(List<SaleItem> items, ShopConfiguration shopConfiguration, Customer? customer = null);
    
    /// <summary>
    /// Applies all applicable discounts to a list of sale items
    /// </summary>
    /// <param name="items">Sale items to apply discounts to</param>
    /// <param name="discounts">Available discounts to consider</param>
    /// <param name="customer">Customer for membership-based discounts (optional)</param>
    /// <returns>Discount calculation result with applied discounts</returns>
    Task<DiscountCalculationResult> ApplyDiscountsAsync(List<SaleItem> items, List<Discount> discounts, Customer? customer = null);
    
    /// <summary>
    /// Calculates taxes for sale items based on shop configuration
    /// </summary>
    /// <param name="items">Sale items to calculate taxes for</param>
    /// <param name="shopConfiguration">Shop configuration containing tax rates and rules</param>
    /// <returns>Tax calculation result with breakdown</returns>
    Task<TaxCalculationResult> CalculateTaxesAsync(List<SaleItem> items, ShopConfiguration shopConfiguration);
    
    /// <summary>
    /// Calculates pricing for weight-based products
    /// </summary>
    /// <param name="product">The weight-based product</param>
    /// <param name="weight">Weight in kilograms</param>
    /// <param name="shopConfiguration">Shop configuration for pricing rules</param>
    /// <returns>Weight-based pricing result</returns>
    Task<WeightBasedPricingResult> CalculateWeightBasedPricingAsync(Product product, decimal weight, ShopConfiguration shopConfiguration);
    
    /// <summary>
    /// Applies complex pricing rules including bulk discounts, tiered pricing, and membership benefits
    /// </summary>
    /// <param name="items">Sale items to apply pricing rules to</param>
    /// <param name="shopConfiguration">Shop configuration with pricing rules</param>
    /// <param name="customer">Customer for membership-based pricing (optional)</param>
    /// <returns>Pricing rules application result</returns>
    Task<PricingRulesResult> ApplyPricingRulesAsync(List<SaleItem> items, ShopConfiguration shopConfiguration, Customer? customer = null);
    
    /// <summary>
    /// Validates that all calculations are within acceptable business rules and limits
    /// </summary>
    /// <param name="calculation">The calculation result to validate</param>
    /// <param name="shopConfiguration">Shop configuration with validation rules</param>
    /// <returns>Validation result with any errors or warnings</returns>
    Task<CalculationValidationResult> ValidateCalculationAsync(OrderTotalCalculation calculation, ShopConfiguration shopConfiguration);
    
    /// <summary>
    /// Recalculates all totals when a single item is modified (quantity, weight, discount, etc.)
    /// </summary>
    /// <param name="modifiedItem">The item that was modified</param>
    /// <param name="allItems">All items in the current sale</param>
    /// <param name="shopConfiguration">Shop configuration for calculations</param>
    /// <param name="customer">Customer for membership calculations (optional)</param>
    /// <returns>Updated order calculation reflecting the changes</returns>
    Task<OrderTotalCalculation> RecalculateOnItemChangeAsync(SaleItem modifiedItem, List<SaleItem> allItems, ShopConfiguration shopConfiguration, Customer? customer = null);
}