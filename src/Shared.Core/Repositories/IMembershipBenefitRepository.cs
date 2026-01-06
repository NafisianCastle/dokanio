using Shared.Core.Entities;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository interface for membership benefit operations
/// </summary>
public interface IMembershipBenefitRepository : IRepository<MembershipBenefit>
{
    /// <summary>
    /// Get benefits by customer membership ID
    /// </summary>
    Task<List<MembershipBenefit>> GetByCustomerMembershipIdAsync(Guid customerMembershipId);
    
    /// <summary>
    /// Get active benefits by customer membership ID
    /// </summary>
    Task<List<MembershipBenefit>> GetActiveBenefitsByCustomerMembershipIdAsync(Guid customerMembershipId);
    
    /// <summary>
    /// Get benefits by type
    /// </summary>
    Task<List<MembershipBenefit>> GetByTypeAsync(BenefitType type);
    
    /// <summary>
    /// Get benefits expiring within specified days
    /// </summary>
    Task<List<MembershipBenefit>> GetExpiringBenefitsAsync(int daysFromNow);
    
    /// <summary>
    /// Update benefit usage count
    /// </summary>
    Task<bool> UpdateUsageCountAsync(Guid benefitId, int usageCount);
    
    /// <summary>
    /// Get available benefits for customer (not expired, not max used)
    /// </summary>
    Task<List<MembershipBenefit>> GetAvailableBenefitsForCustomerAsync(Guid customerId);
}