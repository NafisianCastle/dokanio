using Shared.Core.Entities;

namespace Shared.Core.Services;

/// <summary>
/// Interface for the synchronization engine that handles offline-first data synchronization
/// Implements automatic background sync with connectivity detection and conflict resolution
/// </summary>
public interface ISyncEngine
{
    /// <summary>
    /// Synchronizes all data types (sales, products, stock) with the server
    /// </summary>
    /// <returns>Result of the sync operation</returns>
    Task<SyncResult> SyncAllAsync();
    
    /// <summary>
    /// Synchronizes sales data with the server
    /// </summary>
    /// <returns>Result of the sales sync operation</returns>
    Task<SyncResult> SyncSalesAsync();
    
    /// <summary>
    /// Synchronizes product data with the server
    /// </summary>
    /// <returns>Result of the products sync operation</returns>
    Task<SyncResult> SyncProductsAsync();
    
    /// <summary>
    /// Synchronizes stock data with the server
    /// </summary>
    /// <returns>Result of the stock sync operation</returns>
    Task<SyncResult> SyncStockAsync();
    
    /// <summary>
    /// Starts the background sync service with connectivity monitoring
    /// </summary>
    Task StartBackgroundSyncAsync();
    
    /// <summary>
    /// Stops the background sync service
    /// </summary>
    Task StopBackgroundSyncAsync();
    
    /// <summary>
    /// Event raised when sync progress changes
    /// </summary>
    event EventHandler<SyncProgressEventArgs> SyncProgress;
    
    /// <summary>
    /// Event raised when connectivity status changes
    /// </summary>
    event EventHandler<ConnectivityChangedEventArgs> ConnectivityChanged;
}

/// <summary>
/// Result of a synchronization operation
/// </summary>
public class SyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int RecordsUploaded { get; set; }
    public int RecordsDownloaded { get; set; }
    public int ConflictsResolved { get; set; }
    public DateTime SyncTimestamp { get; set; } = DateTime.UtcNow;
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Event arguments for sync progress updates
/// </summary>
public class SyncProgressEventArgs : EventArgs
{
    public string Operation { get; set; } = string.Empty;
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public double ProgressPercentage => TotalRecords > 0 ? (double)ProcessedRecords / TotalRecords * 100 : 0;
}

/// <summary>
/// Event arguments for connectivity changes
/// </summary>
public class ConnectivityChangedEventArgs : EventArgs
{
    public bool IsConnected { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Configuration for sync operations
/// </summary>
public class SyncConfiguration
{
    public string ServerBaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public Guid DeviceId { get; set; }
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan ConnectivityCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public double RetryBackoffMultiplier { get; set; } = 2.0;
    public int BatchSize { get; set; } = 100;
}

/// <summary>
/// Sync request for uploading data to server
/// </summary>
public class SyncUploadRequest
{
    public Guid DeviceId { get; set; }
    public DateTime LastSyncTimestamp { get; set; }
    public List<SaleDto> Sales { get; set; } = new();
    public List<StockUpdateDto> StockUpdates { get; set; } = new();
}

/// <summary>
/// Sync response for downloading data from server
/// </summary>
public class SyncDownloadResponse
{
    public DateTime ServerTimestamp { get; set; }
    public List<ProductDto> Products { get; set; } = new();
    public List<StockDto> Stock { get; set; } = new();
    public bool HasMoreData { get; set; }
}

/// <summary>
/// DTO for sale data in sync operations
/// </summary>
public class SaleDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid DeviceId { get; set; }
    public List<SaleItemDto> Items { get; set; } = new();
}

/// <summary>
/// DTO for sale item data in sync operations
/// </summary>
public class SaleItemDto
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? BatchNumber { get; set; }
}

/// <summary>
/// DTO for product data in sync operations
/// </summary>
public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? Category { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid DeviceId { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? SellingPrice { get; set; }
}

/// <summary>
/// DTO for stock data in sync operations
/// </summary>
public class StockDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public Guid DeviceId { get; set; }
}

/// <summary>
/// DTO for stock update operations
/// </summary>
public class StockUpdateDto
{
    public Guid ProductId { get; set; }
    public int QuantityChange { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid DeviceId { get; set; }
    public string Reason { get; set; } = string.Empty; // "SALE", "PURCHASE", "ADJUSTMENT"
}