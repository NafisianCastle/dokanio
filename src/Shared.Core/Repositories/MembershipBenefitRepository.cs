using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository implementation for membership benefit operations
/// </summary>
public class MembershipBenefitRepository : Repository<MembershipBenefit>, IMembershipBenefitRepository
{
    public MembershipBenefitRepository(PosDbContext context, ILogger<Repository<MembershipBenefit>> logger) 
        : base(context, logger)
    {
    }

    /// <summary>
    /// Get benefits by customer membership ID
    /// </summary>
    public async Task<List<MembershipBenefit>> GetByCustomerMembershipIdAsync(Guid customerMembershipId)
    {
        try
        {
            return await _context.MembershipBenefits
                .Where(mb => mb.CustomerMembershipId == customerMembershipId && !mb.IsDeleted)
                .OrderBy(mb => mb.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting benefits for customer membership {Id}", customerMembershipId);
            throw;
        }
    }

    /// <summary>
    /// Get active benefits by customer membership ID
    /// </summary>
    public async Task<List<MembershipBenefit>> GetActiveBenefitsByCustomerMembershipIdAsync(Guid customerMembershipId)
    {
        try
        {
            var now = DateTime.UtcNow;
            
            return await _context.MembershipBenefits
                .Where(mb => mb.CustomerMembershipId == customerMembershipId && 
                           mb.IsActive && 
                           !mb.IsDeleted &&
                           (mb.StartDate == null || mb.StartDate <= now) &&
                           (mb.EndDate == null || mb.EndDate >= now) &&
                           (mb.MaxUsages == null || mb.UsageCount < mb.MaxUsages))
                .OrderBy(mb => mb.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active benefits for customer membership {Id}", customerMembershipId);
            throw;
        }
    }

    /// <summary>
    /// Get benefits by type
    /// </summary>
    public async Task<List<MembershipBenefit>> GetByTypeAsync(BenefitType type)
    {
        try
        {
            return await _context.MembershipBenefits
                .Include(mb => mb.CustomerMembership)
                .ThenInclude(cm => cm.Customer)
                .Where(mb => mb.Type == type && mb.IsActive && !mb.IsDeleted)
                .OrderBy(mb => mb.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting benefits by type {Type}", type);
            throw;
        }
    }

    /// <summary>
    /// Get benefits expiring within specified days
    /// </summary>
    public async Task<List<MembershipBenefit>> GetExpiringBenefitsAsync(int daysFromNow)
    {
        try
        {
            var expiryDate = DateTime.UtcNow.AddDays(daysFromNow);
            
            return await _context.MembershipBenefits
                .Include(mb => mb.CustomerMembership)
                .ThenInclude(cm => cm.Customer)
                .Where(mb => mb.EndDate.HasValue && 
                           mb.EndDate.Value <= expiryDate && 
                           mb.IsActive && 
                           !mb.IsDeleted)
                .OrderBy(mb => mb.EndDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expiring benefits within {Days} days", daysFromNow);
            throw;
        }
    }

    /// <summary>
    /// Update benefit usage count
    /// </summary>
    public async Task<bool> UpdateUsageCountAsync(Guid benefitId, int usageCount)
    {
        try
        {
            var benefit = await GetByIdAsync(benefitId);
            if (benefit == null)
            {
                _logger.LogWarning("Membership benefit {Id} not found for usage update", benefitId);
                return false;
            }

            benefit.UsageCount = usageCount;
            await UpdateAsync(benefit);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating usage count for benefit {Id}", benefitId);
            throw;
        }
    }

    /// <summary>
    /// Get available benefits for customer (not expired, not max used)
    /// </summary>
    public async Task<List<MembershipBenefit>> GetAvailableBenefitsForCustomerAsync(Guid customerId)
    {
        try
        {
            var now = DateTime.UtcNow;
            
            return await _context.MembershipBenefits
                .Include(mb => mb.CustomerMembership)
                .Where(mb => mb.CustomerMembership.CustomerId == customerId &&
                           mb.IsActive && 
                           !mb.IsDeleted &&
                           mb.CustomerMembership.IsActive &&
                           !mb.CustomerMembership.IsDeleted &&
                           (mb.StartDate == null || mb.StartDate <= now) &&
                           (mb.EndDate == null || mb.EndDate >= now) &&
                           (mb.MaxUsages == null || mb.UsageCount < mb.MaxUsages))
                .OrderBy(mb => mb.Type)
                .ThenBy(mb => mb.Value)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available benefits for customer {CustomerId}", customerId);
            throw;
        }
    }
}