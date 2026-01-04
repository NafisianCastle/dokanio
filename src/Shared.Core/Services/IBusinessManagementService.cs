using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Service interface for managing businesses and shops in the multi-tenant POS system
/// </summary>
public interface IBusinessManagementService
{
    #region Business Management
    
    /// <summary>
    /// Creates a new business
    /// </summary>
    /// <param name="request">Business creation request</param>
    /// <returns>Created business response</returns>
    Task<BusinessResponse> CreateBusinessAsync(CreateBusinessRequest request);
    
    /// <summary>
    /// Updates an existing business
    /// </summary>
    /// <param name="request">Business update request</param>
    /// <returns>Updated business response</returns>
    Task<BusinessResponse> UpdateBusinessAsync(UpdateBusinessRequest request);
    
    /// <summary>
    /// Gets a business by ID
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <returns>Business response if found, null otherwise</returns>
    Task<BusinessResponse?> GetBusinessByIdAsync(Guid businessId);
    
    /// <summary>
    /// Gets all businesses owned by a specific user
    /// </summary>
    /// <param name="ownerId">Owner user ID</param>
    /// <returns>Collection of business responses</returns>
    Task<IEnumerable<BusinessResponse>> GetBusinessesByOwnerAsync(Guid ownerId);
    
    /// <summary>
    /// Gets businesses by type
    /// </summary>
    /// <param name="businessType">Business type to filter by</param>
    /// <returns>Collection of business responses</returns>
    Task<IEnumerable<BusinessResponse>> GetBusinessesByTypeAsync(BusinessType businessType);
    
    /// <summary>
    /// Deletes a business (soft delete)
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="userId">User performing the deletion</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> DeleteBusinessAsync(Guid businessId, Guid userId);
    
    #endregion
    
    #region Shop Management
    
    /// <summary>
    /// Creates a new shop
    /// </summary>
    /// <param name="request">Shop creation request</param>
    /// <returns>Created shop response</returns>
    Task<ShopResponse> CreateShopAsync(CreateShopRequest request);
    
    /// <summary>
    /// Updates an existing shop
    /// </summary>
    /// <param name="request">Shop update request</param>
    /// <returns>Updated shop response</returns>
    Task<ShopResponse> UpdateShopAsync(UpdateShopRequest request);
    
    /// <summary>
    /// Gets a shop by ID
    /// </summary>
    /// <param name="shopId">Shop ID</param>
    /// <returns>Shop response if found, null otherwise</returns>
    Task<ShopResponse?> GetShopByIdAsync(Guid shopId);
    
    /// <summary>
    /// Gets all shops belonging to a specific business
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <returns>Collection of shop responses</returns>
    Task<IEnumerable<ShopResponse>> GetShopsByBusinessAsync(Guid businessId);
    
    /// <summary>
    /// Gets shops accessible by a specific user
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="userId">User ID</param>
    /// <returns>Collection of shop responses the user can access</returns>
    Task<IEnumerable<ShopResponse>> GetShopsByBusinessAndUserAsync(Guid businessId, Guid userId);
    
    /// <summary>
    /// Deletes a shop (soft delete)
    /// </summary>
    /// <param name="shopId">Shop ID</param>
    /// <param name="userId">User performing the deletion</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> DeleteShopAsync(Guid shopId, Guid userId);
    
    #endregion
    
    #region Configuration Management
    
    /// <summary>
    /// Gets business configuration
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <returns>Business configuration</returns>
    Task<BusinessConfiguration> GetBusinessConfigurationAsync(Guid businessId);
    
    /// <summary>
    /// Updates business configuration
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="configuration">New configuration</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> UpdateBusinessConfigurationAsync(Guid businessId, BusinessConfiguration configuration);
    
    /// <summary>
    /// Gets shop configuration
    /// </summary>
    /// <param name="shopId">Shop ID</param>
    /// <returns>Shop configuration</returns>
    Task<ShopConfiguration> GetShopConfigurationAsync(Guid shopId);
    
    /// <summary>
    /// Updates shop configuration
    /// </summary>
    /// <param name="shopId">Shop ID</param>
    /// <param name="configuration">New configuration</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> UpdateShopConfigurationAsync(Guid shopId, ShopConfiguration configuration);
    
    #endregion
    
    #region Business Type Validation
    
    /// <summary>
    /// Validates business type configuration
    /// </summary>
    /// <param name="businessType">Business type</param>
    /// <param name="configuration">Configuration to validate</param>
    /// <returns>Validation result</returns>
    Task<BusinessValidationResult> ValidateBusinessTypeConfigurationAsync(BusinessType businessType, BusinessConfiguration configuration);
    
    /// <summary>
    /// Validates shop configuration against business type
    /// </summary>
    /// <param name="shopId">Shop ID</param>
    /// <param name="configuration">Configuration to validate</param>
    /// <returns>Validation result</returns>
    Task<ShopValidationResult> ValidateShopConfigurationAsync(Guid shopId, ShopConfiguration configuration);
    
    /// <summary>
    /// Gets default configuration for a business type
    /// </summary>
    /// <param name="businessType">Business type</param>
    /// <returns>Default business configuration</returns>
    Task<BusinessConfiguration> GetDefaultBusinessConfigurationAsync(BusinessType businessType);
    
    /// <summary>
    /// Gets default shop configuration for a business
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <returns>Default shop configuration</returns>
    Task<ShopConfiguration> GetDefaultShopConfigurationAsync(Guid businessId);
    
    #endregion
    
    #region Custom Attributes
    
    /// <summary>
    /// Gets required product attributes for a business type
    /// </summary>
    /// <param name="businessType">Business type</param>
    /// <returns>List of required attribute names</returns>
    Task<IEnumerable<string>> GetRequiredProductAttributesAsync(BusinessType businessType);
    
    /// <summary>
    /// Gets optional product attributes for a business type
    /// </summary>
    /// <param name="businessType">Business type</param>
    /// <returns>List of optional attribute names</returns>
    Task<IEnumerable<string>> GetOptionalProductAttributesAsync(BusinessType businessType);
    
    /// <summary>
    /// Validates product attributes against business type requirements
    /// </summary>
    /// <param name="businessType">Business type</param>
    /// <param name="attributes">Product attributes to validate</param>
    /// <returns>Validation result</returns>
    Task<BusinessValidationResult> ValidateProductAttributesAsync(BusinessType businessType, BusinessTypeAttributes attributes);
    
    #endregion
}