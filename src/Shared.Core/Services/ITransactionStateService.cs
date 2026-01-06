using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Service for managing transaction state persistence and auto-save functionality
/// Ensures transaction data is not lost during system failures
/// </summary>
public interface ITransactionStateService
{
    /// <summary>
    /// Automatically saves transaction state at regular intervals
    /// </summary>
    /// <param name="saleSessionId">Sale session ID</param>
    /// <param name="transactionData">Transaction data to save</param>
    /// <returns>True if save was successful</returns>
    Task<bool> AutoSaveTransactionStateAsync(Guid saleSessionId, TransactionState transactionData);
    
    /// <summary>
    /// Manually saves transaction state
    /// </summary>
    /// <param name="saleSessionId">Sale session ID</param>
    /// <param name="transactionData">Transaction data to save</param>
    /// <returns>True if save was successful</returns>
    Task<bool> SaveTransactionStateAsync(Guid saleSessionId, TransactionState transactionData);
    
    /// <summary>
    /// Restores transaction state from saved data
    /// </summary>
    /// <param name="saleSessionId">Sale session ID</param>
    /// <returns>Restored transaction state or null if not found</returns>
    Task<TransactionState?> RestoreTransactionStateAsync(Guid saleSessionId);
    
    /// <summary>
    /// Gets all unsaved transaction states for crash recovery
    /// </summary>
    /// <param name="userId">User ID to filter by</param>
    /// <param name="deviceId">Device ID to filter by</param>
    /// <returns>List of unsaved transaction states</returns>
    Task<List<TransactionState>> GetUnsavedTransactionStatesAsync(Guid? userId = null, Guid? deviceId = null);
    
    /// <summary>
    /// Marks transaction state as saved/completed
    /// </summary>
    /// <param name="saleSessionId">Sale session ID</param>
    /// <returns>True if marked successfully</returns>
    Task<bool> MarkTransactionAsCompletedAsync(Guid saleSessionId);
    
    /// <summary>
    /// Clears old transaction states to prevent storage bloat
    /// </summary>
    /// <param name="olderThanDays">Clear states older than this many days</param>
    /// <returns>Number of states cleared</returns>
    Task<int> ClearOldTransactionStatesAsync(int olderThanDays = 7);
    
    /// <summary>
    /// Starts auto-save timer for a session
    /// </summary>
    /// <param name="saleSessionId">Sale session ID</param>
    /// <param name="intervalSeconds">Auto-save interval in seconds</param>
    /// <returns>True if auto-save started</returns>
    Task<bool> StartAutoSaveAsync(Guid saleSessionId, int intervalSeconds = 30);
    
    /// <summary>
    /// Stops auto-save timer for a session
    /// </summary>
    /// <param name="saleSessionId">Sale session ID</param>
    /// <returns>True if auto-save stopped</returns>
    Task<bool> StopAutoSaveAsync(Guid saleSessionId);
}

/// <summary>
/// Represents the complete state of a transaction for persistence
/// </summary>
public class TransactionState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SaleSessionId { get; set; }
    public Guid UserId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid ShopId { get; set; }
    
    /// <summary>
    /// Customer information if selected
    /// </summary>
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerMobileNumber { get; set; }
    
    /// <summary>
    /// Sale items in the transaction
    /// </summary>
    public List<TransactionSaleItem> SaleItems { get; set; } = new();
    
    /// <summary>
    /// Payment information
    /// </summary>
    public PaymentMethod PaymentMethod { get; set; }
    public decimal? AmountReceived { get; set; }
    public decimal? ChangeAmount { get; set; }
    
    /// <summary>
    /// Calculation totals
    /// </summary>
    public decimal Subtotal { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalTax { get; set; }
    public decimal FinalTotal { get; set; }
    
    /// <summary>
    /// Applied discounts
    /// </summary>
    public List<AppliedDiscount> AppliedDiscounts { get; set; } = new();
    
    /// <summary>
    /// State metadata
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSavedAt { get; set; } = DateTime.UtcNow;
    public bool IsCompleted { get; set; } = false;
    public bool IsAutoSaved { get; set; } = false;
    public string? Notes { get; set; }
    
    /// <summary>
    /// Serialized additional data
    /// </summary>
    public string? AdditionalData { get; set; }
}

/// <summary>
/// Represents a sale item in a transaction state
/// </summary>
public class TransactionSaleItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Weight { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsWeightBased { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? BatchNumber { get; set; }
}

/// <summary>
/// Represents an applied discount in a transaction
/// </summary>
public class AppliedDiscount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Percentage { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool IsAutoApplied { get; set; }
}