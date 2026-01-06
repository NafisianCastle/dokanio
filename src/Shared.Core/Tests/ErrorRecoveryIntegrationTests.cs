using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.DependencyInjection;
using Shared.Core.Services;
using Xunit;

namespace Shared.Core.Tests;

/// <summary>
/// Integration tests for enhanced error recovery and resilience functionality
/// </summary>
public class ErrorRecoveryIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IEnhancedErrorRecoveryService _enhancedErrorRecoveryService;
    private readonly ITransactionStateService _transactionStateService;
    private readonly IOfflineQueueService _offlineQueueService;
    private readonly ICrashRecoveryService _crashRecoveryService;

    public ErrorRecoveryIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        
        _serviceProvider = services.BuildServiceProvider();
        
        _enhancedErrorRecoveryService = _serviceProvider.GetRequiredService<IEnhancedErrorRecoveryService>();
        _transactionStateService = _serviceProvider.GetRequiredService<ITransactionStateService>();
        _offlineQueueService = _serviceProvider.GetRequiredService<IOfflineQueueService>();
        _crashRecoveryService = _serviceProvider.GetRequiredService<ICrashRecoveryService>();
    }

    [Fact]
    public async Task InitializeRecoverySystem_ShouldSucceed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        // Act
        var result = await _enhancedErrorRecoveryService.InitializeRecoverySystemAsync(userId, deviceId);

        // Assert
        Assert.True(result.Success);
        Assert.NotEqual(Guid.Empty, result.ApplicationSessionId);
        Assert.NotEmpty(result.InitializationActions);
    }

    [Fact]
    public async Task TransactionStateService_AutoSave_ShouldWork()
    {
        // Arrange
        var saleSessionId = Guid.NewGuid();
        var transactionState = new TransactionState
        {
            SaleSessionId = saleSessionId,
            UserId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            ShopId = Guid.NewGuid(),
            FinalTotal = 100.50m,
            SaleItems = new List<TransactionSaleItem>
            {
                new TransactionSaleItem
                {
                    ProductName = "Test Product",
                    Quantity = 2,
                    UnitPrice = 50.25m,
                    LineTotal = 100.50m
                }
            }
        };

        // Act
        var saveResult = await _transactionStateService.AutoSaveTransactionStateAsync(saleSessionId, transactionState);
        var restoredState = await _transactionStateService.RestoreTransactionStateAsync(saleSessionId);

        // Assert
        Assert.True(saveResult);
        Assert.NotNull(restoredState);
        Assert.Equal(transactionState.FinalTotal, restoredState.FinalTotal);
        Assert.Single(restoredState.SaleItems);
        Assert.Equal("Test Product", restoredState.SaleItems[0].ProductName);
    }

    [Fact]
    public async Task OfflineQueueService_QueueAndProcess_ShouldWork()
    {
        // Arrange
        var operation = new OfflineOperation
        {
            OperationType = "TestOperation",
            EntityType = "TestEntity",
            EntityId = Guid.NewGuid(),
            SerializedData = "test data",
            Priority = OperationPriority.Normal,
            UserId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            ShopId = Guid.NewGuid()
        };

        // Act
        var queueResult = await _offlineQueueService.QueueOperationAsync(operation);
        var queuedOperations = await _offlineQueueService.GetQueuedOperationsAsync();
        var statistics = await _offlineQueueService.GetQueueStatisticsAsync();

        // Assert
        Assert.True(queueResult);
        Assert.NotEmpty(queuedOperations);
        Assert.True(statistics.TotalQueuedOperations > 0);
    }

    [Fact]
    public async Task CrashRecoveryService_DetectAndRecover_ShouldWork()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        // Act
        var sessionId = await _crashRecoveryService.RecordApplicationStartupAsync(userId, deviceId);
        var crashDetected = await _crashRecoveryService.DetectCrashAsync(userId, deviceId);
        var recoverableWork = await _crashRecoveryService.GetRecoverableWorkAsync(userId, deviceId);
        var recoveryResult = await _crashRecoveryService.PerformAutomaticRecoveryAsync(userId, deviceId);

        // Assert
        Assert.NotEqual(Guid.Empty, sessionId);
        Assert.False(crashDetected); // No crash should be detected for a clean startup
        Assert.NotNull(recoverableWork);
        Assert.NotNull(recoveryResult);
        Assert.False(recoveryResult.CrashDetected);
    }

    [Fact]
    public async Task EnhancedErrorRecoveryService_ComprehensiveRecovery_ShouldWork()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var exception = new InvalidOperationException("Test exception for recovery");

        // Act
        var recoveryResult = await _enhancedErrorRecoveryService.PerformComprehensiveRecoveryAsync(exception, userId, deviceId);
        var statusResult = await _enhancedErrorRecoveryService.GetRecoveryStatusAsync();

        // Assert
        Assert.NotNull(recoveryResult);
        Assert.NotEmpty(recoveryResult.RecoveryActions);
        Assert.NotNull(statusResult);
        Assert.True(statusResult.LastHealthCheck > DateTime.MinValue);
    }

    [Fact]
    public async Task EnhancedErrorRecoveryService_GracefulShutdown_ShouldWork()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var shutdownResult = await _enhancedErrorRecoveryService.PerformGracefulShutdownAsync(sessionId);

        // Assert
        Assert.True(shutdownResult.Success);
        Assert.NotEmpty(shutdownResult.ShutdownActions);
    }

    [Fact]
    public async Task TransactionStateService_StartStopAutoSave_ShouldWork()
    {
        // Arrange
        var saleSessionId = Guid.NewGuid();

        // Act
        var startResult = await _transactionStateService.StartAutoSaveAsync(saleSessionId, 5); // 5 second interval
        await Task.Delay(100); // Brief delay to ensure timer is set up
        var stopResult = await _transactionStateService.StopAutoSaveAsync(saleSessionId);

        // Assert
        Assert.True(startResult);
        Assert.True(stopResult);
    }

    [Fact]
    public async Task OfflineQueueService_GetStatistics_ShouldReturnValidData()
    {
        // Act
        var statistics = await _offlineQueueService.GetQueueStatisticsAsync();

        // Assert
        Assert.NotNull(statistics);
        Assert.True(statistics.TotalQueuedOperations >= 0);
        Assert.True(statistics.PendingOperations >= 0);
        Assert.True(statistics.ProcessedOperations >= 0);
        Assert.NotNull(statistics.OperationsByPriority);
        Assert.NotNull(statistics.OperationsByType);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}