using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

public interface IDiscountService
{
    Task<List<Discount>> GetApplicableDiscountsAsync(Product product, Customer? customer, DateTime saleDate);
    Task<DiscountCalculationResult> CalculateDiscountsAsync(Sale sale, Customer? customer);
    Task<DiscountValidationResult> ValidateDiscountRulesAsync(Discount discount);
    Task<Discount> CreateDiscountAsync(DiscountCreateRequest request);
    Task<Discount> UpdateDiscountAsync(Guid discountId, DiscountCreateRequest request);
    Task<bool> DeleteDiscountAsync(Guid discountId);
    Task<bool> IsDiscountActiveAsync(Discount discount, DateTime checkDate);
    Task<IEnumerable<DiscountDto>> GetAllDiscountsAsync();
    Task<DiscountDto?> GetDiscountByIdAsync(Guid discountId);
    Task<decimal> CalculateDiscountAmountAsync(Discount discount, decimal baseAmount, int quantity = 1);
}