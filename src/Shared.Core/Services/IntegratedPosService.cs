using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Integrated POS service that coordinates all advanced features
/// </summary>
public class IntegratedPosService : IIntegratedPosService
{
    private readonly ISaleService _saleService;
    private readonly IProductService _productService;
    private readonly IMembershipService _membershipService;
    private readonly IDiscountService _discountService;
    private readonly IConfigurationService _configurationService;
    private readonly ILicenseService _licenseService;
    private readonly IWeightBasedPricingService _weightBasedPricingService;

    public IntegratedPosService(
        ISaleService saleService,
        IProductService productService,
        IMembershipService membershipService,
        IDiscountService discountService,
        IConfigurationService configurationService,
        ILicenseService licenseService,
        IWeightBasedPricingService weightBasedPricingService)
    {
        _saleService = saleService;
        _productService = productService;
        _membershipService = membershipService;
        _discountService = discountService;
        _configurationService = configurationService;
        _licenseService = licenseService;
        _weightBasedPricingService = weightBasedPricingService;
    }

    public async Task<IntegratedSaleResult> CreateIntegratedSaleAsync(IntegratedSaleRequest request)
    {
        var result = new IntegratedSaleResult();

        try
        {
            // Validate system first
            var systemValidation = await ValidateSystemForSaleAsync();
            if (!systemValidation.IsValid)
            {
                result.IsValid = false;
                result.Errors.AddRange(systemValidation.ValidationErrors);
                return result;
            }

            // Create sale with customer if membership number provided
            Sale sale;
            if (!string.IsNullOrEmpty(request.MembershipNumber))
            {
                sale = await _saleService.CreateSaleWithCustomerAsync(
                    request.InvoiceNumber, 
                    request.DeviceId, 
                    request.MembershipNumber);
                
                // Get customer information
                if (sale.Customer != null)
                {
                    result.Customer = MapCustomerToDto(sale.Customer);
                }
            }
            else
            {
                sale = await _saleService.CreateSaleAsync(request.InvoiceNumber, request.DeviceId);
            }

            result.Sale = await MapSaleToDto(sale);
            result.Calculation = await _saleService.CalculateFullSaleTotalAsync(sale);
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    public async Task<IntegratedSaleResult> AddItemToIntegratedSaleAsync(AddItemToSaleRequest request)
    {
        var result = new IntegratedSaleResult();

        try
        {
            // Get product information
            var product = await _productService.GetProductByIdAsync(request.ProductId);
            if (product == null)
            {
                result.IsValid = false;
                result.Errors.Add("Product not found");
                return result;
            }

            // Check if product is weight-based
            if (product.IsWeightBased)
            {
                result.IsValid = false;
                result.Errors.Add("Weight-based products must be added using AddWeightBasedItemToIntegratedSaleAsync");
                return result;
            }

            // Use override price if provided, otherwise use product price
            var unitPrice = request.UnitPriceOverride ?? product.UnitPrice;

            // Add item to sale
            var sale = await _saleService.AddItemToSaleAsync(
                request.SaleId, 
                request.ProductId, 
                request.Quantity, 
                unitPrice, 
                request.BatchNumber);

            result.Sale = await MapSaleToDto(sale);
            result.Calculation = await _saleService.CalculateFullSaleTotalAsync(sale);

            // Get customer information if exists
            if (sale.Customer != null)
            {
                result.Customer = MapCustomerToDto(sale.Customer);
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    public async Task<IntegratedSaleResult> AddWeightBasedItemToIntegratedSaleAsync(AddWeightBasedItemRequest request)
    {
        var result = new IntegratedSaleResult();

        try
        {
            // Get product information
            var product = await _productService.GetProductByIdAsync(request.ProductId);
            if (product == null)
            {
                result.IsValid = false;
                result.Errors.Add("Product not found");
                return result;
            }

            // Validate product is weight-based
            if (!product.IsWeightBased)
            {
                result.IsValid = false;
                result.Errors.Add("Product is not weight-based");
                return result;
            }

            // Validate weight
            if (!await _weightBasedPricingService.ValidateWeightAsync(request.Weight, product))
            {
                result.IsValid = false;
                result.Errors.Add("Invalid weight value");
                return result;
            }

            // Add weight-based item to sale
            var sale = await _saleService.AddWeightBasedItemToSaleAsync(
                request.SaleId, 
                request.ProductId, 
                request.Weight, 
                request.BatchNumber);

            result.Sale = await MapSaleToDto(sale);
            result.Calculation = await _saleService.CalculateFullSaleTotalAsync(sale);

            // Get customer information if exists
            if (sale.Customer != null)
            {
                result.Customer = MapCustomerToDto(sale.Customer);
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    public async Task<IntegratedSaleResult> CompleteIntegratedSaleAsync(CompleteSaleRequest request)
    {
        var result = new IntegratedSaleResult();

        try
        {
            // Complete sale with full integration
            var sale = await _saleService.CompleteSaleAsync(request.SaleId, request.PaymentMethod);

            result.Sale = await MapSaleToDto(sale);
            result.Calculation = await _saleService.CalculateFullSaleTotalAsync(sale);

            // Get customer information if exists
            if (sale.Customer != null)
            {
                result.Customer = MapCustomerToDto(sale.Customer);
                
                // Get membership discount details
                var membershipDiscount = await _membershipService.CalculateMembershipDiscountAsync(sale.Customer, sale);
                result.MembershipDiscount = membershipDiscount;
            }

            // Add any warnings about applied discounts
            if (result.Calculation.AppliedDiscounts.Any())
            {
                result.Warnings.Add($"Applied {result.Calculation.AppliedDiscounts.Count} discount(s)");
            }

            if (result.MembershipDiscount?.DiscountAmount > 0)
            {
                result.Warnings.Add($"Applied membership discount: {result.MembershipDiscount.DiscountAmount:C}");
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    public async Task<IntegratedSaleResult> GetIntegratedSaleAsync(Guid saleId)
    {
        var result = new IntegratedSaleResult();

        try
        {
            // This would require getting the sale from repository
            // For now, return a placeholder
            result.Errors.Add("GetIntegratedSaleAsync not fully implemented - requires sale repository access");
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    public async Task<SystemValidationResult> ValidateSystemForSaleAsync()
    {
        var result = new SystemValidationResult();

        try
        {
            // Check license status
            var licenseValidation = await _licenseService.ValidateLicenseAsync();
            result.LicenseStatus = licenseValidation.Status;
            
            if (result.LicenseStatus != LicenseStatus.Active)
            {
                result.IsValid = false;
                result.ValidationErrors.Add($"License is not active: {result.LicenseStatus}");
                
                if (result.LicenseStatus == LicenseStatus.Expired)
                {
                    result.ValidationErrors.Add("Please renew your license to continue using the system");
                }
            }

            // Check trial time if applicable
            var remainingTrialTime = await _licenseService.GetRemainingTrialTimeAsync();
            if (remainingTrialTime > TimeSpan.Zero)
            {
                result.RemainingTrialTime = remainingTrialTime;
                if (remainingTrialTime.TotalDays <= 7)
                {
                    result.Warnings.Add($"Trial expires in {remainingTrialTime.TotalDays:F0} days");
                }
            }

            // Check enabled features
            var features = new[] { "WeightBasedPricing", "MembershipSystem", "DiscountSystem", "AdvancedReporting" };
            foreach (var feature in features)
            {
                if (await _licenseService.IsFeatureEnabledAsync(feature))
                {
                    result.EnabledFeatures.Add(feature);
                }
                else
                {
                    result.DisabledFeatures.Add(feature);
                }
            }

            // Validate configuration
            try
            {
                var currencySettings = await _configurationService.GetCurrencySettingsAsync();
                var taxSettings = await _configurationService.GetTaxSettingsAsync();
                var businessSettings = await _configurationService.GetBusinessSettingsAsync();
                
                if (string.IsNullOrEmpty(businessSettings.BusinessName))
                {
                    result.Warnings.Add("Business name not configured");
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Configuration validation warning: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.ValidationErrors.Add($"System validation error: {ex.Message}");
        }

        return result;
    }

    public async Task<PosConfigurationSummary> GetPosConfigurationAsync()
    {
        var summary = new PosConfigurationSummary();

        try
        {
            summary.Currency = await _configurationService.GetCurrencySettingsAsync();
            summary.Tax = await _configurationService.GetTaxSettingsAsync();
            summary.Business = await _configurationService.GetBusinessSettingsAsync();
            summary.Localization = await _configurationService.GetLocalizationSettingsAsync();

            // Check feature availability
            summary.WeightBasedPricingEnabled = await _licenseService.IsFeatureEnabledAsync("WeightBasedPricing");
            summary.MembershipSystemEnabled = await _licenseService.IsFeatureEnabledAsync("MembershipSystem");
            summary.DiscountSystemEnabled = await _licenseService.IsFeatureEnabledAsync("DiscountSystem");

            // Get weight precision from configuration
            summary.WeightPrecision = await _configurationService.GetConfigurationAsync("WeightPrecision", 3);
        }
        catch (Exception)
        {
            // Return default configuration on error
        }

        return summary;
    }

    private async Task<EnhancedSaleDto> MapSaleToDto(Sale sale)
    {
        // This is a simplified mapping - in a real implementation,
        // you'd want to use AutoMapper or similar
        return new EnhancedSaleDto
        {
            Id = sale.Id,
            InvoiceNumber = sale.InvoiceNumber,
            TotalAmount = sale.TotalAmount,
            DiscountAmount = sale.DiscountAmount,
            TaxAmount = sale.TaxAmount,
            MembershipDiscountAmount = sale.MembershipDiscountAmount,
            PaymentMethod = sale.PaymentMethod,
            CreatedAt = sale.CreatedAt,
            DeviceId = sale.DeviceId,
            CustomerId = sale.CustomerId,
            CustomerName = sale.Customer?.Name,
            MembershipNumber = sale.Customer?.MembershipNumber
        };
    }

    private CustomerDto MapCustomerToDto(Customer customer)
    {
        return new CustomerDto
        {
            Id = customer.Id,
            MembershipNumber = customer.MembershipNumber,
            Name = customer.Name,
            Email = customer.Email,
            Phone = customer.Phone,
            JoinDate = customer.JoinDate,
            Tier = customer.Tier,
            TotalSpent = customer.TotalSpent,
            VisitCount = customer.VisitCount,
            LastVisit = customer.LastVisit,
            IsActive = customer.IsActive
        };
    }
}