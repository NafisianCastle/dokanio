using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using System.Diagnostics;

namespace Shared.Core.Services;

/// <summary>
/// Real-time calculation engine that provides immediate updates for sales calculations.
/// Designed for sub-100ms performance on sales with up to 50 items (Requirement 3.1, 9.2).
/// Supports category-based tax rates (Requirement 3.2) and detailed breakdowns (Requirement 3.6).
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

    /// <summary>
    /// Calculates the total price for a single sale item including all applicable pricing rules.
    /// Handles both regular (quantity-based) and weight-based products.
    /// Requirement 3.1: Recalculate within 100ms.
    /// </summary>
    public async Task<LineItemCalculationResult> CalculateLineItemAsync(SaleItem item, ShopConfiguration shopConfiguration)
    {
        var sw = Stopwatch.StartNew();
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
                result.CalculationNotes.Add($"Weight-based pricing: {weightPricing.FormattedWeight} × {weightPricing.FormattedRate} = {weightPricing.FormattedPrice}");
            }
            else
            {
                result.BasePrice = item.UnitPrice;
                result.UnitPrice = item.UnitPrice;
                // Proper rounding: round to 2 decimal places using banker's rounding avoidance
                result.LineSubtotal = Math.Round(item.UnitPrice * item.Quantity, 2, MidpointRounding.AwayFromZero);
            }

            // Tax is calculated at the order level via CalculateTaxesAsync for proper aggregation
            // and category-based grouping. Line items do not include tax to avoid double-counting.
            result.DiscountAmount = 0;
            result.TaxAmount = 0;
            result.LineTotal = result.LineSubtotal;
            result.CalculatedAt = DateTime.UtcNow;

            sw.Stop();
            _logger.LogDebug("Line item calculation completed in {ElapsedMs}ms: Subtotal {Subtotal}, Tax {Tax}, Total {Total}",
                sw.ElapsedMilliseconds, result.LineSubtotal, result.TaxAmount, result.LineTotal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating line item for product {ProductId}", item.ProductId);
            result.CalculationNotes.Add($"Calculation error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Calculates all totals for an entire order including subtotal, discounts, taxes, and final total.
    /// Requirement 3.1: Complete within 100ms for up to 50 items.
    /// Requirement 3.6: Provides detailed calculation breakdown.
    /// </summary>
    public async Task<OrderTotalCalculation> CalculateOrderTotalsAsync(List<SaleItem> items, ShopConfiguration shopConfiguration, Customer? customer = null)
    {
        var sw = Stopwatch.StartNew();
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

            // Calculate subtotal (sum of line subtotals before tax)
            result.Subtotal = result.LineItems.Sum(li => li.LineSubtotal);

            // Apply discounts
            var availableDiscounts = new List<Discount>();
            var discountResult = await ApplyDiscountsAsync(items, availableDiscounts, customer);
            result.TotalDiscountAmount = discountResult.TotalDiscountAmount;
            result.OrderLevelDiscounts = discountResult.AppliedDiscounts;

            // Calculate taxes using category-based rates (Requirement 3.2)
            var taxResult = await CalculateTaxesAsync(items, shopConfiguration);
            result.TotalTaxAmount = taxResult.TotalTaxAmount;
            result.OrderLevelTaxes = taxResult.AppliedTaxes;

            // Apply pricing rules (membership, bulk, etc.)
            var pricingRulesResult = await ApplyPricingRulesAsync(items, shopConfiguration, customer);
            result.AppliedPricingRules = pricingRulesResult.AppliedRules;

            // Calculate final total: subtotal - discounts + taxes
            result.FinalTotal = Math.Round(
                result.Subtotal - result.TotalDiscountAmount + result.TotalTaxAmount,
                2, MidpointRounding.AwayFromZero);

            // Create detailed breakdown for transparency (Requirement 3.6)
            result.Breakdown = CreateCalculationBreakdown(result, items, shopConfiguration);

            // Validate calculation
            var validation = await ValidateCalculationAsync(result, shopConfiguration);
            result.IsValid = validation.IsValid;
            result.ValidationMessages = validation.Errors.Select(e => e.Message).ToList();

            result.CalculatedAt = DateTime.UtcNow;

            sw.Stop();
            _logger.LogDebug(
                "Order calculation completed in {ElapsedMs}ms: Subtotal {Subtotal}, Discount {Discount}, Tax {Tax}, Total {Total}",
                sw.ElapsedMilliseconds, result.Subtotal, result.TotalDiscountAmount, result.TotalTaxAmount, result.FinalTotal);

            if (sw.ElapsedMilliseconds > 100)
            {
                _logger.LogWarning("Order calculation exceeded 100ms target: {ElapsedMs}ms for {ItemCount} items",
                    sw.ElapsedMilliseconds, items.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating order totals");
            result.IsValid = false;
            result.ValidationMessages.Add($"Calculation error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Applies all applicable discounts to a list of sale items.
    /// </summary>
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

                // Only apply discounts that are in the provided list (or all if list is empty)
                var discountsToApply = discounts.Count > 0
                    ? applicableDiscounts.Where(d => discounts.Any(ad => ad.Id == d.Id)).ToList()
                    : applicableDiscounts;

                foreach (var discount in discountsToApply)
                {
                    var discountAmount = await _discountService.CalculateDiscountAmountAsync(
                        discount, item.TotalPrice, item.Quantity);

                    if (discountAmount > 0)
                    {
                        var appliedDiscount = new Shared.Core.DTOs.AppliedDiscount
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

    /// <summary>
    /// Calculates taxes for sale items based on shop configuration.
    /// Supports category-based tax rates: each product category can have its own tax rate.
    /// Falls back to the default shop tax rate for uncategorized products.
    /// Requirement 3.2: Apply correct tax rates based on product categories and shop configuration.
    /// </summary>
    public async Task<TaxCalculationResult> CalculateTaxesAsync(List<SaleItem> items, ShopConfiguration shopConfiguration)
    {
        _logger.LogDebug("Calculating taxes for {ItemCount} items (default rate: {TaxRate}, categories: {CategoryCount})",
            items.Count, shopConfiguration.TaxRate, shopConfiguration.CategoryTaxRates.Count);

        var result = new TaxCalculationResult
        {
            TaxIncludedInPrice = shopConfiguration.TaxIncludedInPrice
        };

        try
        {
            if (shopConfiguration.TaxIncludedInPrice)
            {
                // Tax is already included in prices - no additional tax to add
                result.CalculatedAt = DateTime.UtcNow;
                _logger.LogDebug("Tax is included in price - no additional tax calculated");
                return result;
            }

            // Group items by their effective tax rate (category-based or default)
            // This enables per-category tax breakdown for transparency (Requirement 3.6)
            var taxGroups = items
                .GroupBy(item => new
                {
                    Category = item.Product?.Category ?? string.Empty,
                    TaxRate = shopConfiguration.GetTaxRateForCategory(item.Product?.Category)
                })
                .ToList();

            foreach (var group in taxGroups)
            {
                var taxRate = group.Key.TaxRate;
                if (taxRate <= 0) continue; // Skip zero-rate items

                var groupTaxableAmount = group.Sum(i => i.TotalPrice);
                var groupTaxAmount = Math.Round(groupTaxableAmount * taxRate, 2, MidpointRounding.AwayFromZero);

                if (groupTaxAmount <= 0) continue;

                var categoryLabel = string.IsNullOrWhiteSpace(group.Key.Category)
                    ? "General"
                    : group.Key.Category;

                var appliedTax = new AppliedTax
                {
                    TaxName = categoryLabel == "General" ? "Sales Tax" : $"{categoryLabel} Tax",
                    TaxRate = taxRate,
                    TaxableAmount = groupTaxableAmount,
                    TaxAmount = groupTaxAmount,
                    Description = $"{categoryLabel} tax at {taxRate:P2}"
                };

                result.AppliedTaxes.Add(appliedTax);
                result.TotalTaxAmount += groupTaxAmount;

                var breakdown = new TaxBreakdown
                {
                    Category = categoryLabel,
                    TaxableAmount = groupTaxableAmount,
                    TaxRate = taxRate,
                    TaxAmount = groupTaxAmount,
                    ApplicableItemIds = group.Select(i => i.Id).ToList()
                };

                result.TaxBreakdowns.Add(breakdown);
            }

            result.CalculatedAt = DateTime.UtcNow;

            _logger.LogDebug("Tax calculation completed: {TaxGroupCount} tax groups, total tax {TotalTax}",
                result.TaxBreakdowns.Count, result.TotalTaxAmount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating taxes");
        }

        return result;
    }

    /// <summary>
    /// Calculates pricing for weight-based products.
    /// Requirement 3.4: Update pricing using current weight.
    /// </summary>
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

    /// <summary>
    /// Applies complex pricing rules including bulk discounts, tiered pricing, and membership benefits.
    /// </summary>
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

    /// <summary>
    /// Validates that all calculations are within acceptable business rules and limits.
    /// </summary>
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
            if (shopConfiguration.PricingRules.MaxDiscountPercentage > 0 &&
                discountPercentage > shopConfiguration.PricingRules.MaxDiscountPercentage)
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

            // Validate calculation consistency: FinalTotal = Subtotal - Discounts + Tax
            var expectedTotal = Math.Round(
                calculation.Subtotal - calculation.TotalDiscountAmount + calculation.TotalTaxAmount,
                2, MidpointRounding.AwayFromZero);

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

    /// <summary>
    /// Recalculates all totals when a single item is modified (quantity, weight, discount, etc.).
    /// Requirement 3.1: Recalculate all affected totals within 100ms.
    /// </summary>
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
        // Membership discount calculation based on tier
        var discountPercentage = customer.Tier switch
        {
            MembershipTier.Bronze => 0.05m,   // 5%
            MembershipTier.Silver => 0.10m,   // 10%
            MembershipTier.Gold => 0.15m,     // 15%
            MembershipTier.Platinum => 0.20m, // 20%
            _ => 0m
        };

        if (discountPercentage <= 0) return 0;

        var totalAmount = items.Sum(i => i.TotalPrice);
        return Math.Round(totalAmount * discountPercentage, 2, MidpointRounding.AwayFromZero);
    }

    private decimal CalculateBulkPricingDiscount(List<SaleItem> items, ShopConfiguration shopConfiguration)
    {
        // Bulk pricing: 5% discount for orders with 10+ total items
        var totalQuantity = items.Sum(i => i.Quantity);
        if (totalQuantity >= 10)
        {
            var totalAmount = items.Sum(i => i.TotalPrice);
            return Math.Round(totalAmount * 0.05m, 2, MidpointRounding.AwayFromZero);
        }
        return 0;
    }

    /// <summary>
    /// Creates a detailed calculation breakdown for transparency.
    /// Requirement 3.6: Provide calculation breakdowns for transparency.
    /// </summary>
    private CalculationBreakdown CreateCalculationBreakdown(
        OrderTotalCalculation calculation,
        List<SaleItem> items,
        ShopConfiguration shopConfiguration)
    {
        var breakdown = new CalculationBreakdown();

        // Step 1: Subtotal
        breakdown.Items.Add(new BreakdownItem
        {
            Description = $"Subtotal ({calculation.TotalItems} item{(calculation.TotalItems != 1 ? "s" : "")}, qty {calculation.TotalQuantity})",
            Amount = calculation.Subtotal,
            Type = "Subtotal",
            IsAddition = true,
            Category = "Base"
        });
        breakdown.CalculationSteps.Add($"Subtotal: {calculation.Subtotal:C}");

        // Step 2: Per-item breakdown
        foreach (var lineItem in calculation.LineItems)
        {
            var matchingItem = items.FirstOrDefault(i => i.Id == lineItem.SaleItemId);
            var productName = matchingItem?.Product?.Name ?? "Item";
            var itemDescription = lineItem.Weight.HasValue
                ? $"  {productName}: {lineItem.Weight:F3}kg × {lineItem.UnitPrice:C}/kg"
                : $"  {productName}: {lineItem.Quantity} × {lineItem.UnitPrice:C}";

            breakdown.Items.Add(new BreakdownItem
            {
                Description = itemDescription,
                Amount = lineItem.LineSubtotal,
                Type = "LineItem",
                IsAddition = true,
                Category = matchingItem?.Product?.Category ?? "General"
            });
        }

        // Step 3: Discounts
        if (calculation.TotalDiscountAmount > 0)
        {
            breakdown.Items.Add(new BreakdownItem
            {
                Description = $"Discounts ({calculation.OrderLevelDiscounts.Count} applied)",
                Amount = calculation.TotalDiscountAmount,
                Type = "Discount",
                IsAddition = false,
                Category = "Adjustment"
            });
            breakdown.CalculationSteps.Add($"Less Discounts: -{calculation.TotalDiscountAmount:C}");

            foreach (var discount in calculation.OrderLevelDiscounts)
            {
                breakdown.Items.Add(new BreakdownItem
                {
                    Description = $"  {discount.DiscountName}: {discount.Reason}",
                    Amount = discount.CalculatedAmount,
                    Type = "DiscountDetail",
                    IsAddition = false,
                    Category = "Adjustment"
                });
            }
        }

        // Step 4: Tax breakdown by category (Requirement 3.2 transparency)
        if (calculation.TotalTaxAmount > 0)
        {
            breakdown.Items.Add(new BreakdownItem
            {
                Description = $"Tax ({calculation.OrderLevelTaxes.Count} rate{(calculation.OrderLevelTaxes.Count != 1 ? "s" : "")})",
                Amount = calculation.TotalTaxAmount,
                Type = "Tax",
                IsAddition = true,
                Category = "Tax"
            });
            breakdown.CalculationSteps.Add($"Plus Tax: +{calculation.TotalTaxAmount:C}");

            foreach (var tax in calculation.OrderLevelTaxes)
            {
                breakdown.Items.Add(new BreakdownItem
                {
                    Description = $"  {tax.TaxName} ({tax.TaxRate:P2} on {tax.TaxableAmount:C})",
                    Amount = tax.TaxAmount,
                    Type = "TaxDetail",
                    IsAddition = true,
                    Category = "Tax"
                });
            }
        }

        // Step 5: Final total
        breakdown.Items.Add(new BreakdownItem
        {
            Description = "Final Total",
            Amount = calculation.FinalTotal,
            Type = "Total",
            IsAddition = true,
            Category = "Final"
        });
        breakdown.CalculationSteps.Add($"Final Total: {calculation.FinalTotal:C}");

        // Summary totals dictionary
        breakdown.Totals["Subtotal"] = calculation.Subtotal;
        breakdown.Totals["Discounts"] = calculation.TotalDiscountAmount;
        breakdown.Totals["Tax"] = calculation.TotalTaxAmount;
        breakdown.Totals["Final"] = calculation.FinalTotal;

        // Add per-category tax totals for transparency
        foreach (var tax in calculation.OrderLevelTaxes)
        {
            breakdown.Totals[$"Tax_{tax.TaxName}"] = tax.TaxAmount;
        }

        return breakdown;
    }

    #endregion
}
