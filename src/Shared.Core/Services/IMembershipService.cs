using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

public interface IMembershipService
{
    Task<Customer?> GetCustomerByMembershipNumberAsync(string membershipNumber);
    Task<Customer> RegisterCustomerAsync(CustomerRegistrationRequest request);
    Task<MembershipDiscount> CalculateMembershipDiscountAsync(Customer customer, Sale sale);
    Task UpdateCustomerPurchaseHistoryAsync(Customer customer, Sale sale);
    Task<MembershipTier> CalculateMembershipTierAsync(Customer customer);
    Task<CustomerAnalytics> GetCustomerAnalyticsAsync();
    Task<IEnumerable<Customer>> GetTopCustomersAsync(int count);
    Task<bool> ValidateCustomerAsync(Customer customer);
    Task<string> GenerateUniqueMembershipNumberAsync();
}