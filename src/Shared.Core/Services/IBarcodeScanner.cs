namespace Shared.Core.Services;

/// <summary>
/// Interface for barcode scanner services supporting camera and USB scanners
/// Provides barcode scanning capabilities with error handling
/// </summary>
public interface IBarcodeScanner
{
    /// <summary>
    /// Initializes the barcode scanner
    /// </summary>
    /// <returns>True if initialization successful</returns>
    Task<bool> InitializeAsync();
    
    /// <summary>
    /// Scans for a barcode (blocking operation)
    /// </summary>
    /// <returns>Scanned barcode string or null if scan failed/cancelled</returns>
    Task<string?> ScanAsync();
    
    /// <summary>
    /// Starts continuous scanning mode
    /// </summary>
    /// <returns>True if continuous scanning started successfully</returns>
    Task<bool> StartContinuousScanAsync();
    
    /// <summary>
    /// Stops continuous scanning mode
    /// </summary>
    Task StopContinuousScanAsync();
    
    /// <summary>
    /// Checks if scanner is available and ready
    /// </summary>
    /// <returns>True if scanner is ready</returns>
    Task<bool> IsAvailableAsync();
    
    /// <summary>
    /// Gets list of available scanners
    /// </summary>
    /// <returns>List of available scanner information</returns>
    Task<IEnumerable<ScannerInfo>> GetAvailableScannersAsync();
    
    /// <summary>
    /// Connects to a specific scanner
    /// </summary>
    /// <param name="scannerId">ID of the scanner to connect to</param>
    /// <returns>True if connection successful</returns>
    Task<bool> ConnectToScannerAsync(string scannerId);
    
    /// <summary>
    /// Disconnects from the current scanner
    /// </summary>
    Task DisconnectAsync();
    
    /// <summary>
    /// Event raised when a barcode is scanned in continuous mode
    /// </summary>
    event EventHandler<BarcodeScannedEventArgs> BarcodeScanned;
    
    /// <summary>
    /// Event raised when scanner status changes
    /// </summary>
    event EventHandler<ScannerStatusChangedEventArgs> StatusChanged;
}

/// <summary>
/// Information about an available scanner
/// </summary>
public class ScannerInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "Camera", "USB", "Bluetooth"
    public bool IsConnected { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Event arguments for barcode scanned events
/// </summary>
public class BarcodeScannedEventArgs : EventArgs
{
    public string Barcode { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty; // "EAN13", "Code128", etc.
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ScanQuality Quality { get; set; } = ScanQuality.Good;
}

/// <summary>
/// Event arguments for scanner status changes
/// </summary>
public class ScannerStatusChangedEventArgs : EventArgs
{
    public bool IsConnected { get; set; }
    public string Status { get; set; } = string.Empty;
    public ScannerError? Error { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Quality of the scanned barcode
/// </summary>
public enum ScanQuality
{
    Poor = 0,
    Fair = 1,
    Good = 2,
    Excellent = 3
}

/// <summary>
/// Types of scanner errors
/// </summary>
public enum ScannerError
{
    None = 0,
    NotConnected = 1,
    CameraNotAvailable = 2,
    PermissionDenied = 3,
    CommunicationError = 4,
    ScannerBusy = 5,
    InvalidBarcode = 6,
    UnknownError = 7
}

/// <summary>
/// Configuration for barcode scanning
/// </summary>
public class ScannerConfiguration
{
    public bool EnableContinuousMode { get; set; } = false;
    public TimeSpan ScanTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool EnableBeep { get; set; } = true;
    public bool EnableVibration { get; set; } = true;
    public List<string> SupportedFormats { get; set; } = new() { "EAN13", "EAN8", "Code128", "Code39" };
}