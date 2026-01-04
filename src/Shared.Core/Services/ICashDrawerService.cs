namespace Shared.Core.Services;

/// <summary>
/// Interface for cash drawer control service for desktop applications
/// Provides cash drawer management capabilities with error handling
/// </summary>
public interface ICashDrawerService
{
    /// <summary>
    /// Opens the cash drawer
    /// </summary>
    /// <returns>Result of the open operation</returns>
    Task<CashDrawerResult> OpenDrawerAsync();
    
    /// <summary>
    /// Checks if cash drawer is connected and available
    /// </summary>
    /// <returns>True if cash drawer is connected</returns>
    Task<bool> IsConnectedAsync();
    
    /// <summary>
    /// Checks if cash drawer is currently open
    /// </summary>
    /// <returns>True if drawer is open</returns>
    Task<bool> IsOpenAsync();
    
    /// <summary>
    /// Gets status of the cash drawer
    /// </summary>
    /// <returns>Current status of the cash drawer</returns>
    Task<CashDrawerStatus> GetStatusAsync();
    
    /// <summary>
    /// Gets list of available cash drawers
    /// </summary>
    /// <returns>List of available cash drawer information</returns>
    Task<IEnumerable<CashDrawerInfo>> GetAvailableDrawersAsync();
    
    /// <summary>
    /// Connects to a specific cash drawer
    /// </summary>
    /// <param name="drawerId">ID of the cash drawer to connect to</param>
    /// <returns>True if connection successful</returns>
    Task<bool> ConnectToDrawerAsync(string drawerId);
    
    /// <summary>
    /// Disconnects from the current cash drawer
    /// </summary>
    Task DisconnectAsync();
    
    /// <summary>
    /// Event raised when cash drawer status changes
    /// </summary>
    event EventHandler<CashDrawerStatusChangedEventArgs> StatusChanged;
}

/// <summary>
/// Result of a cash drawer operation
/// </summary>
public class CashDrawerResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public CashDrawerError? Error { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Information about an available cash drawer
/// </summary>
public class CashDrawerInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "USB", "Serial", "Network"
    public bool IsConnected { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Status of the cash drawer
/// </summary>
public enum CashDrawerStatus
{
    Unknown = 0,
    Closed = 1,
    Open = 2,
    Error = 3,
    NotConnected = 4
}

/// <summary>
/// Types of cash drawer errors
/// </summary>
public enum CashDrawerError
{
    None = 0,
    NotConnected = 1,
    CommunicationError = 2,
    DrawerJammed = 3,
    PowerError = 4,
    UnknownError = 5
}

/// <summary>
/// Event arguments for cash drawer status changes
/// </summary>
public class CashDrawerStatusChangedEventArgs : EventArgs
{
    public CashDrawerStatus Status { get; set; }
    public bool IsConnected { get; set; }
    public CashDrawerError? Error { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Configuration for cash drawer operations
/// </summary>
public class CashDrawerConfiguration
{
    public string Port { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public TimeSpan OpenTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public bool AutoClose { get; set; } = false;
    public TimeSpan AutoCloseDelay { get; set; } = TimeSpan.FromSeconds(30);
}