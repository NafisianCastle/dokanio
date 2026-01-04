using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.Models;
using System.Collections.ObjectModel;

namespace Desktop.ViewModels;

public partial class PurchaseViewModel : BaseViewModel
{
    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private Supplier? selectedSupplier;

    [ObservableProperty]
    private string purchaseNumber = string.Empty;

    [ObservableProperty]
    private DateTime purchaseDate = DateTime.Today;

    public ObservableCollection<Supplier> Suppliers { get; } = new();
    public ObservableCollection<Product> SearchResults { get; } = new();
    public ObservableCollection<PurchaseItem> PurchaseItems { get; } = new();

    public decimal TotalAmount => PurchaseItems.Sum(item => item.Total);

    public PurchaseViewModel()
    {
        Title = "Purchase Entry";
        LoadSampleData();
        GeneratePurchaseNumber();
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
        var existingItem = PurchaseItems.FirstOrDefault(item => item.ProductId == product.Id);
        
        if (existingItem != null)
        {
            existingItem.Quantity++;
        }
        else
        {
            PurchaseItems.Add(new PurchaseItem
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = 1,
                UnitCost = product.UnitPrice * 0.8m, // Assume 20% markup
                BatchNumber = $"BATCH-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(100, 999)}",
                ExpiryDate = DateTime.Today.AddMonths(24) // Default 2 years expiry
            });
        }

        SearchText = string.Empty;
        SearchResults.Clear();
        
        OnPropertyChanged(nameof(TotalAmount));
    }

    [RelayCommand]
    private void RemoveItem(PurchaseItem item)
    {
        PurchaseItems.Remove(item);
        OnPropertyChanged(nameof(TotalAmount));
    }

    [RelayCommand]
    private async Task CompletePurchase()
    {
        if (SelectedSupplier == null)
        {
            ErrorMessage = "Please select a supplier";
            return;
        }

        if (!PurchaseItems.Any())
        {
            ErrorMessage = "Please add items to the purchase";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            // Simulate purchase processing
            await Task.Delay(1000);

            var purchase = new Purchase
            {
                Id = Guid.NewGuid(),
                PurchaseNumber = PurchaseNumber,
                SupplierId = SelectedSupplier.Id,
                SupplierName = SelectedSupplier.Name,
                TotalAmount = TotalAmount,
                PurchaseDate = PurchaseDate,
                Items = PurchaseItems.ToList()
            };

            // In real app, save to database here
            // Also update product stock quantities
            
            // Reset the form
            ResetPurchase();
            
            // Show success message
            ErrorMessage = $"Purchase completed successfully! Purchase Number: {purchase.PurchaseNumber}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error completing purchase: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ResetPurchase()
    {
        PurchaseItems.Clear();
        SelectedSupplier = null;
        SearchText = string.Empty;
        SearchResults.Clear();
        ErrorMessage = string.Empty;
        GeneratePurchaseNumber();
        PurchaseDate = DateTime.Today;
        
        OnPropertyChanged(nameof(TotalAmount));
    }

    private void GeneratePurchaseNumber()
    {
        PurchaseNumber = $"PUR-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
    }

    private void LoadSampleData()
    {
        var sampleSuppliers = new List<Supplier>
        {
            new() { Id = Guid.NewGuid(), Name = "MediCorp Pharmaceuticals", IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "HealthPlus Distributors", IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "Global Medical Supplies", IsActive = true }
        };

        foreach (var supplier in sampleSuppliers)
        {
            Suppliers.Add(supplier);
        }
    }

    private List<Product> GetSampleProducts()
    {
        return new List<Product>
        {
            new() { Id = Guid.NewGuid(), Name = "Paracetamol 500mg", Barcode = "1234567890123", UnitPrice = 25.50m, Category = "Medicine" },
            new() { Id = Guid.NewGuid(), Name = "Aspirin 75mg", Barcode = "2345678901234", UnitPrice = 15.75m, Category = "Medicine" },
            new() { Id = Guid.NewGuid(), Name = "Vitamin C Tablets", Barcode = "3456789012345", UnitPrice = 45.00m, Category = "Supplement" },
            new() { Id = Guid.NewGuid(), Name = "Cough Syrup", Barcode = "4567890123456", UnitPrice = 85.25m, Category = "Medicine" },
            new() { Id = Guid.NewGuid(), Name = "Bandages", Barcode = "5678901234567", UnitPrice = 12.50m, Category = "Medical Supply" },
            new() { Id = Guid.NewGuid(), Name = "Antiseptic Solution", Barcode = "6789012345678", UnitPrice = 35.00m, Category = "Medical Supply" },
            new() { Id = Guid.NewGuid(), Name = "Thermometer", Barcode = "7890123456789", UnitPrice = 150.00m, Category = "Medical Device" }
        };
    }
}