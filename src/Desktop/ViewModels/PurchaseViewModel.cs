using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using System.Collections.ObjectModel;
using Desktop.Models;
using Microsoft.Extensions.Logging;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Shared.Core.Services;

namespace Desktop.ViewModels;

public class PurchaseViewModel : BaseViewModel
{
    private readonly IProductRepository _productRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IStockRepository _stockRepository;
    private readonly IInventoryUpdater _inventoryUpdater;
    private readonly ILogger<PurchaseViewModel> _logger;

    [Reactive] public string SearchText { get; set; } = string.Empty;
    [Reactive] public Supplier? SelectedSupplier { get; set; }
    [Reactive] public string PurchaseNumber { get; set; } = string.Empty;
    [Reactive] public DateTime PurchaseDate { get; set; } = DateTime.Today;

    public ObservableCollection<Supplier> Suppliers { get; } = new();
    public ObservableCollection<Product> SearchResults { get; } = new();
    public ObservableCollection<PurchaseItem> PurchaseItems { get; } = new();

    public decimal TotalAmount => PurchaseItems.Sum(i => i.Total);

    public ReactiveCommand<Unit, Unit> LoadSuppliersCommand { get; }
    public ReactiveCommand<string, Unit> SearchProductsCommand { get; }
    public ReactiveCommand<Product, Unit> AddProductCommand { get; }
    public ReactiveCommand<PurchaseItem, Unit> RemoveItemCommand { get; }
    public ReactiveCommand<Unit, Unit> CompletePurchaseCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetPurchaseCommand { get; }

    // Design-time constructor
    public PurchaseViewModel()
    {
        Title = "Purchase Entry";
        GeneratePurchaseNumber();
        LoadSuppliersCommand  = ReactiveCommand.CreateFromTask(LoadSuppliersAsync);
        SearchProductsCommand = ReactiveCommand.CreateFromTask<string>(SearchProductsAsync);
        AddProductCommand     = ReactiveCommand.Create<Product>(AddProduct);
        RemoveItemCommand     = ReactiveCommand.Create<PurchaseItem>(RemoveItem);
        CompletePurchaseCommand = ReactiveCommand.CreateFromTask(CompletePurchaseAsync);
        ResetPurchaseCommand  = ReactiveCommand.Create(ResetPurchase);
    }

    public PurchaseViewModel(
        IProductRepository productRepository,
        ISupplierRepository supplierRepository,
        IStockRepository stockRepository,
        IInventoryUpdater inventoryUpdater,
        ILogger<PurchaseViewModel> logger) : this()
    {
        _productRepository  = productRepository;
        _supplierRepository = supplierRepository;
        _stockRepository    = stockRepository;
        _inventoryUpdater   = inventoryUpdater;
        _logger             = logger;

        this.WhenAnyValue(x => x.SearchText)
            .Subscribe(v => _ = SearchProductsAsync(v));

        _ = Task.Run(LoadSuppliersAsync);
    }

    private async Task LoadSuppliersAsync()
    {
        try
        {
            if (_supplierRepository == null) return;
            var suppliers = await _supplierRepository.GetActiveSuppliersAsync();
            Suppliers.Clear();
            foreach (var s in suppliers)
                Suppliers.Add(new Supplier { Id = s.Id, Name = s.Name, ContactPerson = s.ContactPerson,
                    Phone = s.Phone, Email = s.Email, Address = s.Address,
                    IsActive = s.IsActive, CreatedAt = s.CreatedAt, UpdatedAt = s.UpdatedAt });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading suppliers");
            SetError($"Error loading suppliers: {ex.Message}");
        }
    }

    private async Task SearchProductsAsync(string? searchTerm = null)
    {
        searchTerm ??= SearchText;
        SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(searchTerm)) return;
        try
        {
            if (_productRepository == null) return;
            var results = await _productRepository.SearchAsync(searchTerm);
            foreach (var p in results.Take(10))
                SearchResults.Add(new Product { Id = p.Id, Name = p.Name, Barcode = p.Barcode,
                    UnitPrice = p.UnitPrice, Category = p.Category });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error searching products"); }
    }

    private void AddProduct(Product product)
    {
        var existing = PurchaseItems.FirstOrDefault(i => i.ProductId == product.Id);
        if (existing != null)
        {
            existing.Quantity++;
        }
        else
        {
            PurchaseItems.Add(new PurchaseItem
            {
                Id          = Guid.NewGuid(),
                ProductId   = product.Id,
                ProductName = product.Name,
                Quantity    = 1,
                UnitCost    = product.UnitPrice * 0.8m,
                BatchNumber = $"BATCH-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(100, 999)}",
                ExpiryDate  = DateTime.Today.AddMonths(24)
            });
        }
        SearchText = string.Empty;
        SearchResults.Clear();
        this.RaisePropertyChanged(nameof(TotalAmount));
    }

    private void RemoveItem(PurchaseItem item)
    {
        PurchaseItems.Remove(item);
        this.RaisePropertyChanged(nameof(TotalAmount));
    }

    private async Task CompletePurchaseAsync()
    {
        if (SelectedSupplier == null) { SetError("Please select a supplier"); return; }
        if (!PurchaseItems.Any()) { SetError("Please add items to the purchase"); return; }

        IsBusy = true;
        ClearError();
        try
        {
            foreach (var item in PurchaseItems)
            {
                if (_productRepository == null) break;
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                if (product == null) { _logger?.LogWarning("Product {Id} not found", item.ProductId); continue; }

                if (!string.IsNullOrWhiteSpace(item.BatchNumber)) product.BatchNumber = item.BatchNumber;
                if (item.ExpiryDate.HasValue) product.ExpiryDate = item.ExpiryDate;
                product.PurchasePrice = item.UnitCost;
                product.UpdatedAt     = DateTime.UtcNow;
                product.SyncStatus    = SyncStatus.NotSynced;
                await _productRepository.UpdateAsync(product);

                if (_stockRepository != null)
                {
                    var entries = await _stockRepository.FindAsync(s => s.ProductId == item.ProductId);
                    var stock   = entries.FirstOrDefault();
                    if (stock != null)
                    {
                        stock.Quantity  += item.Quantity;
                        stock.UpdatedAt  = DateTime.UtcNow;
                        stock.SyncStatus = SyncStatus.NotSynced;
                        await _stockRepository.UpdateAsync(stock);
                    }
                }
            }
            if (_productRepository != null) await _productRepository.SaveChangesAsync();

            _logger.LogInformation("Purchase {PurchaseNumber} completed — ₹{Total}", PurchaseNumber, TotalAmount);
            SuccessMessage = $"Purchase completed! {PurchaseNumber} — ₹{TotalAmount:N2}";
            ResetPurchase();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing purchase");
            SetError($"Error completing purchase: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private void ResetPurchase()
    {
        PurchaseItems.Clear();
        SelectedSupplier = null;
        SearchText       = string.Empty;
        SearchResults.Clear();
        ClearError();
        GeneratePurchaseNumber();
        PurchaseDate = DateTime.Today;
        this.RaisePropertyChanged(nameof(TotalAmount));
    }

    private void GeneratePurchaseNumber() =>
        PurchaseNumber = $"PUR-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
}
