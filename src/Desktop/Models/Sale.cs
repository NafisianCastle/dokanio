using System.ComponentModel;
using System.Runtime.CompilerServices;

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

public class SaleItem : INotifyPropertyChanged
{
    private int _quantity;
    private decimal _unitPrice;
    
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    
    public int Quantity 
    { 
        get => _quantity;
        set
        {
            if (_quantity != value)
            {
                _quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Total));
            }
        }
    }
    
    public decimal UnitPrice 
    { 
        get => _unitPrice;
        set
        {
            if (_unitPrice != value)
            {
                _unitPrice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Total));
            }
        }
    }
    
    public string? BatchNumber { get; set; }
    public decimal Total => Quantity * UnitPrice;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public enum PaymentMethod
{
    Cash,
    Card,
    UPI,
    BankTransfer
}