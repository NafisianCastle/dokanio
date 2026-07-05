using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Shared.Core.Enums;
using Shared.Core.Repositories;

namespace Desktop.ViewModels;

public class ProductViewModel : BaseViewModel
{
    private readonly IProductRepository? _productRepository;
    private readonly IStockRepository? _stockRepository;
    private readonly ILogger<ProductViewModel>? _logger;

    [Reactive] public string SearchText { get; set; } = string.Empty;
    [Reactive] public Desktop.Models.Product? SelectedProduct { get; set; }
    [Reactive] public bool IsAddingProduct { get; set; }
    [Reactive] public string ProductName { get; set; } = string.Empty;
    [Reactive] public string Barcode { get; set; } = string.Empty;
    [Reactive] public string Category { get; set; } = string.Empty;
    [Reactive] public decimal UnitPrice { get; set; }
    [Reactive] public string BatchNumber { get; set; } = string.Empty;
    [Reactive] public DateTime? ExpiryDate { get; set; }
    [Reactive] public int StockQuantity { get; set; }

    public ObservableCollection<Desktop.Models.Product> Products { get; } = new();
    public ObservableCollection<Desktop.Models.Product> FilteredProducts { get; } = new();
    public ObservableCollection<Desktop.Models.Product> ExpiringProducts { get; } = new();

    public ReactiveCommand<Unit, Unit> LoadProductsCommand { get; }
    public ReactiveCommand<Unit, Unit> AddNewProductCommand { get; }
    public ReactiveCommand<Desktop.Models.Product, Unit> EditProductCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveProductCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelEditCommand { get; }
    public ReactiveCommand<Desktop.Models.Product, Unit> DeleteProductCommand { get; }

    // Design-time constructor
    public ProductViewModel()
    {
        Title = "Product Management";
        LoadProductsCommand  = ReactiveCommand.CreateFromTask(LoadProductsAsync);
        AddNewProductCommand = ReactiveCommand.Create(AddNewProduct);
        EditProductCommand   = ReactiveCommand.Create<Desktop.Models.Product>(EditProduct);
        SaveProductCommand   = ReactiveCommand.CreateFromTask(SaveProductAsync);
        CancelEditCommand    = ReactiveCommand.Create(CancelEdit);
        DeleteProductCommand = ReactiveCommand.CreateFromTask<Desktop.Models.Product>(DeleteProductAsync);
    }

    public ProductViewModel(
        IProductRepository productRepository,
        IStockRepository stockRepository,
        ILogger<ProductViewModel> logger) : this()
    {
        _productRepository = productRepository;
        _stockRepository   = stockRepository;
        _logger            = logger;

        this.WhenAnyValue(x => x.SearchText).Subscribe(_ => RefreshFilteredProducts());

        _ = Task.Run(LoadProductsAsync);
    }

    private async Task LoadProductsAsync()
    {
        if (_productRepository == null) return;
        IsBusy = true;
        ClearError();
        try
        {
            var entities = await _productRepository.GetActiveProductsAsync();
            Products.Clear();
            foreach (var e in entities) Products.Add(MapToDesktopModel(e));
            RefreshFilteredProducts();
            RefreshExpiringProducts();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading products");
            SetError($"Error loading products: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private void AddNewProduct()
    {
        IsAddingProduct = true;
        ClearForm();
    }

    private void EditProduct(Desktop.Models.Product product)
    {
        SelectedProduct = product;
        IsAddingProduct = true;
        ProductName  = product.Name;
        Barcode      = product.Barcode ?? string.Empty;
        Category     = product.Category ?? string.Empty;
        UnitPrice    = product.UnitPrice;
        BatchNumber  = product.BatchNumber ?? string.Empty;
        ExpiryDate   = product.ExpiryDate;
        StockQuantity = product.StockQuantity;
    }

    private async Task SaveProductAsync()
    {
        if (string.IsNullOrWhiteSpace(ProductName)) { SetError("Product name is required"); return; }
        if (UnitPrice <= 0) { SetError("Unit price must be greater than zero"); return; }

        IsBusy = true;
        ClearError();
        try
        {
            if (SelectedProduct != null && _productRepository != null)
            {
                var entity = await _productRepository.GetByIdAsync(SelectedProduct.Id);
                if (entity != null)
                {
                    entity.Name        = ProductName;
                    entity.Barcode     = string.IsNullOrWhiteSpace(Barcode) ? null : Barcode;
                    entity.Category    = Category;
                    entity.UnitPrice   = UnitPrice;
                    entity.BatchNumber = string.IsNullOrWhiteSpace(BatchNumber) ? null : BatchNumber;
                    entity.ExpiryDate  = ExpiryDate;
                    entity.UpdatedAt   = DateTime.UtcNow;
                    entity.SyncStatus  = SyncStatus.NotSynced;
                    await _productRepository.UpdateAsync(entity);
                    await _productRepository.SaveChangesAsync();

                    SelectedProduct.Name          = ProductName;
                    SelectedProduct.Barcode        = Barcode;
                    SelectedProduct.Category       = Category;
                    SelectedProduct.UnitPrice      = UnitPrice;
                    SelectedProduct.BatchNumber    = BatchNumber;
                    SelectedProduct.ExpiryDate     = ExpiryDate;
                    SelectedProduct.StockQuantity  = StockQuantity;
                    SelectedProduct.UpdatedAt      = DateTime.UtcNow;
                    _logger?.LogInformation("Updated product {ProductName}", ProductName);
                }
            }
            else if (_productRepository != null)
            {
                var entity = new Shared.Core.Entities.Product
                {
                    Id          = Guid.NewGuid(),
                    Name        = ProductName,
                    Barcode     = string.IsNullOrWhiteSpace(Barcode) ? null : Barcode,
                    Category    = Category,
                    UnitPrice   = UnitPrice,
                    BatchNumber = string.IsNullOrWhiteSpace(BatchNumber) ? null : BatchNumber,
                    ExpiryDate  = ExpiryDate,
                    IsActive    = true,
                    CreatedAt   = DateTime.UtcNow,
                    UpdatedAt   = DateTime.UtcNow,
                    SyncStatus  = SyncStatus.NotSynced
                };
                await _productRepository.AddAsync(entity);
                await _productRepository.SaveChangesAsync();

                var dm = MapToDesktopModel(entity);
                dm.StockQuantity = StockQuantity;
                Products.Add(dm);
                _logger?.LogInformation("Created product {ProductName}", ProductName);
            }

            RefreshFilteredProducts();
            RefreshExpiringProducts();
            CancelEdit();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving product");
            SetError($"Error saving product: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private void CancelEdit()
    {
        IsAddingProduct = false;
        SelectedProduct = null;
        ClearForm();
    }

    private async Task DeleteProductAsync(Desktop.Models.Product product)
    {
        if (_productRepository == null) return;
        IsBusy = true;
        ClearError();
        try
        {
            var entity = await _productRepository.GetByIdAsync(product.Id);
            if (entity != null)
            {
                entity.IsActive   = false;
                entity.IsDeleted  = true;
                entity.DeletedAt  = DateTime.UtcNow;
                entity.UpdatedAt  = DateTime.UtcNow;
                entity.SyncStatus = SyncStatus.NotSynced;
                await _productRepository.UpdateAsync(entity);
                await _productRepository.SaveChangesAsync();
            }
            product.IsActive = false;
            RefreshFilteredProducts();
            RefreshExpiringProducts();
            _logger?.LogInformation("Soft-deleted product {ProductName}", product.Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting product");
            SetError($"Error deleting product: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private void ClearForm()
    {
        ProductName   = string.Empty;
        Barcode       = string.Empty;
        Category      = string.Empty;
        UnitPrice     = 0;
        BatchNumber   = string.Empty;
        ExpiryDate    = null;
        StockQuantity = 0;
        ClearError();
    }

    private void RefreshFilteredProducts()
    {
        FilteredProducts.Clear();
        var filtered = Products.Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.ToLowerInvariant();
            filtered = filtered.Where(p =>
                p.Name.ToLowerInvariant().Contains(q) ||
                (p.Barcode?.Contains(SearchText) == true) ||
                (p.Category?.ToLowerInvariant().Contains(q) == true));
        }
        foreach (var p in filtered.OrderBy(p => p.Name)) FilteredProducts.Add(p);
    }

    private void RefreshExpiringProducts()
    {
        ExpiringProducts.Clear();
        var threshold = DateTime.Today.AddDays(30);
        foreach (var p in Products
            .Where(p => p.IsActive && p.ExpiryDate.HasValue &&
                        p.ExpiryDate.Value <= threshold && p.ExpiryDate.Value >= DateTime.Today)
            .OrderBy(p => p.ExpiryDate))
            ExpiringProducts.Add(p);
    }

    private static Desktop.Models.Product MapToDesktopModel(Shared.Core.Entities.Product e) => new()
    {
        Id            = e.Id,
        Name          = e.Name,
        Barcode       = e.Barcode,
        Category      = e.Category,
        UnitPrice     = e.UnitPrice,
        BatchNumber   = e.BatchNumber,
        ExpiryDate    = e.ExpiryDate,
        IsActive      = e.IsActive,
        CreatedAt     = e.CreatedAt,
        UpdatedAt     = e.UpdatedAt,
        StockQuantity = 0
    };
}
