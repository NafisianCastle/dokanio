using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using System.Collections.Concurrent;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of the synchronization engine for offline-first data synchronization
/// Handles automatic background sync with connectivity detection, retry logic, and conflict resolution
/// </summary>
public class SyncEngine : ISyncEngine, IDisposable
{
    private readonly PosDbContext _context;
    private readonly ISyncApiClient _apiClient;
    private readonly IConnectivityService _connectivityService;
    private readonly ILogger<SyncEngine> _logger;
    private readonly SyncConfiguration _configuration;
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockRepository _stockRepository;
    
    private readonly Timer _syncTimer;
    private readonly Timer _connectivityTimer;
    private readonly ConcurrentDictionary<string, DateTime> _lastSyncTimes = new();
    private readonly ConcurrentDictionary<string, int> _retryAttempts = new();
    private bool _isDisposed = false;
    private bool _isSyncing = false;

    public event EventHandler<SyncProgressEventArgs>? SyncProgress;
    public event EventHandler<ConnectivityChangedEventArgs>? ConnectivityChanged;

    public SyncEngine(
        PosDbContext context,
        ISyncApiClient apiClient,
        IConnectivityService connectivityService,
        ILogger<SyncEngine> logger,
        SyncConfiguration configuration,
        ISaleRepository saleRepository,
        IProductRepository productRepository,
        IStockRepository stockRepository)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _stockRepository = stockRepository ?? throw new ArgumentNullException(nameof(stockRepository));

        // Initialize timers (but don't start them yet)
        _syncTimer = new Timer(OnSyncTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
        _connectivityTimer = new Timer(OnConnectivityTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);

        // Subscribe to connectivity changes
        _connectivityService.ConnectivityChanged += OnConnectivityChanged;
    }

    /// <summary>
    /// Synchronizes all data types with exponential backoff retry logic
    /// </summary>
    public async Task<SyncResult> SyncAllAsync()
    {
        if (_isSyncing)
        {
            _logger.LogDebug("Sync already in progress, skipping");
            return new SyncResult { IsSuccess = false, ErrorMessage = "Sync already in progress" };
        }

        _isSyncing = true;
        var overallResult = new SyncResult { IsSuccess = true };
        var totalItemsSynced = 0;

        try
        {
            _logger.LogInformation("Starting full synchronization");

            // Check connectivity first
            if (!_connectivityService.IsConnected)
            {
                _logger.LogWarning("No connectivity available, skipping sync");
                return new SyncResult { IsSuccess = false, ErrorMessage = "No network connectivity" };
            }

            // Sync in order: Sales (upload first), then Products and Stock (download)
            var salesResult = await SyncSalesAsync();
            var productsResult = await SyncProductsAsync();
            var stockResult = await SyncStockAsync();

            // Combine results
            totalItemsSynced = salesResult.ItemsSynced + productsResult.ItemsSynced + stockResult.ItemsSynced;
            
            overallResult.IsSuccess = salesResult.IsSuccess && productsResult.IsSuccess && stockResult.IsSuccess;
            overallResult.ItemsSynced = totalItemsSynced;
            overallResult.SyncTime = DateTime.UtcNow;

            if (!overallResult.IsSuccess)
            {
                var errors = new List<string>();
                if (!string.IsNullOrEmpty(salesResult.ErrorMessage)) errors.Add($"Sales: {salesResult.ErrorMessage}");
                if (!string.IsNullOrEmpty(productsResult.ErrorMessage)) errors.Add($"Products: {productsResult.ErrorMessage}");
                if (!string.IsNullOrEmpty(stockResult.ErrorMessage)) errors.Add($"Stock: {stockResult.ErrorMessage}");
                overallResult.ErrorMessage = string.Join("; ", errors);
            }

            if (overallResult.IsSuccess)
            {
                // Reset retry attempts on successful sync
                _retryAttempts.Clear();
                _lastSyncTimes["all"] = DateTime.UtcNow;
            }

            _logger.LogInformation("Full synchronization completed. Success: {Success}, Items synced: {ItemsSynced}",
                overallResult.IsSuccess, overallResult.ItemsSynced);

            return overallResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during full synchronization");
            return new SyncResult { IsSuccess = false, ErrorMessage = ex.Message, SyncTime = DateTime.UtcNow };
        }
        finally
        {
            _isSyncing = false;
        }
    }

    /// <summary>
    /// Synchronizes sales data with the server (upload local sales)
    /// </summary>
    public async Task<SyncResult> SyncSalesAsync()
    {
        var result = new SyncResult { SyncTime = DateTime.UtcNow };
        
        try
        {
            _logger.LogDebug("Starting sales synchronization");
            OnSyncProgress("Syncing sales", 0, 0);

            // Get unsynced sales from Local_Storage
            var unsyncedSales = await _context.Sales
                .Include(s => s.Items)
                .Where(s => s.SyncStatus == SyncStatus.NotSynced || s.SyncStatus == SyncStatus.SyncFailed)
                .ToListAsync();

            if (!unsyncedSales.Any())
            {
                _logger.LogDebug("No unsynced sales found");
                result.IsSuccess = true;
                result.ItemsSynced = 0;
                return result;
            }

            _logger.LogDebug("Found {Count} unsynced sales", unsyncedSales.Count);
            OnSyncProgress("Syncing sales", unsyncedSales.Count, 0);

            // Convert to DTOs
            var saleDtos = unsyncedSales.Select(sale => new SaleDto
            {
                Id = sale.Id,
                InvoiceNumber = sale.InvoiceNumber,
                TotalAmount = sale.TotalAmount,
                PaymentMethod = sale.PaymentMethod,
                CreatedAt = sale.CreatedAt,
                DeviceId = sale.DeviceId,
                Items = sale.Items.Select(item => new SaleItemDto
                {
                    Id = item.Id,
                    SaleId = item.SaleId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    BatchNumber = item.BatchNumber
                }).ToList()
            }).ToList();

            // Create upload request
            var uploadRequest = new SyncUploadRequest
            {
                DeviceId = _configuration.DeviceId,
                LastSyncTimestamp = _lastSyncTimes.GetValueOrDefault("sales", DateTime.MinValue),
                Sales = saleDtos
            };

            // Upload with retry logic
            var uploadResult = await ExecuteWithRetryAsync(
                () => _apiClient.UploadChangesAsync(uploadRequest),
                "sales_upload"
            );

            if (uploadResult.Success)
            {
                // Mark sales as synced
                foreach (var sale in unsyncedSales)
                {
                    sale.SyncStatus = SyncStatus.Synced;
                    sale.ServerSyncedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                result.IsSuccess = true;
                result.ItemsSynced = unsyncedSales.Count;
                _lastSyncTimes["sales"] = DateTime.UtcNow;

                _logger.LogInformation("Successfully synced {Count} sales", unsyncedSales.Count);
            }
            else
            {
                // Mark sales as sync failed
                foreach (var sale in unsyncedSales)
                {
                    sale.SyncStatus = SyncStatus.SyncFailed;
                }

                await _context.SaveChangesAsync();

                result.IsSuccess = false;
                result.ErrorMessage = uploadResult.Message;
                result.ItemsSynced = 0;

                _logger.LogWarning("Failed to sync sales: {Message}", uploadResult.Message);
            }

            OnSyncProgress("Syncing sales", unsyncedSales.Count, unsyncedSales.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sales synchronization");
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            result.ItemsSynced = 0;
            return result;
        }
    }

    /// <summary>
    /// Synchronizes product data with the server (download server products)
    /// </summary>
    public async Task<SyncResult> SyncProductsAsync()
    {
        var result = new SyncResult { SyncTime = DateTime.UtcNow };
        
        try
        {
            _logger.LogDebug("Starting products synchronization");
            OnSyncProgress("Syncing products", 0, 0);

            var lastSyncTime = _lastSyncTimes.GetValueOrDefault("products", DateTime.MinValue);

            // Download changes from server with retry logic
            var downloadResult = await ExecuteWithRetryAsync(
                () => _apiClient.DownloadChangesAsync(_configuration.DeviceId, lastSyncTime),
                "products_download"
            );

            if (!downloadResult.Success || downloadResult.Data == null)
            {
                result.IsSuccess = false;
                result.ErrorMessage = downloadResult.Message;
                result.ItemsSynced = 0;
                return result;
            }

            var serverData = downloadResult.Data;
            var serverProducts = serverData.Products;

            if (!serverProducts.Any())
            {
                _logger.LogDebug("No product updates from server");
                result.IsSuccess = true;
                result.ItemsSynced = 0;
                return result;
            }

            _logger.LogDebug("Received {Count} product updates from server", serverProducts.Count);
            OnSyncProgress("Syncing products", serverProducts.Count, 0);

            int processedCount = 0;

            foreach (var productDto in serverProducts)
            {
                try
                {
                    var existingProduct = await _context.Products
                        .FirstOrDefaultAsync(p => p.Id == productDto.Id);

                    if (existingProduct != null)
                    {
                        // Conflict resolution: Server wins for product prices and details
                        _logger.LogDebug("Resolving conflict for product {ProductId}: server wins", productDto.Id);
                        
                        existingProduct.Name = productDto.Name;
                        existingProduct.Barcode = productDto.Barcode;
                        existingProduct.Category = productDto.Category;
                        existingProduct.UnitPrice = productDto.UnitPrice; // Server wins for prices
                        existingProduct.IsActive = productDto.IsActive;
                        existingProduct.UpdatedAt = productDto.UpdatedAt;
                        existingProduct.BatchNumber = productDto.BatchNumber;
                        existingProduct.ExpiryDate = productDto.ExpiryDate;
                        existingProduct.PurchasePrice = productDto.PurchasePrice;
                        existingProduct.SellingPrice = productDto.SellingPrice;
                        existingProduct.SyncStatus = SyncStatus.Synced;
                        existingProduct.ServerSyncedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        // New product from server
                        var newProduct = new Product
                        {
                            Id = productDto.Id,
                            Name = productDto.Name,
                            Barcode = productDto.Barcode,
                            Category = productDto.Category,
                            UnitPrice = productDto.UnitPrice,
                            IsActive = productDto.IsActive,
                            CreatedAt = productDto.CreatedAt,
                            UpdatedAt = productDto.UpdatedAt,
                            DeviceId = productDto.DeviceId,
                            BatchNumber = productDto.BatchNumber,
                            ExpiryDate = productDto.ExpiryDate,
                            PurchasePrice = productDto.PurchasePrice,
                            SellingPrice = productDto.SellingPrice,
                            SyncStatus = SyncStatus.Synced,
                            ServerSyncedAt = DateTime.UtcNow
                        };

                        _context.Products.Add(newProduct);
                    }

                    processedCount++;
                    OnSyncProgress("Syncing products", serverProducts.Count, processedCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing product {ProductId}", productDto.Id);
                }
            }

            await _context.SaveChangesAsync();

            result.IsSuccess = true;
            result.ItemsSynced = processedCount;
            _lastSyncTimes["products"] = serverData.ServerTimestamp;

            _logger.LogInformation("Successfully synced {Count} products", processedCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during products synchronization");
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            result.ItemsSynced = 0;
            return result;
        }
    }

    /// <summary>
    /// Synchronizes stock data with the server (download server stock updates)
    /// </summary>
    public async Task<SyncResult> SyncStockAsync()
    {
        var result = new SyncResult { SyncTime = DateTime.UtcNow };
        
        try
        {
            _logger.LogDebug("Starting stock synchronization");
            OnSyncProgress("Syncing stock", 0, 0);

            var lastSyncTime = _lastSyncTimes.GetValueOrDefault("stock", DateTime.MinValue);

            // Download changes from server with retry logic
            var downloadResult = await ExecuteWithRetryAsync(
                () => _apiClient.DownloadChangesAsync(_configuration.DeviceId, lastSyncTime),
                "stock_download"
            );

            if (!downloadResult.Success || downloadResult.Data == null)
            {
                result.IsSuccess = false;
                result.ErrorMessage = downloadResult.Message;
                result.ItemsSynced = 0;
                return result;
            }

            var serverData = downloadResult.Data;
            var serverStock = serverData.Stock;

            if (!serverStock.Any())
            {
                _logger.LogDebug("No stock updates from server");
                result.IsSuccess = true;
                result.ItemsSynced = 0;
                return result;
            }

            _logger.LogDebug("Received {Count} stock updates from server", serverStock.Count);
            OnSyncProgress("Syncing stock", serverStock.Count, 0);

            int processedCount = 0;

            foreach (var stockDto in serverStock)
            {
                try
                {
                    var existingStock = await _context.Stock
                        .FirstOrDefaultAsync(s => s.ProductId == stockDto.ProductId);

                    if (existingStock != null)
                    {
                        // Conflict resolution: Recalculate from transactions (server authoritative)
                        _logger.LogDebug("Resolving stock conflict for product {ProductId}: using server data", stockDto.ProductId);
                        
                        existingStock.Quantity = stockDto.Quantity;
                        existingStock.LastUpdatedAt = stockDto.LastUpdatedAt;
                        existingStock.SyncStatus = SyncStatus.Synced;
                        existingStock.ServerSyncedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        // New stock record from server
                        var newStock = new Stock
                        {
                            Id = stockDto.Id,
                            ProductId = stockDto.ProductId,
                            Quantity = stockDto.Quantity,
                            LastUpdatedAt = stockDto.LastUpdatedAt,
                            SyncStatus = SyncStatus.Synced,
                            ServerSyncedAt = DateTime.UtcNow
                        };

                        _context.Stock.Add(newStock);
                    }

                    processedCount++;
                    OnSyncProgress("Syncing stock", serverStock.Count, processedCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing stock for product {ProductId}", stockDto.ProductId);
                }
            }

            await _context.SaveChangesAsync();

            result.IsSuccess = true;
            result.ItemsSynced = processedCount;
            _lastSyncTimes["stock"] = serverData.ServerTimestamp;

            _logger.LogInformation("Successfully synced {Count} stock records", processedCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during stock synchronization");
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            result.ItemsSynced = 0;
            return result;
        }
    }

    /// <summary>
    /// Starts background sync with connectivity monitoring
    /// </summary>
    public async Task StartBackgroundSyncAsync()
    {
        _logger.LogInformation("Starting background sync service");

        // Start connectivity monitoring
        await _connectivityService.StartMonitoringAsync();

        // Start periodic sync timer
        _syncTimer.Change(_configuration.SyncInterval, _configuration.SyncInterval);

        // Start connectivity check timer
        _connectivityTimer.Change(_configuration.ConnectivityCheckInterval, _configuration.ConnectivityCheckInterval);

        _logger.LogInformation("Background sync service started with {SyncInterval} sync interval", _configuration.SyncInterval);
    }

    /// <summary>
    /// Stops background sync service
    /// </summary>
    public async Task StopBackgroundSyncAsync()
    {
        _logger.LogInformation("Stopping background sync service");

        // Stop timers
        _syncTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _connectivityTimer.Change(Timeout.Infinite, Timeout.Infinite);

        // Stop connectivity monitoring
        await _connectivityService.StopMonitoringAsync();

        _logger.LogInformation("Background sync service stopped");
    }

    /// <summary>
    /// Executes an operation with exponential backoff retry logic
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationKey) where T : SyncApiResult, new()
    {
        var attempt = 0;
        var delay = _configuration.InitialRetryDelay;

        while (attempt < _configuration.MaxRetryAttempts)
        {
            try
            {
                var result = await operation();
                
                if (result.Success)
                {
                    // Reset retry count on success
                    _retryAttempts.TryRemove(operationKey, out _);
                    return result;
                }

                // If not the last attempt, wait and retry
                if (attempt < _configuration.MaxRetryAttempts - 1)
                {
                    _logger.LogWarning("Operation {Operation} failed (attempt {Attempt}/{MaxAttempts}): {Message}. Retrying in {Delay}ms",
                        operationKey, attempt + 1, _configuration.MaxRetryAttempts, result.Message, delay.TotalMilliseconds);

                    await Task.Delay(delay);
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * _configuration.RetryBackoffMultiplier);
                    attempt++;
                }
                else
                {
                    _logger.LogError("Operation {Operation} failed after {MaxAttempts} attempts: {Message}",
                        operationKey, _configuration.MaxRetryAttempts, result.Message);
                    
                    _retryAttempts[operationKey] = _configuration.MaxRetryAttempts;
                    return result;
                }
            }
            catch (Exception ex)
            {
                if (attempt < _configuration.MaxRetryAttempts - 1)
                {
                    _logger.LogWarning(ex, "Operation {Operation} threw exception (attempt {Attempt}/{MaxAttempts}). Retrying in {Delay}ms",
                        operationKey, attempt + 1, _configuration.MaxRetryAttempts, delay.TotalMilliseconds);

                    await Task.Delay(delay);
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * _configuration.RetryBackoffMultiplier);
                    attempt++;
                }
                else
                {
                    _logger.LogError(ex, "Operation {Operation} threw exception after {MaxAttempts} attempts",
                        operationKey, _configuration.MaxRetryAttempts);
                    
                    _retryAttempts[operationKey] = _configuration.MaxRetryAttempts;
                    return new T { Success = false, Message = ex.Message, Errors = { ex.ToString() } };
                }
            }
        }

        return new T { Success = false, Message = "Max retry attempts exceeded" };
    }

    private async void OnSyncTimerElapsed(object? state)
    {
        if (_connectivityService.IsConnected && !_isSyncing)
        {
            _logger.LogDebug("Periodic sync triggered");
            try
            {
                await SyncAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic sync");
            }
        }
    }

    private async void OnConnectivityTimerElapsed(object? state)
    {
        try
        {
            var isReachable = await _connectivityService.IsServerReachableAsync(_configuration.ServerBaseUrl);
            if (isReachable && !_isSyncing)
            {
                _logger.LogDebug("Server reachable, triggering sync");
                await SyncAllAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during connectivity check");
        }
    }

    private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        _logger.LogInformation("Connectivity changed: {IsConnected}", e.IsConnected);
        
        // Forward the event
        ConnectivityChanged?.Invoke(this, e);

        // Trigger sync when connectivity is restored
        if (e.IsConnected && !_isSyncing)
        {
            _logger.LogInformation("Connectivity restored, triggering sync");
            try
            {
                await SyncAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connectivity-triggered sync");
            }
        }
    }

    private void OnSyncProgress(string operation, int total, int processed)
    {
        SyncProgress?.Invoke(this, new SyncProgressEventArgs
        {
            Message = operation,
            Progress = total > 0 ? (int)((double)processed / total * 100) : 0,
            IsCompleted = processed >= total
        });
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _syncTimer?.Dispose();
            _connectivityTimer?.Dispose();
            
            if (_connectivityService != null)
            {
                _connectivityService.ConnectivityChanged -= OnConnectivityChanged;
            }

            _isDisposed = true;
        }
    }
}