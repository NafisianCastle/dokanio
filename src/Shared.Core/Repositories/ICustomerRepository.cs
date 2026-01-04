using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Repositories;

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByMembershipNumberAsync(string membershipNumber);
    Task<IEnumerable<Customer>> GetByTierAsync(MembershipTier tier);
    Task<IEnumerable<Customer>> GetActiveCustomersAsync();
    Task<IEnumerable<Customer>> GetTopCustomersBySpendingAsync(int count);
    Task<decimal> GetTotalSpentByCustomerAsync(Guid customerId);
    Task<int> GetVisitCountByCustomerAsync(Guid customerId);
    Task<IEnumerable<Customer>> GetCustomersJoinedAfterAsync(DateTime date);
    Task<bool> IsMembershipNumberUniqueAsync(string membershipNumber, Guid? excludeCustomerId = null);
}