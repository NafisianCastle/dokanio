using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;

namespace Shared.Core.Services;

public class SaleService : ISaleService
{
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleItemRepository _saleItemRepository;
    private readonly IProductService _productService;
    private readonly IInventoryService _inventoryService;

    public SaleService(
        ISaleRepository saleRepository,
        ISaleItemRepository saleItemRepository,
        IProductService productService,
        IInventoryService inventoryService)
    {
        _saleRepository = saleRepository;
        _saleItemRepository = saleItemRepository;
        _productService = productService;
        _inventoryService = inventoryService;
    }

    public async Task<Sale> CreateSaleAsync(string invoiceNumber, Guid deviceId)
    {
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = invoiceNumber,
            TotalAmount = 0,
            PaymentMethod = PaymentMethod.Cash, // Default, will be set on completion
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

        var saleItem = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice,
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

        // Set payment method
        sale.PaymentMethod = paymentMethod;
        
        // Ensure total is calculated
        sale.TotalAmount = await CalculateSaleTotalAsync(saleId);
        
        await _saleRepository.UpdateAsync(sale);
        await _saleRepository.SaveChangesAsync();

        // Update inventory for all items in the sale
        await _inventoryService.ProcessSaleInventoryUpdateAsync(sale);

        return sale;
    }

    public async Task<decimal> CalculateSaleTotalAsync(Guid saleId)
    {
        var saleItems = await _saleItemRepository.FindAsync(si => si.SaleId == saleId);
        return await CalculateSaleTotalAsync(saleItems);
    }

    public async Task<decimal> CalculateSaleTotalAsync(IEnumerable<SaleItem> saleItems)
    {
        return await Task.FromResult(saleItems.Sum(item => item.Quantity * item.UnitPrice));
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
}