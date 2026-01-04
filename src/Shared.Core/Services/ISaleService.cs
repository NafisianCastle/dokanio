using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

public interface ISaleService
{
    Task<Sale> CreateSaleAsync(string invoiceNumber, Guid deviceId);
    Task<Sale> AddItemToSaleAsync(Guid saleId, Guid productId, int quantity, decimal unitPrice, string? batchNumber = null);
    Task<Sale> AddWeightBasedItemToSaleAsync(Guid saleId, Guid productId, decimal weight, string? batchNumber = null);
    Task<Sale> CompleteSaleAsync(Guid saleId, PaymentMethod paymentMethod);
    Task<Sale> CompleteSaleAsync(Sale sale);
    Task<decimal> CalculateSaleTotalAsync(Guid saleId);
    Task<decimal> CalculateSaleTotalAsync(IEnumerable<SaleItem> saleItems);
    Task<bool> ValidateProductForSaleAsync(Guid productId);
    
    // Additional methods for desktop application
    Task<Sale?> GetSaleByInvoiceNumberAsync(string invoiceNumber);
    Task<decimal> GetDailySalesAsync(DateTime date);
    Task<int> GetDailyTransactionCountAsync(DateTime date);
    Task<IEnumerable<Sale>> GetSalesByDateRangeAsync(DateTime fromDate, DateTime toDate);
    
    // Refund support
    Task<RefundRecord?> GetRefundBySaleIdAsync(Guid saleId);
    Task ProcessRefundAsync(RefundRecord refund);
}

// Supporting classes for refunds
public class RefundRecord
{
    public Guid Id { get; set; }
    public Guid OriginalSaleId { get; set; }
    public decimal RefundAmount { get; set; }
    public string RefundReason { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public DateTime RefundDate { get; set; }
    public bool IsPartialRefund { get; set; }
    public string ProcessedBy { get; set; } = string.Empty;
    public List<RefundItem> RefundItems { get; set; } = new();
}

public class RefundItem
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? BatchNumber { get; set; }
}