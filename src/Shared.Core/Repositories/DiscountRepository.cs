using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Repositories;

public class DiscountRepository : Repository<Discount>, IDiscountRepository
{
    public DiscountRepository(PosDbContext context, ILogger<DiscountRepository> logger) : base(context, logger)
    {
    }

    public async Task<IEnumerable<Discount>> GetActiveDiscountsAsync()
    {
        return await _context.Discounts
            .Where(d => d.IsActive && !d.IsDeleted)
            .Include(d => d.Product)
            .OrderBy(d => d.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Discount>> GetDiscountsByProductAsync(Guid productId)
    {
        return await _context.Discounts
            .Where(d => d.IsActive && !d.IsDeleted && 
                       (d.ProductId == productId || d.Scope == DiscountScope.Sale))
            .Include(d => d.Product)
            .ToListAsync();
    }

    public async Task<IEnumerable<Discount>> GetDiscountsByCategoryAsync(string category)
    {
        return await _context.Discounts
            .Where(d => d.IsActive && !d.IsDeleted && 
                       (d.Category == category || d.Scope == DiscountScope.Sale))
            .Include(d => d.Product)
            .ToListAsync();
    }

    public async Task<IEnumerable<Discount>> GetDiscountsByMembershipTierAsync(MembershipTier tier)
    {
        return await _context.Discounts
            .Where(d => d.IsActive && !d.IsDeleted && 
                       (d.RequiredMembershipTier == null || d.RequiredMembershipTier <= tier))
            .Include(d => d.Product)
            .ToListAsync();
    }

    public async Task<IEnumerable<Discount>> GetDiscountsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.Discounts
            .Where(d => d.IsActive && !d.IsDeleted &&
                       d.StartDate <= endDate && d.EndDate >= startDate)
            .Include(d => d.Product)
            .ToListAsync();
    }

    public async Task<IEnumerable<Discount>> GetApplicableDiscountsAsync(
        Guid? productId, 
        string? category, 
        MembershipTier? membershipTier, 
        DateTime checkDate, 
        TimeSpan checkTime)
    {
        var query = _context.Discounts
            .Where(d => d.IsActive && !d.IsDeleted &&
                       d.StartDate <= checkDate && d.EndDate >= checkDate);

        // Filter by time if specified
        query = query.Where(d => 
            (d.StartTime == null && d.EndTime == null) ||
            (d.StartTime <= checkTime && d.EndTime >= checkTime));

        // Filter by membership tier
        if (membershipTier.HasValue)
        {
            query = query.Where(d => 
                d.RequiredMembershipTier == null || 
                d.RequiredMembershipTier <= membershipTier.Value);
        }
        else
        {
            query = query.Where(d => d.RequiredMembershipTier == null);
        }

        // Filter by scope
        query = query.Where(d => 
            d.Scope == DiscountScope.Sale ||
            (d.Scope == DiscountScope.Product && d.ProductId == productId) ||
            (d.Scope == DiscountScope.Category && d.Category == category));

        return await query
            .Include(d => d.Product)
            .OrderByDescending(d => d.Value) // Apply highest value discounts first
            .ToListAsync();
    }

    public async Task<bool> IsDiscountActiveAsync(Guid discountId, DateTime checkDate, TimeSpan checkTime)
    {
        var discount = await _context.Discounts
            .FirstOrDefaultAsync(d => d.Id == discountId && !d.IsDeleted);

        if (discount == null || !discount.IsActive)
            return false;

        // Check date range
        if (checkDate < discount.StartDate || checkDate > discount.EndDate)
            return false;

        // Check time range if specified
        if (discount.StartTime.HasValue && discount.EndTime.HasValue)
        {
            if (checkTime < discount.StartTime.Value || checkTime > discount.EndTime.Value)
                return false;
        }

        return true;
    }
}