using Shared.Core.DTOs;
using Shared.Core.Entities;

namespace Shared.Core.Services;

public interface IDiscountManagementService
{
    Task<IEnumerable<DiscountDto>> GetAllDiscountsAsync();
    Task<DiscountDto?> GetDiscountByIdAsync(Guid discountId);
    Task<DiscountDto> CreateDiscountAsync(DiscountCreateRequest request);
    Task<DiscountDto> UpdateDiscountAsync(Guid discountId, DiscountCreateRequest request);
    Task<bool> DeleteDiscountAsync(Guid discountId);
    Task<bool> ActivateDiscountAsync(Guid discountId);
    Task<bool> DeactivateDiscountAsync(Guid discountId);
    Task<IEnumerable<DiscountDto>> GetActiveDiscountsAsync();
    Task<IEnumerable<DiscountDto>> GetDiscountsByProductAsync(Guid productId);
    Task<IEnumerable<DiscountDto>> GetDiscountsByCategoryAsync(string category);
    Task<DiscountValidationResult> ValidateDiscountAsync(DiscountCreateRequest request);
}