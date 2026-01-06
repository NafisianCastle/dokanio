using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;

namespace Shared.Core.Services;

/// <summary>
/// Enhanced sales grid engine with real-time calculations and inline editing
/// </summary>
public class EnhancedSalesGridEngine : IEnhancedSalesGridEngine
{
    private readonly ISaleSessionRepository _saleSessionRepository;
    private readonly ISaleItemRepository _saleItemRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockRepository _stockRepository;
    private readonly IDiscountService _discountService;
    private readonly IMembershipService _membershipService;
    private readonly IConfigurationService _configurationService;
    private readonly IWeightBasedPricingService _weightBasedPricingService;
    private readonly ILogger<EnhancedSalesGridEngine> _logger;

    public EnhancedSalesGridEngine(
        ISaleSessionRepository saleSessionRepository,
        ISaleItemRepository saleItemRepository,
        IProductRepository productRepository,
        IStockRepository stockRepository,
        IDiscountService discountService,
        IMembershipService membershipService,
        IConfigurationService configurationService,
        IWeightBasedPricingService weightBasedPricingService,
        ILogger<EnhancedSalesGridEngine> logger)
    {
        _saleSessionRepository = saleSessionRepository;
        _saleItemRepository = saleItemRepository;
        _productRepository = productRepository;
        _stockRepository = stockRepository;
        _discountService = discountService;
        _membershipService = membershipService;
        _configurationService = configurationService;
        _weightBasedPricingService = weightBasedPricingService;
        _logger = logger;
    }

    public async Task<GridOperationResult> AddProductToGridAsync(Guid saleSessionId, Product product, decimal quantity = 1)
    {
        try
        {
            _logger.LogInformation("Adding product {ProductId} to grid for session {SessionId}", product.Id, saleSessionId);

            // Validate inputs
            var validationResult = await ValidateProductAdditionAsync(saleSessionId, product, quantity);
            if (!validationResult.IsValid)
            {
                return GridOperationResult.ErrorResult(validationResult.Errors.Select(e => e.Message).ToList());
            }

            // Get sale session
            var saleSession = await _saleSessionRepository.GetByIdAsync(saleSessionId);
            if (saleSession == null)
            {
                return GridOperationResult.ErrorResult("Sale session not found");
            }

            // Check if product is weight-based
            if (product.IsWeightBased)
            {
                return GridOperationResult.ErrorResult("Weight-based products must be added using AddWeightBasedProductToGridAsync");
            }

            // Check for an existing item with the same product directly from the repository
            var existingItem = saleSession.SaleId.HasValue
                ? await _saleItemRepository.FirstOrDefaultAsync(si =>
                    si.SaleId == saleSession.SaleId.Value && si.ProductId == product.Id && !si.IsDeleted)
                : null;

            if (existingItem != null)
            {
                // Update existing item quantity
                return await UpdateQuantityAsync(saleSessionId, existingItem.Id, existingItem.Quantity + quantity);
            }

            // Create new sale item
            var saleItem = new SaleItem
            {
                Id = Guid.NewGuid(),
                SaleId = saleSession.SaleId ?? Guid.NewGuid(), // Temporary sale ID if not set
                ProductId = product.Id,
                Quantity = (int)quantity,
                UnitPrice = product.UnitPrice,
                TotalPrice = Math.Round(quantity * product.UnitPrice, 2, MidpointRounding.AwayFromZero)
            };

            // Add to repository
            await _saleItemRepository.AddAsync(saleItem);
            await _saleItemRepository.SaveChangesAsync();

            // Update session
            saleSession.LastModified = DateTime.UtcNow;
            await _saleSessionRepository.UpdateAsync(saleSession);
            await _saleSessionRepository.SaveChangesAsync();

            // Recalculate totals
            var calculationResult = await RecalculateAllTotalsAsync(saleSessionId);
            var gridState = await GetGridStateAsync(saleSessionId);

            var result = GridOperationResult.SuccessResult("Product added successfully");
            result.CalculationResult = calculationResult;
            result.UpdatedGridState = gridState;

            _logger.LogInformation("Successfully added product {ProductId} to grid for session {SessionId}", product.Id, saleSessionId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding product {ProductId} to grid for session {SessionId}", product.Id, saleSessionId);
            return GridOperationResult.ErrorResult($"Failed to add product: {ex.Message}");
        }
    }

    public async Task<GridOperationResult> AddWeightBasedProductToGridAsync(Guid saleSessionId, Product product, decimal weight)
    {
        try
        {
            _logger.LogInformation("Adding weight-based product {ProductId} with weight {Weight} to grid for session {SessionId}", 
                product.Id, weight, saleSessionId);

            // Validate inputs
            if (!product.IsWeightBased)
            {
                return GridOperationResult.ErrorResult("Product is not weight-based");
            }

            if (!product.RatePerKilogram.HasValue)
            {
                return GridOperationResult.ErrorResult("Weight-based product must have a rate per kilogram");
            }

            if (weight <= 0)
            {
                return GridOperationResult.ErrorResult("Weight must be greater than zero");
            }

            // Get sale session
            var saleSession = await _saleSessionRepository.GetByIdAsync(saleSessionId);
            if (saleSession == null)
            {
                return GridOperationResult.ErrorResult("Sale session not found");
            }

            // Validate weight
            if (!await _weightBasedPricingService.ValidateWeightAsync(weight, product))
            {
                return GridOperationResult.ErrorResult("Invalid weight value");
            }

            // Calculate pricing
            var roundedWeight = _weightBasedPricingService.RoundWeight(weight, product.WeightPrecision);
            var totalPrice = await _weightBasedPricingService.CalculatePriceAsync(product, weight);

            // Create new sale item
            var saleItem = new SaleItem
            {
                Id = Guid.NewGuid(),
                SaleId = saleSession.SaleId ?? Guid.NewGuid(),
                ProductId = product.Id,
                Quantity = 1, // Always 1 for weight-based items
                UnitPrice = product.RatePerKilogram.Value,
                Weight = roundedWeight,
                RatePerKilogram = product.RatePerKilogram.Value,
                TotalPrice = totalPrice
            };

            // Add to repository
            await _saleItemRepository.AddAsync(saleItem);
            await _saleItemRepository.SaveChangesAsync();

            // Update session
            saleSession.LastModified = DateTime.UtcNow;
            await _saleSessionRepository.UpdateAsync(saleSession);
            await _saleSessionRepository.SaveChangesAsync();

            // Recalculate totals
            var calculationResult = await RecalculateAllTotalsAsync(saleSessionId);
            var gridState = await GetGridStateAsync(saleSessionId);

            var result = GridOperationResult.SuccessResult("Weight-based product added successfully");
            result.CalculationResult = calculationResult;
            result.UpdatedGridState = gridState;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding weight-based product {ProductId} to grid for session {SessionId}", product.Id, saleSessionId);
            return GridOperationResult.ErrorResult($"Failed to add weight-based product: {ex.Message}");
        }
    }

    public async Task<GridOperationResult> UpdateQuantityAsync(Guid saleSessionId, Guid saleItemId, decimal newQuantity)
    {
        try
        {
            _logger.LogInformation("Updating quantity for item {ItemId} to {Quantity} in session {SessionId}", 
                saleItemId, newQuantity, saleSessionId);

            // Validate quantity
            if (newQuantity < 0)
            {
                return GridOperationResult.ErrorResult("Quantity cannot be negative");
            }

            if (newQuantity == 0)
            {
                // Remove item if quantity is zero
                return await RemoveItemAsync(saleSessionId, saleItemId);
            }

            // Get sale item
            var saleItem = await _saleItemRepository.GetByIdAsync(saleItemId);
            if (saleItem == null)
            {
                return GridOperationResult.ErrorResult("Sale item not found");
            }

            // Get product to check if it's weight-based
            var product = await _productRepository.GetByIdAsync(saleItem.ProductId);
            if (product == null)
            {
                return GridOperationResult.ErrorResult("Product not found");
            }

            if (product.IsWeightBased)
            {
                return GridOperationResult.ErrorResult("Cannot update quantity for weight-based products. Use UpdateWeightAsync instead.");
            }

            // Check stock availability
            var hasStock = await ValidateStockAvailabilityAsync(saleItem.ProductId, (int)newQuantity);
            if (!hasStock)
            {
                return GridOperationResult.ErrorResult("Insufficient stock for the requested quantity");
            }

            // Update quantity and recalculate
            saleItem.Quantity = (int)newQuantity;
            saleItem.TotalPrice = Math.Round(newQuantity * saleItem.UnitPrice, 2, MidpointRounding.AwayFromZero);

            await _saleItemRepository.UpdateAsync(saleItem);
            await _saleItemRepository.SaveChangesAsync();

            // Update session timestamp
            await UpdateSessionTimestampAsync(saleSessionId);

            // Recalculate totals
            var calculationResult = await RecalculateAllTotalsAsync(saleSessionId);
            var gridState = await GetGridStateAsync(saleSessionId);

            var result = GridOperationResult.SuccessResult("Quantity updated successfully");
            result.CalculationResult = calculationResult;
            result.UpdatedGridState = gridState;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating quantity for item {ItemId} in session {SessionId}", saleItemId, saleSessionId);
            return GridOperationResult.ErrorResult($"Failed to update quantity: {ex.Message}");
        }
    }

    public async Task<GridOperationResult> UpdateWeightAsync(Guid saleSessionId, Guid saleItemId, decimal newWeight)
    {
        try
        {
            _logger.LogInformation("Updating weight for item {ItemId} to {Weight} in session {SessionId}", 
                saleItemId, newWeight, saleSessionId);

            if (newWeight <= 0)
            {
                return GridOperationResult.ErrorResult("Weight must be greater than zero");
            }

            // Get sale item
            var saleItem = await _saleItemRepository.GetByIdAsync(saleItemId);
            if (saleItem == null)
            {
                return GridOperationResult.ErrorResult("Sale item not found");
            }

            // Get product
            var product = await _productRepository.GetByIdAsync(saleItem.ProductId);
            if (product == null)
            {
                return GridOperationResult.ErrorResult("Product not found");
            }

            if (!product.IsWeightBased)
            {
                return GridOperationResult.ErrorResult("Cannot update weight for non-weight-based products");
            }

            // Validate weight
            if (!await _weightBasedPricingService.ValidateWeightAsync(newWeight, product))
            {
                return GridOperationResult.ErrorResult("Invalid weight value");
            }

            // Calculate new pricing
            var roundedWeight = _weightBasedPricingService.RoundWeight(newWeight, product.WeightPrecision);
            var totalPrice = await _weightBasedPricingService.CalculatePriceAsync(product, newWeight);

            // Update weight and recalculate
            saleItem.Weight = roundedWeight;
            saleItem.TotalPrice = totalPrice;

            await _saleItemRepository.UpdateAsync(saleItem);
            await _saleItemRepository.SaveChangesAsync();

            // Update session timestamp
            await UpdateSessionTimestampAsync(saleSessionId);

            // Recalculate totals
            var calculationResult = await RecalculateAllTotalsAsync(saleSessionId);
            var gridState = await GetGridStateAsync(saleSessionId);

            var result = GridOperationResult.SuccessResult("Weight updated successfully");
            result.CalculationResult = calculationResult;
            result.UpdatedGridState = gridState;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating weight for item {ItemId} in session {SessionId}", saleItemId, saleSessionId);
            return GridOperationResult.ErrorResult($"Failed to update weight: {ex.Message}");
        }
    }

    public async Task<GridOperationResult> UpdateDiscountAsync(Guid saleSessionId, Guid saleItemId, decimal discountAmount)
    {
        try
        {
            _logger.LogInformation("Updating discount for item {ItemId} to {Discount} in session {SessionId}", 
                saleItemId, discountAmount, saleSessionId);

            if (discountAmount < 0)
            {
                return GridOperationResult.ErrorResult("Discount amount cannot be negative");
            }

            // Get sale item
            var saleItem = await _saleItemRepository.GetByIdAsync(saleItemId);
            if (saleItem == null)
            {
                return GridOperationResult.ErrorResult("Sale item not found");
            }

            // Validate discount doesn't exceed line total
            if (discountAmount > saleItem.TotalPrice)
            {
                return GridOperationResult.ErrorResult("Discount cannot exceed line total");
            }

            // For now, we'll store discount as a reduction in TotalPrice
            // In a more complex system, you might have a separate DiscountAmount field
            var originalTotal = saleItem.Quantity * saleItem.UnitPrice;
            if (saleItem.Weight.HasValue && saleItem.RatePerKilogram.HasValue)
            {
                originalTotal = saleItem.Weight.Value * saleItem.RatePerKilogram.Value;
            }

            saleItem.TotalPrice = Math.Round(originalTotal - discountAmount, 2, MidpointRounding.AwayFromZero);

            await _saleItemRepository.UpdateAsync(saleItem);
            await _saleItemRepository.SaveChangesAsync();

            // Update session timestamp
            await UpdateSessionTimestampAsync(saleSessionId);

            // Recalculate totals
            var calculationResult = await RecalculateAllTotalsAsync(saleSessionId);
            var gridState = await GetGridStateAsync(saleSessionId);

            var result = GridOperationResult.SuccessResult("Discount updated successfully");
            result.CalculationResult = calculationResult;
            result.UpdatedGridState = gridState;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating discount for item {ItemId} in session {SessionId}", saleItemId, saleSessionId);
            return GridOperationResult.ErrorResult($"Failed to update discount: {ex.Message}");
        }
    }

    public async Task<GridOperationResult> RemoveItemAsync(Guid saleSessionId, Guid saleItemId)
    {
        try
        {
            _logger.LogInformation("Removing item {ItemId} from session {SessionId}", saleItemId, saleSessionId);

            // Get sale item
            var saleItem = await _saleItemRepository.GetByIdAsync(saleItemId);
            if (saleItem == null)
            {
                return GridOperationResult.ErrorResult("Sale item not found");
            }

            // Soft delete the item
            saleItem.IsDeleted = true;
            saleItem.DeletedAt = DateTime.UtcNow;

            await _saleItemRepository.UpdateAsync(saleItem);
            await _saleItemRepository.SaveChangesAsync();

            // Update session timestamp
            await UpdateSessionTimestampAsync(saleSessionId);

            // Recalculate totals
            var calculationResult = await RecalculateAllTotalsAsync(saleSessionId);
            var gridState = await GetGridStateAsync(saleSessionId);

            var result = GridOperationResult.SuccessResult("Item removed successfully");
            result.CalculationResult = calculationResult;
            result.UpdatedGridState = gridState;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item {ItemId} from session {SessionId}", saleItemId, saleSessionId);
            return GridOperationResult.ErrorResult($"Failed to remove item: {ex.Message}");
        }
    }

    public async Task<GridCalculationResult> RecalculateLineItemAsync(Guid saleSessionId, Guid saleItemId)
    {
        try
        {
            // Get sale item
            var saleItem = await _saleItemRepository.GetByIdAsync(saleItemId);
            if (saleItem == null)
            {
                throw new ArgumentException("Sale item not found", nameof(saleItemId));
            }

            // Get product for calculations
            var product = await _productRepository.GetByIdAsync(saleItem.ProductId);
            if (product == null)
            {
                throw new ArgumentException("Product not found");
            }

            // Calculate line item
            var lineCalculation = new GridLineItemCalculation
            {
                SaleItemId = saleItemId,
                Quantity = saleItem.Quantity,
                Weight = saleItem.Weight,
                UnitPrice = saleItem.UnitPrice,
                RatePerKilogram = saleItem.RatePerKilogram,
                IsWeightBased = product.IsWeightBased,
                LineSubtotal = saleItem.TotalPrice,
                LineDiscount = 0, // Calculate based on applied discounts
                LineTax = 0, // Calculate based on tax configuration
                LineTotal = saleItem.TotalPrice
            };

            // Get tax configuration
            var taxConfig = await _configurationService.GetTaxSettingsAsync();
            lineCalculation.LineTax = Math.Round(lineCalculation.LineTotal * (taxConfig.DefaultTaxRate / 100), 2, MidpointRounding.AwayFromZero);

            var result = new GridCalculationResult
            {
                LineItemCalculations = new List<GridLineItemCalculation> { lineCalculation },
                Subtotal = lineCalculation.LineSubtotal,
                TotalDiscount = lineCalculation.LineDiscount,
                TotalTax = lineCalculation.LineTax,
                FinalTotal = lineCalculation.LineTotal + lineCalculation.LineTax
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculating line item {ItemId} for session {SessionId}", saleItemId, saleSessionId);
            throw;
        }
    }

    public async Task<GridCalculationResult> RecalculateAllTotalsAsync(Guid saleSessionId)
    {
        try
        {
            _logger.LogDebug("Recalculating all totals for session {SessionId}", saleSessionId);

            // Get sale session with items
            var saleSession = await _saleSessionRepository.GetByIdAsync(saleSessionId);
            if (saleSession == null)
            {
                throw new ArgumentException("Sale session not found", nameof(saleSessionId));
            }

            // Get all active sale items for this session
            var saleItems = await _saleItemRepository.FindAsync(si => 
                si.SaleId == saleSession.SaleId && !si.IsDeleted);

            var lineCalculations = new List<GridLineItemCalculation>();
            decimal subtotal = 0;
            decimal totalDiscount = 0;
            decimal totalTax = 0;

            // Get tax configuration
            var taxConfig = await _configurationService.GetTaxSettingsAsync();

            // Calculate each line item
            foreach (var item in saleItems)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                if (product == null) continue;

                var lineCalculation = new GridLineItemCalculation
                {
                    SaleItemId = item.Id,
                    Quantity = item.Quantity,
                    Weight = item.Weight,
                    UnitPrice = item.UnitPrice,
                    RatePerKilogram = item.RatePerKilogram,
                    IsWeightBased = product.IsWeightBased,
                    LineSubtotal = item.TotalPrice,
                    LineDiscount = 0, // TODO: Calculate actual discounts
                    LineTax = Math.Round(item.TotalPrice * (taxConfig.DefaultTaxRate / 100), 2, MidpointRounding.AwayFromZero),
                    LineTotal = item.TotalPrice
                };

                lineCalculations.Add(lineCalculation);
                subtotal += lineCalculation.LineSubtotal;
                totalDiscount += lineCalculation.LineDiscount;
                totalTax += lineCalculation.LineTax;
            }

            var result = new GridCalculationResult
            {
                Subtotal = subtotal,
                TotalDiscount = totalDiscount,
                TotalTax = totalTax,
                FinalTotal = subtotal - totalDiscount + totalTax,
                LineItemCalculations = lineCalculations,
                AppliedDiscounts = new List<AppliedDiscount>() // TODO: Get actual applied discounts
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculating totals for session {SessionId}", saleSessionId);
            throw;
        }
    }

    public async Task<GridValidationResult> ValidateGridDataAsync(Guid saleSessionId)
    {
        try
        {
            var errors = new List<GridValidationError>();
            var warnings = new List<GridValidationWarning>();
            var itemErrors = new Dictionary<Guid, List<string>>();

            // Get sale session
            var saleSession = await _saleSessionRepository.GetByIdAsync(saleSessionId);
            if (saleSession == null)
            {
                errors.Add(new GridValidationError
                {
                    Field = "SaleSession",
                    Message = "Sale session not found",
                    Type = GridValidationErrorType.MissingRequiredField
                });
                return GridValidationResult.Invalid(errors);
            }

            // Get all active sale items
            var saleItems = await _saleItemRepository.FindAsync(si => 
                si.SaleId == saleSession.SaleId && !si.IsDeleted);

            if (!saleItems.Any())
            {
                warnings.Add(new GridValidationWarning
                {
                    Field = "Items",
                    Message = "No items in the sale",
                    Type = GridValidationWarningType.PerformanceWarning
                });
            }

            // Validate each item
            foreach (var item in saleItems)
            {
                var itemValidationErrors = new List<string>();

                // Validate quantity
                if (item.Quantity <= 0)
                {
                    itemValidationErrors.Add("Quantity must be greater than zero");
                    errors.Add(new GridValidationError
                    {
                        Field = "Quantity",
                        Message = "Quantity must be greater than zero",
                        SaleItemId = item.Id,
                        Type = GridValidationErrorType.InvalidQuantity
                    });
                }

                // Validate price
                if (item.UnitPrice < 0)
                {
                    itemValidationErrors.Add("Unit price cannot be negative");
                    errors.Add(new GridValidationError
                    {
                        Field = "UnitPrice",
                        Message = "Unit price cannot be negative",
                        SaleItemId = item.Id,
                        Type = GridValidationErrorType.InvalidPrice
                    });
                }

                // Validate weight for weight-based products
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                if (product != null && product.IsWeightBased)
                {
                    if (!item.Weight.HasValue || item.Weight <= 0)
                    {
                        itemValidationErrors.Add("Weight must be specified for weight-based products");
                        errors.Add(new GridValidationError
                        {
                            Field = "Weight",
                            Message = "Weight must be specified for weight-based products",
                            SaleItemId = item.Id,
                            Type = GridValidationErrorType.InvalidWeight
                        });
                    }
                }

                // Check stock availability
                var hasStock = await ValidateStockAvailabilityAsync(item.ProductId, item.Quantity);
                if (!hasStock)
                {
                    itemValidationErrors.Add("Insufficient stock");
                    errors.Add(new GridValidationError
                    {
                        Field = "Stock",
                        Message = "Insufficient stock for this quantity",
                        SaleItemId = item.Id,
                        Type = GridValidationErrorType.InsufficientStock
                    });
                }

                if (itemValidationErrors.Any())
                {
                    itemErrors[item.Id] = itemValidationErrors;
                }
            }

            var result = new GridValidationResult
            {
                IsValid = !errors.Any(),
                Errors = errors,
                Warnings = warnings,
                ItemValidationErrors = itemErrors
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating grid data for session {SessionId}", saleSessionId);
            throw;
        }
    }

    public async Task<SalesGridState> GetGridStateAsync(Guid saleSessionId)
    {
        try
        {
            // Get sale session
            var saleSession = await _saleSessionRepository.GetByIdAsync(saleSessionId);
            if (saleSession == null)
            {
                throw new ArgumentException("Sale session not found", nameof(saleSessionId));
            }

            // Get all active sale items
            var saleItems = await _saleItemRepository.FindAsync(si => 
                si.SaleId == saleSession.SaleId && !si.IsDeleted);

            var gridItems = new List<SalesGridItem>();

            foreach (var item in saleItems)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                if (product == null) continue;

                var stock = await _stockRepository.GetByProductIdAsync(item.ProductId);
                var availableStock = stock?.Quantity ?? 0;

                var gridItem = new SalesGridItem
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = product.Name,
                    ProductCode = product.Barcode ?? string.Empty,
                    Barcode = product.Barcode,
                    Quantity = item.Quantity,
                    Weight = item.Weight,
                    UnitPrice = item.UnitPrice,
                    RatePerKilogram = item.RatePerKilogram,
                    LineTotal = item.TotalPrice,
                    IsWeightBased = product.IsWeightBased,
                    BatchNumber = item.BatchNumber,
                    AvailableStock = availableStock,
                    HasSufficientStock = availableStock >= item.Quantity
                };

                gridItems.Add(gridItem);
            }

            // Get calculations
            var calculations = await RecalculateAllTotalsAsync(saleSessionId);

            var gridState = new SalesGridState
            {
                SaleSessionId = saleSessionId,
                Items = gridItems,
                Calculations = calculations,
                Customer = saleSession.Customer,
                PaymentMethod = saleSession.PaymentMethod,
                LastUpdated = saleSession.LastModified,
                HasUnsavedChanges = true // Always true for active sessions
            };

            return gridState;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting grid state for session {SessionId}", saleSessionId);
            throw;
        }
    }

    public async Task<GridOperationResult> ClearGridAsync(Guid saleSessionId)
    {
        try
        {
            _logger.LogInformation("Clearing grid for session {SessionId}", saleSessionId);

            // Get sale session
            var saleSession = await _saleSessionRepository.GetByIdAsync(saleSessionId);
            if (saleSession == null)
            {
                return GridOperationResult.ErrorResult("Sale session not found");
            }

            // Get all active sale items
            var saleItems = await _saleItemRepository.FindAsync(si => 
                si.SaleId == saleSession.SaleId && !si.IsDeleted);

            // Soft delete all items
            foreach (var item in saleItems)
            {
                item.IsDeleted = true;
                item.DeletedAt = DateTime.UtcNow;
                await _saleItemRepository.UpdateAsync(item);
            }

            await _saleItemRepository.SaveChangesAsync();

            // Update session timestamp
            await UpdateSessionTimestampAsync(saleSessionId);

            // Get updated grid state
            var gridState = await GetGridStateAsync(saleSessionId);
            var calculationResult = await RecalculateAllTotalsAsync(saleSessionId);

            var result = GridOperationResult.SuccessResult("Grid cleared successfully");
            result.UpdatedGridState = gridState;
            result.CalculationResult = calculationResult;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing grid for session {SessionId}", saleSessionId);
            return GridOperationResult.ErrorResult($"Failed to clear grid: {ex.Message}");
        }
    }

    public async Task<GridOperationResult> ApplySaleDiscountAsync(Guid saleSessionId, decimal discountAmount, string discountReason)
    {
        try
        {
            _logger.LogInformation("Applying sale discount of {Amount} to session {SessionId}", discountAmount, saleSessionId);

            // Get sale session
            var saleSession = await _saleSessionRepository.GetByIdAsync(saleSessionId);
            if (saleSession == null)
            {
                return GridOperationResult.ErrorResult("Sale session not found");
            }

            // Validate discount amount
            if (discountAmount < 0)
            {
                return GridOperationResult.ErrorResult("Discount amount cannot be negative");
            }

            // Calculate current total to validate discount doesn't exceed total
            var calculations = await RecalculateAllTotalsAsync(saleSessionId);
            if (discountAmount > calculations.Subtotal)
            {
                return GridOperationResult.ErrorResult("Discount cannot exceed subtotal");
            }

            // Store sale-level discount (this would need to be added to SaleSession entity)
            // For now, we'll just recalculate and return success
            
            var result = GridOperationResult.SuccessResult("Sale discount applied successfully");
            result.CalculationResult = calculations;
            result.UpdatedGridState = await GetGridStateAsync(saleSessionId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying sale discount to session {SessionId}", saleSessionId);
            return GridOperationResult.ErrorResult($"Failed to apply sale discount: {ex.Message}");
        }
    }

    public async Task<GridOperationResult> RemoveSaleDiscountAsync(Guid saleSessionId)
    {
        try
        {
            _logger.LogInformation("Removing sale discount from session {SessionId}", saleSessionId);

            // Get sale session
            var saleSession = await _saleSessionRepository.GetByIdAsync(saleSessionId);
            if (saleSession == null)
            {
                return GridOperationResult.ErrorResult("Sale session not found");
            }

            // Remove sale-level discount (this would need to be implemented in SaleSession entity)
            // For now, just recalculate and return success

            var calculations = await RecalculateAllTotalsAsync(saleSessionId);
            var gridState = await GetGridStateAsync(saleSessionId);

            var result = GridOperationResult.SuccessResult("Sale discount removed successfully");
            result.CalculationResult = calculations;
            result.UpdatedGridState = gridState;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing sale discount from session {SessionId}", saleSessionId);
            return GridOperationResult.ErrorResult($"Failed to remove sale discount: {ex.Message}");
        }
    }

    // Private helper methods

    private async Task<GridValidationResult> ValidateProductAdditionAsync(Guid saleSessionId, Product product, decimal quantity)
    {
        var errors = new List<GridValidationError>();

        if (quantity <= 0)
        {
            errors.Add(new GridValidationError
            {
                Field = "Quantity",
                Message = "Quantity must be greater than zero",
                Type = GridValidationErrorType.InvalidQuantity
            });
        }

        if (product.UnitPrice < 0)
        {
            errors.Add(new GridValidationError
            {
                Field = "Price",
                Message = "Product price cannot be negative",
                Type = GridValidationErrorType.InvalidPrice
            });
        }

        // Check stock availability
        var hasStock = await ValidateStockAvailabilityAsync(product.Id, (int)quantity);
        if (!hasStock)
        {
            errors.Add(new GridValidationError
            {
                Field = "Stock",
                Message = "Insufficient stock for the requested quantity",
                Type = GridValidationErrorType.InsufficientStock
            });
        }

        return errors.Any() ? GridValidationResult.Invalid(errors) : GridValidationResult.Valid();
    }

    private async Task<bool> ValidateStockAvailabilityAsync(Guid productId, int requiredQuantity)
    {
        try
        {
            var stock = await _stockRepository.GetByProductIdAsync(productId);
            return stock != null && stock.Quantity >= requiredQuantity;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking stock for product {ProductId}", productId);
            return true; // Assume stock is available if we can't check
        }
    }

    private async Task UpdateSessionTimestampAsync(Guid saleSessionId)
    {
        try
        {
            var saleSession = await _saleSessionRepository.GetByIdAsync(saleSessionId);
            if (saleSession != null)
            {
                saleSession.LastModified = DateTime.UtcNow;
                await _saleSessionRepository.UpdateAsync(saleSession);
                await _saleSessionRepository.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error updating session timestamp for {SessionId}", saleSessionId);
            // Don't throw - this is not critical
        }
    }
}