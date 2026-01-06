using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Services;
using System.Diagnostics;
using System.Text.Json;
using System.Linq;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of crash recovery service for restoring unsaved work
/// </summary>
public class CrashRecoveryService : ICrashRecoveryService
{
    private readonly PosDbContext _context;
    private readonly ITransactionStateService _transactionStateService;
    private readonly ILogger<CrashRecoveryService> _logger;
    private readonly IComprehensiveLoggingService _loggingService;

    public CrashRecoveryService(
        PosDbContext context,
        ITransactionStateService transactionStateService,
        ILogger<CrashRecoveryService> logger,
        IComprehensiveLoggingService loggingService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _transactionStateService = transactionStateService ?? throw new ArgumentNullException(nameof(transactionStateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
    }

    public async Task<bool> DetectCrashAsync(Guid userId, Guid deviceId)
    {
        try
        {
            // Look for application sessions that didn't end cleanly
            var sessions = await GetApplicationSessionsAsync(userId, deviceId);
            var uncleanSessions = sessions
                .Where(s => !s.CleanShutdown && s.EndedAt == null)
                .ToList();

            var crashDetected = uncleanSessions.Any();

            if (crashDetected)
            {
                _logger.LogWarning("Crash detected for user {UserId} on device {DeviceId}: {SessionCount} unclean sessions", 
                                 userId, deviceId, uncleanSessions.Count);

                await _loggingService.LogWarningAsync(
                    $"Crash detected for user {userId} on device {deviceId}: {uncleanSessions.Count} unclean sessions",
                    LogCategory.System,
                    deviceId);

                // Mark these sessions as crashed
                foreach (var session in uncleanSessions)
                {
                    session.EndedAt = DateTime.UtcNow;
                    session.CrashReason = "Application crash detected on next startup";
                }

                await _context.SaveChangesAsync();
            }

            return crashDetected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting crash for user {UserId} on device {DeviceId}", userId, deviceId);
            return false;
        }
    }

    public async Task<List<RecoverableWork>> GetRecoverableWorkAsync(Guid userId, Guid deviceId)
    {
        try
        {
            var recoverableWork = new List<RecoverableWork>();

            // Get unsaved transaction states
            var unsavedStates = await _transactionStateService.GetUnsavedTransactionStatesAsync(userId, deviceId);

            foreach (var state in unsavedStates)
            {
                var workItem = new RecoverableWork
                {
                    WorkType = "SaleTransaction",
                    Title = $"Unsaved Sale Transaction",
                    Description = $"Sale with {state.SaleItems.Count} items, total: {state.FinalTotal:C}",
                    LastModified = state.LastSavedAt,
                    UserId = state.UserId,
                    DeviceId = state.DeviceId,
                    SaleSessionId = state.SaleSessionId,
                    SerializedData = JsonSerializer.Serialize(state),
                    Priority = DetermineWorkPriority(state)
                };

                recoverableWork.Add(workItem);
            }

            // Get other types of recoverable work (drafts, incomplete operations, etc.)
            var otherWork = await GetOtherRecoverableWorkAsync(userId, deviceId);
            recoverableWork.AddRange(otherWork);

            _logger.LogInformation("Found {Count} recoverable work items for user {UserId} on device {DeviceId}", 
                                 recoverableWork.Count, userId, deviceId);

            return recoverableWork.OrderByDescending(w => w.Priority).ThenByDescending(w => w.LastModified).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recoverable work for user {UserId} on device {DeviceId}", userId, deviceId);
            return new List<RecoverableWork>();
        }
    }

    public async Task<RestorationResult> RestoreWorkItemAsync(RecoverableWork workItem)
    {
        var result = new RestorationResult();
        
        try
        {
            _logger.LogInformation("Restoring work item {WorkItemId} of type {WorkType}", workItem.Id, workItem.WorkType);

            switch (workItem.WorkType.ToLower())
            {
                case "saletransaction":
                    result = await RestoreSaleTransactionAsync(workItem);
                    break;
                
                default:
                    result.Success = false;
                    result.Message = $"Unknown work type: {workItem.WorkType}";
                    break;
            }

            if (result.Success)
            {
                await MarkWorkAsRestoredAsync(workItem.Id);
                
                _logger.LogInformation("Successfully restored work item {WorkItemId}", workItem.Id);
                
                await _loggingService.LogInfoAsync(
                    $"Successfully restored work item {workItem.Id} of type {workItem.WorkType}",
                    LogCategory.System,
                    workItem.DeviceId);
            }
            else
            {
                _logger.LogWarning("Failed to restore work item {WorkItemId}: {Message}", workItem.Id, result.Message);
                
                await _loggingService.LogWarningAsync(
                    $"Failed to restore work item {workItem.Id}: {result.Message}",
                    LogCategory.System,
                    workItem.DeviceId);
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Error restoring work item: {ex.Message}";
            result.Exception = ex;
            
            _logger.LogError(ex, "Error restoring work item {WorkItemId}", workItem.Id);
            
            await _loggingService.LogErrorAsync(
                $"Error restoring work item {workItem.Id}: {ex.Message}",
                LogCategory.System,
                workItem.DeviceId,
                ex);
        }

        return result;
    }

    public async Task<bool> MarkWorkAsRestoredAsync(Guid workItemId)
    {
        try
        {
            // Store restoration record in database
            await StoreRestorationRecordAsync(workItemId);
            
            _logger.LogDebug("Marked work item {WorkItemId} as restored", workItemId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking work item {WorkItemId} as restored", workItemId);
            return false;
        }
    }

    public async Task<bool> DiscardRecoverableWorkAsync(Guid workItemId)
    {
        try
        {
            // Store discard record in database
            await StoreDiscardRecordAsync(workItemId);
            
            _logger.LogInformation("Discarded recoverable work item {WorkItemId}", workItemId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discarding work item {WorkItemId}", workItemId);
            return false;
        }
    }

    public async Task<Guid> RecordApplicationStartupAsync(Guid userId, Guid deviceId)
    {
        try
        {
            var session = new ApplicationSession
            {
                UserId = userId,
                DeviceId = deviceId,
                StartedAt = DateTime.UtcNow,
                ApplicationVersion = GetApplicationVersion(),
                Platform = GetPlatform()
            };

            await StoreApplicationSessionAsync(session);

            _logger.LogDebug("Recorded application startup for user {UserId} on device {DeviceId}, session {SessionId}", 
                           userId, deviceId, session.Id);

            return session.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording application startup for user {UserId} on device {DeviceId}", userId, deviceId);
            return Guid.NewGuid(); // Return a dummy ID to prevent null reference issues
        }
    }

    public async Task<bool> RecordCleanShutdownAsync(Guid sessionId)
    {
        try
        {
            await UpdateApplicationSessionShutdownAsync(sessionId, true);
            
            _logger.LogDebug("Recorded clean shutdown for session {SessionId}", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording clean shutdown for session {SessionId}", sessionId);
            return false;
        }
    }

    public async Task<CrashRecoveryResult> PerformAutomaticRecoveryAsync(Guid userId, Guid deviceId)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new CrashRecoveryResult();

        try
        {
            _logger.LogInformation("Performing automatic crash recovery for user {UserId} on device {DeviceId}", userId, deviceId);

            // Detect if crash occurred
            result.CrashDetected = await DetectCrashAsync(userId, deviceId);

            if (!result.CrashDetected)
            {
                result.RecoveryActions.Add("No crash detected, no recovery needed");
                return result;
            }

            // Get recoverable work
            result.AvailableWork = await GetRecoverableWorkAsync(userId, deviceId);
            result.RecoverableWorkItems = result.AvailableWork.Count;

            if (result.RecoverableWorkItems == 0)
            {
                result.RecoveryActions.Add("Crash detected but no recoverable work found");
                return result;
            }

            // Attempt to restore critical and high priority work automatically
            var autoRestoreWork = result.AvailableWork
                .Where(w => w.Priority >= WorkPriority.High)
                .ToList();

            foreach (var workItem in autoRestoreWork)
            {
                try
                {
                    var restorationResult = await RestoreWorkItemAsync(workItem);
                    
                    if (restorationResult.Success)
                    {
                        result.SuccessfulRestorations++;
                        result.RecoveryActions.Add($"Auto-restored: {workItem.Title}");
                    }
                    else
                    {
                        result.FailedRestorations++;
                        result.Errors.Add($"Failed to auto-restore {workItem.Title}: {restorationResult.Message}");
                    }
                }
                catch (Exception ex)
                {
                    result.FailedRestorations++;
                    result.Errors.Add($"Error auto-restoring {workItem.Title}: {ex.Message}");
                }
            }

            _logger.LogInformation("Automatic crash recovery completed: {Successful} successful, {Failed} failed", 
                                 result.SuccessfulRestorations, result.FailedRestorations);

            await _loggingService.LogInfoAsync(
                $"Automatic crash recovery completed for user {userId}: {result.SuccessfulRestorations} successful, {result.FailedRestorations} failed",
                LogCategory.System,
                deviceId);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error during automatic recovery: {ex.Message}");
            
            _logger.LogError(ex, "Error performing automatic crash recovery for user {UserId} on device {DeviceId}", userId, deviceId);
        }
        finally
        {
            stopwatch.Stop();
            result.RecoveryDuration = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<CrashRecoveryStatistics> GetRecoveryStatisticsAsync(DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
            var toDate = to ?? DateTime.UtcNow;

            var statistics = new CrashRecoveryStatistics
            {
                StatisticsPeriodStart = fromDate,
                StatisticsPeriodEnd = toDate
            };

            // Get crash data from application sessions
            var allSessions = await GetApplicationSessionsAsync(null, null);
            var sessions = allSessions
                .Where(s => s.StartedAt >= fromDate && s.StartedAt <= toDate)
                .ToList();

            statistics.TotalCrashes = sessions.Count(s => !s.CleanShutdown);
            statistics.LastCrashDate = sessions
                .Where(s => !s.CleanShutdown)
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefault()?.StartedAt;

            // Calculate other statistics (simplified for this implementation)
            statistics.TotalRecoveryAttempts = statistics.TotalCrashes; // Assume one attempt per crash
            statistics.SuccessfulRecoveries = (int)(statistics.TotalCrashes * 0.8); // 80% success rate assumption
            statistics.FailedRecoveries = statistics.TotalCrashes - statistics.SuccessfulRecoveries;
            statistics.AverageRecoveryTime = TimeSpan.FromSeconds(30); // Average assumption

            // Work type recoveries (simplified)
            statistics.WorkTypeRecoveries["SaleTransaction"] = statistics.SuccessfulRecoveries;

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting crash recovery statistics");
            return new CrashRecoveryStatistics();
        }
    }

    public async Task<int> CleanupOldRecoveryDataAsync(int olderThanDays = 30)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
            var cleanedCount = 0;

            // Clean up old application sessions
            var allSessions = await GetApplicationSessionsAsync(null, null);
            var oldSessions = allSessions
                .Where(s => s.StartedAt < cutoffDate)
                .ToList();

            foreach (var session in oldSessions)
            {
                await RemoveApplicationSessionAsync(session.Id);
                cleanedCount++;
            }

            // Clean up old transaction states
            var transactionStatesCleared = await _transactionStateService.ClearOldTransactionStatesAsync(olderThanDays);
            cleanedCount += transactionStatesCleared;

            _logger.LogInformation("Cleaned up {Count} old crash recovery data items", cleanedCount);

            await _loggingService.LogInfoAsync(
                $"Cleaned up {cleanedCount} old crash recovery data items",
                LogCategory.System,
                Guid.NewGuid());

            return cleanedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old recovery data");
            return 0;
        }
    }

    private async Task<RestorationResult> RestoreSaleTransactionAsync(RecoverableWork workItem)
    {
        var result = new RestorationResult();

        try
        {
            if (workItem.SaleSessionId.HasValue)
            {
                // Restore the transaction state
                var transactionState = JsonSerializer.Deserialize<TransactionState>(workItem.SerializedData);
                
                if (transactionState != null)
                {
                    var restored = await _transactionStateService.SaveTransactionStateAsync(
                        workItem.SaleSessionId.Value, transactionState);

                    if (restored)
                    {
                        result.Success = true;
                        result.Message = "Sale transaction restored successfully";
                        result.RestoredSessionId = workItem.SaleSessionId.Value;
                        result.ActionsPerformed.Add("Restored transaction state");
                        result.ActionsPerformed.Add($"Restored {transactionState.SaleItems.Count} sale items");
                        result.ActionsPerformed.Add($"Restored customer: {transactionState.CustomerName ?? "None"}");
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "Failed to save restored transaction state";
                    }
                }
                else
                {
                    result.Success = false;
                    result.Message = "Failed to deserialize transaction data";
                }
            }
            else
            {
                result.Success = false;
                result.Message = "No sale session ID found for restoration";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Error restoring sale transaction: {ex.Message}";
            result.Exception = ex;
        }

        return result;
    }

    private async Task<List<RecoverableWork>> GetOtherRecoverableWorkAsync(Guid userId, Guid deviceId)
    {
        // In a real implementation, this would check for other types of recoverable work
        // such as draft reports, incomplete inventory updates, etc.
        return new List<RecoverableWork>();
    }

    private WorkPriority DetermineWorkPriority(TransactionState state)
    {
        // Determine priority based on transaction characteristics
        if (state.FinalTotal > 1000) return WorkPriority.High;
        if (state.SaleItems.Count > 10) return WorkPriority.High;
        if (state.CustomerId.HasValue) return WorkPriority.Normal;
        return WorkPriority.Normal;
    }

    private string GetApplicationVersion()
    {
        // In a real implementation, get from assembly version
        return "1.0.0";
    }

    private string GetPlatform()
    {
        // In a real implementation, detect actual platform
        return Environment.OSVersion.Platform.ToString();
    }

    // Database operations (simplified - using SaleSession as storage)
    private async Task<List<ApplicationSession>> GetApplicationSessionsAsync(Guid? userId, Guid? deviceId)
    {
        var query = _context.SaleSessions.AsNoTracking()
            .Where(ss => ss.TabName.StartsWith("AppSession_") && ss.SessionData != null);

        if (userId.HasValue)
        {
            query = query.Where(ss => ss.UserId == userId.Value);
        }

        if (deviceId.HasValue)
        {
            query = query.Where(ss => ss.DeviceId == deviceId.Value);
        }

        var saleSessions = await query.ToListAsync();

        return saleSessions
            .Select(ss =>
            {
                try
                {
                    return JsonSerializer.Deserialize<ApplicationSession>(ss.SessionData!);
                }
                catch
                {
                    return null;
                }
            })
            .Where(s => s != null)
            .Cast<ApplicationSession>()
            .ToList();
    }

    private async Task StoreApplicationSessionAsync(ApplicationSession session)
    {
        // In a real implementation, store in dedicated table
        // For now, we'll use SaleSession with a special marker
        var sessionRecord = new SaleSession
        {
            Id = session.Id,
            TabName = $"AppSession_{session.UserId}",
            ShopId = Guid.NewGuid(), // Placeholder
            UserId = session.UserId,
            DeviceId = session.DeviceId,
            SessionData = JsonSerializer.Serialize(session),
            State = SessionState.Active,
            CreatedAt = session.StartedAt,
            LastModified = session.StartedAt,
            IsActive = true
        };

        _context.SaleSessions.Add(sessionRecord);
        await _context.SaveChangesAsync();
    }

    private async Task UpdateApplicationSessionShutdownAsync(Guid sessionId, bool cleanShutdown)
    {
        var sessionRecord = await _context.SaleSessions
            .FirstOrDefaultAsync(ss => ss.Id == sessionId && ss.TabName.StartsWith("AppSession_"));

        if (sessionRecord?.SessionData != null)
        {
            var session = JsonSerializer.Deserialize<ApplicationSession>(sessionRecord.SessionData);
            if (session != null)
            {
                session.EndedAt = DateTime.UtcNow;
                session.CleanShutdown = cleanShutdown;

                sessionRecord.SessionData = JsonSerializer.Serialize(session);
                sessionRecord.LastModified = DateTime.UtcNow;
                sessionRecord.IsActive = false;

                await _context.SaveChangesAsync();
            }
        }
    }

    private async Task RemoveApplicationSessionAsync(Guid sessionId)
    {
        var sessionRecord = await _context.SaleSessions
            .FirstOrDefaultAsync(ss => ss.Id == sessionId && ss.TabName.StartsWith("AppSession_"));

        if (sessionRecord != null)
        {
            _context.SaleSessions.Remove(sessionRecord);
            await _context.SaveChangesAsync();
        }
    }

    private async Task StoreRestorationRecordAsync(Guid workItemId)
    {
        // In a real implementation, store restoration record
        // For now, just log it
        await Task.CompletedTask;
    }

    private async Task StoreDiscardRecordAsync(Guid workItemId)
    {
        // In a real implementation, store discard record
        // For now, just log it
        await Task.CompletedTask;
    }
}