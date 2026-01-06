using Shared.Core.Entities;
using Shared.Core.Repositories;

namespace Shared.Core.Services;

/// <summary>
/// Comprehensive barcode integration service supporting multiple formats
/// Provides barcode scanning with product lookup and shop inventory validation
/// </summary>
public class BarcodeIntegrationService : IBarcodeIntegrationService
{
    private readonly IBarcodeScanner _barcodeScanner;
    private readonly IProductRepository _productRepository;
    private readonly IStockRepository _stockRepository;
    private readonly IMultiTabSalesManager _salesManager;
    private readonly IEnhancedSalesGridEngine _salesGridEngine;
    private bool _isInitialized = false;

    public event EventHandler<BarcodeProcessedEventArgs>? BarcodeProcessed;
    public event EventHandler<ScanErrorEventArgs>? ScanError;

    public BarcodeIntegrationService(
        IBarcodeScanner barcodeScanner,
        IProductRepository productRepository,
        IStockRepository stockRepository,
        IMultiTabSalesManager salesManager,
        IEnhancedSalesGridEngine salesGridEngine)
    {
        _barcodeScanner = barcodeScanner;
        _productRepository = productRepository;
        _stockRepository = stockRepository;
        _salesManager = salesManager;
        _salesGridEngine = salesGridEngine;
    }

    public async Task<bool> InitializeAsync()
    {
        try
        {
            var scannerInitialized = await _barcodeScanner.InitializeAsync();
            if (!scannerInitialized)
            {
                return false;
            }

            // Subscribe to scanner events
            _barcodeScanner.BarcodeScanned += OnBarcodeScanned;
            _barcodeScanner.StatusChanged += OnScannerStatusChanged;

            _isInitialized = true;
            return true;
        }
        catch (Exception ex)
        {
            ScanError?.Invoke(this, new ScanErrorEventArgs
            {
                ErrorMessage = "Failed to initialize barcode integration service",
                Exception = ex,
                ErrorCode = "INIT_FAILED"
            });
            return false;
        }
    }

    public async Task<BarcodeResult> ScanBarcodeAsync(ScanOptions options)
    {
        try
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            if (!await _barcodeScanner.IsAvailableAsync())
            {
                return new BarcodeResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Barcode scanner is not available"
                };
            }

            // Perform the scan
            var scannedBarcode = await _barcodeScanner.ScanAsync();
            
            if (string.IsNullOrEmpty(scannedBarcode))
            {
                return new BarcodeResult
                {
                    IsSuccess = false,
                    ErrorMessage = "No barcode detected or scan timeout"
                };
            }

            // Validate barcode format
            var isValidFormat = await ValidateBarcodeFormatAsync(scannedBarcode);
            if (!isValidFormat)
            {
                return new BarcodeResult
                {
                    IsSuccess = false,
                    Barcode = scannedBarcode,
                    ErrorMessage = "Unsupported barcode format"
                };
            }

            // Lookup product
            var product = await LookupProductByBarcodeAsync(scannedBarcode, options.ShopId);
            
            var result = new BarcodeResult
            {
                IsSuccess = true,
                Barcode = scannedBarcode,
                Format = DetermineBarcodeFormat(scannedBarcode),
                Product = product,
                Quality = ScanQuality.Good,
                IsProductFound = product != null
            };

            // Check stock availability if product found
            if (product != null)
            {
                var stock = await _stockRepository.GetByProductIdAsync(product.Id);
                result.IsInStock = stock?.Quantity > 0;
                result.AvailableQuantity = stock?.Quantity ?? 0;
            }

            // Provide feedback
            var feedback = await ProvideScanFeedbackAsync(result);
            
            return result;
        }
        catch (Exception ex)
        {
            ScanError?.Invoke(this, new ScanErrorEventArgs
            {
                ErrorMessage = "Error during barcode scanning",
                Exception = ex,
                ErrorCode = "SCAN_ERROR"
            });

            return new BarcodeResult
            {
                IsSuccess = false,
                ErrorMessage = $"Scan error: {ex.Message}"
            };
        }
    }

    public async Task<Product?> LookupProductByBarcodeAsync(string barcode, Guid shopId)
    {
        try
        {
            // First, try to find the product by barcode
            var product = await _productRepository.GetByBarcodeAsync(barcode);
            
            if (product == null || !product.IsActive)
            {
                return null;
            }

            // Validate that the product is available in the specified shop
            // This would typically involve checking shop-specific inventory
            // For now, we'll assume all active products are available in all shops
            
            return product;
        }
        catch (Exception ex)
        {
            ScanError?.Invoke(this, new ScanErrorEventArgs
            {
                ErrorMessage = "Error looking up product by barcode",
                Exception = ex,
                ErrorCode = "LOOKUP_ERROR"
            });
            return null;
        }
    }

    public async Task<bool> ValidateBarcodeFormatAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return false;
        }

        // Validate common barcode formats
        return await Task.FromResult(
            IsValidEAN13(barcode) ||
            IsValidEAN8(barcode) ||
            IsValidCode128(barcode) ||
            IsValidCode39(barcode) ||
            IsValidUPCA(barcode)
        );
    }

    public async Task<ScanFeedback> ProvideScanFeedbackAsync(BarcodeResult result)
    {
        var feedback = new ScanFeedback();

        if (result.IsSuccess && result.IsProductFound)
        {
            feedback.Type = FeedbackType.Success;
            feedback.ShouldPlayBeep = true;
            feedback.ShouldVibrate = true;
            feedback.VisualMessage = $"Product found: {result.Product?.Name}";
            
            if (!result.IsInStock)
            {
                feedback.Type = FeedbackType.Warning;
                feedback.VisualMessage += " (Out of stock)";
            }
        }
        else if (result.IsSuccess && !result.IsProductFound)
        {
            feedback.Type = FeedbackType.Warning;
            feedback.ShouldPlayBeep = false;
            feedback.ShouldVibrate = true;
            feedback.VisualMessage = "Product not found in inventory";
        }
        else
        {
            feedback.Type = FeedbackType.Error;
            feedback.ShouldPlayBeep = false;
            feedback.ShouldVibrate = false;
            feedback.VisualMessage = result.ErrorMessage ?? "Scan failed";
        }

        return await Task.FromResult(feedback);
    }

    public async Task<List<BarcodeFormat>> GetSupportedFormatsAsync()
    {
        return await Task.FromResult(new List<BarcodeFormat>
        {
            new BarcodeFormat
            {
                Name = "EAN-13",
                Description = "European Article Number (13 digits)",
                IsSupported = true,
                CommonUses = new List<string> { "Retail products", "Books", "Magazines" }
            },
            new BarcodeFormat
            {
                Name = "EAN-8",
                Description = "European Article Number (8 digits)",
                IsSupported = true,
                CommonUses = new List<string> { "Small retail items" }
            },
            new BarcodeFormat
            {
                Name = "Code 128",
                Description = "High-density linear barcode",
                IsSupported = true,
                CommonUses = new List<string> { "Shipping", "Packaging", "Internal inventory" }
            },
            new BarcodeFormat
            {
                Name = "Code 39",
                Description = "Variable length alphanumeric barcode",
                IsSupported = true,
                CommonUses = new List<string> { "Automotive", "Defense", "Healthcare" }
            },
            new BarcodeFormat
            {
                Name = "UPC-A",
                Description = "Universal Product Code (12 digits)",
                IsSupported = true,
                CommonUses = new List<string> { "North American retail" }
            }
        });
    }

    public async Task<ScanProcessResult> ProcessScannedBarcodeAsync(string barcode, Guid sessionId)
    {
        try
        {
            // Get the current sale session
            var session = await _salesManager.GetSaleSessionAsync(sessionId);
            if (session == null)
            {
                return new ScanProcessResult
                {
                    IsSuccess = false,
                    Message = "Invalid sale session",
                    ErrorCode = "INVALID_SESSION"
                };
            }

            // Lookup the product
            var product = await LookupProductByBarcodeAsync(barcode, session.ShopId);
            if (product == null)
            {
                return new ScanProcessResult
                {
                    IsSuccess = false,
                    Message = "Product not found",
                    ErrorCode = "PRODUCT_NOT_FOUND",
                    RequiresUserAction = true,
                    UserActionMessage = "Would you like to add this product to inventory?"
                };
            }

            // Check stock availability
            var stock = await _stockRepository.GetByProductIdAsync(product.Id);
            if (stock == null || stock.Quantity <= 0)
            {
                return new ScanProcessResult
                {
                    IsSuccess = false,
                    Message = "Product is out of stock",
                    Product = product,
                    ErrorCode = "OUT_OF_STOCK",
                    RequiresUserAction = true,
                    UserActionMessage = "Product is out of stock. Continue anyway?"
                };
            }

            // Add product to the sale grid
            var addResult = await _salesGridEngine.AddProductToGridAsync(sessionId, product, 1);
            
            if (!addResult.Success)
            {
                return new ScanProcessResult
                {
                    IsSuccess = false,
                    Message = addResult.Message,
                    Product = product,
                    ErrorCode = "ADD_TO_GRID_FAILED"
                };
            }
            
            // Trigger the processed event
            BarcodeProcessed?.Invoke(this, new BarcodeProcessedEventArgs
            {
                Barcode = barcode,
                Product = product,
                SessionId = sessionId
            });

            return new ScanProcessResult
            {
                IsSuccess = true,
                Message = $"Added {product.Name} to sale",
                Product = product,
                SaleItemId = addResult.UpdatedGridState?.Items.LastOrDefault()?.Id
            };
        }
        catch (Exception ex)
        {
            ScanError?.Invoke(this, new ScanErrorEventArgs
            {
                ErrorMessage = "Error processing scanned barcode",
                Exception = ex,
                ErrorCode = "PROCESS_ERROR"
            });

            return new ScanProcessResult
            {
                IsSuccess = false,
                Message = $"Processing error: {ex.Message}",
                ErrorCode = "PROCESS_ERROR"
            };
        }
    }

    private void OnBarcodeScanned(object? sender, BarcodeScannedEventArgs e)
    {
        // Handle continuous scanning mode
        // This would typically be used when the scanner is in continuous mode
        // and we want to automatically process scanned barcodes
    }

    private void OnScannerStatusChanged(object? sender, ScannerStatusChangedEventArgs e)
    {
        // Handle scanner status changes
        // This could be used to update UI or notify users of scanner issues
    }

    private BarcodeFormat DetermineBarcodeFormat(string barcode)
    {
        if (IsValidEAN13(barcode))
        {
            return new BarcodeFormat { Name = "EAN-13", IsSupported = true };
        }
        else if (IsValidEAN8(barcode))
        {
            return new BarcodeFormat { Name = "EAN-8", IsSupported = true };
        }
        else if (IsValidUPCA(barcode))
        {
            return new BarcodeFormat { Name = "UPC-A", IsSupported = true };
        }
        else if (IsValidCode128(barcode))
        {
            return new BarcodeFormat { Name = "Code 128", IsSupported = true };
        }
        else if (IsValidCode39(barcode))
        {
            return new BarcodeFormat { Name = "Code 39", IsSupported = true };
        }

        return new BarcodeFormat { Name = "Unknown", IsSupported = false };
    }

    private bool IsValidEAN13(string barcode)
    {
        return barcode.Length == 13 && barcode.All(char.IsDigit);
    }

    private bool IsValidEAN8(string barcode)
    {
        return barcode.Length == 8 && barcode.All(char.IsDigit);
    }

    private bool IsValidUPCA(string barcode)
    {
        return barcode.Length == 12 && barcode.All(char.IsDigit);
    }

    private bool IsValidCode128(string barcode)
    {
        // Code 128 supports all 128 ASCII characters.
        // A simple validation is to check for non-empty and non-control characters.
        if (string.IsNullOrWhiteSpace(barcode)) return false;

        // Ensure all characters are valid printable ASCII characters (or other supported sets if needed)
        return barcode.All(c => !char.IsControl(c));
    }

    private bool IsValidCode39(string barcode)
    {
        // Code 39 supports uppercase letters, numbers, and some special characters
        // But must be at least 3 characters and not contain invalid characters like !@#$%^&*()
        if (barcode.Length < 3) return false;
        
        var validChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 -.$/+%*";
        return barcode.All(c => validChars.Contains(c));
    }
}