using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository interface for customer membership operations
/// </summary>
public interface ICustomerMembershipRepository : IRepository<CustomerMembership>
{
    /// <summary>
    /// Get membership by customer ID
    /// </summary>
    Task<CustomerMembership?> GetByCustomerIdAsync(Guid customerId);
    
    /// <summary>
    /// Get all memberships by tier
    /// </summary>
    Task<List<CustomerMembership>> GetByTierAsync(MembershipTier tier);
    
    /// <summary>
    /// Get memberships expiring within specified days
    /// </summary>
    Task<List<CustomerMembership>> GetExpiringMembershipsAsync(int daysFromNow);
    
    /// <summary>
    /// Get active memberships with benefits
    /// </summary>
    Task<List<CustomerMembership>> GetActiveMembershipsWithBenefitsAsync();
    
    /// <summary>
    /// Update membership tier based on spending
    /// </summary>
    Task<bool> UpdateMembershipTierAsync(Guid customerMembershipId, MembershipTier newTier, decimal totalSpent);
    
    /// <summary>
    /// Get membership statistics by tier
    /// </summary>
    Task<Dictionary<MembershipTier, int>> GetMembershipStatisticsByTierAsync();
}