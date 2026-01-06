using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;

namespace Shared.Core.Services;

public class DiscountService : IDiscountService
{
    private readonly IDiscountRepository _discountRepository;
    private readonly IProductRepository _productRepository;
    private readonly IDeviceContextService _deviceContextService;
    private readonly ILogger<DiscountService> _logger;

    public DiscountService(
        IDiscountRepository discountRepository,
        IProductRepository productRepository,
        IDeviceContextService deviceContextService,
        ILogger<DiscountService> logger)
    {
        _discountRepository = discountRepository;
        _productRepository = productRepository;
        _deviceContextService = deviceContextService;
        _logger = logger;
    }

    public async Task<List<Discount>> GetApplicableDiscountsAsync(Product product, Customer? customer, DateTime saleDate)
    {
        var currentTime = saleDate.TimeOfDay;
        var membershipTier = customer?.Tier;

        var applicableDiscounts = await _discountRepository.GetApplicableDiscountsAsync(
            product.Id, 
            product.Category, 
            membershipTier, 
            saleDate, 
            currentTime);

        return applicableDiscounts.ToList();
    }

    public async Task<DiscountCalculationResult> CalculateDiscountsAsync(Sale sale, Customer? customer)
    {
        var result = new DiscountCalculationResult();
        var saleDate = sale.CreatedAt;

        foreach (var saleItem in sale.Items)
        {
            var product = saleItem.Product;
            if (product == null) continue;

            var applicableDiscounts = await GetApplicableDiscountsAsync(product, customer, saleDate);

            foreach (var discount in applicableDiscounts)
            {
                // Check quantity requirements
                if (discount.MinimumQuantity.HasValue && saleItem.Quantity < discount.MinimumQuantity.Value)
                    continue;

                // Check minimum amount requirements for sale-level discounts
                if (discount.Scope == DiscountScope.Sale && discount.MinimumAmount.HasValue && 
                    sale.TotalAmount < discount.MinimumAmount.Value)
                    continue;

                var discountAmount = await CalculateDiscountAmountAsync(discount, saleItem.TotalPrice, saleItem.Quantity);

                if (discountAmount > 0)
                {
                    var appliedDiscount = new AppliedDiscount
                    {
                        DiscountId = discount.Id,
                        DiscountName = discount.Name,
                        Type = discount.Type,
                        Value = discount.Value,
                        CalculatedAmount = discountAmount,
                        Reason = GenerateDiscountReason(discount, saleItem, customer)
                    };

                    result.AppliedDiscounts.Add(appliedDiscount);
                    result.TotalDiscountAmount += discountAmount;
                    result.DiscountReasons.Add(appliedDiscount.Reason);
                }
            }
        }

        return result;
    }

    public async Task<DiscountValidationResult> ValidateDiscountRulesAsync(Discount discount)
    {
        var result = new DiscountValidationResult { IsValid = true };

        // Validate basic properties
        if (string.IsNullOrWhiteSpace(discount.Name))
        {
            result.IsValid = false;
            result.ValidationErrors.Add("Discount name is required");
        }

        if (discount.Value <= 0)
        {
            result.IsValid = false;
            result.ValidationErrors.Add("Discount value must be greater than zero");
        }

        if (discount.Type == DiscountType.Percentage && discount.Value > 100)
        {
            result.IsValid = false;
            result.ValidationErrors.Add("Percentage discount cannot exceed 100%");
        }

        // Validate date range
        if (discount.StartDate >= discount.EndDate)
        {
            result.IsValid = false;
            result.ValidationErrors.Add("Start date must be before end date");
        }

        // Validate time range
        if (discount.StartTime.HasValue && discount.EndTime.HasValue && 
            discount.StartTime.Value >= discount.EndTime.Value)
        {
            result.IsValid = false;
            result.ValidationErrors.Add("Start time must be before end time");
        }

        // Validate scope-specific requirements
        if (discount.Scope == DiscountScope.Product && !discount.ProductId.HasValue)
        {
            result.IsValid = false;
            result.ValidationErrors.Add("Product ID is required for product-specific discounts");
        }

        if (discount.Scope == DiscountScope.Category && string.IsNullOrWhiteSpace(discount.Category))
        {
            result.IsValid = false;
            result.ValidationErrors.Add("Category is required for category-specific discounts");
        }

        // Validate product exists if specified
        if (discount.ProductId.HasValue)
        {
            var product = await _productRepository.GetByIdAsync(discount.ProductId.Value);
            if (product == null)
            {
                result.IsValid = false;
                result.ValidationErrors.Add("Specified product does not exist");
            }
        }

        return result;
    }

    public async Task<Discount> CreateDiscountAsync(DiscountCreateRequest request)
    {
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
            IsActive = request.IsActive,
            DeviceId = _deviceContextService.GetCurrentDeviceId()
        };

        var validationResult = await ValidateDiscountRulesAsync(discount);
        if (!validationResult.IsValid)
        {
            throw new ArgumentException($"Invalid discount rules: {string.Join(", ", validationResult.ValidationErrors)}");
        }

        await _discountRepository.AddAsync(discount);
        await _discountRepository.SaveChangesAsync();

        _logger.LogInformation("Created discount {DiscountName} with ID {DiscountId}", discount.Name, discount.Id);

        return discount;
    }

    public async Task<Discount> UpdateDiscountAsync(Guid discountId, DiscountCreateRequest request)
    {
        var discount = await _discountRepository.GetByIdAsync(discountId);
        if (discount == null)
            throw new ArgumentException($"Discount with ID {discountId} not found");

        discount.Name = request.Name;
        discount.Description = request.Description;
        discount.Type = request.Type;
        discount.Value = request.Value;
        discount.Scope = request.Scope;
        discount.ProductId = request.ProductId;
        discount.Category = request.Category;
        discount.RequiredMembershipTier = request.RequiredMembershipTier;
        discount.MinimumQuantity = request.MinimumQuantity;
        discount.MinimumAmount = request.MinimumAmount;
        discount.StartDate = request.StartDate;
        discount.EndDate = request.EndDate;
        discount.StartTime = request.StartTime;
        discount.EndTime = request.EndTime;
        discount.IsActive = request.IsActive;
        discount.UpdatedAt = DateTime.UtcNow;

        var validationResult = await ValidateDiscountRulesAsync(discount);
        if (!validationResult.IsValid)
        {
            throw new ArgumentException($"Invalid discount rules: {string.Join(", ", validationResult.ValidationErrors)}");
        }

        await _discountRepository.UpdateAsync(discount);
        await _discountRepository.SaveChangesAsync();

        _logger.LogInformation("Updated discount {DiscountName} with ID {DiscountId}", discount.Name, discount.Id);

        return discount;
    }

    public async Task<bool> DeleteDiscountAsync(Guid discountId)
    {
        var discount = await _discountRepository.GetByIdAsync(discountId);
        if (discount == null)
            return false;

        await _discountRepository.DeleteAsync(discountId);
        await _discountRepository.SaveChangesAsync();

        _logger.LogInformation("Deleted discount {DiscountName} with ID {DiscountId}", discount.Name, discount.Id);

        return true;
    }

    public async Task<bool> IsDiscountActiveAsync(Discount discount, DateTime checkDate)
    {
        if (!discount.IsActive || discount.IsDeleted)
            return false;

        return await _discountRepository.IsDiscountActiveAsync(discount.Id, checkDate, checkDate.TimeOfDay);
    }

    public async Task<IEnumerable<DiscountDto>> GetAllDiscountsAsync()
    {
        var discounts = await _discountRepository.GetAllAsync();
        return discounts.Select(MapToDto);
    }

    public async Task<DiscountDto?> GetDiscountByIdAsync(Guid discountId)
    {
        var discount = await _discountRepository.GetByIdAsync(discountId);
        return discount != null ? MapToDto(discount) : null;
    }

    public async Task<decimal> CalculateDiscountAmountAsync(Discount discount, decimal baseAmount, int quantity = 1)
    {
        if (!discount.IsActive || discount.IsDeleted)
            return 0;

        decimal discountAmount = discount.Type switch
        {
            DiscountType.Percentage => Math.Round(baseAmount * discount.Value / 100, 2),
            DiscountType.FixedAmount => discount.Value * quantity,
            _ => 0
        };

        // Ensure discount doesn't exceed the base amount
        return Math.Min(discountAmount, baseAmount);
    }

    private string GenerateDiscountReason(Discount discount, SaleItem saleItem, Customer? customer)
    {
        var reason = $"{discount.Name}";
        
        if (discount.Type == DiscountType.Percentage)
            reason += $" ({discount.Value}% off)";
        else
            reason += $" (${discount.Value} off)";

        if (customer != null && discount.RequiredMembershipTier.HasValue)
            reason += $" - {customer.Tier} member discount";

        return reason;
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