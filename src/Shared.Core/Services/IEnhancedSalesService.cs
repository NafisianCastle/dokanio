using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Enhanced sales service interface for multi-business operations
/// Extends the base ISaleService with business type-specific validation and shop-level configurations
/// </summary>
public interface IEnhancedSalesService : ISaleService
{
    /// <summary>
    /// Creates a sale with business type validation
    /// </summary>
    /// <param name="shopId">Shop where the sale is being created</param>
    /// <param name="userId">User creating the sale</param>
    /// <param name="invoiceNumber">Invoice number for the sale</param>
    /// <returns>Created sale with validation applied</returns>
    Task<Sale> CreateSaleWithValidationAsync(Guid shopId, Guid userId, string invoiceNumber);

    /// <summary>
    /// Validates a product for sale based on business type rules
    /// </summary>
    /// <param name="productId">Product to validate</param>
    /// <param name="shopId">Shop context for validation</param>
    /// <returns>Validation result with details</returns>
    Task<ValidationResult> ValidateProductForSaleAsync(Guid productId, Guid shopId);

    /// <summary>
    /// Calculates sale totals with business-specific rules and shop-level configurations
    /// </summary>
    /// <param name="sale">Sale to calculate</param>
    /// <returns>Calculation result with business rule applications</returns>
    Task<SaleCalculationResult> CalculateWithBusinessRulesAsync(Sale sale);

    /// <summary>
    /// Gets AI-powered recommendations for a sale in progress
    /// </summary>
    /// <param name="saleId">Sale ID to get recommendations for</param>
    /// <returns>Recommendation result with suggested products and actions</returns>
    Task<RecommendationResult> GetSaleRecommendationsAsync(Guid saleId);

    /// <summary>
    /// Validates business type-specific rules for a product
    /// </summary>
    /// <param name="product">Product to validate</param>
    /// <param name="businessType">Business type context</param>
    /// <returns>Validation result</returns>
    Task<ValidationResult> ValidateBusinessTypeRulesAsync(Product product, BusinessType businessType);

    /// <summary>
    /// Gets shop-specific pricing rules and configurations
    /// </summary>
    /// <param name="shopId">Shop ID</param>
    /// <returns>Shop configuration for pricing</returns>
    Task<ShopConfiguration> GetShopPricingConfigurationAsync(Guid shopId);

    /// <summary>
    /// Applies shop-level discounts and pricing rules
    /// </summary>
    /// <param name="sale">Sale to apply rules to</param>
    /// <param name="shopConfiguration">Shop configuration</param>
    /// <returns>Updated sale with applied rules</returns>
    Task<Sale> ApplyShopPricingRulesAsync(Sale sale, ShopConfiguration shopConfiguration);
}