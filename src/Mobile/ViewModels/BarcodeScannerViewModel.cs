using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.Services;
using Shared.Core.Entities;

namespace Mobile.ViewModels;

public partial class BarcodeScannerViewModel : BaseViewModel
{
    private readonly IProductService _productService;
    private readonly IBarcodeIntegrationService? _barcodeIntegrationService;
    private readonly Guid _sessionId;
    private readonly Guid _shopId;

    public BarcodeScannerViewModel(
        IProductService productService,
        IBarcodeIntegrationService? barcodeIntegrationService = null,
        Guid? sessionId = null,
        Guid? shopId = null)
    {
        _productService = productService;
        _barcodeIntegrationService = barcodeIntegrationService;
        _sessionId = sessionId ?? Guid.NewGuid();
        _shopId = shopId ?? Guid.NewGuid();
        
        Title = "Barcode Scanner";
        
        // Subscribe to barcode integration events if available
        if (_barcodeIntegrationService != null)
        {
            _barcodeIntegrationService.BarcodeProcessed += OnBarcodeProcessed;
            _barcodeIntegrationService.ScanError += OnScanError;
        }
    }

    [ObservableProperty]
    private bool isDetecting = true;

    [ObservableProperty]
    private string scannedBarcode = string.Empty;

    [ObservableProperty]
    private string productInfo = string.Empty;

    [ObservableProperty]
    private string scannerStatus = "Ready";

    [ObservableProperty]
    private bool hasValidProduct;

    [ObservableProperty]
    private bool isContinuousScanning;

    private Product? _currentProduct;

    [RelayCommand]
    private async Task ToggleDetectionAsync()
    {
        if (_barcodeIntegrationService == null)
        {
            // Fallback behavior
            IsDetecting = !IsDetecting;
            if (IsDetecting)
            {
                ClearError();
                ProductInfo = string.Empty;
                ScannedBarcode = string.Empty;
                HasValidProduct = false;
            }
            return;
        }

        try
        {
            if (IsDetecting)
            {
                // Stop scanning
                IsDetecting = false;
                IsContinuousScanning = false;
                ScannerStatus = "Stopped";
            }
            else
            {
                // Start continuous scanning
                IsDetecting = true;
                IsContinuousScanning = true;
                ScannerStatus = "Scanning...";
                ClearError();
                ProductInfo = string.Empty;
                ScannedBarcode = string.Empty;
                HasValidProduct = false;

                await StartContinuousScanningAsync();
            }
        }
        catch (Exception ex)
        {
            SetError($"Scanner error: {ex.Message}");
            IsDetecting = false;
            IsContinuousScanning = false;
            ScannerStatus = "Error";
        }
    }

    [RelayCommand]
    private async Task ManualEntryAsync()
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
            await ProcessBarcodeAsync(result);
        }
    }

    [RelayCommand]
    private async Task AddToSaleAsync()
    {
        if (_currentProduct != null)
        {
            try
            {
                if (_barcodeIntegrationService != null)
                {
                    var processResult = await _barcodeIntegrationService.ProcessScannedBarcodeAsync(
                        ScannedBarcode, _sessionId);
                    
                    if (processResult.IsSuccess)
                    {
                        // Navigate back to sale page with success
                        var navigationParameter = new Dictionary<string, object>
                        {
                            { "ScannedBarcode", ScannedBarcode },
                            { "ProductAdded", true }
                        };
                        
                        await Shell.Current.GoToAsync("//sale", navigationParameter);
                    }
                    else
                    {
                        SetError(processResult.Message ?? "Failed to add product to sale");
                    }
                }
                else
                {
                    // Fallback behavior
                    var navigationParameter = new Dictionary<string, object>
                    {
                        { "ScannedBarcode", ScannedBarcode }
                    };
                    
                    await Shell.Current.GoToAsync("//sale", navigationParameter);
                }
            }
            catch (Exception ex)
            {
                SetError($"Error adding to sale: {ex.Message}");
            }
        }
    }

    private async Task ProcessBarcodeAsync(string barcode)
    {
        try
        {
            IsDetecting = false; // Stop detecting while processing
            IsContinuousScanning = false;
            ScannedBarcode = barcode;
            ClearError();
            ScannerStatus = "Processing...";

            if (_barcodeIntegrationService != null)
            {
                // Use the barcode integration service
                var scanOptions = new ScanOptions
                {
                    ShopId = _shopId,
                    SessionId = _sessionId,
                    AutoAddToSale = false
                };

                // Validate barcode format
                var isValid = await _barcodeIntegrationService.ValidateBarcodeFormatAsync(barcode);
                if (!isValid)
                {
                    ProductInfo = "Invalid barcode format";
                    SetError("Invalid barcode format");
                    ScannerStatus = "Ready";
                    await Task.Delay(2000);
                    IsDetecting = true;
                    return;
                }

                // Lookup product
                var product = await _barcodeIntegrationService.LookupProductByBarcodeAsync(barcode, _shopId);
                
                if (product != null)
                {
                    _currentProduct = product;
                    HasValidProduct = true;
                    ProductInfo = $"Product: {product.Name}\nPrice: ₹{product.UnitPrice:F2}";
                    ScannerStatus = "Product Found";

                    // Provide haptic feedback
#if ANDROID || IOS
                    try
                    {
                        Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.Click);
                    }
                    catch
                    {
                        // Haptic feedback not available
                    }
#endif
                }
                else
                {
                    _currentProduct = null;
                    HasValidProduct = false;
                    ProductInfo = "Product not found";
                    SetError($"No product found for barcode: {barcode}");
                    ScannerStatus = "Product Not Found";
                    
                    // Resume scanning after a delay
                    await Task.Delay(2000);
                    IsDetecting = true;
                    IsContinuousScanning = true;
                    ScannerStatus = "Scanning...";
                }
            }
            else
            {
                // Fallback to original implementation
                var product = await _productService.GetProductByBarcodeAsync(barcode);
                
                if (product != null)
                {
                    ProductInfo = $"Product: {product.Name}\nPrice: ₹{product.UnitPrice:F2}";
                    HasValidProduct = true;
                    ScannerStatus = "Product Found";
                    
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
                    HasValidProduct = false;
                    SetError($"No product found for barcode: {barcode}");
                    ScannerStatus = "Product Not Found";
                    
                    // Resume scanning after a delay
                    await Task.Delay(2000);
                    IsDetecting = true;
                    ScannerStatus = "Scanning...";
                }
            }
        }
        catch (Exception ex)
        {
            SetError($"Scanner error: {ex.Message}");
            ProductInfo = "Error occurred";
            HasValidProduct = false;
            ScannerStatus = "Error";
            
            // Resume scanning after a delay
            await Task.Delay(2000);
            IsDetecting = true;
            ScannerStatus = "Scanning...";
        }
    }

    private async Task StartContinuousScanningAsync()
    {
        // This would integrate with the actual camera/scanner hardware
        // For now, we'll simulate continuous scanning
        while (IsContinuousScanning && IsDetecting)
        {
            try
            {
                await Task.Delay(1000); // Simulate scan interval
                
                if (!IsContinuousScanning || !IsDetecting)
                    break;
                
                // In a real implementation, this would capture from camera
                // and use ML.NET or similar to detect barcodes
                
                // For demo purposes, we'll occasionally simulate finding a barcode
                if (Random.Shared.Next(1, 20) == 1) // 5% chance per second
                {
                    var sampleBarcodes = new[] 
                    { 
                        "1234567890123", 
                        "2345678901234", 
                        "3456789012345" 
                    };
                    var randomBarcode = sampleBarcodes[Random.Shared.Next(sampleBarcodes.Length)];
                    await ProcessBarcodeAsync(randomBarcode);
                    break;
                }
            }
            catch (Exception ex)
            {
                SetError($"Continuous scanning error: {ex.Message}");
                break;
            }
        }
    }

    private void OnBarcodeProcessed(object? sender, BarcodeProcessedEventArgs e)
    {
        // Handle automatic barcode processing
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await ProcessBarcodeAsync(e.Barcode);
        });
    }

    private void OnScanError(object? sender, ScanErrorEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SetError(e.ErrorMessage ?? "Unknown scan error");
            ScannerStatus = "Error";
            IsDetecting = false;
            IsContinuousScanning = false;
        });
    }

    public void Initialize()
    {
        IsDetecting = true;
        IsContinuousScanning = false;
        ClearError();
        ProductInfo = string.Empty;
        ScannedBarcode = string.Empty;
        HasValidProduct = false;
        ScannerStatus = "Ready";
        _currentProduct = null;
    }

    public void Dispose()
    {
        if (_barcodeIntegrationService != null)
        {
            _barcodeIntegrationService.BarcodeProcessed -= OnBarcodeProcessed;
            _barcodeIntegrationService.ScanError -= OnScanError;
        }
    }
}