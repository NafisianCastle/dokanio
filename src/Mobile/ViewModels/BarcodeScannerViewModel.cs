using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.Services;

namespace Mobile.ViewModels;

public partial class BarcodeScannerViewModel : BaseViewModel
{
    private readonly IProductService _productService;

    public BarcodeScannerViewModel(IProductService productService)
    {
        _productService = productService;
        Title = "Barcode Scanner";
    }

    [ObservableProperty]
    private bool isDetecting = true;

    [ObservableProperty]
    private string scannedBarcode = string.Empty;

    [ObservableProperty]
    private string productInfo = string.Empty;

    [RelayCommand]
    private void ToggleDetection()
    {
        IsDetecting = !IsDetecting;
        if (IsDetecting)
        {
            ClearError();
            ProductInfo = string.Empty;
            ScannedBarcode = string.Empty;
        }
    }

    [RelayCommand]
    private async Task ManualEntry()
    {
        var result = await Shell.Current.DisplayPromptAsync(
            "Manual Barcode Entry", 
            "Enter barcode manually:", 
            "OK", 
            "Cancel", 
            "Barcode", 
            keyboard: Keyboard.Numeric);

        if (!string.IsNullOrEmpty(result))
        {
            await ProcessBarcode(result);
        }
    }

    private async Task ProcessBarcode(string barcode)
    {
        try
        {
            IsDetecting = false; // Stop detecting while processing
            ScannedBarcode = barcode;
            ClearError();

            // Look up product by barcode
            var product = await _productService.GetProductByBarcodeAsync(barcode);
            
            if (product != null)
            {
                ProductInfo = $"Product: {product.Name}\nPrice: ${product.UnitPrice:F2}";
                
                // Navigate back to sale page with scanned product
                var navigationParameter = new Dictionary<string, object>
                {
                    { "ScannedBarcode", barcode }
                };
                
                await Shell.Current.GoToAsync("//sale", navigationParameter);
            }
            else
            {
                ProductInfo = "Product not found";
                SetError($"No product found for barcode: {barcode}");
                
                // Resume scanning after a delay
                await Task.Delay(2000);
                IsDetecting = true;
            }
        }
        catch (Exception ex)
        {
            SetError($"Scanner error: {ex.Message}");
            ProductInfo = "Error occurred";
            
            // Resume scanning after a delay
            await Task.Delay(2000);
            IsDetecting = true;
        }
    }

    public void Initialize()
    {
        IsDetecting = true;
        ClearError();
        ProductInfo = string.Empty;
        ScannedBarcode = string.Empty;
    }
}