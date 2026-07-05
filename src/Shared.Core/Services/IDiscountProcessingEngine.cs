using Shared.Core.Entities;

namespace Shared.Core.Services;

/// <summary>
/// Interface for the discount processing engine that handles automatic discount application,
/// membership integration, combination rules, and audit trail tracking.
/// Requirements: 4.1, 4.2, 4.3, 4.4, 4.5
/// </summary>
public interface IDiscountProcessingEngine
{
    /// <summary>
    /// Calculates all applicable discounts for a sale, including membership discounts,
    /// promotional discounts, and combination rules.
    /// Requirement 4.1: Automatically apply eligible discounts for customers with membership benefits.
    /// Requirement 4.3: Apply discounts in the correct order and combination rules.
    /// </summary>
    /// <param name="sale">The sale to calculate discounts for</param>
    /// <param name="customer">Optional customer for membership-based discounts</param>
    /// <returns>Complete discount calculation result with all applied discounts</returns>
    Task<Shared.Core.DTOs.DiscountCalculationResult> CalculateDiscountsAsync(Sale sale, Customer? customer = null);

    /// <summary>
    /// Gets all discounts that are applicable to the given sale and customer,
    /// without applying them. Useful for previewing available discounts.
    /// Requirement 4.1: Identify eligible discounts based on customer membership benefits.
    /// </summary>
    /// <param name="sale">The sale to check discounts for</param>
    /// <param name="customer">Optional customer for membership-based discounts</param>
    /// <returns>Collection of applicable discounts with eligibility details</returns>
    Task<IEnumerable<ApplicableDiscount>> GetApplicableDiscountsAsync(Sale sale, Customer? customer = null);

    /// <summary>
    /// Validates whether a specific discount can be applied to a sale,
    /// checking eligibility, constraints, and combination rules.
    /// Requirement 4.3: Enforce combination rules and priorities.
    /// Requirement 4.4: Prevent discount amounts from exceeding line item totals.
    /// </summary>
    /// <param name="discount">The discount to validate</param>
    /// <param name="sale">The sale to validate against</param>
    /// <returns>Validation result with any errors or warnings</returns>
    Task<Shared.Core.DTOs.DiscountValidationResult> ValidateDiscountApplicationAsync(Discount discount, Sale sale);

    /// <summary>
    /// Calculates the membership discount amount for a customer on a given sale.
    /// Requirement 4.1: Automatically apply eligible membership discounts.
    /// Requirement 4.2: Calculate percentage-based discounts correctly.
    /// </summary>
    /// <param name="customer">The customer with membership benefits</param>
    /// <param name="sale">The sale to calculate the membership discount for</param>
    /// <returns>The calculated membership discount amount</returns>
    Task<decimal> CalculateMembershipDiscountAsync(Customer customer, Sale sale);

    /// <summary>
    /// Saves the applied discounts for a sale to the database for audit purposes.
    /// Requirement 4.5: Track and store applied discounts for audit purposes.
    /// </summary>
    /// <param name="saleId">The ID of the sale</param>
    /// <param name="appliedDiscounts">The discounts that were applied</param>
    Task SaveAppliedDiscountsAsync(Guid saleId, IEnumerable<Shared.Core.DTOs.AppliedDiscount> appliedDiscounts);
}

/// <summary>
/// Represents a discount that is applicable to a sale, with eligibility details.
/// </summary>
public class ApplicableDiscount
{
    /// <summary>The discount entity</summary>
    public Discount Discount { get; set; } = null!;

    /// <summary>Whether this discount is eligible for the current sale/customer</summary>
    public bool IsEligible { get; set; }

    /// <summary>The calculated discount amount if applied</summary>
    public decimal CalculatedAmount { get; set; }

    /// <summary>Human-readable reason for eligibility or ineligibility</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Priority order for application (lower = applied first)</summary>
    public int Priority { get; set; }

    /// <summary>Whether this discount can be combined with others</summary>
    public bool IsCombineable { get; set; } = true;

    /// <summary>The scope of this discount (product, category, or sale-level)</summary>
    public string Scope { get; set; } = string.Empty;
}
