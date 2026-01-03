using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

public interface ISaleService
{
    Task<Sale> CreateSaleAsync(string invoiceNumber, Guid deviceId);
    Task<Sale> AddItemToSaleAsync(Guid saleId, Guid productId, int quantity, decimal unitPrice, string? batchNumber = null);
    Task<Sale> CompleteSaleAsync(Guid saleId, PaymentMethod paymentMethod);
    Task<decimal> CalculateSaleTotalAsync(Guid saleId);
    Task<decimal> CalculateSaleTotalAsync(IEnumerable<SaleItem> saleItems);
    Task<bool> ValidateProductForSaleAsync(Guid productId);
}