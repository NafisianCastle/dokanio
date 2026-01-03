using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;

namespace Shared.Core.Services;

public class InventoryService : IInventoryService
{
    private readonly IStockRepository _stockRepository;
    private readonly IProductRepository _productRepository;
    private readonly ISaleItemRepository _saleItemRepository;

    public InventoryService(
        IStockRepository stockRepository,
        IProductRepository productRepository,
        ISaleItemRepository saleItemRepository)
    {
        _stockRepository = stockRepository;
        _productRepository = productRepository;
        _saleItemRepository = saleItemRepository;
    }

    public async Task UpdateStockAsync(Guid productId, int quantityChange, Guid deviceId)
    {
        var existingStock = await _stockRepository.FindAsync(s => s.ProductId == productId);
        var stockRecord = existingStock.FirstOrDefault();

        if (stockRecord == null)
        {
            // Create new stock record if it doesn't exist
            stockRecord = new Stock
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                Quantity = Math.Max(0, quantityChange), // Ensure non-negative stock
                LastUpdatedAt = DateTime.UtcNow,
                DeviceId = deviceId,
                SyncStatus = SyncStatus.NotSynced
            };

            await _stockRepository.AddAsync(stockRecord);
        }
        else
        {
            // Update existing stock
            stockRecord.Quantity = Math.Max(0, stockRecord.Quantity + quantityChange);
            stockRecord.LastUpdatedAt = DateTime.UtcNow;
            stockRecord.SyncStatus = SyncStatus.NotSynced;

            await _stockRepository.UpdateAsync(stockRecord);
        }

        await _stockRepository.SaveChangesAsync();
    }

    public async Task<int> GetCurrentStockAsync(Guid productId)
    {
        var stockRecords = await _stockRepository.FindAsync(s => s.ProductId == productId);
        var stockRecord = stockRecords.FirstOrDefault();
        return stockRecord?.Quantity ?? 0;
    }

    public async Task<IEnumerable<Product>> GetLowStockProductsAsync(int threshold = 10)
    {
        var allStock = await _stockRepository.GetAllAsync();
        var lowStockProductIds = allStock
            .Where(s => s.Quantity < threshold)
            .Select(s => s.ProductId)
            .ToList();

        var lowStockProducts = new List<Product>();
        foreach (var productId in lowStockProductIds)
        {
            var product = await _productRepository.GetByIdAsync(productId);
            if (product != null && product.IsActive)
            {
                lowStockProducts.Add(product);
            }
        }

        return lowStockProducts;
    }

    public async Task<bool> HasSufficientStockAsync(Guid productId, int requiredQuantity)
    {
        var currentStock = await GetCurrentStockAsync(productId);
        return currentStock >= requiredQuantity;
    }

    public async Task ProcessSaleInventoryUpdateAsync(Sale sale)
    {
        var saleItems = await _saleItemRepository.FindAsync(si => si.SaleId == sale.Id);

        foreach (var saleItem in saleItems)
        {
            // Decrease stock by the quantity sold
            await UpdateStockAsync(saleItem.ProductId, -saleItem.Quantity, sale.DeviceId);
        }
    }
}