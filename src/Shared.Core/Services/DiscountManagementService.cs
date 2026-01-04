using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Repositories;

namespace Shared.Core.Services;

public class DiscountManagementService : IDiscountManagementService
{
    private readonly IDiscountRepository _discountRepository;
    private readonly IDiscountService _discountService;
    private readonly ILogger<DiscountManagementService> _logger;

    public DiscountManagementService(
        IDiscountRepository discountRepository,
        IDiscountService discountService,
        ILogger<DiscountManagementService> logger)
    {
        _discountRepository = discountRepository;
        _discountService = discountService;
        _logger = logger;
    }

    public async Task<IEnumerable<DiscountDto>> GetAllDiscountsAsync()
    {
        _logger.LogDebug("Getting all discounts for management");
        return await _discountService.GetAllDiscountsAsync();
    }

    public async Task<DiscountDto?> GetDiscountByIdAsync(Guid discountId)
    {
        _logger.LogDebug("Getting discount {DiscountId} for management", discountId);
        return await _discountService.GetDiscountByIdAsync(discountId);
    }

    public async Task<DiscountDto> CreateDiscountAsync(DiscountCreateRequest request)
    {
        _logger.LogInformation("Creating new discount: {DiscountName}", request.Name);
        
        var discount = await _discountService.CreateDiscountAsync(request);
        var discountDto = await _discountService.GetDiscountByIdAsync(discount.Id);
        
        _logger.LogInformation("Successfully created discount {DiscountName} with ID {DiscountId}", 
            discount.Name, discount.Id);
        
        return discountDto!;
    }

    public async Task<DiscountDto> UpdateDiscountAsync(Guid discountId, DiscountCreateRequest request)
    {
        _logger.LogInformation("Updating discount {DiscountId}: {DiscountName}", discountId, request.Name);
        
        var discount = await _discountService.UpdateDiscountAsync(discountId, request);
        var discountDto = await _discountService.GetDiscountByIdAsync(discount.Id);
        
        _logger.LogInformation("Successfully updated discount {DiscountName} with ID {DiscountId}", 
            discount.Name, discount.Id);
        
        return discountDto!;
    }

    public async Task<bool> DeleteDiscountAsync(Guid discountId)
    {
        _logger.LogInformation("Deleting discount {DiscountId}", discountId);
        
        var result = await _discountService.DeleteDiscountAsync(discountId);
        
        if (result)
        {
            _logger.LogInformation("Successfully deleted discount {DiscountId}", discountId);
        }
        else
        {
            _logger.LogWarning("Failed to delete discount {DiscountId} - discount not found", discountId);
        }
        
        return result;
    }

    public async Task<bool> ActivateDiscountAsync(Guid discountId)
    {
        _logger.LogInformation("Activating discount {DiscountId}", discountId);
        
        var discount = await _discountRepository.GetByIdAsync(discountId);
        if (discount == null)
        {
            _logger.LogWarning("Cannot activate discount {DiscountId} - discount not found", discountId);
            return false;
        }

        discount.IsActive = true;
        discount.UpdatedAt = DateTime.UtcNow;
        
        await _discountRepository.UpdateAsync(discount);
        await _discountRepository.SaveChangesAsync();
        
        _logger.LogInformation("Successfully activated discount {DiscountId}", discountId);
        return true;
    }

    public async Task<bool> DeactivateDiscountAsync(Guid discountId)
    {
        _logger.LogInformation("Deactivating discount {DiscountId}", discountId);
        
        var discount = await _discountRepository.GetByIdAsync(discountId);
        if (discount == null)
        {
            _logger.LogWarning("Cannot deactivate discount {DiscountId} - discount not found", discountId);
            return false;
        }

        discount.IsActive = false;
        discount.UpdatedAt = DateTime.UtcNow;
        
        await _discountRepository.UpdateAsync(discount);
        await _discountRepository.SaveChangesAsync();
        
        _logger.LogInformation("Successfully deactivated discount {DiscountId}", discountId);
        return true;
    }

    public async Task<IEnumerable<DiscountDto>> GetActiveDiscountsAsync()
    {
        _logger.LogDebug("Getting active discounts for management");
        
        var activeDiscounts = await _discountRepository.GetActiveDiscountsAsync();
        return activeDiscounts.Select(MapToDto);
    }

    public async Task<IEnumerable<DiscountDto>> GetDiscountsByProductAsync(Guid productId)
    {
        _logger.LogDebug("Getting discounts for product {ProductId}", productId);
        
        var discounts = await _discountRepository.GetDiscountsByProductAsync(productId);
        return discounts.Select(MapToDto);
    }

    public async Task<IEnumerable<DiscountDto>> GetDiscountsByCategoryAsync(string category)
    {
        _logger.LogDebug("Getting discounts for category {Category}", category);
        
        var discounts = await _discountRepository.GetDiscountsByCategoryAsync(category);
        return discounts.Select(MapToDto);
    }

    public async Task<DiscountValidationResult> ValidateDiscountAsync(DiscountCreateRequest request)
    {
        _logger.LogDebug("Validating discount request: {DiscountName}", request.Name);
        
        // Create a temporary discount entity for validation
        var discount = new Discount
        {
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            Value = request.Value,
            Scope = request.Scope,
            ProductId = request.ProductId,
            Category = request.Category,
            RequiredMembershipTier = request.RequiredMembershipTier,
            MinimumQuantity = request.MinimumQuantity,
            MinimumAmount = request.MinimumAmount,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IsActive = request.IsActive
        };
        
        return await _discountService.ValidateDiscountRulesAsync(discount);
    }

    private DiscountDto MapToDto(Discount discount)
    {
        return new DiscountDto
        {
            Id = discount.Id,
            Name = discount.Name,
            Description = discount.Description,
            Type = discount.Type,
            Value = discount.Value,
            Scope = discount.Scope,
            ProductId = discount.ProductId,
            ProductName = discount.Product?.Name,
            Category = discount.Category,
            RequiredMembershipTier = discount.RequiredMembershipTier,
            MinimumQuantity = discount.MinimumQuantity,
            MinimumAmount = discount.MinimumAmount,
            StartDate = discount.StartDate,
            EndDate = discount.EndDate,
            StartTime = discount.StartTime,
            EndTime = discount.EndTime,
            IsActive = discount.IsActive,
            CreatedAt = discount.CreatedAt,
            UpdatedAt = discount.UpdatedAt
        };
    }
}