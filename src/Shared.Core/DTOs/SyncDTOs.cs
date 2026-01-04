using Shared.Core.Enums;

namespace Shared.Core.DTOs;

public class SyncUploadRequest
{
    public Guid DeviceId { get; set; }
    public DateTime LastSyncTimestamp { get; set; }
    public List<SaleDto> Sales { get; set; } = new();
    public List<StockUpdateDto> StockUpdates { get; set; } = new();
}

public class SyncDownloadResponse
{
    public DateTime ServerTimestamp { get; set; }
    public List<ProductDto> Products { get; set; } = new();
    public List<StockDto> Stock { get; set; } = new();
    public bool HasMoreData { get; set; }
}

public class SaleDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid DeviceId { get; set; }
    public List<SaleItemDto> Items { get; set; } = new();
}

public class SaleItemDto
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? BatchNumber { get; set; }
}

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

public class StockDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}

public class StockUpdateDto
{
    public Guid ProductId { get; set; }
    public int QuantityChange { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class SyncConfiguration
{
    public Guid DeviceId { get; set; }
    public string ServerUrl { get; set; } = string.Empty;
    public string ServerBaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan ConnectivityCheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(5);
    public double RetryBackoffMultiplier { get; set; } = 2.0;
    public int BatchSize { get; set; } = 100;
}

public class SyncProgressEventArgs : EventArgs
{
    public string Operation { get; set; } = string.Empty;
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public double ProgressPercentage => TotalRecords > 0 ? (double)ProcessedRecords / TotalRecords * 100 : 0;
}