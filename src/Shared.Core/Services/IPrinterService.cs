using Shared.Core.Entities;

namespace Shared.Core.Services;

/// <summary>
/// Interface for printer services supporting Bluetooth thermal printers
/// Provides receipt printing capabilities with error handling
/// </summary>
public interface IPrinterService
{
    /// <summary>
    /// Checks if a printer is connected and available
    /// </summary>
    /// <returns>True if printer is connected and ready</returns>
    Task<bool> IsConnectedAsync();
    
    /// <summary>
    /// Prints a receipt for the given sale
    /// </summary>
    /// <param name="sale">Sale to print receipt for</param>
    /// <returns>Result of the print operation</returns>
    Task<PrintResult> PrintReceiptAsync(Sale sale);
    
    /// <summary>
    /// Prints a test page to verify printer functionality
    /// </summary>
    /// <returns>Result of the test print operation</returns>
    Task<PrintResult> PrintTestPageAsync();
    
    /// <summary>
    /// Gets list of available printers
    /// </summary>
    /// <returns>List of available printer information</returns>
    Task<IEnumerable<PrinterInfo>> GetAvailablePrintersAsync();
    
    /// <summary>
    /// Connects to a specific printer
    /// </summary>
    /// <param name="printerId">ID of the printer to connect to</param>
    /// <returns>True if connection successful</returns>
    Task<bool> ConnectToPrinterAsync(string printerId);
    
    /// <summary>
    /// Disconnects from the current printer
    /// </summary>
    Task DisconnectAsync();
    
    /// <summary>
    /// Event raised when printer status changes
    /// </summary>
    event EventHandler<PrinterStatusChangedEventArgs> StatusChanged;
}

/// <summary>
/// Result of a print operation
/// </summary>
public class PrintResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public PrintError? Error { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Information about an available printer
/// </summary>
public class PrinterInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "Bluetooth", "USB", "Network"
    public bool IsConnected { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Types of printer errors
/// </summary>
public enum PrintError
{
    None = 0,
    NotConnected = 1,
    OutOfPaper = 2,
    LowBattery = 3,
    CommunicationError = 4,
    PrinterBusy = 5,
    InvalidData = 6,
    UnknownError = 7
}

/// <summary>
/// Event arguments for printer status changes
/// </summary>
public class PrinterStatusChangedEventArgs : EventArgs
{
    public bool IsConnected { get; set; }
    public string Status { get; set; } = string.Empty;
    public PrintError? Error { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Configuration for receipt formatting
/// </summary>
public class ReceiptConfiguration
{
    public string ShopName { get; set; } = "POS Shop";
    public string ShopAddress { get; set; } = string.Empty;
    public string ShopPhone { get; set; } = string.Empty;
    public int PaperWidth { get; set; } = 48; // Characters per line
    public bool PrintLogo { get; set; } = false;
    public bool PrintBarcode { get; set; } = true;
    public string FooterMessage { get; set; } = "Thank you for your business!";
}