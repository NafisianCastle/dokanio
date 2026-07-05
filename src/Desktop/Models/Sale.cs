using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Desktop.Models;

public class Sale
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<SaleItem> Items { get; set; } = new();
}

public class SaleItem : ReactiveObject
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? BatchNumber { get; set; }

    [Reactive] public int Quantity { get; set; }
    [Reactive] public decimal UnitPrice { get; set; }

    public decimal Total => Quantity * UnitPrice;
}

public enum PaymentMethod
{
    Cash,
    Card,
    UPI,
    BankTransfer
}
