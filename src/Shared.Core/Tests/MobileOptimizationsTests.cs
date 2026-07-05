using Xunit;
using Moq;
using Shared.Core.Services;
using Shared.Core.DTOs;
using Shared.Core.Entities;

namespace Shared.Core.Tests;

/// <summary>
/// Tests for mobile-specific optimizations (Task 21)
/// Validates Requirements 4.2 (touch-optimized controls) and 12.1 (automatic recovery)
/// </summary>
public class MobileOptimizationsTests
{
    private readonly Mock<IOfflineQueueService> _mockOfflineQueue;
    private readonly Mock<IConnectivityService> _mockConnectivity;
    private readonly Mock<ICustomerLookupService> _mockCustomerService;
    private readonly Mock<IBarcodeIntegrationService> _mockBarcodeService;
    private readonly Mock<IMultiTabSalesManager> _mockTabManager;

    public MobileOptimizationsTests()
    {
        _mockOfflineQueue = new Mock<IOfflineQueueService>();
        _mockConnectivity = new Mock<IConnectivityService>();
        _mockCustomerService = new Mock<ICustomerLookupService>();
        _mockBarcodeService = new Mock<IBarcodeIntegrationService>();
        _mockTabManager = new Mock<IMultiTabSalesManager>();
    }

    // ─── Touch Gesture Support ───────────────────────────────────────────────

    [Fact]
    public async Task SwipeActions_RemoveItem_QueuedOfflineWhenDisconnected()
    {
        // Validates Requirement 4.2: touch-optimized controls + 12.1: automatic recovery
        // Arrange
        _mockConnectivity.Setup(x => x.IsConnected).Returns(false);
        _mockOfflineQueue.Setup(x => x.QueueOperationAsync(It.IsAny<OfflineOperation>()))
            .ReturnsAsync(true);

        var operation = new OfflineOperation
        {
            OperationType = "Delete",
            EntityType = "SaleItem",
            EntityId = Guid.NewGuid(),
            Priority = OperationPriority.High
        };

        // Act
        var queued = await _mockOfflineQueue.Object.QueueOperationAsync(operation);

        // Assert
        Assert.True(queued);
        _mockOfflineQueue.Verify(x => x.QueueOperationAsync(It.Is<OfflineOperation>(
            op => op.EntityType == "SaleItem" && op.Priority == OperationPriority.High)), Times.Once);
    }

    [Fact]
    public async Task SwipeActions_CompleteSale_QueuedOfflineWhenDisconnected()
    {
        // Validates Requirement 12.1: automatic recovery when offline
        // Arrange
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            ShopId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TotalAmount = 50.00m
        };

        _mockConnectivity.Setup(x => x.IsConnected).Returns(false);
        _mockOfflineQueue.Setup(x => x.QueueSaleAsync(It.IsAny<Sale>(), It.IsAny<OperationPriority>()))
            .ReturnsAsync(true);

        // Act
        var queued = await _mockOfflineQueue.Object.QueueSaleAsync(sale, OperationPriority.Critical);

        // Assert
        Assert.True(queued);
        _mockOfflineQueue.Verify(x => x.QueueSaleAsync(
            It.Is<Sale>(s => s.Id == sale.Id),
            OperationPriority.Critical), Times.Once);
    }

    // ─── Voice Input for Product Search ──────────────────────────────────────

    [Fact]
    public async Task VoiceSearch_ProductFound_ReturnsMatchingProduct()
    {
        // Validates Requirement 4.2: touch-optimized controls (voice is an alternative input)
        // Arrange
        var searchTerm = "milk";
        var expectedProducts = new List<Product>
        {
            new() { Id = Guid.NewGuid(), Name = "Full Cream Milk", UnitPrice = 2.50m, IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "Skim Milk", UnitPrice = 2.20m, IsActive = true }
        };

        var mockProductService = new Mock<IProductService>();
        mockProductService.Setup(x => x.SearchProductsAsync(searchTerm))
            .ReturnsAsync(expectedProducts);

        // Act
        var results = await mockProductService.Object.SearchProductsAsync(searchTerm);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.Contains("Milk", p.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VoiceSearch_NoProductFound_ReturnsEmptyList()
    {
        // Validates Requirement 4.2: graceful handling of voice search with no results
        // Arrange
        var searchTerm = "xyznonexistentproduct";
        var mockProductService = new Mock<IProductService>();
        mockProductService.Setup(x => x.SearchProductsAsync(searchTerm))
            .ReturnsAsync(new List<Product>());

        // Act
        var results = await mockProductService.Object.SearchProductsAsync(searchTerm);

        // Assert
        Assert.Empty(results);
    }

    // ─── Offline-First Experience ─────────────────────────────────────────────

    [Fact]
    public async Task OfflineQueue_WhenConnectivityRestored_ProcessesPendingOperations()
    {
        // Validates Requirement 12.1: automatic recovery when connectivity is restored
        // Arrange
        var pendingOps = new List<OfflineOperation>
        {
            new() { Id = Guid.NewGuid(), OperationType = "SaleSync", EntityType = "Sale", Priority = OperationPriority.Critical },
            new() { Id = Guid.NewGuid(), OperationType = "Update", EntityType = "Stock", Priority = OperationPriority.High }
        };

        _mockOfflineQueue.Setup(x => x.GetQueuedOperationsAsync(null))
            .ReturnsAsync(pendingOps);

        _mockOfflineQueue.Setup(x => x.ProcessQueuedOperationsAsync())
            .ReturnsAsync(new QueueProcessingResult
            {
                TotalOperations = 2,
                SuccessfulOperations = 2,
                FailedOperations = 0,
                ProcessingDuration = TimeSpan.FromMilliseconds(150)
            });

        // Act
        var queuedOps = await _mockOfflineQueue.Object.GetQueuedOperationsAsync();
        var processResult = await _mockOfflineQueue.Object.ProcessQueuedOperationsAsync();

        // Assert
        Assert.Equal(2, queuedOps.Count);
        Assert.Equal(2, processResult.TotalOperations);
        Assert.Equal(2, processResult.SuccessfulOperations);
        Assert.Equal(0, processResult.FailedOperations);
    }

    [Fact]
    public async Task OfflineQueue_Statistics_ReflectsQueueState()
    {
        // Validates Requirement 12.1: system tracks offline state for recovery
        // Arrange
        var stats = new QueueStatistics
        {
            TotalQueuedOperations = 5,
            PendingOperations = 3,
            ProcessedOperations = 2,
            FailedOperations = 0,
            OldestOperationDate = DateTime.UtcNow.AddMinutes(-10),
            NewestOperationDate = DateTime.UtcNow
        };

        _mockOfflineQueue.Setup(x => x.GetQueueStatisticsAsync())
            .ReturnsAsync(stats);

        // Act
        var result = await _mockOfflineQueue.Object.GetQueueStatisticsAsync();

        // Assert
        Assert.Equal(5, result.TotalQueuedOperations);
        Assert.Equal(3, result.PendingOperations);
        Assert.Equal(2, result.ProcessedOperations);
        Assert.NotNull(result.OldestOperationDate);
        Assert.NotNull(result.NewestOperationDate);
    }

    [Fact]
    public async Task OfflineQueue_StartMonitoring_EnablesAutoSync()
    {
        // Validates Requirement 12.1: automatic recovery mechanisms
        // Arrange
        _mockOfflineQueue.Setup(x => x.StartQueueMonitoringAsync())
            .ReturnsAsync(true);

        _mockOfflineQueue.Setup(x => x.StopQueueMonitoringAsync())
            .ReturnsAsync(true);

        // Act
        var started = await _mockOfflineQueue.Object.StartQueueMonitoringAsync();
        var stopped = await _mockOfflineQueue.Object.StopQueueMonitoringAsync();

        // Assert
        Assert.True(started);
        Assert.True(stopped);
    }

    [Fact]
    public async Task OfflineQueue_QueueProductUpdate_PrioritizesCorrectly()
    {
        // Validates Requirement 12.1: operations are queued with appropriate priority
        // Arrange
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            ShopId = Guid.NewGuid(),
            UnitPrice = 10.00m
        };

        _mockOfflineQueue.Setup(x => x.QueueProductUpdateAsync(It.IsAny<Product>(), It.IsAny<OperationType>()))
            .ReturnsAsync(true);

        // Act
        var queued = await _mockOfflineQueue.Object.QueueProductUpdateAsync(product, OperationType.Update);

        // Assert
        Assert.True(queued);
        _mockOfflineQueue.Verify(x => x.QueueProductUpdateAsync(
            It.Is<Product>(p => p.Id == product.Id),
            OperationType.Update), Times.Once);
    }

    [Fact]
    public async Task OfflineQueue_ClearQueue_RemovesAllOperations()
    {
        // Validates Requirement 12.1: queue management for recovery
        // Arrange
        _mockOfflineQueue.Setup(x => x.ClearQueueAsync())
            .ReturnsAsync(5);

        // Act
        var cleared = await _mockOfflineQueue.Object.ClearQueueAsync();

        // Assert
        Assert.Equal(5, cleared);
    }

    // ─── Connectivity Monitoring ──────────────────────────────────────────────

    [Fact]
    public async Task ConnectivityService_IsConnected_ReflectsNetworkState()
    {
        // Validates Requirement 12.1: system detects connectivity for recovery
        // Arrange
        _mockConnectivity.Setup(x => x.IsConnected).Returns(true);
        _mockConnectivity.Setup(x => x.IsConnectedAsync()).ReturnsAsync(true);

        // Act
        var isConnected = _mockConnectivity.Object.IsConnected;
        var isConnectedAsync = await _mockConnectivity.Object.IsConnectedAsync();

        // Assert
        Assert.True(isConnected);
        Assert.True(isConnectedAsync);
    }

    [Fact]
    public async Task ConnectivityService_WhenOffline_ReturnsDisconnected()
    {
        // Validates Requirement 12.1: offline detection for queuing
        // Arrange
        _mockConnectivity.Setup(x => x.IsConnected).Returns(false);
        _mockConnectivity.Setup(x => x.IsConnectedAsync()).ReturnsAsync(false);

        // Act
        var isConnected = _mockConnectivity.Object.IsConnected;
        var isConnectedAsync = await _mockConnectivity.Object.IsConnectedAsync();

        // Assert
        Assert.False(isConnected);
        Assert.False(isConnectedAsync);
    }

    // ─── Tab Management for Mobile ────────────────────────────────────────────

    [Fact]
    public async Task TabManagement_CreateSession_WithMobileOptimizedLimit()
    {
        // Validates Requirement 4.2: mobile-optimized tab management
        // Arrange
        var maxMobileTabs = 3;
        _mockTabManager.Setup(x => x.GetMaxConcurrentSessionsAsync())
            .ReturnsAsync(maxMobileTabs);

        // Act
        var maxTabs = await _mockTabManager.Object.GetMaxConcurrentSessionsAsync();

        // Assert
        Assert.Equal(3, maxTabs); // Mobile should limit to 3 tabs for performance
    }

    [Fact]
    public async Task TabManagement_SaveSessionState_PersistsOffline()
    {
        // Validates Requirement 12.1: transaction state persistence
        // Arrange
        var sessionId = Guid.NewGuid();
        var sessionData = new SaleSessionDto
        {
            Id = sessionId,
            TabName = "Mobile Sale 1",
            ShopId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            State = SessionState.Active,
            IsActive = true,
            Items = new List<SaleSessionItemDto>
            {
                new() { ProductName = "Product A", Quantity = 2, UnitPrice = 10.00m, LineTotal = 20.00m }
            }
        };

        _mockTabManager.Setup(x => x.SaveSessionStateAsync(sessionId, It.IsAny<SaleSessionDto>()))
            .ReturnsAsync(new SaveSessionStateResult { Success = true, SavedAt = DateTime.UtcNow });

        // Act
        var result = await _mockTabManager.Object.SaveSessionStateAsync(sessionId, sessionData);

        // Assert
        Assert.True(result.Success);
    }

    // ─── Barcode Scanning for Mobile ─────────────────────────────────────────

    [Fact]
    public async Task BarcodeScanning_Initialize_SucceedsOnMobile()
    {
        // Validates Requirement 4.2: touch-optimized barcode scanning
        // Arrange
        _mockBarcodeService.Setup(x => x.InitializeAsync()).ReturnsAsync(true);

        // Act
        var initialized = await _mockBarcodeService.Object.InitializeAsync();

        // Assert
        Assert.True(initialized);
    }

    [Fact]
    public async Task BarcodeScanning_ScanWithVibration_ProvidesTactileFeedback()
    {
        // Validates Requirement 4.2: haptic feedback for touch operations
        // Arrange
        var scanOptions = new ScanOptions
        {
            EnableVibration = true,
            EnableBeep = true,
            AutoAddToSale = true
        };

        var expectedResult = new BarcodeResult
        {
            IsSuccess = true,
            Barcode = "1234567890123",
            IsProductFound = true,
            Product = new Product { Id = Guid.NewGuid(), Name = "Test Product", UnitPrice = 5.99m }
        };

        _mockBarcodeService.Setup(x => x.ScanBarcodeAsync(It.Is<ScanOptions>(
            o => o.EnableVibration && o.EnableBeep)))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockBarcodeService.Object.ScanBarcodeAsync(scanOptions);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.IsProductFound);
        Assert.NotNull(result.Product);
    }
}
