using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.Models;
using System.Collections.ObjectModel;

namespace Desktop.ViewModels;

public partial class ProductViewModel : BaseViewModel
{
    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private Product? selectedProduct;

    [ObservableProperty]
    private bool isAddingProduct;

    [ObservableProperty]
    private string productName = string.Empty;

    [ObservableProperty]
    private string barcode = string.Empty;

    [ObservableProperty]
    private string category = string.Empty;

    [ObservableProperty]
    private decimal unitPrice;

    [ObservableProperty]
    private string batchNumber = string.Empty;

    [ObservableProperty]
    private DateTime? expiryDate;

    [ObservableProperty]
    private int stockQuantity;

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<Product> FilteredProducts { get; } = new();
    public ObservableCollection<Product> ExpiringProducts { get; } = new();

    public ProductViewModel()
    {
        Title = "Product Management";
        LoadSampleProducts();
        RefreshFilteredProducts();
        RefreshExpiringProducts();
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshFilteredProducts();
    }

    [RelayCommand]
    private void AddNewProduct()
    {
        IsAddingProduct = true;
        ClearForm();
    }

    [RelayCommand]
    private void EditProduct(Product product)
    {
        SelectedProduct = product;
        IsAddingProduct = true;
        
        ProductName = product.Name;
        Barcode = product.Barcode ?? string.Empty;
        Category = product.Category ?? string.Empty;
        UnitPrice = product.UnitPrice;
        BatchNumber = product.BatchNumber ?? string.Empty;
        ExpiryDate = product.ExpiryDate;
        StockQuantity = product.StockQuantity;
    }

    [RelayCommand]
    private async Task SaveProduct()
    {
        if (string.IsNullOrWhiteSpace(ProductName))
        {
            ErrorMessage = "Product name is required";
            return;
        }

        if (UnitPrice <= 0)
        {
            ErrorMessage = "Unit price must be greater than zero";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            await Task.Delay(500); // Simulate saving

            if (SelectedProduct != null)
            {
                // Update existing product
                SelectedProduct.Name = ProductName;
                SelectedProduct.Barcode = Barcode;
                SelectedProduct.Category = Category;
                SelectedProduct.UnitPrice = UnitPrice;
                SelectedProduct.BatchNumber = BatchNumber;
                SelectedProduct.ExpiryDate = ExpiryDate;
                SelectedProduct.StockQuantity = StockQuantity;
                SelectedProduct.UpdatedAt = DateTime.Now;
            }
            else
            {
                // Add new product
                var newProduct = new Product
                {
                    Id = Guid.NewGuid(),
                    Name = ProductName,
                    Barcode = Barcode,
                    Category = Category,
                    UnitPrice = UnitPrice,
                    BatchNumber = BatchNumber,
                    ExpiryDate = ExpiryDate,
                    StockQuantity = StockQuantity,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                Products.Add(newProduct);
            }

            RefreshFilteredProducts();
            RefreshExpiringProducts();
            CancelEdit();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving product: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsAddingProduct = false;
        SelectedProduct = null;
        ClearForm();
    }

    [RelayCommand]
    private async Task DeleteProduct(Product product)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            await Task.Delay(300); // Simulate deletion

            // Soft delete
            product.IsActive = false;
            product.UpdatedAt = DateTime.Now;

            RefreshFilteredProducts();
            RefreshExpiringProducts();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting product: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearForm()
    {
        ProductName = string.Empty;
        Barcode = string.Empty;
        Category = string.Empty;
        UnitPrice = 0;
        BatchNumber = string.Empty;
        ExpiryDate = null;
        StockQuantity = 0;
        ErrorMessage = string.Empty;
    }

    private void RefreshFilteredProducts()
    {
        FilteredProducts.Clear();

        var filtered = Products.Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            filtered = filtered.Where(p => 
                p.Name.ToLowerInvariant().Contains(searchLower) ||
                (p.Barcode?.Contains(SearchText) == true) ||
                (p.Category?.ToLowerInvariant().Contains(searchLower) == true));
        }

        foreach (var product in filtered.OrderBy(p => p.Name))
        {
            FilteredProducts.Add(product);
        }
    }

    private void RefreshExpiringProducts()
    {
        ExpiringProducts.Clear();

        var expiringThreshold = DateTime.Today.AddDays(30); // Products expiring in next 30 days
        var expiring = Products.Where(p => 
            p.IsActive && 
            p.ExpiryDate.HasValue && 
            p.ExpiryDate.Value <= expiringThreshold &&
            p.ExpiryDate.Value >= DateTime.Today)
            .OrderBy(p => p.ExpiryDate);

        foreach (var product in expiring)
        {
            ExpiringProducts.Add(product);
        }
    }

    private void LoadSampleProducts()
    {
        var sampleProducts = new List<Product>
        {
            new() 
            { 
                Id = Guid.NewGuid(), 
                Name = "Paracetamol 500mg", 
                Barcode = "1234567890123", 
                Category = "Medicine",
                UnitPrice = 25.50m,
                BatchNumber = "BATCH001",
                ExpiryDate = DateTime.Today.AddMonths(18),
                StockQuantity = 100,
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-30),
                UpdatedAt = DateTime.Now.AddDays(-5)
            },
            new() 
            { 
                Id = Guid.NewGuid(), 
                Name = "Aspirin 75mg", 
                Barcode = "2345678901234", 
                Category = "Medicine",
                UnitPrice = 15.75m,
                BatchNumber = "BATCH002",
                ExpiryDate = DateTime.Today.AddDays(15), // Expiring soon
                StockQuantity = 50,
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-25),
                UpdatedAt = DateTime.Now.AddDays(-3)
            },
            new() 
            { 
                Id = Guid.NewGuid(), 
                Name = "Vitamin C Tablets", 
                Barcode = "3456789012345", 
                Category = "Supplement",
                UnitPrice = 45.00m,
                BatchNumber = "BATCH003",
                ExpiryDate = DateTime.Today.AddMonths(12),
                StockQuantity = 75,
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-20),
                UpdatedAt = DateTime.Now.AddDays(-1)
            },
            new() 
            { 
                Id = Guid.NewGuid(), 
                Name = "Cough Syrup", 
                Barcode = "4567890123456", 
                Category = "Medicine",
                UnitPrice = 85.25m,
                BatchNumber = "BATCH004",
                ExpiryDate = DateTime.Today.AddDays(25), // Expiring soon
                StockQuantity = 30,
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-15),
                UpdatedAt = DateTime.Now
            }
        };

        foreach (var product in sampleProducts)
        {
            Products.Add(product);
        }
    }
}