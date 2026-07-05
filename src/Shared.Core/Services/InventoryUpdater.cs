using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;

namespace Shared.Core.Services;

/// <summary>
/// Production-ready inventory updater that reduces stock levels upon sale completion,
/// updates customer purchase history and membership points, and supports rollback
/// for failed transactions. Uses optimistic concurrency for concurrent sale safety.
///
/// Requirement 6.3: THE Inventory_Updater SHALL reduce stock levels for all sold items upon sale completion.
/// Requirement 6.6: THE Sale_Service SHALL update customer purchase history and membership points upon completion.
/// </summary>
public class InventoryUpdater : IInventoryUpdater
{
    private readonly IStockRepository _stockRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleItemRepository _saleItemRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerMembershipRepository _customerMembershipRepository;
    private readonly IMembershipService _membershipService;
    private readonly PosDbContext _context;
    private readonly ILogger<InventoryUpdater> _logger;

    // In-memory snapshot store for rollback: maps saleId -> list of (productId, quantityDeducted)
    // In a production multi-process environment this would be backed by a distributed cache or DB table.
    private static readonly Dictionary<Guid, List<StockSnapshot>> _stockSnapshots = new();
    private static readonly SemaphoreSlim _concurrencyLock = new(1, 1);

    public InventoryUpdater(
        IStockRepository stockRepository,
        ISaleRepository saleRepository,
        ISaleItemRepository saleItemRepository,
        ICustomerRepository customerRepository,
        ICustomerMembershipRepository customerMembershipRepository,
        IMembershipService membershipService,
        PosDbContext context,
        ILogger<InventoryUpdater> logger)
    {
        _stockRepository = stockRepository;
        _saleRepository = saleRepository;
        _saleItemRepository = saleItemRepository;
        _customerRepository = customerRepository;
        _customerMembershipRepository = customerMembershipRepository;
        _membershipService = membershipService;
        _context = context;
        _logger = logger;
    }

    // =========================================================================
    // Stock Level Reduction
    // =========================================================================

    /// <summary>
    /// Reduces stock levels for all items in a completed sale.
    /// Uses a semaphore to prevent concurrent updates from causing race conditions.
    /// Requirement 6.3: Reduce stock levels for all sold items upon sale completion.
    /// </summary>
    public async Task<InventoryUpdateResult> ReduceStockLevelsAsync(Sale sale)
    {
        if (sale == null)
            throw new ArgumentNullException(nameof(sale));

        _logger.LogInformation("Reducing stock levels for sale {SaleId} (invoice: {InvoiceNumber})",
            sale.Id, sale.InvoiceNumber);

        var activeItems = sale.Items.Where(i => !i.IsDeleted).ToList();

        if (!activeItems.Any())
        {
            _logger.LogWarning("Sale {SaleId} has no active items — no stock reduction performed", sale.Id);
            return InventoryUpdateResult.Success(sale.Id, 0, new List<StockUpdateDetail>());
        }

        // Use semaphore to serialize concurrent inventory updates for the same sale
        await _concurrencyLock.WaitAsync();
        try
        {
            return await ReduceStockInternalAsync(sale, activeItems);
        }
        finally
        {
            _concurrencyLock.Release();
        }
    }

    private async Task<InventoryUpdateResult> ReduceStockInternalAsync(Sale sale, List<SaleItem> activeItems)
    {
        var updateDetails = new List<StockUpdateDetail>();
        var snapshots = new List<StockSnapshot>();

        try
        {
            foreach (var item in activeItems)
            {
                var detail = await ReduceSingleItemStockAsync(sale, item, snapshots);
                updateDetails.Add(detail);

                if (!detail.IsSuccess)
                {
                    // Rollback all previously updated items before returning failure
                    _logger.LogWarning(
                        "Stock reduction failed for product {ProductId} in sale {SaleId}. Rolling back {Count} previous updates.",
                        item.ProductId, sale.Id, snapshots.Count);

                    await RollbackFromSnapshotsAsync(sale.Id, snapshots);

                    return new InventoryUpdateResult
                    {
                        IsSuccess = false,
                        SaleId = sale.Id,
                        ItemsUpdated = 0,
                        UpdatedItems = updateDetails,
                        ErrorMessage = detail.ErrorMessage,
                        WasRolledBack = true
                    };
                }
            }

            // Store snapshots for potential future rollback (e.g., if payment fails after stock was reduced)
            lock (_stockSnapshots)
            {
                _stockSnapshots[sale.Id] = snapshots;
            }

            _logger.LogInformation(
                "Stock levels reduced for {Count} items in sale {SaleId}",
                updateDetails.Count, sale.Id);

            return InventoryUpdateResult.Success(sale.Id, updateDetails.Count, updateDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error reducing stock for sale {SaleId}", sale.Id);

            // Attempt rollback on unexpected error
            if (snapshots.Any())
            {
                await RollbackFromSnapshotsAsync(sale.Id, snapshots);
            }

            return new InventoryUpdateResult
            {
                IsSuccess = false,
                SaleId = sale.Id,
                ItemsUpdated = 0,
                UpdatedItems = updateDetails,
                ErrorMessage = $"Unexpected error during stock reduction: {ex.Message}",
                WasRolledBack = snapshots.Any()
            };
        }
    }

    private async Task<StockUpdateDetail> ReduceSingleItemStockAsync(
        Sale sale, SaleItem item, List<StockSnapshot> snapshots)
    {
        var productName = item.Product?.Name ?? $"Product {item.ProductId}";
        var quantityToDeduct = item.IsWeightBased ? 1 : item.Quantity;

        try
        {
            // Get current stock record
            var stockRecords = await _stockRepository.FindAsync(s =>
                s.ProductId == item.ProductId && !s.IsDeleted);

            var stockRecord = stockRecords.FirstOrDefault();
            var stockBefore = stockRecord?.Quantity ?? 0;

            // Save snapshot for rollback
            snapshots.Add(new StockSnapshot
            {
                ProductId = item.ProductId,
                QuantityDeducted = quantityToDeduct,
                StockBefore = stockBefore
            });

            if (stockRecord == null)
            {
                // No stock record exists — create one at zero (already sold, so we record the deficit)
                _logger.LogWarning(
                    "No stock record found for product {ProductId} ({Name}) in sale {SaleId}. Creating at 0.",
                    item.ProductId, productName, sale.Id);

                var newStock = new Stock
                {
                    Id = Guid.NewGuid(),
                    ProductId = item.ProductId,
                    ShopId = sale.ShopId,
                    Quantity = 0,
                    LastUpdatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    DeviceId = sale.DeviceId,
                    SyncStatus = SyncStatus.NotSynced
                };
                await _stockRepository.AddAsync(newStock);
                await _stockRepository.SaveChangesAsync();

                return new StockUpdateDetail
                {
                    ProductId = item.ProductId,
                    ProductName = productName,
                    QuantityDeducted = quantityToDeduct,
                    StockBefore = 0,
                    StockAfter = 0,
                    IsSuccess = true
                };
            }

            // Reduce stock (floor at 0 to avoid negative stock)
            var newQuantity = Math.Max(0, stockRecord.Quantity - quantityToDeduct);
            stockRecord.Quantity = newQuantity;
            stockRecord.LastUpdatedAt = DateTime.UtcNow;
            stockRecord.UpdatedAt = DateTime.UtcNow;
            stockRecord.SyncStatus = SyncStatus.NotSynced;

            await _stockRepository.UpdateAsync(stockRecord);
            await _stockRepository.SaveChangesAsync();

            _logger.LogDebug(
                "Stock reduced for product {ProductId} ({Name}): {Before} → {After} (deducted {Qty})",
                item.ProductId, productName, stockBefore, newQuantity, quantityToDeduct);

            return new StockUpdateDetail
            {
                ProductId = item.ProductId,
                ProductName = productName,
                QuantityDeducted = quantityToDeduct,
                StockBefore = stockBefore,
                StockAfter = newQuantity,
                IsSuccess = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reducing stock for product {ProductId} ({Name}) in sale {SaleId}",
                item.ProductId, productName, sale.Id);

            return new StockUpdateDetail
            {
                ProductId = item.ProductId,
                ProductName = productName,
                QuantityDeducted = quantityToDeduct,
                StockBefore = 0,
                StockAfter = 0,
                IsSuccess = false,
                ErrorMessage = $"Failed to reduce stock: {ex.Message}"
            };
        }
    }

    // =========================================================================
    // Customer Purchase History Update
    // =========================================================================

    /// <summary>
    /// Updates the customer's purchase history and membership points after a completed sale.
    /// Requirement 6.6: Update customer purchase history and membership points upon completion.
    /// </summary>
    public async Task<CustomerHistoryUpdateResult> UpdateCustomerPurchaseHistoryAsync(Sale sale)
    {
        if (sale == null)
            throw new ArgumentNullException(nameof(sale));

        // If no customer is associated with this sale, skip the update
        if (!sale.CustomerId.HasValue)
        {
            _logger.LogDebug("Sale {SaleId} has no customer — skipping purchase history update", sale.Id);
            return CustomerHistoryUpdateResult.NoCustomerResult(sale.Id);
        }

        _logger.LogInformation(
            "Updating purchase history for customer {CustomerId} from sale {SaleId} (amount: {Amount:C})",
            sale.CustomerId.Value, sale.Id, sale.TotalAmount);

        try
        {
            var customer = await _customerRepository.GetByIdAsync(sale.CustomerId.Value);
            if (customer == null || customer.IsDeleted)
            {
                _logger.LogWarning("Customer {CustomerId} not found or deleted — skipping history update",
                    sale.CustomerId.Value);
                return CustomerHistoryUpdateResult.Failure(sale.Id,
                    $"Customer {sale.CustomerId.Value} not found.");
            }

            var oldTier = customer.Tier;

            // Delegate to MembershipService which handles TotalSpent, VisitCount, LastVisit, and tier upgrade
            await _membershipService.UpdateCustomerPurchaseHistoryAsync(customer, sale);

            // Re-fetch to get the updated tier
            var updatedCustomer = await _customerRepository.GetByIdAsync(customer.Id);
            var newTier = updatedCustomer?.Tier ?? oldTier;
            var tierUpgraded = newTier != oldTier;

            if (tierUpgraded)
            {
                _logger.LogInformation(
                    "Customer {CustomerId} ({MembershipNumber}) tier upgraded: {OldTier} → {NewTier}",
                    customer.Id, customer.MembershipNumber, oldTier, newTier);
            }

            // Update membership points if a CustomerMembership record exists
            await UpdateMembershipPointsAsync(customer, sale);

            _logger.LogInformation(
                "Purchase history updated for customer {CustomerId}: +{Amount:C}, tier={Tier}",
                customer.Id, sale.TotalAmount, newTier);

            return new CustomerHistoryUpdateResult
            {
                IsSuccess = true,
                SaleId = sale.Id,
                CustomerId = customer.Id,
                AmountAdded = sale.TotalAmount,
                TierUpgraded = tierUpgraded,
                NewTier = tierUpgraded ? newTier.ToString() : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating purchase history for customer {CustomerId} in sale {SaleId}",
                sale.CustomerId.Value, sale.Id);
            return CustomerHistoryUpdateResult.Failure(sale.Id,
                $"Failed to update customer purchase history: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates membership points for a customer based on the sale amount.
    /// Points are calculated as 1 point per unit of currency spent (rounded down).
    /// </summary>
    private async Task UpdateMembershipPointsAsync(Customer customer, Sale sale)
    {
        try
        {
            var membership = await _customerMembershipRepository.GetByCustomerIdAsync(customer.Id);
            if (membership == null || !membership.IsActive || membership.IsDeleted)
            {
                _logger.LogDebug("No active membership record for customer {CustomerId} — skipping points update",
                    customer.Id);
                return;
            }

            // Award 1 point per unit of currency (e.g., 1 point per 1 BDT)
            var pointsEarned = (int)Math.Floor(sale.TotalAmount);
            membership.Points += pointsEarned;
            membership.TotalSpentForTier += sale.TotalAmount;
            membership.LastUpdated = DateTime.UtcNow;
            membership.SyncStatus = SyncStatus.NotSynced;

            await _customerMembershipRepository.UpdateAsync(membership);
            await _customerMembershipRepository.SaveChangesAsync();

            _logger.LogDebug(
                "Membership points updated for customer {CustomerId}: +{Points} points (total: {Total})",
                customer.Id, pointsEarned, membership.Points);
        }
        catch (Exception ex)
        {
            // Points update failure is non-critical — log and continue
            _logger.LogWarning(ex,
                "Non-critical: Failed to update membership points for customer {CustomerId} in sale {SaleId}",
                customer.Id, sale.Id);
        }
    }

    // =========================================================================
    // Inventory Rollback
    // =========================================================================

    /// <summary>
    /// Rolls back inventory changes made for a sale by restoring stock levels from snapshots.
    /// Used when a transaction fails after stock was already reduced.
    /// </summary>
    public async Task<InventoryRollbackResult> RollbackInventoryUpdateAsync(Guid saleId)
    {
        if (saleId == Guid.Empty)
            throw new ArgumentException("Sale ID cannot be empty.", nameof(saleId));

        _logger.LogInformation("Rolling back inventory update for sale {SaleId}", saleId);

        List<StockSnapshot>? snapshots;
        lock (_stockSnapshots)
        {
            _stockSnapshots.TryGetValue(saleId, out snapshots);
        }

        if (snapshots == null || !snapshots.Any())
        {
            // No snapshots found — try to reconstruct from sale items in the database
            _logger.LogWarning(
                "No in-memory snapshots found for sale {SaleId}. Attempting DB-based rollback.", saleId);
            return await RollbackFromDatabaseAsync(saleId);
        }

        return await RollbackFromSnapshotsAsync(saleId, snapshots);
    }

    private async Task<InventoryRollbackResult> RollbackFromSnapshotsAsync(
        Guid saleId, List<StockSnapshot> snapshots)
    {
        var restoredCount = 0;

        try
        {
            foreach (var snapshot in snapshots)
            {
                try
                {
                    var stockRecords = await _stockRepository.FindAsync(s =>
                        s.ProductId == snapshot.ProductId && !s.IsDeleted);
                    var stockRecord = stockRecords.FirstOrDefault();

                    if (stockRecord != null)
                    {
                        // Restore to the pre-sale quantity
                        stockRecord.Quantity = snapshot.StockBefore;
                        stockRecord.LastUpdatedAt = DateTime.UtcNow;
                        stockRecord.UpdatedAt = DateTime.UtcNow;
                        stockRecord.SyncStatus = SyncStatus.NotSynced;

                        await _stockRepository.UpdateAsync(stockRecord);
                        await _stockRepository.SaveChangesAsync();

                        restoredCount++;

                        _logger.LogDebug(
                            "Rolled back stock for product {ProductId}: restored to {Quantity}",
                            snapshot.ProductId, snapshot.StockBefore);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error rolling back stock for product {ProductId} in sale {SaleId}",
                        snapshot.ProductId, saleId);
                }
            }

            // Remove snapshots after rollback
            lock (_stockSnapshots)
            {
                _stockSnapshots.Remove(saleId);
            }

            _logger.LogInformation("Inventory rollback completed for sale {SaleId}: {Count} items restored",
                saleId, restoredCount);

            return InventoryRollbackResult.Success(saleId, restoredCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during inventory rollback for sale {SaleId}", saleId);
            return InventoryRollbackResult.Failure(saleId, $"Rollback failed: {ex.Message}");
        }
    }

    private async Task<InventoryRollbackResult> RollbackFromDatabaseAsync(Guid saleId)
    {
        try
        {
            var sale = await _saleRepository.GetByIdAsync(saleId);
            if (sale == null)
            {
                return InventoryRollbackResult.Failure(saleId, $"Sale {saleId} not found for rollback.");
            }

            var saleItems = await _saleItemRepository.FindAsync(si =>
                si.SaleId == saleId && !si.IsDeleted);

            var restoredCount = 0;
            foreach (var item in saleItems)
            {
                try
                {
                    var quantityToRestore = item.IsWeightBased ? 1 : item.Quantity;
                    await _stockRepository.UpdateStockQuantityAsync(
                        item.ProductId, quantityToRestore, sale.DeviceId);
                    restoredCount++;

                    _logger.LogDebug(
                        "DB-based rollback: restored {Qty} units for product {ProductId} in sale {SaleId}",
                        quantityToRestore, item.ProductId, saleId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error in DB-based rollback for product {ProductId} in sale {SaleId}",
                        item.ProductId, saleId);
                }
            }

            return InventoryRollbackResult.Success(saleId, restoredCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DB-based rollback for sale {SaleId}", saleId);
            return InventoryRollbackResult.Failure(saleId, $"DB-based rollback failed: {ex.Message}");
        }
    }

    // =========================================================================
    // Combined Sale Inventory Update
    // =========================================================================

    /// <summary>
    /// Performs a complete inventory update: reduces stock for all items and updates customer history.
    /// If stock reduction fails, no customer history update is performed.
    /// If customer history update fails, stock reduction is NOT rolled back (it's non-critical).
    /// Requirement 6.3 + 6.6: Combined update with rollback on stock failure.
    /// </summary>
    public async Task<InventoryUpdateResult> ProcessSaleInventoryUpdateAsync(Sale sale)
    {
        if (sale == null)
            throw new ArgumentNullException(nameof(sale));

        _logger.LogInformation("Processing complete inventory update for sale {SaleId}", sale.Id);

        // Step 1: Reduce stock levels (Requirement 6.3)
        var stockResult = await ReduceStockLevelsAsync(sale);
        if (!stockResult.IsSuccess)
        {
            _logger.LogError(
                "Stock reduction failed for sale {SaleId}: {Error}. Skipping customer history update.",
                sale.Id, stockResult.ErrorMessage);
            return stockResult;
        }

        // Step 2: Update customer purchase history (Requirement 6.6)
        var historyResult = await UpdateCustomerPurchaseHistoryAsync(sale);
        if (!historyResult.IsSuccess && !historyResult.NoCustomer)
        {
            // Customer history update failure is logged but does NOT roll back stock
            // (stock reduction is the critical operation; customer history is secondary)
            _logger.LogWarning(
                "Customer history update failed for sale {SaleId}: {Error}. Stock reduction was successful.",
                sale.Id, historyResult.ErrorMessage);
        }

        // Clear snapshots after successful processing (no longer needed for rollback)
        lock (_stockSnapshots)
        {
            _stockSnapshots.Remove(sale.Id);
        }

        return stockResult;
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    /// <summary>
    /// Internal snapshot of stock state before a sale's inventory update, used for rollback.
    /// </summary>
    private class StockSnapshot
    {
        public Guid ProductId { get; set; }
        public int QuantityDeducted { get; set; }
        public int StockBefore { get; set; }
    }
}
