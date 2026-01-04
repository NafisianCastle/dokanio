using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.Models;
using System.Collections.ObjectModel;

namespace Desktop.ViewModels;

public partial class SaleViewModel : BaseViewModel
{
    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string customerName = string.Empty;

    [ObservableProperty]
    private string customerPhone = string.Empty;

    [ObservableProperty]
    private PaymentMethod selectedPaymentMethod = PaymentMethod.Cash;

    [ObservableProperty]
    private decimal amountReceived;

    [ObservableProperty]
    private bool isScanning;

    public ObservableCollection<Product> SearchResults { get; } = new();
    public ObservableCollection<SaleItem> SaleItems { get; } = new();
    public List<PaymentMethod> PaymentMethods { get; } = Enum.GetValues<PaymentMethod>().ToList();

    public decimal Subtotal => SaleItems.Sum(item => item.Total);
    public decimal Tax => Subtotal * 0.1m; // 10% tax
    public decimal Total => Subtotal + Tax;
    public decimal ChangeAmount => AmountReceived - Total;

    public SaleViewModel()
    {
        Title = "New Sale";
        
        // Sample products for demo
        LoadSampleProducts();
    }

    partial void OnSearchTextChanged(string value)
    {
        SearchProducts(value);
    }

    [RelayCommand]
    private void SearchProducts(string? searchTerm = null)
    {
        searchTerm ??= SearchText;
        
        SearchResults.Clear();
        
        if (string.IsNullOrWhiteSpace(searchTerm))
            return;

        // Sample search logic - in real app this would query a database
        var sampleProducts = GetSampleProducts()
            .Where(p => p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                       (p.Barcode?.Contains(searchTerm) == true))
            .Take(5);

        foreach (var product in sampleProducts)
        {
            SearchResults.Add(product);
        }
    }

    [RelayCommand]
    private void AddProduct(Product product)
    {
        var existingItem = SaleItems.FirstOrDefault(item => item.ProductId == product.Id);
        
        if (existingItem != null)
        {
            existingItem.Quantity++;
        }
        else
        {
            SaleItems.Add(new SaleItem
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = 1,
                UnitPrice = product.UnitPrice,
                BatchNumber = product.BatchNumber
            });
        }

        SearchText = string.Empty;
        SearchResults.Clear();
        
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(Tax));
        OnPropertyChanged(nameof(Total));
    }

    [RelayCommand]
    private void RemoveItem(SaleItem item)
    {
        SaleItems.Remove(item);
        
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(Tax));
        OnPropertyChanged(nameof(Total));
    }

    [RelayCommand]
    private async Task CompleteSale()
    {
        if (!SaleItems.Any())
        {
            ErrorMessage = "Please add items to the sale";
            return;
        }

        if (SelectedPaymentMethod == PaymentMethod.Cash && AmountReceived < Total)
        {
            ErrorMessage = "Amount received is less than total";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            // Simulate sale processing
            await Task.Delay(1000);

            var sale = new Sale
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"INV-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
                TotalAmount = Total,
                PaymentMethod = SelectedPaymentMethod,
                CustomerName = CustomerName,
                CustomerPhone = CustomerPhone,
                CreatedAt = DateTime.Now,
                Items = SaleItems.ToList()
            };

            // In real app, save to database here
            
            // Reset the form
            ResetSale();
            
            // Show success message (in real app, might show receipt dialog)
            ErrorMessage = $"Sale completed successfully! Invoice: {sale.InvoiceNumber}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error completing sale: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ResetSale()
    {
        SaleItems.Clear();
        CustomerName = string.Empty;
        CustomerPhone = string.Empty;
        AmountReceived = 0;
        SelectedPaymentMethod = PaymentMethod.Cash;
        SearchText = string.Empty;
        SearchResults.Clear();
        ErrorMessage = string.Empty;
        
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(Tax));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(ChangeAmount));
    }

    [RelayCommand]
    private void StartBarcodeScan()
    {
        IsScanning = true;
        
        // Simulate barcode scanning
        Task.Delay(2000).ContinueWith(_ =>
        {
            IsScanning = false;
            SearchText = "1234567890123"; // Sample barcode
            SearchProducts();
        });
    }

    private void LoadSampleProducts()
    {
        // This would be loaded from database in real app
    }

    private List<Product> GetSampleProducts()
    {
        return new List<Product>
        {
            new() { Id = Guid.NewGuid(), Name = "Paracetamol 500mg", Barcode = "1234567890123", UnitPrice = 25.50m, Category = "Medicine", StockQuantity = 100 },
            new() { Id = Guid.NewGuid(), Name = "Aspirin 75mg", Barcode = "2345678901234", UnitPrice = 15.75m, Category = "Medicine", StockQuantity = 50 },
            new() { Id = Guid.NewGuid(), Name = "Vitamin C Tablets", Barcode = "3456789012345", UnitPrice = 45.00m, Category = "Supplement", StockQuantity = 75 },
            new() { Id = Guid.NewGuid(), Name = "Cough Syrup", Barcode = "4567890123456", UnitPrice = 85.25m, Category = "Medicine", StockQuantity = 30 },
            new() { Id = Guid.NewGuid(), Name = "Bandages", Barcode = "5678901234567", UnitPrice = 12.50m, Category = "Medical Supply", StockQuantity = 200 }
        };
    }
}