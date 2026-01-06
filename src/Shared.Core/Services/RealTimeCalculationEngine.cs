using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using System.Globalization;

namespace Shared.Core.Services;

/// <summary>
/// Real-time calculation engine that provides immediate updates for sales calculations
/// </summary>
public class RealTimeCalculationEngine : IRealTimeCalculationEngine
{
    private readonly IDiscountService _discountService;
    private readonly IWeightBasedPricingService _weightBasedPricingService;
    private readonly ILogger<RealTimeCalculationEngine> _logger;

    public RealTimeCalculationEngine(
        IDiscountService discountService,
        IWeightBasedPricingService weightBasedPricingService,
        ILogger<RealTimeCalculationEngine> logger)
    {
        _discountService = discountService;
        _weightBasedPricingService = weightBasedPricingService;
        _logger = logger;
    }

    public async Task<LineItemCalculationResult> CalculateLineItemAsync(SaleItem item, ShopConfiguration shopConfiguration)
    {
        _logger.LogDebug("Calculating line item for product {ProductId}, quantity {Quantity}", 
            item.ProductId, item.Quantity);

        var result = new LineItemCalculationResult
        {
            SaleItemId = item.Id,
            Quantity = item.Quantity,
            Weight = item.Weight
        };

        try
        {
            // Calculate base price based on product type
            if (item.Product?.IsWeightBased == true && item.Weight.HasValue)
            {
                var weightPricing = await CalculateWeightBasedPricingAsync(item.Product, item.Weight.Value, shopConfiguration);
                result.BasePrice = weightPricing.AdjustedPrice;
                result.UnitPrice = item.Product.RatePerKilogram ?? 0;
                result.LineSubtotal = weightPricing.AdjustedPrice;
                result.CalculationNotes.Add($"Weight-based pricing: {weightPricing.FormattedWeight} × {weightPricing.FormattedRate}");
            }
            else
            {
                result.BasePrice = item.UnitPrice;
                result.UnitPrice = item.UnitPrice;
                result.LineSubtotal = item.UnitPrice * item.Quantity;
            }

            // Apply any item-level discounts (will be handled by order-level calculation)
            result.DiscountAmount = 0;
            result.TaxAmount = 0;
            result.LineTotal = result.LineSubtotal;

            result.CalculatedAt = DateTime.UtcNow;
            
            _logger.LogDebug("Line item calculation completed: Subtotal {Subtotal}, Total {Total}", 
                result.LineSubtotal, result.LineTotal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating line item for product {ProductId}", item.ProductId);
            result.CalculationNotes.Add($"Calculation error: {ex.Message}");
        }

        return result;
    }

    public async Task<OrderTotalCalculation> CalculateOrderTotalsAsync(List<SaleItem> items, ShopConfiguration shopConfiguration, Customer? customer = null)
    {
        _logger.LogDebug("Calculating order totals for {ItemCount} items", items.Count);

        var result = new OrderTotalCalculation
        {
            TotalItems = items.Count,
            TotalQuantity = items.Sum(i => i.Quantity),
            TotalWeight = items.Where(i => i.Weight.HasValue).Sum(i => i.Weight)
        };

        try
        {
            // Calculate line items
            foreach (var item in items)
            {
                var lineResult = await CalculateLineItemAsync(item, shopConfiguration);
                result.LineItems.Add(lineResult);
            }

            // Calculate subtotal
            result.Subtotal = result.LineItems.Sum(li => li.LineSubtotal);

            // Apply discounts if available
            var availableDiscounts = new List<Discount>(); // Would be retrieved from discount service
            var discountResult = await ApplyDiscountsAsync(items, availableDiscounts, customer);
            result.TotalDiscountAmount = discountResult.TotalDiscountAmount;
            result.OrderLevelDiscounts = discountResult.AppliedDiscounts;

            // Calculate taxes
            var taxResult = await CalculateTaxesAsync(items, shopConfiguration);
            result.TotalTaxAmount = taxResult.TotalTaxAmount;
            result.OrderLevelTaxes = taxResult.AppliedTaxes;

            // Apply pricing rules
            var pricingRulesResult = await ApplyPricingRulesAsync(items, shopConfiguration, customer);
            result.AppliedPricingRules = pricingRulesResult.AppliedRules;

            // Calculate final total
            result.FinalTotal = result.Subtotal - result.TotalDiscountAmount + result.TotalTaxAmount;

            // Create breakdown
            result.Breakdown = CreateCalculationBreakdown(result);

            // Validate calculation
            var validation = await ValidateCalculationAsync(result, shopConfiguration);
            result.IsValid = validation.IsValid;
            result.ValidationMessages = validation.Errors.Select(e => e.Message).ToList();

            result.CalculatedAt = DateTime.UtcNow;

            _logger.LogDebug("Order calculation completed: Subtotal {Subtotal}, Discount {Discount}, Tax {Tax}, Total {Total}", 
                result.Subtotal, result.TotalDiscountAmount, result.TotalTaxAmount, result.FinalTotal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating order totals");
            result.IsValid = false;
            result.ValidationMessages.Add($"Calculation error: {ex.Message}");
        }

        return result;
    }

    public async Task<DiscountCalculationResult> ApplyDiscountsAsync(List<SaleItem> items, List<Discount> discounts, Customer? customer = null)
    {
        _logger.LogDebug("Applying discounts to {ItemCount} items with {DiscountCount} available discounts", 
            items.Count, discounts.Count);

        var result = new DiscountCalculationResult();

        try
        {
            foreach (var item in items)
            {
                if (item.Product == null) continue;

                var applicableDiscounts = await _discountService.GetApplicableDiscountsAsync(
                    item.Product, customer, DateTime.UtcNow);

                foreach (var discount in applicableDiscounts.Where(d => discounts.Contains(d)))
                {
                    var discountAmount = await _discountService.CalculateDiscountAmountAsync(
                        discount, item.TotalPrice, item.Quantity);

                    if (discountAmount > 0)
                    {
                        var appliedDiscount = new AppliedDiscount
                        {
                            DiscountId = discount.Id,
                            DiscountName = discount.Name,
                            Type = discount.Type,
                            Value = discount.Value,
                            CalculatedAmount = discountAmount,
                            Reason = GenerateDiscountReason(discount, item, customer)
                        };

                        result.AppliedDiscounts.Add(appliedDiscount);
                        result.TotalDiscountAmount += discountAmount;
                        result.DiscountReasons.Add(appliedDiscount.Reason);
                    }
                }
            }

            _logger.LogDebug("Discount calculation completed: Total discount {TotalDiscount}", 
                result.TotalDiscountAmount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying discounts");
        }

        return result;
    }

    public async Task<TaxCalculationResult> CalculateTaxesAsync(List<SaleItem> items, ShopConfiguration shopConfiguration)
    {
        _logger.LogDebug("Calculating taxes for {ItemCount} items with tax rate {TaxRate}", 
            items.Count, shopConfiguration.TaxRate);

        var result = new TaxCalculationResult
        {
            TaxIncludedInPrice = false // Assuming tax is added on top
        };

        try
        {
            var taxableAmount = items.Sum(i => i.TotalPrice);
            var taxAmount = Math.Round(taxableAmount * shopConfiguration.TaxRate, 2, MidpointRounding.AwayFromZero);

            if (taxAmount > 0)
            {
                var appliedTax = new AppliedTax
                {
                    TaxName = "Sales Tax",
                    TaxRate = shopConfiguration.TaxRate,
                    TaxableAmount = taxableAmount,
                    TaxAmount = taxAmount,
                    Description = $"Sales tax at {shopConfiguration.TaxRate:P2}"
                };

                result.AppliedTaxes.Add(appliedTax);
                result.TotalTaxAmount = taxAmount;

                var breakdown = new TaxBreakdown
                {
                    Category = "General",
                    TaxableAmount = taxableAmount,
                    TaxRate = shopConfiguration.TaxRate,
                    TaxAmount = taxAmount,
                    ApplicableItemIds = items.Select(i => i.Id).ToList()
                };

                result.TaxBreakdowns.Add(breakdown);
            }

            result.CalculatedAt = DateTime.UtcNow;

            _logger.LogDebug("Tax calculation completed: Taxable amount {TaxableAmount}, Tax amount {TaxAmount}", 
                taxableAmount, result.TotalTaxAmount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating taxes");
        }

        return result;
    }

    public async Task<WeightBasedPricingResult> CalculateWeightBasedPricingAsync(Product product, decimal weight, ShopConfiguration shopConfiguration)
    {
        _logger.LogDebug("Calculating weight-based pricing for product {ProductId}, weight {Weight}", 
            product.Id, weight);

        var result = new WeightBasedPricingResult
        {
            Weight = weight,
            RatePerKilogram = product.RatePerKilogram ?? 0
        };

        try
        {
            var pricingResult = await _weightBasedPricingService.GetPricingDetailsAsync(product, weight);
            
            result.BasePrice = pricingResult.TotalPrice;
            result.AdjustedPrice = pricingResult.TotalPrice;
            result.FormattedWeight = pricingResult.FormattedWeight;
            result.FormattedRate = pricingResult.FormattedRate;
            result.FormattedPrice = pricingResult.FormattedPrice;
            result.IsValid = pricingResult.IsValid;
            result.ValidationErrors = pricingResult.ValidationErrors;

            // Apply any shop-specific pricing adjustments
            if (shopConfiguration.PricingRules.EnableDynamicPricing)
            {
                // Apply dynamic pricing adjustments (placeholder for future implementation)
                var adjustment = new PricingAdjustment
                {
                    AdjustmentType = "Dynamic Pricing",
                    AdjustmentAmount = 0,
                    Reason = "No dynamic pricing rules configured",
                    OriginalValue = result.BasePrice,
                    AdjustedValue = result.BasePrice
                };
                result.PricingAdjustments.Add(adjustment);
            }

            _logger.LogDebug("Weight-based pricing calculation completed: {Weight} × {Rate} = {Price}", 
                weight, result.RatePerKilogram, result.AdjustedPrice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating weight-based pricing for product {ProductId}", product.Id);
            result.IsValid = false;
            result.ValidationErrors.Add($"Calculation error: {ex.Message}");
        }

        return result;
    }

    public async Task<PricingRulesResult> ApplyPricingRulesAsync(List<SaleItem> items, ShopConfiguration shopConfiguration, Customer? customer = null)
    {
        _logger.LogDebug("Applying pricing rules to {ItemCount} items", items.Count);

        var result = new PricingRulesResult
        {
            OriginalTotal = items.Sum(i => i.TotalPrice),
            HasMembershipBenefits = customer?.Tier != null && customer.Tier != MembershipTier.None
        };

        try
        {
            result.AdjustedTotal = result.OriginalTotal;

            // Apply membership-based pricing
            if (result.HasMembershipBenefits && customer != null)
            {
                var membershipDiscount = CalculateMembershipDiscount(items, customer, shopConfiguration);
                if (membershipDiscount > 0)
                {
                    var membershipRule = new PricingRuleApplication
                    {
                        RuleName = "Membership Discount",
                        RuleType = "Membership",
                        AdjustmentAmount = -membershipDiscount,
                        Description = $"{customer.Tier} member discount",
                        AffectedItemIds = items.Select(i => i.Id).ToList()
                    };
                    result.AppliedRules.Add(membershipRule);
                    result.AdjustedTotal -= membershipDiscount;
                }
            }

            // Apply bulk pricing if enabled
            if (shopConfiguration.PricingRules.EnableTieredPricing)
            {
                var bulkDiscount = CalculateBulkPricingDiscount(items, shopConfiguration);
                if (bulkDiscount > 0)
                {
                    var bulkRule = new PricingRuleApplication
                    {
                        RuleName = "Bulk Pricing",
                        RuleType = "Bulk",
                        AdjustmentAmount = -bulkDiscount,
                        Description = "Bulk quantity discount",
                        AffectedItemIds = items.Select(i => i.Id).ToList()
                    };
                    result.AppliedRules.Add(bulkRule);
                    result.AdjustedTotal -= bulkDiscount;
                }
            }

            result.TotalAdjustment = result.OriginalTotal - result.AdjustedTotal;
            result.RuleDescriptions = result.AppliedRules.Select(r => r.Description).ToList();
            result.CalculatedAt = DateTime.UtcNow;

            _logger.LogDebug("Pricing rules applied: Original {Original}, Adjusted {Adjusted}, Adjustment {Adjustment}", 
                result.OriginalTotal, result.AdjustedTotal, result.TotalAdjustment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying pricing rules");
        }

        return result;
    }

    public async Task<CalculationValidationResult> ValidateCalculationAsync(OrderTotalCalculation calculation, ShopConfiguration shopConfiguration)
    {
        _logger.LogDebug("Validating calculation with final total {FinalTotal}", calculation.FinalTotal);

        var result = new CalculationValidationResult();

        try
        {
            // Validate totals are non-negative
            if (calculation.FinalTotal < 0)
            {
                result.Errors.Add(new ValidationError
                {
                    Code = "NEGATIVE_TOTAL",
                    Message = "Final total cannot be negative",
                    Field = "FinalTotal",
                    Value = calculation.FinalTotal,
                    Severity = ValidationSeverity.Error
                });
            }

            // Validate discount limits
            if (calculation.TotalDiscountAmount > calculation.Subtotal)
            {
                result.Errors.Add(new ValidationError
                {
                    Code = "EXCESSIVE_DISCOUNT",
                    Message = "Total discount cannot exceed subtotal",
                    Field = "TotalDiscountAmount",
                    Value = calculation.TotalDiscountAmount,
                    Severity = ValidationSeverity.Error
                });
            }

            // Validate maximum discount percentage
            var discountPercentage = calculation.Subtotal > 0 ? (calculation.TotalDiscountAmount / calculation.Subtotal) : 0;
            if (discountPercentage > shopConfiguration.PricingRules.MaxDiscountPercentage)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Code = "HIGH_DISCOUNT",
                    Message = $"Discount percentage ({discountPercentage:P2}) exceeds maximum allowed ({shopConfiguration.PricingRules.MaxDiscountPercentage:P2})",
                    Field = "TotalDiscountAmount",
                    Value = discountPercentage,
                    Suggestion = "Consider manager approval for high discounts"
                });
            }

            // Validate calculation consistency
            var expectedTotal = calculation.Subtotal - calculation.TotalDiscountAmount + calculation.TotalTaxAmount;
            if (Math.Abs(calculation.FinalTotal - expectedTotal) > 0.01m)
            {
                result.Errors.Add(new ValidationError
                {
                    Code = "CALCULATION_MISMATCH",
                    Message = $"Final total ({calculation.FinalTotal:C}) does not match expected total ({expectedTotal:C})",
                    Field = "FinalTotal",
                    Value = calculation.FinalTotal,
                    Severity = ValidationSeverity.Critical
                });
            }

            result.IsValid = !result.Errors.Any();

            _logger.LogDebug("Calculation validation completed: Valid {IsValid}, Errors {ErrorCount}, Warnings {WarningCount}", 
                result.IsValid, result.Errors.Count, result.Warnings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating calculation");
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Code = "VALIDATION_ERROR",
                Message = $"Validation error: {ex.Message}",
                Severity = ValidationSeverity.Critical
            });
        }

        return result;
    }

    public async Task<OrderTotalCalculation> RecalculateOnItemChangeAsync(SaleItem modifiedItem, List<SaleItem> allItems, ShopConfiguration shopConfiguration, Customer? customer = null)
    {
        _logger.LogDebug("Recalculating order due to item change: Item {ItemId}", modifiedItem.Id);

        try
        {
            // Update the modified item in the list
            var itemIndex = allItems.FindIndex(i => i.Id == modifiedItem.Id);
            if (itemIndex >= 0)
            {
                allItems[itemIndex] = modifiedItem;
            }

            // Recalculate the entire order
            return await CalculateOrderTotalsAsync(allItems, shopConfiguration, customer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculating order on item change");
            throw;
        }
    }

    #region Private Helper Methods

    private string GenerateDiscountReason(Discount discount, SaleItem item, Customer? customer)
    {
        var reason = discount.Name;
        
        if (discount.Type == DiscountType.Percentage)
            reason += $" ({discount.Value}% off)";
        else
            reason += $" (${discount.Value} off)";

        if (customer != null && discount.RequiredMembershipTier.HasValue)
            reason += $" - {customer.Tier} member discount";

        return reason;
    }

    private decimal CalculateMembershipDiscount(List<SaleItem> items, Customer customer, ShopConfiguration shopConfiguration)
    {
        // Simple membership discount calculation based on tier
        var discountPercentage = customer.Tier switch
        {
            MembershipTier.Bronze => 0.05m, // 5%
            MembershipTier.Silver => 0.10m, // 10%
            MembershipTier.Gold => 0.15m,   // 15%
            MembershipTier.Platinum => 0.20m, // 20%
            _ => 0m
        };

        var totalAmount = items.Sum(i => i.TotalPrice);
        return Math.Round(totalAmount * discountPercentage, 2, MidpointRounding.AwayFromZero);
    }

    private decimal CalculateBulkPricingDiscount(List<SaleItem> items, ShopConfiguration shopConfiguration)
    {
        // Simple bulk pricing: 5% discount for orders with 10+ items
        var totalQuantity = items.Sum(i => i.Quantity);
        if (totalQuantity >= 10)
        {
            var totalAmount = items.Sum(i => i.TotalPrice);
            return Math.Round(totalAmount * 0.05m, 2, MidpointRounding.AwayFromZero);
        }
        return 0;
    }

    private CalculationBreakdown CreateCalculationBreakdown(OrderTotalCalculation calculation)
    {
        var breakdown = new CalculationBreakdown();

        breakdown.Items.Add(new BreakdownItem
        {
            Description = "Subtotal",
            Amount = calculation.Subtotal,
            Type = "Subtotal",
            IsAddition = true,
            Category = "Base"
        });

        if (calculation.TotalDiscountAmount > 0)
        {
            breakdown.Items.Add(new BreakdownItem
            {
                Description = "Total Discounts",
                Amount = calculation.TotalDiscountAmount,
                Type = "Discount",
                IsAddition = false,
                Category = "Adjustment"
            });
        }

        if (calculation.TotalTaxAmount > 0)
        {
            breakdown.Items.Add(new BreakdownItem
            {
                Description = "Total Tax",
                Amount = calculation.TotalTaxAmount,
                Type = "Tax",
                IsAddition = true,
                Category = "Tax"
            });
        }

        breakdown.Items.Add(new BreakdownItem
        {
            Description = "Final Total",
            Amount = calculation.FinalTotal,
            Type = "Total",
            IsAddition = true,
            Category = "Final"
        });

        breakdown.Totals["Subtotal"] = calculation.Subtotal;
        breakdown.Totals["Discounts"] = calculation.TotalDiscountAmount;
        breakdown.Totals["Tax"] = calculation.TotalTaxAmount;
        breakdown.Totals["Final"] = calculation.FinalTotal;

        breakdown.CalculationSteps.Add($"Subtotal: {calculation.Subtotal:C}");
        if (calculation.TotalDiscountAmount > 0)
            breakdown.CalculationSteps.Add($"Less Discounts: -{calculation.TotalDiscountAmount:C}");
        if (calculation.TotalTaxAmount > 0)
            breakdown.CalculationSteps.Add($"Plus Tax: +{calculation.TotalTaxAmount:C}");
        breakdown.CalculationSteps.Add($"Final Total: {calculation.FinalTotal:C}");

        return breakdown;
    }

    #endregion
}