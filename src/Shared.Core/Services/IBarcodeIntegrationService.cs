using Shared.Core.Entities;

namespace Shared.Core.Services;

/// <summary>
/// Interface for comprehensive barcode integration service
/// Provides barcode scanning with product lookup and shop inventory validation
/// </summary>
public interface IBarcodeIntegrationService
{
    /// <summary>
    /// Scans a barcode and returns the result with product information
    /// </summary>
    /// <param name="options">Scanning options and configuration</param>
    /// <returns>Barcode scan result with product details</returns>
    Task<BarcodeResult> ScanBarcodeAsync(ScanOptions options);
    
    /// <summary>
    /// Looks up a product by barcode in the specified shop's inventory
    /// </summary>
    /// <param name="barcode">The barcode to lookup</param>
    /// <param name="shopId">The shop ID to search in</param>
    /// <returns>Product information if found, null otherwise</returns>
    Task<Product?> LookupProductByBarcodeAsync(string barcode, Guid shopId);
    
    /// <summary>
    /// Validates if a barcode format is supported
    /// </summary>
    /// <param name="barcode">The barcode to validate</param>
    /// <returns>True if format is supported</returns>
    Task<bool> ValidateBarcodeFormatAsync(string barcode);
    
    /// <summary>
    /// Provides visual and audio feedback for scan results
    /// </summary>
    /// <param name="result">The scan result to provide feedback for</param>
    /// <returns>Feedback information</returns>
    Task<ScanFeedback> ProvideScanFeedbackAsync(BarcodeResult result);
    
    /// <summary>
    /// Gets list of supported barcode formats
    /// </summary>
    /// <returns>List of supported barcode formats</returns>
    Task<List<BarcodeFormat>> GetSupportedFormatsAsync();
    
    /// <summary>
    /// Initializes the barcode integration service
    /// </summary>
    /// <returns>True if initialization successful</returns>
    Task<bool> InitializeAsync();
    
    /// <summary>
    /// Processes a scanned barcode and adds it to the current sale session
    /// </summary>
    /// <param name="barcode">The scanned barcode</param>
    /// <param name="sessionId">The current sale session ID</param>
    /// <returns>Result of adding the product to the sale</returns>
    Task<ScanProcessResult> ProcessScannedBarcodeAsync(string barcode, Guid sessionId);
    
    /// <summary>
    /// Event raised when a barcode is successfully scanned and processed
    /// </summary>
    event EventHandler<BarcodeProcessedEventArgs> BarcodeProcessed;
    
    /// <summary>
    /// Event raised when a scan fails or encounters an error
    /// </summary>
    event EventHandler<ScanErrorEventArgs> ScanError;
}

/// <summary>
/// Options for barcode scanning
/// </summary>
public class ScanOptions
{
    public Guid ShopId { get; set; }
    public Guid? SessionId { get; set; }
    public bool EnableContinuousMode { get; set; } = false;
    public TimeSpan ScanTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool EnableBeep { get; set; } = true;
    public bool EnableVibration { get; set; } = true;
    public List<string> PreferredFormats { get; set; } = new();
    public bool AutoAddToSale { get; set; } = true;
}

/// <summary>
/// Result of a barcode scan operation
/// </summary>
public class BarcodeResult
{
    public bool IsSuccess { get; set; }
    public string? Barcode { get; set; }
    public BarcodeFormat Format { get; set; }
    public Product? Product { get; set; }
    public ScanQuality Quality { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
    public bool IsProductFound { get; set; }
    public bool IsInStock { get; set; }
    public decimal? AvailableQuantity { get; set; }
}

/// <summary>
/// Feedback information for scan results
/// </summary>
public class ScanFeedback
{
    public bool ShouldPlayBeep { get; set; }
    public bool ShouldVibrate { get; set; }
    public string? VisualMessage { get; set; }
    public FeedbackType Type { get; set; }
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(2);
}

/// <summary>
/// Result of processing a scanned barcode
/// </summary>
public class ScanProcessResult
{
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
    public Product? Product { get; set; }
    public Guid? SaleItemId { get; set; }
    public string? ErrorCode { get; set; }
    public bool RequiresUserAction { get; set; }
    public string? UserActionMessage { get; set; }
}

/// <summary>
/// Supported barcode formats
/// </summary>
public class BarcodeFormat
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSupported { get; set; }
    public List<string> CommonUses { get; set; } = new();
}

/// <summary>
/// Types of feedback
/// </summary>
public enum FeedbackType
{
    Success,
    Warning,
    Error,
    Information
}

/// <summary>
/// Event arguments for barcode processed events
/// </summary>
public class BarcodeProcessedEventArgs : EventArgs
{
    public string Barcode { get; set; } = string.Empty;
    public Product? Product { get; set; }
    public Guid SessionId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event arguments for scan error events
/// </summary>
public class ScanErrorEventArgs : EventArgs
{
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public Exception? Exception { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}