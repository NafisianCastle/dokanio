using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.Entities;
using Shared.Core.Services;

namespace Mobile.ViewModels;

public partial class ProductListViewModel : BaseViewModel
{
    private readonly IProductService _productService;

    public ProductListViewModel(IProductService productService)
    {
        _productService = productService;
        Title = "Products";
        Products = new ObservableCollection<Product>();
    }

    [ObservableProperty]
    private ObservableCollection<Product> products;

    [ObservableProperty]
    private Product? selectedProduct;

    [ObservableProperty]
    private string searchText = string.Empty;

    [RelayCommand]
    private async Task LoadProducts()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ClearError();

            var allProducts = await _productService.GetAllActiveProductsAsync();
            
            Products.Clear();
            foreach (var product in allProducts)
            {
                Products.Add(product);
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to load products: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SearchProducts()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ClearError();

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                await LoadProducts();
                return;
            }

            var searchResults = await _productService.SearchProductsAsync(SearchText);
            
            Products.Clear();
            foreach (var product in searchResults)
            {
                Products.Add(product);
            }
        }
        catch (Exception ex)
        {
            SetError($"Search failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectProduct(Product product)
    {
        if (product == null) return;

        SelectedProduct = product;
        
        // Navigate to sale page with selected product
        var navigationParameter = new Dictionary<string, object>
        {
            { "SelectedProduct", product }
        };
        
        await Shell.Current.GoToAsync("//sale", navigationParameter);
    }

    [RelayCommand]
    private async Task ScanBarcode()
    {
        await Shell.Current.GoToAsync("//scanner");
    }

    public async Task Initialize()
    {
        await LoadProducts();
    }
}