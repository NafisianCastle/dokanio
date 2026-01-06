using Shared.Core.DTOs;
using Shared.Core.Entities;

namespace Shared.Core.Services;

/// <summary>
/// Comprehensive validation service for all POS system entities and operations
/// </summary>
public interface IValidationService
{
    #region Field-Level Validation
    
    /// <summary>
    /// Validates a single field value against specified rules
    /// </summary>
    /// <param name="fieldName">Name of the field being validated</param>
    /// <param name="value">Value to validate</param>
    /// <param name="validationRules">Validation rules to apply</param>
    /// <returns>Field validation result</returns>
    Task<FieldValidationResult> ValidateFieldAsync(string fieldName, object? value, FieldValidationRules validationRules);
    
    /// <summary>
    /// Validates multiple fields at once
    /// </summary>
    /// <param name="fieldValues">Dictionary of field names and values</param>
    /// <param name="validationRules">Dictionary of field validation rules</param>
    /// <returns>Multi-field validation result</returns>
    Task<MultiFieldValidationResult> ValidateFieldsAsync(Dictionary<string, object?> fieldValues, Dictionary<string, FieldValidationRules> validationRules);
    
    #endregion
    
    #region Entity Validation
    
    /// <summary>
    /// Validates a product entity with business rules
    /// </summary>
    /// <param name="product">Product to validate</param>
    /// <param name="shopId">Shop context for validation</param>
    /// <returns>Product validation result</returns>
    Task<EntityValidationResult> ValidateProductAsync(Product product, Guid shopId);
    
    /// <summary>
    /// Validates a sale entity with business rules
    /// </summary>
    /// <param name="sale">Sale to validate</param>
    /// <returns>Sale validation result</returns>
    Task<EntityValidationResult> ValidateSaleAsync(Sale sale);
    
    /// <summary>
    /// Validates a sale item with stock and business rules
    /// </summary>
    /// <param name="saleItem">Sale item to validate</param>
    /// <param name="shopId">Shop context for stock validation</param>
    /// <returns>Sale item validation result</returns>
    Task<EntityValidationResult> ValidateSaleItemAsync(SaleItem saleItem, Guid shopId);
    
    /// <summary>
    /// Validates a customer entity
    /// </summary>
    /// <param name="customer">Customer to validate</param>
    /// <returns>Customer validation result</returns>
    Task<EntityValidationResult> ValidateCustomerAsync(Customer customer);
    
    #endregion
    
    #region Business Rule Validation
    
    /// <summary>
    /// Validates stock levels for a product and quantity
    /// </summary>
    /// <param name="productId">Product ID to check</param>
    /// <param name="requestedQuantity">Requested quantity</param>
    /// <param name="shopId">Shop context</param>
    /// <returns>Stock validation result</returns>
    Task<StockValidationResult> ValidateStockLevelsAsync(Guid productId, decimal requestedQuantity, Guid shopId);
    
    /// <summary>
    /// Validates product expiry dates for sale
    /// </summary>
    /// <param name="productId">Product ID to check</param>
    /// <param name="batchNumber">Batch number (optional)</param>
    /// <returns>Expiry validation result</returns>
    Task<ExpiryValidationResult> ValidateProductExpiryAsync(Guid productId, string? batchNumber = null);
    
    /// <summary>
    /// Validates pricing rules and calculations
    /// </summary>
    /// <param name="saleItems">Sale items to validate pricing for</param>
    /// <param name="shopId">Shop context</param>
    /// <returns>Pricing validation result</returns>
    Task<PricingValidationResult> ValidatePricingRulesAsync(List<SaleItem> saleItems, Guid shopId);
    
    /// <summary>
    /// Validates discount applications and limits
    /// </summary>
    /// <param name="discounts">Discounts to validate</param>
    /// <param name="saleTotal">Total sale amount</param>
    /// <param name="customerId">Customer ID (optional)</param>
    /// <returns>Discount validation result</returns>
    Task<DiscountValidationResult> ValidateDiscountApplicationAsync(List<Discount> discounts, decimal saleTotal, Guid? customerId = null);
    
    #endregion
    
    #region Real-Time Validation
    
    /// <summary>
    /// Provides real-time validation feedback as user types
    /// </summary>
    /// <param name="fieldName">Field being edited</param>
    /// <param name="currentValue">Current field value</param>
    /// <param name="context">Validation context</param>
    /// <returns>Real-time validation result</returns>
    Task<RealTimeValidationResult> ValidateRealTimeAsync(string fieldName, object? currentValue, ValidationContext context);
    
    /// <summary>
    /// Validates form completion status
    /// </summary>
    /// <param name="formData">Form data to validate</param>
    /// <param name="requiredFields">List of required field names</param>
    /// <returns>Form validation result</returns>
    Task<FormValidationResult> ValidateFormCompletionAsync(Dictionary<string, object?> formData, List<string> requiredFields);
    
    #endregion
    
    #region Localization Support
    
    /// <summary>
    /// Gets localized validation message for a validation error
    /// </summary>
    /// <param name="errorCode">Error code</param>
    /// <param name="parameters">Message parameters</param>
    /// <param name="languageCode">Language code (optional, defaults to system language)</param>
    /// <returns>Localized error message</returns>
    Task<string> GetLocalizedValidationMessageAsync(string errorCode, Dictionary<string, object>? parameters = null, string? languageCode = null);
    
    /// <summary>
    /// Gets all available validation messages for a language
    /// </summary>
    /// <param name="languageCode">Language code</param>
    /// <returns>Dictionary of error codes and messages</returns>
    Task<Dictionary<string, string>> GetValidationMessagesAsync(string languageCode);
    
    #endregion
}