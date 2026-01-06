using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Shared.Core.Services;

/// <summary>
/// Enhanced error recovery service that integrates all recovery mechanisms
/// Provides comprehensive error recovery and resilience capabilities
/// </summary>
public class EnhancedErrorRecoveryService : IEnhancedErrorRecoveryService
{
    private readonly IErrorRecoveryService _errorRecoveryService;
    private readonly ITransactionStateService _transactionStateService;
    private readonly IOfflineQueueService _offlineQueueService;
    private readonly ICrashRecoveryService _crashRecoveryService;
    private readonly ILogger<EnhancedErrorRecoveryService> _logger;
    private readonly IComprehensiveLoggingService _loggingService;

    public EnhancedErrorRecoveryService(
        IErrorRecoveryService errorRecoveryService,
        ITransactionStateService transactionStateService,
        IOfflineQueueService offlineQueueService,
        ICrashRecoveryService crashRecoveryService,
        ILogger<EnhancedErrorRecoveryService> logger,
        IComprehensiveLoggingService loggingService)
    {
        _errorRecoveryService = errorRecoveryService ?? throw new ArgumentNullException(nameof(errorRecoveryService));
        _transactionStateService = transactionStateService ?? throw new ArgumentNullException(nameof(transactionStateService));
        _offlineQueueService = offlineQueueService ?? throw new ArgumentNullException(nameof(offlineQueueService));
        _crashRecoveryService = crashRecoveryService ?? throw new ArgumentNullException(nameof(crashRecoveryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
    }

    public async Task<ComprehensiveRecoveryResult> PerformComprehensiveRecoveryAsync(Exception exception, Guid userId, Guid deviceId)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ComprehensiveRecoveryResult();

        try
        {
            _logger.LogInformation("Starting comprehensive error recovery for user {UserId} on device {DeviceId}", userId, deviceId);

            await _loggingService.LogInfoAsync(
                $"Starting comprehensive error recovery for user {userId} on device {deviceId}",
                LogCategory.System,
                deviceId);

            // Step 1: Perform basic error recovery based on exception type
            result.StorageRecovery = await PerformBasicErrorRecoveryAsync(exception);
            if (result.StorageRecovery.Success)
            {
                result.RecoveryActions.Add("Basic error recovery completed successfully");
            }
            else
            {
                result.Errors.Add($"Basic error recovery failed: {result.StorageRecovery.Message}");
            }

            // Step 2: Perform crash recovery if needed
            result.CrashRecovery = await _crashRecoveryService.PerformAutomaticRecoveryAsync(userId, deviceId);
            result.CrashRecoveryItemsRestored = result.CrashRecovery.SuccessfulRestorations;
            
            if (result.CrashRecovery.CrashDetected)
            {
                result.RecoveryActions.Add($"Crash recovery completed: {result.CrashRecovery.SuccessfulRestorations} items restored");
                result.RecoveryActions.AddRange(result.CrashRecovery.RecoveryActions);
            }

            // Step 3: Process offline queue
            result.QueueProcessing = await _offlineQueueService.ProcessQueuedOperationsAsync();
            result.OfflineOperationsProcessed = result.QueueProcessing.SuccessfulOperations;
            
            if (result.QueueProcessing.TotalOperations > 0)
            {
                result.RecoveryActions.Add($"Processed {result.QueueProcessing.SuccessfulOperations} of {result.QueueProcessing.TotalOperations} queued operations");
            }

            // Step 4: Restore transaction states
            var unsavedStates = await _transactionStateService.GetUnsavedTransactionStatesAsync(userId, deviceId);
            result.TransactionStatesRestored = 0;
            
            foreach (var state in unsavedStates)
            {
                try
                {
                    var restored = await _transactionStateService.SaveTransactionStateAsync(state.SaleSessionId, state);
                    if (restored)
                    {
                        result.TransactionStatesRestored++;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Failed to restore transaction state {state.SaleSessionId}: {ex.Message}");
                }
            }

            if (result.TransactionStatesRestored > 0)
            {
                result.RecoveryActions.Add($"Restored {result.TransactionStatesRestored} transaction states");
            }

            // Step 5: Start monitoring services
            await _offlineQueueService.StartQueueMonitoringAsync();
            result.RecoveryActions.Add("Started offline queue monitoring");

            // Determine overall success
            result.Success = result.Errors.Count == 0 || 
                           (result.StorageRecovery?.Success == true && result.CrashRecovery?.SuccessfulRestorations > 0);
            
            result.Message = result.Success 
                ? "Comprehensive recovery completed successfully"
                : $"Comprehensive recovery completed with {result.Errors.Count} errors";

            _logger.LogInformation("Comprehensive error recovery completed: {Success}, Actions: {ActionCount}, Errors: {ErrorCount}", 
                                 result.Success, result.RecoveryActions.Count, result.Errors.Count);

            await _loggingService.LogInfoAsync(
                $"Comprehensive error recovery completed for user {userId}: {result.Success}",
                LogCategory.System,
                deviceId);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Comprehensive recovery failed: {ex.Message}";
            result.Errors.Add($"Recovery exception: {ex.Message}");

            _logger.LogError(ex, "Error during comprehensive recovery for user {UserId} on device {DeviceId}", userId, deviceId);

            await _loggingService.LogErrorAsync(
                $"Error during comprehensive recovery for user {userId}: {ex.Message}",
                LogCategory.System,
                deviceId,
                ex);
        }
        finally
        {
            stopwatch.Stop();
            result.RecoveryDuration = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<RecoveryInitializationResult> InitializeRecoverySystemAsync(Guid userId, Guid deviceId)
    {
        var result = new RecoveryInitializationResult();

        try
        {
            _logger.LogInformation("Initializing recovery system for user {UserId} on device {DeviceId}", userId, deviceId);

            // Step 1: Record application startup for crash detection
            result.ApplicationSessionId = await _crashRecoveryService.RecordApplicationStartupAsync(userId, deviceId);
            result.InitializationActions.Add("Recorded application startup for crash detection");

            // Step 2: Detect if previous session crashed
            result.CrashDetected = await _crashRecoveryService.DetectCrashAsync(userId, deviceId);
            
            if (result.CrashDetected)
            {
                result.InitializationActions.Add("Previous crash detected");
                
                // Get recoverable work
                var recoverableWork = await _crashRecoveryService.GetRecoverableWorkAsync(userId, deviceId);
                result.RecoverableWorkItems = recoverableWork.Count;
                
                if (result.RecoverableWorkItems > 0)
                {
                    result.Warnings.Add($"Found {result.RecoverableWorkItems} recoverable work items from previous session");
                }
            }

            // Step 3: Start auto-save for any existing sessions
            var unsavedStates = await _transactionStateService.GetUnsavedTransactionStatesAsync(userId, deviceId);
            foreach (var state in unsavedStates)
            {
                await _transactionStateService.StartAutoSaveAsync(state.SaleSessionId);
            }
            
            if (unsavedStates.Any())
            {
                result.InitializationActions.Add($"Started auto-save for {unsavedStates.Count} existing sessions");
            }

            // Step 4: Start offline queue monitoring
            var monitoringStarted = await _offlineQueueService.StartQueueMonitoringAsync();
            if (monitoringStarted)
            {
                result.InitializationActions.Add("Started offline queue monitoring");
            }

            // Step 5: Perform system health check
            var healthResult = await _errorRecoveryService.PerformSystemHealthCheckAsync();
            if (!healthResult.IsHealthy)
            {
                result.Warnings.Add($"System health check found {healthResult.Issues.Count} issues");
            }

            result.Success = true;
            result.Message = "Recovery system initialized successfully";

            _logger.LogInformation("Recovery system initialized for user {UserId}: {CrashDetected}, {RecoverableItems} recoverable items", 
                                 userId, result.CrashDetected, result.RecoverableWorkItems);

            await _loggingService.LogInfoAsync(
                $"Recovery system initialized for user {userId}: crash detected={result.CrashDetected}, recoverable items={result.RecoverableWorkItems}",
                LogCategory.System,
                deviceId);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Recovery system initialization failed: {ex.Message}";

            _logger.LogError(ex, "Error initializing recovery system for user {UserId} on device {DeviceId}", userId, deviceId);

            await _loggingService.LogErrorAsync(
                $"Error initializing recovery system for user {userId}: {ex.Message}",
                LogCategory.System,
                deviceId,
                ex);
        }

        return result;
    }

    public async Task<ShutdownResult> PerformGracefulShutdownAsync(Guid sessionId)
    {
        var result = new ShutdownResult();

        try
        {
            _logger.LogInformation("Performing graceful shutdown for session {SessionId}", sessionId);

            // Step 1: Save all active transaction states
            var unsavedStates = await _transactionStateService.GetUnsavedTransactionStatesAsync();
            result.TransactionStatesSaved = 0;

            foreach (var state in unsavedStates)
            {
                try
                {
                    await _transactionStateService.SaveTransactionStateAsync(state.SaleSessionId, state);
                    await _transactionStateService.StopAutoSaveAsync(state.SaleSessionId);
                    result.TransactionStatesSaved++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save transaction state {SessionId} during shutdown", state.SaleSessionId);
                }
            }

            if (result.TransactionStatesSaved > 0)
            {
                result.ShutdownActions.Add($"Saved {result.TransactionStatesSaved} transaction states");
            }

            // Step 2: Get queue statistics before shutdown
            var queueStats = await _offlineQueueService.GetQueueStatisticsAsync();
            result.QueuedOperations = queueStats.PendingOperations;

            if (result.QueuedOperations > 0)
            {
                result.ShutdownActions.Add($"Left {result.QueuedOperations} operations in offline queue");
            }

            // Step 3: Stop monitoring services
            await _offlineQueueService.StopQueueMonitoringAsync();
            result.ShutdownActions.Add("Stopped offline queue monitoring");

            // Step 4: Record clean shutdown
            var shutdownRecorded = await _crashRecoveryService.RecordCleanShutdownAsync(sessionId);
            if (shutdownRecorded)
            {
                result.ShutdownActions.Add("Recorded clean shutdown for crash detection");
            }

            // Step 5: Clean up old recovery data
            var cleanedItems = await _crashRecoveryService.CleanupOldRecoveryDataAsync();
            if (cleanedItems > 0)
            {
                result.ShutdownActions.Add($"Cleaned up {cleanedItems} old recovery data items");
            }

            result.Success = true;
            result.Message = "Graceful shutdown completed successfully";

            _logger.LogInformation("Graceful shutdown completed for session {SessionId}: {StatesSaved} states saved, {QueuedOps} queued operations", 
                                 sessionId, result.TransactionStatesSaved, result.QueuedOperations);

            await _loggingService.LogInfoAsync(
                $"Graceful shutdown completed for session {sessionId}",
                LogCategory.System,
                Guid.NewGuid());
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Graceful shutdown failed: {ex.Message}";

            _logger.LogError(ex, "Error during graceful shutdown for session {SessionId}", sessionId);

            await _loggingService.LogErrorAsync(
                $"Error during graceful shutdown for session {sessionId}: {ex.Message}",
                LogCategory.System,
                Guid.NewGuid(),
                ex);
        }

        return result;
    }

    public async Task<RecoveryStatusResult> GetRecoveryStatusAsync()
    {
        var result = new RecoveryStatusResult();

        try
        {
            // Get system health status
            var healthResult = await _errorRecoveryService.PerformSystemHealthCheckAsync();
            result.SystemHealthy = healthResult.IsHealthy;
            result.LastHealthCheck = healthResult.CheckTimestamp;
            result.CurrentIssues = healthResult.Issues
                .Where(i => !i.IsResolved)
                .Select(i => i.Description)
                .ToList();

            // Get transaction state statistics
            var unsavedStates = await _transactionStateService.GetUnsavedTransactionStatesAsync();
            result.ActiveTransactionStates = unsavedStates.Count;

            // Get offline queue statistics
            var queueStats = await _offlineQueueService.GetQueueStatisticsAsync();
            result.PendingOfflineOperations = queueStats.PendingOperations;

            // Get crash recovery statistics
            var crashStats = await _crashRecoveryService.GetRecoveryStatisticsAsync(DateTime.UtcNow.AddDays(-7));
            result.UnresolvedCrashRecoveryItems = 0; // Simplified for this implementation

            // Populate statistics dictionary
            result.Statistics["TotalCrashesLast7Days"] = crashStats.TotalCrashes;
            result.Statistics["SuccessfulRecoveries"] = crashStats.SuccessfulRecoveries;
            result.Statistics["FailedRecoveries"] = crashStats.FailedRecoveries;
            result.Statistics["QueueSizeBytes"] = queueStats.QueueSizeBytes;
            result.Statistics["ProcessedOperations"] = queueStats.ProcessedOperations;
            result.Statistics["FailedOperations"] = queueStats.FailedOperations;

            _logger.LogDebug("Recovery status retrieved: Healthy={Healthy}, ActiveStates={ActiveStates}, PendingOps={PendingOps}", 
                           result.SystemHealthy, result.ActiveTransactionStates, result.PendingOfflineOperations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recovery status");
            result.SystemHealthy = false;
            result.CurrentIssues.Add($"Error retrieving recovery status: {ex.Message}");
        }

        return result;
    }

    private async Task<RecoveryResult> PerformBasicErrorRecoveryAsync(Exception exception)
    {
        try
        {
            // Determine recovery strategy based on exception type
            return exception switch
            {
                Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => await _errorRecoveryService.RecoverFromConcurrencyErrorAsync(exception),
                Microsoft.EntityFrameworkCore.DbUpdateException => await _errorRecoveryService.RecoverFromStorageErrorAsync(exception),
                System.Data.Common.DbException => await _errorRecoveryService.RecoverFromStorageErrorAsync(exception),
                InvalidOperationException when exception.Message.Contains("sync") => await _errorRecoveryService.RecoverFromSyncErrorAsync(exception),
                _ => await _errorRecoveryService.RecoverFromStorageErrorAsync(exception) // Default to storage recovery
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during basic error recovery");
            return new RecoveryResult
            {
                Success = false,
                Message = $"Basic error recovery failed: {ex.Message}",
                OriginalException = exception
            };
        }
    }
}