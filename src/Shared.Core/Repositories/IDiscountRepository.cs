using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Repositories;

public interface IDiscountRepository : IRepository<Discount>
{
    Task<IEnumerable<Discount>> GetActiveDiscountsAsync();
    Task<IEnumerable<Discount>> GetDiscountsByProductAsync(Guid productId);
    Task<IEnumerable<Discount>> GetDiscountsByCategoryAsync(string category);
    Task<IEnumerable<Discount>> GetDiscountsByMembershipTierAsync(MembershipTier tier);
    Task<IEnumerable<Discount>> GetDiscountsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<Discount>> GetApplicableDiscountsAsync(Guid? productId, string? category, MembershipTier? membershipTier, DateTime checkDate, TimeSpan checkTime);
    Task<bool> IsDiscountActiveAsync(Guid discountId, DateTime checkDate, TimeSpan checkTime);
}