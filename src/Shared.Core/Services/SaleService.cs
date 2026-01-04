using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Shared.Core.DTOs;

namespace Shared.Core.Services;

public class SaleService : ISaleService
{
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleItemRepository _saleItemRepository;
    private readonly IProductService _productService;
    private readonly IInventoryService _inventoryService;
    private readonly IWeightBasedPricingService _weightBasedPricingService;
    private readonly IMembershipService _membershipService;
    private readonly IDiscountService _discountService;
    private readonly IConfigurationService _configurationService;
    private readonly ILicenseService _licenseService;

    public SaleService(
        ISaleRepository saleRepository,
        ISaleItemRepository saleItemRepository,
        IProductService productService,
        IInventoryService inventoryService,
        IWeightBasedPricingService weightBasedPricingService,
        IMembershipService membershipService,
        IDiscountService discountService,
        IConfigurationService configurationService,
        ILicenseService licenseService)
    {
        _saleRepository = saleRepository;
        _saleItemRepository = saleItemRepository;
        _productService = productService;
        _inventoryService = inventoryService;
        _weightBasedPricingService = weightBasedPricingService;
        _membershipService = membershipService;
        _discountService = discountService;
        _configurationService = configurationService;
        _licenseService = licenseService;
    }

    public async Task<Sale> CreateSaleAsync(string invoiceNumber, Guid deviceId)
    {
        // Check license before creating sale
        var licenseStatus = await _licenseService.CheckLicenseStatusAsync();
        if (licenseStatus != LicenseStatus.Active)
        {
            throw new InvalidOperationException($"Cannot create sale: License status is {licenseStatus}");
        }

        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = invoiceNumber,
            TotalAmount = 0,
            DiscountAmount = 0,
            TaxAmount = 0,
            MembershipDiscountAmount = 0,
            PaymentMethod = PaymentMethod.Cash, // Default, will be set on completion
            DeviceId = deviceId,
            CreatedAt = DateTime.UtcNow,
            SyncStatus = SyncStatus.NotSynced
        };

        await _saleRepository.AddAsync(sale);
        await _saleRepository.SaveChangesAsync();

        return sale;
    }

    public async Task<Sale> CreateSaleWithCustomerAsync(string invoiceNumber, Guid deviceId, string? membershipNumber = null)
    {
        // Check license before creating sale
        var licenseStatus = await _licenseService.CheckLicenseStatusAsync();
        if (licenseStatus != LicenseStatus.Active)
        {
            throw new InvalidOperationException($"Cannot create sale: License status is {licenseStatus}");
        }

        Customer? customer = null;
        if (!string.IsNullOrEmpty(membershipNumber))
        {
            customer = await _membershipService.GetCustomerByMembershipNumberAsync(membershipNumber);
            if (customer == null)
            {
                throw new ArgumentException($"Customer with membership number {membershipNumber} not found");
            }
        }

        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = invoiceNumber,
            TotalAmount = 0,
            DiscountAmount = 0,
            TaxAmount = 0,
            MembershipDiscountAmount = 0,
            PaymentMethod = PaymentMethod.Cash, // Default, will be set on completion
            CustomerId = customer?.Id,
            Customer = customer,
            DeviceId = deviceId,
            CreatedAt = DateTime.UtcNow,
            SyncStatus = SyncStatus.NotSynced
        };

        await _saleRepository.AddAsync(sale);
        await _saleRepository.SaveChangesAsync();

        return sale;
    }

    public async Task<Sale> AddItemToSaleAsync(Guid saleId, Guid productId, int quantity, decimal unitPrice, string? batchNumber = null)
    {
        // Validate product before adding to sale
        if (!await ValidateProductForSaleAsync(productId))
        {
            throw new InvalidOperationException("Product is not valid for sale (may be expired or inactive)");
        }

        var product = await _productService.GetProductByIdAsync(productId);
        if (product == null)
        {
            throw new ArgumentException("Product not found", nameof(productId));
        }

        // Check if this is a weight-based product - should use AddWeightBasedItemToSaleAsync instead
        if (product.IsWeightBased)
        {
            throw new InvalidOperationException("Weight-based products must be added using AddWeightBasedItemToSaleAsync");
        }

        // Check if we have sufficient stock
        if (!await _inventoryService.HasSufficientStockAsync(productId, quantity))
        {
            throw new InvalidOperationException("Insufficient stock for the requested quantity");
        }

        var sale = await _saleRepository.GetByIdAsync(saleId);
        if (sale == null)
        {
            throw new ArgumentException("Sale not found", nameof(saleId));
        }

        var totalPrice = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);

        var saleItem = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalPrice = totalPrice,
            BatchNumber = batchNumber
        };

        await _saleItemRepository.AddAsync(saleItem);
        await _saleItemRepository.SaveChangesAsync();

        // Recalculate sale total
        sale.TotalAmount = await CalculateSaleTotalAsync(saleId);
        await _saleRepository.UpdateAsync(sale);
        await _saleRepository.SaveChangesAsync();

        return sale;
    }

    public async Task<Sale> AddWeightBasedItemToSaleAsync(Guid saleId, Guid productId, decimal weight, string? batchNumber = null)
    {
        // Validate product before adding to sale
        if (!await ValidateProductForSaleAsync(productId))
        {
            throw new InvalidOperationException("Product is not valid for sale (may be expired or inactive)");
        }

        var product = await _productService.GetProductByIdAsync(productId);
        if (product == null)
        {
            throw new ArgumentException("Product not found", nameof(productId));
        }

        // Validate this is a weight-based product
        if (!product.IsWeightBased)
        {
            throw new InvalidOperationException("Product is not weight-based. Use AddItemToSaleAsync for regular products");
        }

        if (!product.RatePerKilogram.HasValue)
        {
            throw new InvalidOperationException("Weight-based product must have a rate per kilogram defined");
        }

        // Validate weight
        if (!await _weightBasedPricingService.ValidateWeightAsync(weight, product))
        {
            throw new ArgumentException("Invalid weight value", nameof(weight));
        }

        var sale = await _saleRepository.GetByIdAsync(saleId);
        if (sale == null)
        {
            throw new ArgumentException("Sale not found", nameof(saleId));
        }

        // Calculate pricing
        var roundedWeight = _weightBasedPricingService.RoundWeight(weight, product.WeightPrecision);
        var totalPrice = await _weightBasedPricingService.CalculatePriceAsync(product, weight);

        var saleItem = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = productId,
            Quantity = 1, // Always 1 for weight-based items
            UnitPrice = product.RatePerKilogram.Value, // Store the rate for reference
            Weight = roundedWeight,
            RatePerKilogram = product.RatePerKilogram.Value,
            TotalPrice = totalPrice,
            BatchNumber = batchNumber
        };

        await _saleItemRepository.AddAsync(saleItem);
        await _saleItemRepository.SaveChangesAsync();

        // Recalculate sale total
        sale.TotalAmount = await CalculateSaleTotalAsync(saleId);
        await _saleRepository.UpdateAsync(sale);
        await _saleRepository.SaveChangesAsync();

        return sale;
    }

    public async Task<Sale> CompleteSaleAsync(Guid saleId, PaymentMethod paymentMethod)
    {
        var sale = await _saleRepository.GetByIdAsync(saleId);
        if (sale == null)
        {
            throw new ArgumentException("Sale not found", nameof(saleId));
        }

        return await CompleteSaleAsync(sale, paymentMethod);
    }

    public async Task<Sale> CompleteSaleAsync(Sale sale, PaymentMethod paymentMethod)
    {
        // Check license before completing sale
        var licenseStatus = await _licenseService.CheckLicenseStatusAsync();
        if (licenseStatus != LicenseStatus.Active)
        {
            throw new InvalidOperationException($"Cannot complete sale: License status is {licenseStatus}");
        }

        // Ensure we have the tracked entity from the database
        var trackedSale = await _saleRepository.GetByIdAsync(sale.Id);
        if (trackedSale == null)
        {
            throw new ArgumentException("Sale not found", nameof(sale));
        }

        // Set payment method
        trackedSale.PaymentMethod = paymentMethod;
        
        // Calculate base total from items
        var baseTotal = await CalculateBaseSaleTotalAsync(trackedSale.Id);
        
        // Apply discounts
        var discountResult = await _discountService.CalculateDiscountsAsync(trackedSale, trackedSale.Customer);
        trackedSale.DiscountAmount = discountResult.TotalDiscountAmount;
        
        // Apply membership discounts if customer exists
        decimal membershipDiscountAmount = 0;
        if (trackedSale.Customer != null)
        {
            var membershipDiscount = await _membershipService.CalculateMembershipDiscountAsync(trackedSale.Customer, trackedSale);
            membershipDiscountAmount = membershipDiscount.DiscountAmount;
            trackedSale.MembershipDiscountAmount = membershipDiscountAmount;
        }
        
        // Calculate tax
        var taxSettings = await _configurationService.GetTaxSettingsAsync();
        var taxableAmount = baseTotal - trackedSale.DiscountAmount - membershipDiscountAmount;
        trackedSale.TaxAmount = Math.Round(taxableAmount * (taxSettings.DefaultTaxRate / 100), 2, MidpointRounding.AwayFromZero);
        
        // Calculate final total
        trackedSale.TotalAmount = baseTotal - trackedSale.DiscountAmount - membershipDiscountAmount + trackedSale.TaxAmount;
        
        // Save applied discounts
        await SaveAppliedDiscountsAsync(trackedSale, discountResult);
        
        await _saleRepository.UpdateAsync(trackedSale);
        await _saleRepository.SaveChangesAsync();

        // Update inventory for all items in the sale
        await _inventoryService.ProcessSaleInventoryUpdateAsync(trackedSale);
        
        // Update customer purchase history if customer exists
        if (trackedSale.Customer != null)
        {
            await _membershipService.UpdateCustomerPurchaseHistoryAsync(trackedSale.Customer, trackedSale);
        }

        return trackedSale;
    }

    public async Task<decimal> CalculateSaleTotalAsync(Guid saleId)
    {
        var saleItems = await _saleItemRepository.FindAsync(si => si.SaleId == saleId);
        return await CalculateSaleTotalAsync(saleItems);
    }

    public async Task<decimal> CalculateSaleTotalAsync(IEnumerable<SaleItem> saleItems)
    {
        decimal total = 0;
        
        foreach (var item in saleItems)
        {
            // Use the pre-calculated TotalPrice which handles both regular and weight-based items
            total += item.TotalPrice;
        }
        
        return await Task.FromResult(total);
    }

    public async Task<SaleCalculationResult> CalculateFullSaleTotalAsync(Guid saleId)
    {
        var sale = await _saleRepository.GetByIdAsync(saleId);
        if (sale == null)
        {
            throw new ArgumentException("Sale not found", nameof(saleId));
        }

        return await CalculateFullSaleTotalAsync(sale);
    }

    public async Task<SaleCalculationResult> CalculateFullSaleTotalAsync(Sale sale)
    {
        // Calculate base total from items
        var baseTotal = await CalculateBaseSaleTotalAsync(sale.Id);
        
        // Apply discounts
        var discountResult = await _discountService.CalculateDiscountsAsync(sale, sale.Customer);
        var discountAmount = discountResult.TotalDiscountAmount;
        
        // Apply membership discounts if customer exists
        decimal membershipDiscountAmount = 0;
        if (sale.Customer != null)
        {
            var membershipDiscount = await _membershipService.CalculateMembershipDiscountAsync(sale.Customer, sale);
            membershipDiscountAmount = membershipDiscount.DiscountAmount;
        }
        
        // Calculate tax
        var taxSettings = await _configurationService.GetTaxSettingsAsync();
        var taxableAmount = baseTotal - discountAmount - membershipDiscountAmount;
        var taxAmount = Math.Round(taxableAmount * (taxSettings.DefaultTaxRate / 100), 2, MidpointRounding.AwayFromZero);
        
        // Calculate final total
        var finalTotal = baseTotal - discountAmount - membershipDiscountAmount + taxAmount;
        
        return new SaleCalculationResult
        {
            BaseTotal = baseTotal,
            DiscountAmount = discountAmount,
            MembershipDiscountAmount = membershipDiscountAmount,
            TaxAmount = taxAmount,
            FinalTotal = finalTotal,
            AppliedDiscounts = discountResult.AppliedDiscounts,
            DiscountReasons = discountResult.DiscountReasons
        };
    }

    public async Task<bool> ValidateProductForSaleAsync(Guid productId)
    {
        // Check if product is active
        if (!await _productService.IsProductActiveAsync(productId))
        {
            return false;
        }

        // Check if medicine is not expired
        if (!await _productService.ValidateMedicineExpiryAsync(productId))
        {
            return false;
        }

        return true;
    }

    public async Task<Sale> CompleteSaleAsync(Sale sale)
    {
        return await CompleteSaleAsync(sale, sale.PaymentMethod);
    }

    private async Task<decimal> CalculateBaseSaleTotalAsync(Guid saleId)
    {
        var saleItems = await _saleItemRepository.FindAsync(si => si.SaleId == saleId);
        return saleItems.Sum(item => item.TotalPrice);
    }

    private async Task SaveAppliedDiscountsAsync(Sale sale, DiscountCalculationResult discountResult)
    {
        // For now, skip saving applied discounts to avoid Entity Framework issues
        // This would typically be handled by a separate SaleDiscountRepository
        // TODO: Implement proper SaleDiscount management
        await Task.CompletedTask;
    }

    public async Task<Sale?> GetSaleByInvoiceNumberAsync(string invoiceNumber)
    {
        var sales = await _saleRepository.FindAsync(s => s.InvoiceNumber == invoiceNumber);
        return sales.FirstOrDefault();
    }

    public async Task<decimal> GetDailySalesAsync(DateTime date)
    {
        // Convert to UTC for consistent comparison with sale CreatedAt timestamps
        var startOfDay = date.Date.ToUniversalTime();
        var endOfDay = startOfDay.AddDays(1);
        
        var sales = await _saleRepository.FindAsync(s => 
            s.CreatedAt >= startOfDay && s.CreatedAt < endOfDay);
        
        return sales.Sum(s => s.TotalAmount);
    }

    public async Task<int> GetDailyTransactionCountAsync(DateTime date)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);
        
        var sales = await _saleRepository.FindAsync(s => 
            s.CreatedAt >= startOfDay && s.CreatedAt < endOfDay);
        
        return sales.Count();
    }

    public async Task<IEnumerable<Sale>> GetSalesByDateRangeAsync(DateTime fromDate, DateTime toDate)
    {
        var startDate = fromDate.Date;
        var endDate = toDate.Date.AddDays(1);
        
        return await _saleRepository.FindAsync(s => 
            s.CreatedAt >= startDate && s.CreatedAt < endDate);
    }

    public async Task<RefundRecord?> GetRefundBySaleIdAsync(Guid saleId)
    {
        // This would typically query a RefundRepository
        // For now, return null as a placeholder
        await Task.CompletedTask;
        return null;
    }

    public async Task ProcessRefundAsync(RefundRecord refund)
    {
        // This would typically:
        // 1. Validate the refund request
        // 2. Update inventory (add back refunded items)
        // 3. Create refund record in database
        // 4. Update original sale status
        
        // For now, just a placeholder
        await Task.CompletedTask;
    }
}