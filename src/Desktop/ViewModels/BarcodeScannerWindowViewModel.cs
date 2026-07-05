using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using Avalonia.Media;
using Shared.Core.Entities;
using Shared.Core.Services;

namespace Desktop.ViewModels;

public class BarcodeScannerWindowViewModel : BaseViewModel
{
    private readonly IBarcodeIntegrationService _barcodeIntegrationService;
    private readonly Guid _sessionId;
    private readonly Guid _shopId;

    [Reactive] public bool IsScanning { get; set; }
    [Reactive] public bool HasScanResult { get; set; }
    [Reactive] public bool HasValidProduct { get; set; }
    [Reactive] public string ScannedBarcode { get; set; } = string.Empty;
    [Reactive] public string ManualBarcode { get; set; } = string.Empty;
    [Reactive] public string ProductInfo { get; set; } = string.Empty;
    [Reactive] public string ScannerStatus { get; set; } = "Disconnected";
    [Reactive] public IBrush ScannerStatusColor { get; set; } = Brushes.Gray;
    [Reactive] public string BusyMessage { get; set; } = string.Empty;

    private Product? _currentProduct;
    private CancellationTokenSource? _scanningCts;

    public event EventHandler<Product?>? ProductScanned;
    public event EventHandler<Product?>? CloseRequested;

    public ReactiveCommand<Unit, Unit> StartScanningCommand { get; }
    public ReactiveCommand<Unit, Unit> StopScanningCommand { get; }
    public ReactiveCommand<Unit, Unit> ProcessManualBarcodeCommand { get; }
    public ReactiveCommand<Unit, Unit> AddToSaleCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    public BarcodeScannerWindowViewModel(
        IBarcodeIntegrationService barcodeIntegrationService,
        Guid sessionId,
        Guid shopId)
    {
        _barcodeIntegrationService = barcodeIntegrationService;
        _sessionId = sessionId;
        _shopId    = shopId;
        Title      = "Barcode Scanner";

        _barcodeIntegrationService.BarcodeProcessed += OnBarcodeProcessed;
        _barcodeIntegrationService.ScanError        += OnScanError;

        var hasProduct = this.WhenAnyValue(x => x.HasValidProduct);

        StartScanningCommand        = ReactiveCommand.CreateFromTask(StartScanningAsync);
        StopScanningCommand         = ReactiveCommand.Create(StopScanning);
        ProcessManualBarcodeCommand = ReactiveCommand.CreateFromTask(ProcessManualBarcodeAsync);
        AddToSaleCommand            = ReactiveCommand.Create(AddToSale, hasProduct);
        CloseCommand                = ReactiveCommand.Create(Close);

        _ = InitializeScannerAsync();
    }

    private async Task StartScanningAsync()
    {
        try
        {
            IsScanning    = true;
            HasScanResult = false;
            HasValidProduct = false;
            ClearError();

            _scanningCts = new CancellationTokenSource();

            var result = await _barcodeIntegrationService.ScanBarcodeAsync(new ScanOptions
            {
                ShopId              = _shopId,
                SessionId           = _sessionId,
                EnableContinuousMode = false,
                ScanTimeout         = TimeSpan.FromSeconds(30),
                EnableBeep          = true,
                EnableVibration     = false,
                AutoAddToSale       = false
            });

            if (result.IsSuccess && !string.IsNullOrEmpty(result.Barcode))
                await ProcessScanResult(result);
            else
                SetError(result.ErrorMessage ?? "Scan failed or timeout");
        }
        catch (Exception ex) { SetError($"Scanning error: {ex.Message}"); }
        finally
        {
            IsScanning = false;
            _scanningCts?.Dispose();
            _scanningCts = null;
        }
    }

    private void StopScanning()
    {
        _scanningCts?.Cancel();
        IsScanning    = false;
        HasScanResult = false;
    }

    private async Task ProcessManualBarcodeAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualBarcode)) { SetError("Please enter a barcode"); return; }

        IsBusy = true;
        BusyMessage = "Looking up product...";
        ClearError();
        try
        {
            var isValid = await _barcodeIntegrationService.ValidateBarcodeFormatAsync(ManualBarcode);
            if (!isValid) { SetError("Invalid barcode format"); return; }

            var product = await _barcodeIntegrationService.LookupProductByBarcodeAsync(ManualBarcode, _shopId);
            await ProcessScanResult(new BarcodeResult
            {
                IsSuccess      = true,
                Barcode        = ManualBarcode,
                Product        = product,
                IsProductFound = product != null
            });
        }
        catch (Exception ex) { SetError($"Lookup error: {ex.Message}"); }
        finally { IsBusy = false; BusyMessage = string.Empty; }
    }

    private void AddToSale()
    {
        if (_currentProduct != null)
        {
            ProductScanned?.Invoke(this, _currentProduct);
            CloseRequested?.Invoke(this, _currentProduct);
        }
    }

    private void Close()
    {
        StopScanning();
        CloseRequested?.Invoke(this, null);
    }

    private async Task InitializeScannerAsync()
    {
        IsBusy = true;
        BusyMessage = "Initializing scanner...";
        try
        {
            var ok = await _barcodeIntegrationService.InitializeAsync();
            ScannerStatus      = ok ? "Ready" : "Failed to initialize";
            ScannerStatusColor = ok ? Brushes.Green : Brushes.Red;
            if (!ok) SetError("Failed to initialize barcode scanner");
        }
        catch (Exception ex)
        {
            ScannerStatus      = "Error";
            ScannerStatusColor = Brushes.Red;
            SetError($"Scanner initialization error: {ex.Message}");
        }
        finally { IsBusy = false; BusyMessage = string.Empty; }
    }

    private async Task ProcessScanResult(BarcodeResult result)
    {
        ScannedBarcode = result.Barcode ?? string.Empty;
        HasScanResult  = true;

        if (result.IsProductFound && result.Product != null)
        {
            _currentProduct = result.Product;
            HasValidProduct = true;
            var stockInfo   = result.IsInStock ? $"In Stock: {result.AvailableQuantity}" : "Out of Stock";
            ProductInfo     = $"{result.Product.Name}\nPrice: ₹{result.Product.UnitPrice:F2}\n{stockInfo}";
            if (!result.IsInStock) SetError("Product is out of stock");
        }
        else
        {
            _currentProduct = null;
            HasValidProduct = false;
            ProductInfo     = "Product not found in inventory";
            SetError("Product not found. Would you like to add it to inventory?");
        }

        await _barcodeIntegrationService.ProvideScanFeedbackAsync(result);
    }

    private void OnBarcodeProcessed(object? sender, BarcodeProcessedEventArgs e) { /* handled via ScanBarcodeAsync */ }

    private void OnScanError(object? sender, ScanErrorEventArgs e)
    {
        SetError(e.ErrorMessage ?? "Unknown scan error");
        IsScanning = false;
    }

    public void Cleanup()
    {
        _scanningCts?.Cancel();
        _scanningCts?.Dispose();
        _barcodeIntegrationService.BarcodeProcessed -= OnBarcodeProcessed;
        _barcodeIntegrationService.ScanError        -= OnScanError;
    }
}
