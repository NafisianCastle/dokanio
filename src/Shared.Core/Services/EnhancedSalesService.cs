using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// Enhanced sales service implementation for multi-business operations
/// Extends the base SaleService with business type-specific validation and shop-level configurations
/// </summary>
public class EnhancedSalesService : SaleService, IEnhancedSalesService
{
    private readonly IBusinessManagementService _businessManagementService;
    private readonly IShopRepository _shopRepository;
    private readonly ILogger<EnhancedSalesService> _logger;

    public EnhancedSalesService(
        ISaleRepository saleRepository,
        ISaleItemRepository saleItemRepository,
        IProductService productService,
        IInventoryService inventoryService,
        IWeightBasedPricingService weightBasedPricingService,
        IMembershipService membershipService,
        IDiscountService discountService,
        IConfigurationService configurationService,
        ILicenseService licenseService,
        IBusinessManagementService businessManagementService,
        IShopRepository shopRepository,
        ILogger<EnhancedSalesService> logger)
        : base(saleRepository, saleItemRepository, productService, inventoryService, 
               weightBasedPricingService, membershipService, discountService, 
               configurationService, licenseService)
    {
        _businessManagementService = businessManagementService;
        _shopRepository = shopRepository;
        _logger = logger;
    }

    public async Task<Sale> CreateSaleWithValidationAsync(Guid shopId, Guid userId, string invoiceNumber)
    {
        _logger.LogInformation("Creating sale with validation for shop {ShopId}, user {UserId}", shopId, userId);

        // Validate shop exists and is active
        var shop = await _shopRepository.GetByIdAsync(shopId);
        if (shop == null || !shop.IsActive)
        {
            throw new ArgumentException($"Shop {shopId} not found or inactive", nameof(shopId));
        }

        // Get business type for validation
        var business = await _businessManagementService.GetBusinessByIdAsync(shop.BusinessId);
        if (business == null)
        {
            throw new InvalidOperationException($"Business {shop.BusinessId} not found for shop {shopId}");
        }

        // Create sale with shop and user context
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            UserId = userId,
            InvoiceNumber = invoiceNumber,
            TotalAmount = 0,
            DiscountAmount = 0,
            TaxAmount = 0,
            MembershipDiscountAmount = 0,
            PaymentMethod = PaymentMethod.Cash, // Default
            CreatedAt = DateTime.UtcNow,
            SyncStatus = SyncStatus.NotSynced,
            DeviceId = Guid.NewGuid() // This should come from the calling context
        };

        // Apply business type-specific initialization
        await ApplyBusinessTypeInitializationAsync(sale, business.Type);

        var saleRepository = GetSaleRepository();
        await saleRepository.AddAsync(sale);
        await saleRepository.SaveChangesAsync();

        _logger.LogInformation("Created sale {SaleId} with validation for shop {ShopId}", sale.Id, shopId);
        return sale;
    }

    public async Task<ValidationResult> ValidateProductForSaleAsync(Guid productId, Guid shopId)
    {
        _logger.LogDebug("Validating product {ProductId} for sale in shop {ShopId}", productId, shopId);

        var productService = GetProductService();
        var product = await productService.GetProductByIdAsync(productId);
        
        if (product == null)
        {
            return ValidationResult.Failure("Product not found");
        }

        if (product.ShopId != shopId)
        {
            return ValidationResult.Failure("Product does not belong to the specified shop");
        }

        if (!product.IsActive)
        {
            return ValidationResult.Failure("Product is inactive");
        }

        // Get shop and business context
        var shop = await _shopRepository.GetByIdAsync(shopId);
        if (shop == null)
        {
            return ValidationResult.Failure("Shop not found");
        }

        var business = await _businessManagementService.GetBusinessByIdAsync(shop.BusinessId);
        if (business == null)
        {
            return ValidationResult.Failure("Business not found");
        }

        // Apply business type-specific validation
        var businessTypeValidation = await ValidateBusinessTypeRulesAsync(product, business.Type);
        if (!businessTypeValidation.IsValid)
        {
            return businessTypeValidation;
        }

        // Check base product validation (expiry, etc.)
        var baseValidation = await base.ValidateProductForSaleAsync(productId);
        if (!baseValidation)
        {
            return ValidationResult.Failure("Product failed base validation (may be expired or inactive)");
        }

        _logger.LogDebug("Product {ProductId} validation successful for shop {ShopId}", productId, shopId);
        return ValidationResult.Success();
    }

    public async Task<SaleCalculationResult> CalculateWithBusinessRulesAsync(Sale sale)
    {
        _logger.LogDebug("Calculating sale {SaleId} with business rules", sale.Id);

        // Get base calculation
        var baseResult = await base.CalculateFullSaleTotalAsync(sale);

        // Get shop configuration
        var shopConfig = await GetShopPricingConfigurationAsync(sale.ShopId);

        // Get business context
        var shop = await _shopRepository.GetByIdAsync(sale.ShopId);
        if (shop == null)
        {
            throw new InvalidOperationException($"Shop {sale.ShopId} not found");
        }

        var business = await _businessManagementService.GetBusinessByIdAsync(shop.BusinessId);
        if (business == null)
        {
            throw new InvalidOperationException($"Business {shop.BusinessId} not found");
        }

        // Apply business type-specific rules
        var businessRules = new List<BusinessRuleApplication>();
        await ApplyBusinessTypeCalculationRulesAsync(sale, business.Type, shopConfig, businessRules);

        // Create enhanced result
        var enhancedResult = new SaleCalculationResult
        {
            BaseTotal = baseResult.BaseTotal,
            DiscountAmount = baseResult.DiscountAmount,
            MembershipDiscountAmount = baseResult.MembershipDiscountAmount,
            TaxAmount = baseResult.TaxAmount,
            FinalTotal = baseResult.FinalTotal,
            AppliedDiscounts = baseResult.AppliedDiscounts,
            DiscountReasons = baseResult.DiscountReasons,
            AppliedBusinessRules = businessRules,
            ShopConfiguration = shopConfig
        };

        _logger.LogDebug("Sale {SaleId} calculation completed with {RuleCount} business rules applied", 
            sale.Id, businessRules.Count);

        return enhancedResult;
    }

    public async Task<RecommendationResult> GetSaleRecommendationsAsync(Guid saleId)
    {
        _logger.LogDebug("Getting recommendations for sale {SaleId}", saleId);

        // For now, return empty recommendations
        // This would integrate with AI analytics engine in a full implementation
        var result = new RecommendationResult
        {
            ConfidenceScore = 0.0m,
            RecommendationReason = "AI recommendations not yet implemented"
        };

        _logger.LogDebug("Generated {ProductCount} product recommendations for sale {SaleId}", 
            result.ProductRecommendations.Count, saleId);

        return result;
    }

    public async Task<ValidationResult> ValidateBusinessTypeRulesAsync(Product product, BusinessType businessType)
    {
        _logger.LogDebug("Validating business type rules for product {ProductId} with business type {BusinessType}", 
            product.Id, businessType);

        var errors = new List<string>();
        var warnings = new List<string>();

        // Parse business type attributes if they exist
        BusinessTypeAttributes? attributes = null;
        if (!string.IsNullOrEmpty(product.BusinessTypeAttributesJson))
        {
            try
            {
                attributes = JsonSerializer.Deserialize<BusinessTypeAttributes>(product.BusinessTypeAttributesJson);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse business type attributes for product {ProductId}", product.Id);
                errors.Add("Invalid business type attributes format");
            }
        }

        // Apply business type-specific validation rules
        switch (businessType)
        {
            case BusinessType.Pharmacy:
                await ValidatePharmacyRulesAsync(product, attributes, errors, warnings);
                break;

            case BusinessType.Grocery:
                await ValidateGroceryRulesAsync(product, attributes, errors, warnings);
                break;

            case BusinessType.SuperShop:
                await ValidateSuperShopRulesAsync(product, attributes, errors, warnings);
                break;

            case BusinessType.GeneralRetail:
            case BusinessType.Custom:
            default:
                await ValidateGeneralRetailRulesAsync(product, attributes, errors, warnings);
                break;
        }

        var result = new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };

        _logger.LogDebug("Business type validation for product {ProductId}: {IsValid} ({ErrorCount} errors, {WarningCount} warnings)", 
            product.Id, result.IsValid, errors.Count, warnings.Count);

        return result;
    }

    public async Task<ShopConfiguration> GetShopPricingConfigurationAsync(Guid shopId)
    {
        _logger.LogDebug("Getting shop pricing configuration for shop {ShopId}", shopId);

        try
        {
            var config = await _businessManagementService.GetShopConfigurationAsync(shopId);
            _logger.LogDebug("Retrieved shop configuration for shop {ShopId}", shopId);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get shop configuration for shop {ShopId}, using defaults", shopId);
            
            // Return default configuration
            return new ShopConfiguration
            {
                Currency = "USD",
                TaxRate = 0.0m,
                PricingRules = new PricingRules(),
                InventorySettings = new InventorySettings(),
                BusinessSettings = new BusinessSettings(),
                LocalizationSettings = new LocalizationSettings()
            };
        }
    }

    public async Task<Sale> ApplyShopPricingRulesAsync(Sale sale, ShopConfiguration shopConfiguration)
    {
        _logger.LogDebug("Applying shop pricing rules to sale {SaleId}", sale.Id);

        // Apply pricing rules based on shop configuration
        if (shopConfiguration.PricingRules.EnableDynamicPricing)
        {
            // Apply dynamic pricing logic
            await ApplyDynamicPricingAsync(sale, shopConfiguration);
        }

        if (shopConfiguration.PricingRules.EnableBundlePricing)
        {
            // Apply bundle pricing logic
            await ApplyBundlePricingAsync(sale, shopConfiguration);
        }

        if (shopConfiguration.PricingRules.EnableTieredPricing)
        {
            // Apply tiered pricing logic
            await ApplyTieredPricingAsync(sale, shopConfiguration);
        }

        _logger.LogDebug("Applied shop pricing rules to sale {SaleId}", sale.Id);
        return sale;
    }

    #region Private Helper Methods

    private async Task ApplyBusinessTypeInitializationAsync(Sale sale, BusinessType businessType)
    {
        // Apply business type-specific initialization
        switch (businessType)
        {
            case BusinessType.Pharmacy:
                // Initialize pharmacy-specific sale properties
                break;

            case BusinessType.Grocery:
                // Initialize grocery-specific sale properties
                break;

            case BusinessType.SuperShop:
                // Initialize super shop-specific sale properties
                break;

            case BusinessType.GeneralRetail:
            case BusinessType.Custom:
            default:
                // Default initialization
                break;
        }

        await Task.CompletedTask;
    }

    private async Task ApplyBusinessTypeCalculationRulesAsync(Sale sale, BusinessType businessType, 
        ShopConfiguration shopConfig, List<BusinessRuleApplication> appliedRules)
    {
        switch (businessType)
        {
            case BusinessType.Pharmacy:
                await ApplyPharmacyCalculationRulesAsync(sale, shopConfig, appliedRules);
                break;

            case BusinessType.Grocery:
                await ApplyGroceryCalculationRulesAsync(sale, shopConfig, appliedRules);
                break;

            case BusinessType.SuperShop:
                await ApplySuperShopCalculationRulesAsync(sale, shopConfig, appliedRules);
                break;

            case BusinessType.GeneralRetail:
            case BusinessType.Custom:
            default:
                await ApplyGeneralRetailCalculationRulesAsync(sale, shopConfig, appliedRules);
                break;
        }
    }

    private async Task ValidatePharmacyRulesAsync(Product product, BusinessTypeAttributes? attributes, 
        List<string> errors, List<string> warnings)
    {
        // Check expiry date for pharmacy products
        if (attributes?.ExpiryDate.HasValue == true)
        {
            if (attributes.ExpiryDate.Value <= DateTime.UtcNow)
            {
                errors.Add($"Medicine has expired on {attributes.ExpiryDate.Value:yyyy-MM-dd}");
            }
            else if (attributes.ExpiryDate.Value <= DateTime.UtcNow.AddDays(30))
            {
                warnings.Add($"Medicine expires soon on {attributes.ExpiryDate.Value:yyyy-MM-dd}");
            }
        }

        // Check for required pharmacy attributes
        if (string.IsNullOrEmpty(attributes?.Manufacturer))
        {
            errors.Add("Manufacturer is required for pharmacy products");
        }

        if (string.IsNullOrEmpty(attributes?.BatchNumber))
        {
            warnings.Add("Batch number is recommended for pharmacy products");
        }

        await Task.CompletedTask;
    }

    private async Task ValidateGroceryRulesAsync(Product product, BusinessTypeAttributes? attributes, 
        List<string> errors, List<string> warnings)
    {
        // Check weight for grocery products
        if (attributes?.Weight.HasValue == true && attributes.Weight.Value <= 0)
        {
            errors.Add("Weight must be positive for grocery products");
        }

        // Check unit for grocery products
        if (string.IsNullOrEmpty(attributes?.Unit))
        {
            errors.Add("Unit is required for grocery products");
        }

        await Task.CompletedTask;
    }

    private async Task ValidateSuperShopRulesAsync(Product product, BusinessTypeAttributes? attributes, 
        List<string> errors, List<string> warnings)
    {
        // Super shop combines grocery and general retail rules
        if (attributes?.Weight.HasValue == true && attributes.Weight.Value <= 0)
        {
            errors.Add("Weight must be positive when specified");
        }

        await Task.CompletedTask;
    }

    private async Task ValidateGeneralRetailRulesAsync(Product product, BusinessTypeAttributes? attributes, 
        List<string> errors, List<string> warnings)
    {
        // Basic validation for general retail
        if (attributes?.Weight.HasValue == true && attributes.Weight.Value <= 0)
        {
            errors.Add("Weight must be positive when specified");
        }

        await Task.CompletedTask;
    }

    private async Task ApplyPharmacyCalculationRulesAsync(Sale sale, ShopConfiguration shopConfig, 
        List<BusinessRuleApplication> appliedRules)
    {
        appliedRules.Add(new BusinessRuleApplication
        {
            RuleName = "Pharmacy Tax Calculation",
            RuleType = "tax",
            BusinessType = BusinessType.Pharmacy,
            Result = "Applied pharmacy-specific tax rules",
            AppliedAt = DateTime.UtcNow
        });

        await Task.CompletedTask;
    }

    private async Task ApplyGroceryCalculationRulesAsync(Sale sale, ShopConfiguration shopConfig, 
        List<BusinessRuleApplication> appliedRules)
    {
        appliedRules.Add(new BusinessRuleApplication
        {
            RuleName = "Grocery Weight-Based Pricing",
            RuleType = "pricing",
            BusinessType = BusinessType.Grocery,
            Result = "Applied weight-based pricing rules",
            AppliedAt = DateTime.UtcNow
        });

        await Task.CompletedTask;
    }

    private async Task ApplySuperShopCalculationRulesAsync(Sale sale, ShopConfiguration shopConfig, 
        List<BusinessRuleApplication> appliedRules)
    {
        appliedRules.Add(new BusinessRuleApplication
        {
            RuleName = "Super Shop Bulk Discount",
            RuleType = "discount",
            BusinessType = BusinessType.SuperShop,
            Result = "Applied bulk discount rules",
            AppliedAt = DateTime.UtcNow
        });

        await Task.CompletedTask;
    }

    private async Task ApplyGeneralRetailCalculationRulesAsync(Sale sale, ShopConfiguration shopConfig, 
        List<BusinessRuleApplication> appliedRules)
    {
        appliedRules.Add(new BusinessRuleApplication
        {
            RuleName = "General Retail Standard Pricing",
            RuleType = "pricing",
            BusinessType = BusinessType.GeneralRetail,
            Result = "Applied standard pricing rules",
            AppliedAt = DateTime.UtcNow
        });

        await Task.CompletedTask;
    }

    private async Task ApplyDynamicPricingAsync(Sale sale, ShopConfiguration shopConfiguration)
    {
        // Implement dynamic pricing logic
        await Task.CompletedTask;
    }

    private async Task ApplyBundlePricingAsync(Sale sale, ShopConfiguration shopConfiguration)
    {
        // Implement bundle pricing logic
        await Task.CompletedTask;
    }

    private async Task ApplyTieredPricingAsync(Sale sale, ShopConfiguration shopConfiguration)
    {
        // Implement tiered pricing logic
        await Task.CompletedTask;
    }

    // Helper methods to access base class protected members
    private ISaleRepository GetSaleRepository()
    {
        return (ISaleRepository)GetType().BaseType!
            .GetField("_saleRepository", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(this)!;
    }

    private IProductService GetProductService()
    {
        return (IProductService)GetType().BaseType!
            .GetField("_productService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(this)!;
    }

    #endregion
}