using Shared.Core.Entities;

namespace Shared.Core.Services;

public interface IInventoryService
{
    Task UpdateStockAsync(Guid productId, int quantityChange, Guid deviceId);
    Task<int> GetCurrentStockAsync(Guid productId);
    Task<IEnumerable<Product>> GetLowStockProductsAsync(int threshold = 10);
    Task<bool> HasSufficientStockAsync(Guid productId, int requiredQuantity);
    Task ProcessSaleInventoryUpdateAsync(Sale sale);
}