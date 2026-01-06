using Shared.Core.DTOs;
using Shared.Core.Entities;

namespace Shared.Core.Services;

/// <summary>
/// Service for fast customer lookup with mobile number validation and auto-fill functionality
/// </summary>
public interface ICustomerLookupService
{
    /// <summary>
    /// Looks up customer by mobile number with caching for fast retrieval
    /// </summary>
    /// <param name="mobileNumber">The mobile number to search for</param>
    /// <returns>Customer information if found, null otherwise</returns>
    Task<CustomerLookupResult?> LookupByMobileNumberAsync(string mobileNumber);

    /// <summary>
    /// Validates mobile number format according to business rules
    /// </summary>
    /// <param name="mobileNumber">The mobile number to validate</param>
    /// <returns>Validation result with details</returns>
    Task<MobileNumberValidationResult> ValidateMobileNumberAsync(string mobileNumber);

    /// <summary>
    /// Gets customer membership details including benefits and discounts
    /// </summary>
    /// <param name="customerId">The customer ID</param>
    /// <returns>Membership details with available benefits</returns>
    Task<CustomerMembershipDetails?> GetMembershipDetailsAsync(Guid customerId);

    /// <summary>
    /// Creates a new customer with the provided information
    /// </summary>
    /// <param name="request">Customer creation request</param>
    /// <returns>Created customer information</returns>
    Task<CustomerCreationResult> CreateNewCustomerAsync(CustomerCreationRequest request);

    /// <summary>
    /// Gets customer preferences for personalized service
    /// </summary>
    /// <param name="customerId">The customer ID</param>
    /// <returns>Customer preferences if available</returns>
    Task<CustomerPreferences?> GetCustomerPreferencesAsync(Guid customerId);

    /// <summary>
    /// Updates customer information after a purchase
    /// </summary>
    /// <param name="customerId">The customer ID</param>
    /// <param name="purchaseAmount">The purchase amount</param>
    /// <returns>Updated customer information</returns>
    Task<CustomerUpdateResult> UpdateCustomerAfterPurchaseAsync(Guid customerId, decimal purchaseAmount);

    /// <summary>
    /// Invalidates cached customer data when information changes
    /// </summary>
    /// <param name="customerId">The customer ID to invalidate</param>
    Task InvalidateCustomerCacheAsync(Guid customerId);

    /// <summary>
    /// Searches for customers by partial name or membership number
    /// </summary>
    /// <param name="searchTerm">The search term</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <returns>List of matching customers</returns>
    Task<List<CustomerSearchResult>> SearchCustomersAsync(string searchTerm, int maxResults = 10);
}