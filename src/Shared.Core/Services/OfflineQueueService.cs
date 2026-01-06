using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using System.Diagnostics;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of offline queue service for managing operations when connectivity is unavailable
/// </summary>
public class OfflineQueueService : IOfflineQueueService, IDisposable
{
    private readonly PosDbContext _context;
    private readonly IConnectivityService _connectivityService;
    private readonly ISyncEngine _syncEngine;
    private readonly ILogger<OfflineQueueService> _logger;
    private readonly IComprehensiveLoggingService _loggingService;
    
    private Timer? _queueMonitorTimer;
    private bool _isMonitoring = false;
    private bool _disposed = false;

    public OfflineQueueService(
        PosDbContext context,
        IConnectivityService connectivityService,
        ISyncEngine syncEngine,
        ILogger<OfflineQueueService> logger,
        IComprehensiveLoggingService loggingService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
        _syncEngine = syncEngine ?? throw new ArgumentNullException(nameof(syncEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        
        // Subscribe to connectivity changes
        _connectivityService.ConnectivityChanged += OnConnectivityChanged;
    }

    public async Task<bool> QueueOperationAsync(OfflineOperation operation)
    {
        try
        {
            // Check if we're online and can process immediately
            if (await _connectivityService.IsConnectedAsync())
            {
                _logger.LogDebug("Device is online, attempting immediate processing of operation {OperationId}", operation.Id);
                
                var immediateResult = await ProcessSingleOperationAsync(operation);
                if (immediateResult)
                {
                    _logger.LogInformation("Operation {OperationId} processed immediately", operation.Id);
                    return true;
                }
                
                _logger.LogWarning("Immediate processing failed for operation {OperationId}, queuing for later", operation.Id);
            }
            
            // Store in local database queue
            await StoreOperationInQueueAsync(operation);
            
            _logger.LogInformation("Queued offline operation {OperationId} of type {OperationType}", 
                                 operation.Id, operation.OperationType);
            
            await _loggingService.LogInfoAsync(
                $"Queued offline operation {operation.Id} of type {operation.OperationType}",
                LogCategory.Sync,
                operation.DeviceId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing operation {OperationId}", operation.Id);
            
            await _loggingService.LogErrorAsync(
                $"Error queuing operation {operation.Id}: {ex.Message}",
                LogCategory.Sync,
                operation.DeviceId,
                ex);
            
            return false;
        }
    }

    public async Task<bool> QueueSaleAsync(Sale sale, OperationPriority priority = OperationPriority.Normal)
    {
        try
        {
            var operation = new OfflineOperation
            {
                OperationType = "SaleSync",
                EntityType = "Sale",
                EntityId = sale.Id,
                SerializedData = JsonSerializer.Serialize(sale),
                Priority = priority,
                UserId = sale.UserId,
                DeviceId = Guid.NewGuid(), // In real implementation, get from context
                ShopId = sale.ShopId
            };
            
            return await QueueOperationAsync(operation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing sale {SaleId}", sale.Id);
            return false;
        }
    }

    public async Task<bool> QueueProductUpdateAsync(Product product, OperationType operationType)
    {
        try
        {
            var operation = new OfflineOperation
            {
                OperationType = operationType.ToString(),
                EntityType = "Product",
                EntityId = product.Id,
                SerializedData = JsonSerializer.Serialize(product),
                Priority = OperationPriority.Normal,
                UserId = Guid.NewGuid(), // In real implementation, get from context
                DeviceId = Guid.NewGuid(), // In real implementation, get from context
                ShopId = product.ShopId
            };
            
            return await QueueOperationAsync(operation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing product update {ProductId}", product.Id);
            return false;
        }
    }

    public async Task<bool> QueueStockUpdateAsync(Stock stock, OperationType operationType)
    {
        try
        {
            var operation = new OfflineOperation
            {
                OperationType = operationType.ToString(),
                EntityType = "Stock",
                EntityId = stock.Id,
                SerializedData = JsonSerializer.Serialize(stock),
                Priority = OperationPriority.High, // Stock updates are important
                UserId = Guid.NewGuid(), // In real implementation, get from context
                DeviceId = Guid.NewGuid(), // In real implementation, get from context
                ShopId = stock.ShopId
            };
            
            return await QueueOperationAsync(operation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing stock update {StockId}", stock.Id);
            return false;
        }
    }

    public async Task<List<OfflineOperation>> GetQueuedOperationsAsync(OperationPriority? priority = null)
    {
        try
        {
            var operations = await GetStoredOperationsAsync(priority);
            
            _logger.LogDebug("Retrieved {Count} queued operations", operations.Count);
            return operations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving queued operations");
            return new List<OfflineOperation>();
        }
    }

    public async Task<QueueProcessingResult> ProcessQueuedOperationsAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new QueueProcessingResult();
        
        try
        {
            _logger.LogInformation("Starting processing of queued operations");
            
            // Check connectivity first
            if (!await _connectivityService.IsConnectedAsync())
            {
                _logger.LogWarning("Device is offline, cannot process queued operations");
                return result;
            }
            
            var queuedOperations = await GetQueuedOperationsAsync();
            result.TotalOperations = queuedOperations.Count;
            
            if (queuedOperations.Count == 0)
            {
                _logger.LogDebug("No queued operations to process");
                return result;
            }
            
            // Process operations by priority (Critical first, then High, Normal, Low)
            var prioritizedOperations = queuedOperations
                .OrderByDescending(op => op.Priority)
                .ThenBy(op => op.CreatedAt)
                .ToList();
            
            foreach (var operation in prioritizedOperations)
            {
                try
                {
                    var success = await ProcessSingleOperationAsync(operation);
                    
                    if (success)
                    {
                        result.SuccessfulOperations++;
                        await RemoveProcessedOperationAsync(operation.Id);
                        
                        _logger.LogDebug("Successfully processed operation {OperationId}", operation.Id);
                    }
                    else
                    {
                        result.FailedOperations++;
                        await IncrementRetryCountAsync(operation.Id);
                        
                        _logger.LogWarning("Failed to process operation {OperationId}", operation.Id);
                    }
                }
                catch (Exception ex)
                {
                    result.FailedOperations++;
                    result.Errors.Add($"Operation {operation.Id}: {ex.Message}");
                    
                    await IncrementRetryCountAsync(operation.Id, ex.Message);
                    
                    _logger.LogError(ex, "Error processing operation {OperationId}", operation.Id);
                }
            }
            
            _logger.LogInformation("Completed processing queued operations: {Successful} successful, {Failed} failed", 
                                 result.SuccessfulOperations, result.FailedOperations);
            
            await _loggingService.LogInfoAsync(
                $"Processed {result.TotalOperations} queued operations: {result.SuccessfulOperations} successful, {result.FailedOperations} failed",
                LogCategory.Sync,
                Guid.NewGuid());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing queued operations");
            result.Errors.Add($"Processing error: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingDuration = stopwatch.Elapsed;
        }
        
        return result;
    }

    public async Task<bool> RemoveProcessedOperationAsync(Guid operationId)
    {
        try
        {
            await RemoveOperationFromQueueAsync(operationId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing processed operation {OperationId}", operationId);
            return false;
        }
    }

    public async Task<int> ClearQueueAsync()
    {
        try
        {
            var count = await ClearStoredOperationsAsync();
            
            _logger.LogWarning("Cleared {Count} operations from offline queue", count);
            
            await _loggingService.LogWarningAsync(
                $"Cleared {count} operations from offline queue",
                LogCategory.Sync,
                Guid.NewGuid());
            
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing offline queue");
            return 0;
        }
    }

    public async Task<QueueStatistics> GetQueueStatisticsAsync()
    {
        try
        {
            var operations = await GetStoredOperationsAsync();
            
            var statistics = new QueueStatistics
            {
                TotalQueuedOperations = operations.Count,
                PendingOperations = operations.Count(op => !op.IsProcessed),
                ProcessedOperations = operations.Count(op => op.IsProcessed),
                FailedOperations = operations.Count(op => op.RetryCount >= op.MaxRetries)
            };
            
            // Group by priority
            foreach (var priority in Enum.GetValues<OperationPriority>())
            {
                statistics.OperationsByPriority[priority] = operations.Count(op => op.Priority == priority);
            }
            
            // Group by type
            foreach (var group in operations.GroupBy(op => op.OperationType))
            {
                statistics.OperationsByType[group.Key] = group.Count();
            }
            
            // Date ranges
            if (operations.Any())
            {
                statistics.OldestOperationDate = operations.Min(op => op.CreatedAt);
                statistics.NewestOperationDate = operations.Max(op => op.CreatedAt);
                statistics.QueueSizeBytes = operations.Sum(op => op.SerializedData.Length * sizeof(char));
            }
            
            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue statistics");
            return new QueueStatistics();
        }
    }

    public async Task<bool> StartQueueMonitoringAsync()
    {
        try
        {
            if (_isMonitoring)
            {
                return true;
            }
            
            _queueMonitorTimer = new Timer(async _ => await MonitorQueueCallback(), 
                                         null, 
                                         TimeSpan.FromMinutes(1), // Initial delay
                                         TimeSpan.FromMinutes(5)); // Check every 5 minutes
            
            _isMonitoring = true;
            
            _logger.LogInformation("Started offline queue monitoring");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting queue monitoring");
            return false;
        }
    }

    public async Task<bool> StopQueueMonitoringAsync()
    {
        try
        {
            if (!_isMonitoring)
            {
                return true;
            }
            
            _queueMonitorTimer?.Dispose();
            _queueMonitorTimer = null;
            _isMonitoring = false;
            
            _logger.LogInformation("Stopped offline queue monitoring");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping queue monitoring");
            return false;
        }
    }

    private async Task<bool> ProcessSingleOperationAsync(OfflineOperation operation)
    {
        try
        {
            // Process based on operation type
            switch (operation.OperationType.ToLower())
            {
                case "salesync":
                    return await ProcessSaleSyncAsync(operation);
                case "create":
                case "update":
                case "delete":
                    return await ProcessEntityOperationAsync(operation);
                default:
                    _logger.LogWarning("Unknown operation type: {OperationType}", operation.OperationType);
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing single operation {OperationId}", operation.Id);
            return false;
        }
    }

    private async Task<bool> ProcessSaleSyncAsync(OfflineOperation operation)
    {
        try
        {
            // Use sync engine to sync the sale
            var syncResult = await _syncEngine.SyncSalesAsync();
            return syncResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing sale for operation {OperationId}", operation.Id);
            return false;
        }
    }

    private async Task<bool> ProcessEntityOperationAsync(OfflineOperation operation)
    {
        try
        {
            // Process entity operations based on type
            switch (operation.EntityType.ToLower())
            {
                case "product":
                    return await ProcessProductOperationAsync(operation);
                case "stock":
                    return await ProcessStockOperationAsync(operation);
                default:
                    _logger.LogWarning("Unknown entity type: {EntityType}", operation.EntityType);
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing entity operation {OperationId}", operation.Id);
            return false;
        }
    }

    private async Task<bool> ProcessProductOperationAsync(OfflineOperation operation)
    {
        try
        {
            // Use sync engine to sync products
            var syncResult = await _syncEngine.SyncProductsAsync();
            return syncResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing product for operation {OperationId}", operation.Id);
            return false;
        }
    }

    private async Task<bool> ProcessStockOperationAsync(OfflineOperation operation)
    {
        try
        {
            // Use sync engine to sync stock
            var syncResult = await _syncEngine.SyncStockAsync();
            return syncResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing stock for operation {OperationId}", operation.Id);
            return false;
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.IsConnected)
        {
            _logger.LogInformation("Connectivity restored, processing queued operations");
            
            // Process queued operations when connectivity is restored
            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessQueuedOperationsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing queued operations after connectivity restored");
                }
            });
        }
        else
        {
            _logger.LogInformation("Connectivity lost, operations will be queued");
        }
    }

    private async Task MonitorQueueCallback()
    {
        try
        {
            if (await _connectivityService.IsConnectedAsync())
            {
                var statistics = await GetQueueStatisticsAsync();
                
                if (statistics.PendingOperations > 0)
                {
                    _logger.LogInformation("Queue monitor: {Pending} pending operations, processing...", 
                                         statistics.PendingOperations);
                    
                    await ProcessQueuedOperationsAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in queue monitor callback");
        }
    }

    // Database storage methods (simplified - in real implementation would use proper entity)
    private async Task StoreOperationInQueueAsync(OfflineOperation operation)
    {
        // For now, store in SaleSession.SessionData as a workaround
        // In a real implementation, you'd create a dedicated OfflineQueue entity
        var queueEntry = new SaleSession
        {
            Id = operation.Id,
            TabName = $"OfflineQueue_{operation.OperationType}",
            ShopId = operation.ShopId,
            UserId = operation.UserId,
            DeviceId = operation.DeviceId,
            SessionData = JsonSerializer.Serialize(operation),
            State = SessionState.Suspended, // Use Suspended to indicate queued
            CreatedAt = operation.CreatedAt,
            LastModified = DateTime.UtcNow,
            IsActive = false
        };
        
        _context.SaleSessions.Add(queueEntry);
        await _context.SaveChangesAsync();
    }

    private async Task<List<OfflineOperation>> GetStoredOperationsAsync(OperationPriority? priority = null)
    {
        var queueEntries = await _context.SaleSessions
            .Where(ss => ss.TabName.StartsWith("OfflineQueue_") && 
                        ss.State == SessionState.Suspended &&
                        !ss.IsDeleted)
            .ToListAsync();
        
        var operations = new List<OfflineOperation>();
        
        foreach (var entry in queueEntries)
        {
            try
            {
                if (entry.SessionData != null)
                {
                    var operation = JsonSerializer.Deserialize<OfflineOperation>(entry.SessionData);
                    if (operation != null && (!priority.HasValue || operation.Priority == priority.Value))
                    {
                        operations.Add(operation);
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize offline operation {EntryId}", entry.Id);
            }
        }
        
        return operations;
    }

    private async Task RemoveOperationFromQueueAsync(Guid operationId)
    {
        var queueEntry = await _context.SaleSessions
            .FirstOrDefaultAsync(ss => ss.Id == operationId && 
                                     ss.TabName.StartsWith("OfflineQueue_"));
        
        if (queueEntry != null)
        {
            _context.SaleSessions.Remove(queueEntry);
            await _context.SaveChangesAsync();
        }
    }

    private async Task<int> ClearStoredOperationsAsync()
    {
        var queueEntries = await _context.SaleSessions
            .Where(ss => ss.TabName.StartsWith("OfflineQueue_"))
            .ToListAsync();
        
        _context.SaleSessions.RemoveRange(queueEntries);
        await _context.SaveChangesAsync();
        
        return queueEntries.Count;
    }

    private async Task IncrementRetryCountAsync(Guid operationId, string? errorMessage = null)
    {
        var queueEntry = await _context.SaleSessions
            .FirstOrDefaultAsync(ss => ss.Id == operationId && 
                                     ss.TabName.StartsWith("OfflineQueue_"));
        
        if (queueEntry?.SessionData != null)
        {
            try
            {
                var operation = JsonSerializer.Deserialize<OfflineOperation>(queueEntry.SessionData);
                if (operation != null)
                {
                    operation.RetryCount++;
                    operation.ErrorMessage = errorMessage;
                    
                    queueEntry.SessionData = JsonSerializer.Serialize(operation);
                    queueEntry.LastModified = DateTime.UtcNow;
                    
                    await _context.SaveChangesAsync();
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to update retry count for operation {OperationId}", operationId);
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _queueMonitorTimer?.Dispose();
            _connectivityService.ConnectivityChanged -= OnConnectivityChanged;
            _disposed = true;
        }
    }
}