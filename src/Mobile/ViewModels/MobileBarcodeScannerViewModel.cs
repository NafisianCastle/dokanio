using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.Services;
using Shared.Core.Entities;
using Microsoft.Extensions.Logging;

namespace Mobile.ViewModels;

/// <summary>
/// Mobile-specific ViewModel for barcode scanning functionality
/// Optimized for touch interfaces and mobile-specific features
/// </summary>
public partial class MobileBarcodeScannerViewModel : BaseViewModel
{
    private readonly IBarcodeIntegrationService _barcodeIntegrationService;
    private readonly IProductService _productService;
    private readonly ILogger<MobileBarcodeScannerViewModel> _logger;

    [ObservableProperty]
    private bool isScannerActive;

    [ObservableProperty]
    private bool isContinuousMode;

    [ObservableProperty]
    private string scanStatus = string.Empty;

    [ObservableProperty]
    private string lastScannedBarcode = string.Empty;

    [ObservableProperty]
    private Product? lastScannedProduct;

    [ObservableProperty]
    private bool enableVibration = true;

    [ObservableProperty]
    private bool enableBeep = true;

    [ObservableProperty]
    private bool autoAddToSale = true;

    [ObservableProperty]
    private TimeSpan scanTimeout = TimeSpan.FromSeconds(30);

    [ObservableProperty]
    private List<string> supportedFormats = new();

    [ObservableProperty]
    private int scanCount;

    [ObservableProperty]
    private DateTime? lastScanTime;

    // Events for communication with parent ViewModels
    public event EventHandler<ProductScannedEventArgs>? ProductScanned;
    public event EventHandler<ScanErrorEventArgs>? ScanFailed;

    public MobileBarcodeScannerViewModel(
        IBarcodeIntegrationService barcodeIntegrationService,
        IProductService productService,
        ILogger<MobileBarcodeScannerViewModel> logger)
    {
        _barcodeIntegrationService = barcodeIntegrationService;
        _productService = productService;
        _logger = logger;
        
        Title = "Barcode Scanner";
        
        // Subscribe to barcode service events
        _barcodeIntegrationService.BarcodeProcessed += OnBarcodeProcessed;
        _barcodeIntegrationService.ScanError += OnScanError;
    }

    [RelayCommand]
    public async Task InitializeScanner()
    {
        try
        {
            IsBusy = true;
            ScanStatus = "Initializing scanner...";
            ClearError();

            var isInitialized = await _barcodeIntegrationService.InitializeAsync();
            if (!isInitialized)
            {
                SetError("Failed to initialize barcode scanner");
                ScanStatus = "Scanner unavailable";
                return;
            }

            // Load supported formats
            var formats = await _barcodeIntegrationService.GetSupportedFormatsAsync();
            SupportedFormats = formats.Where(f => f.IsSupported).Select(f => f.Name).ToList();

            ScanStatus = "Scanner ready";
            _logger.LogInformation("Mobile barcode scanner initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize mobile barcode scanner");
            SetError($"Scanner initialization failed: {ex.Message}");
            ScanStatus = "Initialization failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StartScanning()
    {
        if (IsScannerActive) return;

        try
        {
            IsScannerActive = true;
            ScanStatus = "Ready to scan...";
            ClearError();

            // Trigger haptic feedback
            TriggerHapticFeedback();

            var scanOptions = new ScanOptions
            {
                EnableContinuousMode = IsContinuousMode,
                ScanTimeout = ScanTimeout,
                EnableBeep = EnableBeep,
                EnableVibration = EnableVibration,
                AutoAddToSale = AutoAddToSale,
                PreferredFormats = SupportedFormats
            };

            var result = await _barcodeIntegrationService.ScanBarcodeAsync(scanOptions);
            await ProcessScanResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during mobile barcode scanning");
            SetError($"Scan error: {ex.Message}");
            ScanStatus = "Scan failed";
        }
        finally
        {
            if (!IsContinuousMode)
            {
                IsScannerActive = false;
            }
        }
    }

    [RelayCommand]
    public async Task StopScanning()
    {
        IsScannerActive = false;
        ScanStatus = "Scanner stopped";
        TriggerHapticFeedback();
    }

    [RelayCommand]
    private async Task ToggleContinuousMode()
    {
        IsContinuousMode = !IsContinuousMode;
        TriggerHapticFeedback();
        
        if (IsContinuousMode)
        {
            ScanStatus = "Continuous mode enabled";
        }
        else
        {
            ScanStatus = "Single scan mode";
            if (IsScannerActive)
            {
                await StopScanning();
            }
        }
    }

    [RelayCommand]
    private async Task ManualBarcodeEntry()
    {
        try
        {
            var barcode = await Shell.Current.DisplayPromptAsync(
                "Manual Entry", 
                "Enter barcode manually:", 
                "Lookup", 
                "Cancel", 
                placeholder: "Scan or type barcode",
                keyboard: Keyboard.Text);

            if (string.IsNullOrWhiteSpace(barcode))
                return;

            ScanStatus = "Looking up product...";
            TriggerHapticFeedback();

            // Validate barcode format
            var isValid = await _barcodeIntegrationService.ValidateBarcodeFormatAsync(barcode);
            if (!isValid)
            {
                SetError("Invalid barcode format");
                ScanStatus = "Invalid format";
                return;
            }

            // Create a mock scan result for manual entry
            var result = new BarcodeResult
            {
                IsSuccess = true,
                Barcode = barcode,
                Timestamp = DateTime.UtcNow
            };

            // Lookup product
            var product = await _barcodeIntegrationService.LookupProductByBarcodeAsync(barcode, Guid.Empty);
            result.Product = product;
            result.IsProductFound = product != null;

            await ProcessScanResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual barcode entry");
            SetError($"Manual entry failed: {ex.Message}");
            ScanStatus = "Entry failed";
        }
    }

    [RelayCommand]
    private async Task ClearLastScan()
    {
        LastScannedBarcode = string.Empty;
        LastScannedProduct = null;
        LastScanTime = null;
        ScanStatus = "Scanner ready";
        TriggerHapticFeedback();
    }

    [RelayCommand]
    private async Task RescanLastBarcode()
    {
        if (string.IsNullOrWhiteSpace(LastScannedBarcode))
        {
            SetError("No previous barcode to rescan");
            return;
        }

        try
        {
            ScanStatus = "Rescanning...";
            TriggerHapticFeedback();

            var product = await _barcodeIntegrationService.LookupProductByBarcodeAsync(LastScannedBarcode, Guid.Empty);
            
            var result = new BarcodeResult
            {
                IsSuccess = true,
                Barcode = LastScannedBarcode,
                Product = product,
                IsProductFound = product != null,
                Timestamp = DateTime.UtcNow
            };

            await ProcessScanResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rescanning barcode: {Barcode}", LastScannedBarcode);
            SetError($"Rescan failed: {ex.Message}");
            ScanStatus = "Rescan failed";
        }
    }

    [RelayCommand]
    private async Task ShareBarcode()
    {
        if (string.IsNullOrWhiteSpace(LastScannedBarcode))
        {
            SetError("No barcode to share");
            return;
        }

        try
        {
            TriggerHapticFeedback();
            
            var shareText = $"Barcode: {LastScannedBarcode}";
            if (LastScannedProduct != null)
            {
                shareText += $"\nProduct: {LastScannedProduct.Name}\nPrice: {LastScannedProduct.UnitPrice:C}";
            }

            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Text = shareText,
                Title = "Scanned Barcode"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sharing barcode: {Barcode}", LastScannedBarcode);
            SetError("Failed to share barcode");
            TriggerErrorHapticFeedback();
        }
    }

    [RelayCommand]
    private async Task CopyBarcodeToClipboard()
    {
        if (string.IsNullOrWhiteSpace(LastScannedBarcode))
        {
            SetError("No barcode to copy");
            return;
        }

        try
        {
            TriggerHapticFeedback();
            await Clipboard.Default.SetTextAsync(LastScannedBarcode);
            
            await Shell.Current.DisplayAlert("Copied", "Barcode copied to clipboard", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying barcode to clipboard: {Barcode}", LastScannedBarcode);
            SetError("Failed to copy barcode");
            TriggerErrorHapticFeedback();
        }
    }

    [RelayCommand]
    private async Task ToggleFlashlight()
    {
        try
        {
            TriggerHapticFeedback();
            
            // This would integrate with camera flashlight control
            // For now, show a placeholder message
            await Shell.Current.DisplayAlert(
                "Flashlight", 
                "Flashlight toggle will be available with camera integration", 
                "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling flashlight");
            SetError("Flashlight control failed");
            TriggerErrorHapticFeedback();
        }
    }

    [RelayCommand]
    private async Task ScanFromGallery()
    {
        try
        {
            TriggerHapticFeedback();
            
            var result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Select barcode image"
            });

            if (result != null)
            {
                ScanStatus = "Processing image...";
                
                // This would process the image to extract barcode
                // For now, show a placeholder message
                await Shell.Current.DisplayAlert(
                    "Image Processing", 
                    "Barcode scanning from images will be available soon", 
                    "OK");
                
                ScanStatus = "Scanner ready";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning from gallery");
            SetError("Gallery scan failed");
            ScanStatus = "Gallery scan failed";
            TriggerErrorHapticFeedback();
        }
    }

    private async Task ProcessScanResult(BarcodeResult result)
    {
        try
        {
            if (result.IsSuccess)
            {
                LastScannedBarcode = result.Barcode ?? string.Empty;
                LastScanTime = result.Timestamp;
                ScanCount++;

                if (result.IsProductFound && result.Product != null)
                {
                    LastScannedProduct = result.Product;
                    ScanStatus = $"Found: {result.Product.Name}";
                    
                    // Trigger success haptic feedback
                    TriggerHapticFeedback();
                    
                    // Notify listeners
                    ProductScanned?.Invoke(this, new ProductScannedEventArgs
                    {
                        Barcode = result.Barcode ?? string.Empty,
                        Product = result.Product,
                        ScanTime = result.Timestamp
                    });

                    _logger.LogInformation("Successfully scanned product: {ProductName} (Barcode: {Barcode})", 
                        result.Product.Name, result.Barcode);
                }
                else
                {
                    LastScannedProduct = null;
                    ScanStatus = "Product not found";
                    SetError($"No product found for barcode: {result.Barcode}");
                    
                    // Trigger error haptic feedback
                    TriggerErrorHapticFeedback();
                }
            }
            else
            {
                ScanStatus = result.ErrorMessage ?? "Scan failed";
                SetError(result.ErrorMessage ?? "Unknown scan error");
                
                // Trigger error haptic feedback
                TriggerErrorHapticFeedback();
                
                // Notify listeners
                ScanFailed?.Invoke(this, new ScanErrorEventArgs
                {
                    ErrorMessage = result.ErrorMessage,
                    Timestamp = DateTime.UtcNow
                });
            }

            // Auto-clear status after delay
            _ = Task.Delay(3000).ContinueWith(_ =>
            {
                if (ScanStatus.Contains("Found:") || ScanStatus.Contains("not found") || ScanStatus.Contains("failed"))
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ScanStatus = IsScannerActive ? "Ready to scan..." : "Scanner ready";
                    });
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing scan result");
            SetError($"Failed to process scan: {ex.Message}");
            ScanStatus = "Processing failed";
        }
    }

    private void OnBarcodeProcessed(object? sender, BarcodeProcessedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (e.Product != null)
            {
                var result = new BarcodeResult
                {
                    IsSuccess = true,
                    Barcode = e.Barcode,
                    Product = e.Product,
                    IsProductFound = true,
                    Timestamp = e.Timestamp
                };

                await ProcessScanResult(result);
            }
        });
    }

    private void OnScanError(object? sender, ScanErrorEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var result = new BarcodeResult
            {
                IsSuccess = false,
                ErrorMessage = e.ErrorMessage,
                Timestamp = e.Timestamp
            };

            await ProcessScanResult(result);
        });
    }

    private void TriggerHapticFeedback()
    {
        if (!EnableVibration) return;

        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch
        {
            // Haptic feedback not available
        }
    }

    private void TriggerErrorHapticFeedback()
    {
        if (!EnableVibration) return;

        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
        }
        catch
        {
            // Haptic feedback not available
        }
    }

    public async Task InitializeAsync()
    {
        await InitializeScanner();
    }

    public void Dispose()
    {
        // Unsubscribe from events
        _barcodeIntegrationService.BarcodeProcessed -= OnBarcodeProcessed;
        _barcodeIntegrationService.ScanError -= OnScanError;
    }
}

/// <summary>
/// Event arguments for product scanned events
/// </summary>
public class ProductScannedEventArgs : EventArgs
{
    public string Barcode { get; set; } = string.Empty;
    public Product Product { get; set; } = null!;
    public DateTime ScanTime { get; set; }
}