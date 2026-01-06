using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository implementation for customer membership operations
/// </summary>
public class CustomerMembershipRepository : Repository<CustomerMembership>, ICustomerMembershipRepository
{
    public CustomerMembershipRepository(PosDbContext context, ILogger<Repository<CustomerMembership>> logger) 
        : base(context, logger)
    {
    }

    /// <summary>
    /// Get membership by customer ID
    /// </summary>
    public async Task<CustomerMembership?> GetByCustomerIdAsync(Guid customerId)
    {
        try
        {
            return await _context.CustomerMemberships
                .Include(cm => cm.Benefits)
                .Include(cm => cm.Customer)
                .FirstOrDefaultAsync(cm => cm.CustomerId == customerId && !cm.IsDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer membership for customer {CustomerId}", customerId);
            throw;
        }
    }

    /// <summary>
    /// Get all memberships by tier
    /// </summary>
    public async Task<List<CustomerMembership>> GetByTierAsync(MembershipTier tier)
    {
        try
        {
            return await _context.CustomerMemberships
                .Include(cm => cm.Customer)
                .Where(cm => cm.Tier == tier && cm.IsActive && !cm.IsDeleted)
                .OrderBy(cm => cm.JoinDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting memberships by tier {Tier}", tier);
            throw;
        }
    }

    /// <summary>
    /// Get memberships expiring within specified days
    /// </summary>
    public async Task<List<CustomerMembership>> GetExpiringMembershipsAsync(int daysFromNow)
    {
        try
        {
            var expiryDate = DateTime.UtcNow.AddDays(daysFromNow);
            
            return await _context.CustomerMemberships
                .Include(cm => cm.Customer)
                .Where(cm => cm.ExpiryDate.HasValue && 
                           cm.ExpiryDate.Value <= expiryDate && 
                           cm.IsActive && 
                           !cm.IsDeleted)
                .OrderBy(cm => cm.ExpiryDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expiring memberships within {Days} days", daysFromNow);
            throw;
        }
    }

    /// <summary>
    /// Get active memberships with benefits
    /// </summary>
    public async Task<List<CustomerMembership>> GetActiveMembershipsWithBenefitsAsync()
    {
        try
        {
            return await _context.CustomerMemberships
                .Include(cm => cm.Customer)
                .Include(cm => cm.Benefits.Where(b => b.IsActive && !b.IsDeleted))
                .Where(cm => cm.IsActive && !cm.IsDeleted)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active memberships with benefits");
            throw;
        }
    }

    /// <summary>
    /// Update membership tier based on spending
    /// </summary>
    public async Task<bool> UpdateMembershipTierAsync(Guid customerMembershipId, MembershipTier newTier, decimal totalSpent)
    {
        try
        {
            var membership = await GetByIdAsync(customerMembershipId);
            if (membership == null)
            {
                _logger.LogWarning("Customer membership {Id} not found for tier update", customerMembershipId);
                return false;
            }

            membership.Tier = newTier;
            membership.TotalSpentForTier = totalSpent;
            membership.LastUpdated = DateTime.UtcNow;

            await UpdateAsync(membership);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating membership tier for {Id}", customerMembershipId);
            throw;
        }
    }

    /// <summary>
    /// Get membership statistics by tier
    /// </summary>
    public async Task<Dictionary<MembershipTier, int>> GetMembershipStatisticsByTierAsync()
    {
        try
        {
            return await _context.CustomerMemberships
                .Where(cm => cm.IsActive && !cm.IsDeleted)
                .GroupBy(cm => cm.Tier)
                .ToDictionaryAsync(g => g.Key, g => g.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting membership statistics by tier");
            throw;
        }
    }
}