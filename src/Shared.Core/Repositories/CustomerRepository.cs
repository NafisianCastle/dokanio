using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Repositories;

public class CustomerRepository : Repository<Customer>, ICustomerRepository
{
    public CustomerRepository(PosDbContext context, ILogger<CustomerRepository> logger) : base(context, logger)
    {
    }

    public async Task<Customer?> GetByMembershipNumberAsync(string membershipNumber)
    {
        return await _context.Customers
            .FirstOrDefaultAsync(c => c.MembershipNumber == membershipNumber && c.IsActive);
    }

    public async Task<IEnumerable<Customer>> GetByTierAsync(MembershipTier tier)
    {
        return await _context.Customers
            .Where(c => c.Tier == tier && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Customer>> GetActiveCustomersAsync()
    {
        return await _context.Customers
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Customer>> GetTopCustomersBySpendingAsync(int count)
    {
        return await _context.Customers
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.TotalSpent)
            .Take(count)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalSpentByCustomerAsync(Guid customerId)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId);
        
        return customer?.TotalSpent ?? 0;
    }

    public async Task<int> GetVisitCountByCustomerAsync(Guid customerId)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId);
        
        return customer?.VisitCount ?? 0;
    }

    public async Task<IEnumerable<Customer>> GetCustomersJoinedAfterAsync(DateTime date)
    {
        return await _context.Customers
            .Where(c => c.JoinDate >= date && c.IsActive)
            .OrderBy(c => c.JoinDate)
            .ToListAsync();
    }

    public async Task<bool> IsMembershipNumberUniqueAsync(string membershipNumber, Guid? excludeCustomerId = null)
    {
        var query = _context.Customers.Where(c => c.MembershipNumber == membershipNumber);
        
        if (excludeCustomerId.HasValue)
        {
            query = query.Where(c => c.Id != excludeCustomerId.Value);
        }
        
        return !await query.AnyAsync();
    }
}