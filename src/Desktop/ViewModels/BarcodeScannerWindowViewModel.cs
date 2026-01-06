using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.Services;
using Shared.Core.Entities;
using Avalonia.Media;

namespace Desktop.ViewModels;

public partial class BarcodeScannerWindowViewModel : BaseViewModel
{
    private readonly IBarcodeIntegrationService _barcodeIntegrationService;
    private readonly Guid _sessionId;
    private readonly Guid _shopId;

    [ObservableProperty]
    private bool isScanning;

    [ObservableProperty]
    private bool hasScanResult;

    [ObservableProperty]
    private bool hasValidProduct;

    [ObservableProperty]
    private string scannedBarcode = string.Empty;

    [ObservableProperty]
    private string manualBarcode = string.Empty;

    [ObservableProperty]
    private string productInfo = string.Empty;

    [ObservableProperty]
    private string scannerStatus = "Disconnected";

    [ObservableProperty]
    private IBrush scannerStatusColor = Brushes.Gray;

    [ObservableProperty]
    private string busyMessage = string.Empty;

    private Product? _currentProduct;
    private CancellationTokenSource? _scanningCancellationToken;

    public event EventHandler<Product?>? ProductScanned;
    public event EventHandler<Product?>? CloseRequested;

    public BarcodeScannerWindowViewModel(
        IBarcodeIntegrationService barcodeIntegrationService,
        Guid sessionId,
        Guid shopId)
    {
        _barcodeIntegrationService = barcodeIntegrationService;
        _sessionId = sessionId;
        _shopId = shopId;
        
        Title = "Barcode Scanner";
        
        // Subscribe to barcode integration events
        _barcodeIntegrationService.BarcodeProcessed += OnBarcodeProcessed;
        _barcodeIntegrationService.ScanError += OnScanError;
        
        // Initialize scanner
        _ = InitializeScannerAsync();
    }

    [RelayCommand]
    private async Task StartScanningAsync()
    {
        try
        {
            IsScanning = true;
            HasScanResult = false;
            HasValidProduct = false;
            ClearError();
            
            _scanningCancellationToken = new CancellationTokenSource();
            
            var scanOptions = new ScanOptions
            {
                ShopId = _shopId,
                SessionId = _sessionId,
                EnableContinuousMode = false,
                ScanTimeout = TimeSpan.FromSeconds(30),
                EnableBeep = true,
                EnableVibration = false,
                AutoAddToSale = false
            };

            var result = await _barcodeIntegrationService.ScanBarcodeAsync(scanOptions);
            
            if (result.IsSuccess && !string.IsNullOrEmpty(result.Barcode))
            {
                await ProcessScanResult(result);
            }
            else
            {
                SetError(result.ErrorMessage ?? "Scan failed or timeout");
            }
        }
        catch (Exception ex)
        {
            SetError($"Scanning error: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
            _scanningCancellationToken?.Dispose();
            _scanningCancellationToken = null;
        }
    }

    [RelayCommand]
    private void StopScanning()
    {
        _scanningCancellationToken?.Cancel();
        IsScanning = false;
        HasScanResult = false;
    }

    [RelayCommand]
    private async Task ProcessManualBarcodeAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualBarcode))
        {
            SetError("Please enter a barcode");
            return;
        }

        try
        {
            IsBusy = true;
            BusyMessage = "Looking up product...";
            ClearError();

            // Validate barcode format
            var isValid = await _barcodeIntegrationService.ValidateBarcodeFormatAsync(ManualBarcode);
            if (!isValid)
            {
                SetError("Invalid barcode format");
                return;
            }

            // Lookup product
            var product = await _barcodeIntegrationService.LookupProductByBarcodeAsync(ManualBarcode, _shopId);
            
            var result = new BarcodeResult
            {
                IsSuccess = true,
                Barcode = ManualBarcode,
                Product = product,
                IsProductFound = product != null
            };

            await ProcessScanResult(result);
        }
        catch (Exception ex)
        {
            SetError($"Lookup error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddToSale()
    {
        if (_currentProduct != null)
        {
            ProductScanned?.Invoke(this, _currentProduct);
            CloseRequested?.Invoke(this, _currentProduct);
        }
    }

    [RelayCommand]
    private void Close()
    {
        StopScanning();
        CloseRequested?.Invoke(this, null);
    }

    private async Task InitializeScannerAsync()
    {
        try
        {
            IsBusy = true;
            BusyMessage = "Initializing scanner...";
            
            var initialized = await _barcodeIntegrationService.InitializeAsync();
            
            if (initialized)
            {
                ScannerStatus = "Ready";
                ScannerStatusColor = Brushes.Green;
            }
            else
            {
                ScannerStatus = "Failed to initialize";
                ScannerStatusColor = Brushes.Red;
                SetError("Failed to initialize barcode scanner");
            }
        }
        catch (Exception ex)
        {
            ScannerStatus = "Error";
            ScannerStatusColor = Brushes.Red;
            SetError($"Scanner initialization error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ProcessScanResult(BarcodeResult result)
    {
        ScannedBarcode = result.Barcode ?? string.Empty;
        HasScanResult = true;
        
        if (result.IsProductFound && result.Product != null)
        {
            _currentProduct = result.Product;
            HasValidProduct = true;
            
            var stockInfo = result.IsInStock 
                ? $"In Stock: {result.AvailableQuantity}" 
                : "Out of Stock";
            
            ProductInfo = $"{result.Product.Name}\n" +
                         $"Price: ₹{result.Product.UnitPrice:F2}\n" +
                         $"{stockInfo}";
            
            if (!result.IsInStock)
            {
                SetError("Product is out of stock");
            }
        }
        else
        {
            _currentProduct = null;
            HasValidProduct = false;
            ProductInfo = "Product not found in inventory";
            SetError("Product not found. Would you like to add it to inventory?");
        }

        // Provide feedback
        var feedback = await _barcodeIntegrationService.ProvideScanFeedbackAsync(result);
        
        // Play beep if enabled (would need platform-specific implementation)
        if (feedback.ShouldPlayBeep)
        {
            // SystemSounds.Beep.Play(); // Windows specific
        }
    }

    private void OnBarcodeProcessed(object? sender, BarcodeProcessedEventArgs e)
    {
        // Handle automatic barcode processing if needed
    }

    private void OnScanError(object? sender, ScanErrorEventArgs e)
    {
        SetError(e.ErrorMessage ?? "Unknown scan error");
        IsScanning = false;
    }

    public void Cleanup()
    {
        _scanningCancellationToken?.Cancel();
        _scanningCancellationToken?.Dispose();
        
        if (_barcodeIntegrationService != null)
        {
            _barcodeIntegrationService.BarcodeProcessed -= OnBarcodeProcessed;
            _barcodeIntegrationService.ScanError -= OnScanError;
        }
    }
}