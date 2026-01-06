using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of transaction state persistence service
/// Provides auto-save functionality and crash recovery capabilities
/// </summary>
public class TransactionStateService : ITransactionStateService, IDisposable
{
    private readonly PosDbContext _context;
    private readonly ILogger<TransactionStateService> _logger;
    private readonly IComprehensiveLoggingService _loggingService;
    
    // Auto-save timers for active sessions
    private readonly ConcurrentDictionary<Guid, Timer> _autoSaveTimers = new();
    private readonly ConcurrentDictionary<Guid, TransactionState> _activeStates = new();
    
    private bool _disposed = false;

    public TransactionStateService(
        PosDbContext context,
        ILogger<TransactionStateService> logger,
        IComprehensiveLoggingService loggingService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
    }

    public async Task<bool> AutoSaveTransactionStateAsync(Guid saleSessionId, TransactionState transactionData)
    {
        try
        {
            transactionData.IsAutoSaved = true;
            transactionData.LastSavedAt = DateTime.UtcNow;
            
            // Update active state cache
            _activeStates.AddOrUpdate(saleSessionId, transactionData, (key, oldValue) => transactionData);
            
            var success = await SaveTransactionStateInternalAsync(saleSessionId, transactionData);
            
            if (success)
            {
                _logger.LogDebug("Auto-saved transaction state for session {SessionId}", saleSessionId);
                
                await _loggingService.LogInfoAsync(
                    $"Auto-saved transaction state for session {saleSessionId}",
                    LogCategory.System,
                    transactionData.DeviceId);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error auto-saving transaction state for session {SessionId}", saleSessionId);
            
            await _loggingService.LogErrorAsync(
                $"Error auto-saving transaction state for session {saleSessionId}: {ex.Message}",
                LogCategory.System,
                transactionData.DeviceId,
                ex);
            
            return false;
        }
    }

    public async Task<bool> SaveTransactionStateAsync(Guid saleSessionId, TransactionState transactionData)
    {
        try
        {
            transactionData.IsAutoSaved = false;
            transactionData.LastSavedAt = DateTime.UtcNow;
            
            // Update active state cache
            _activeStates.AddOrUpdate(saleSessionId, transactionData, (key, oldValue) => transactionData);
            
            var success = await SaveTransactionStateInternalAsync(saleSessionId, transactionData);
            
            if (success)
            {
                _logger.LogInformation("Manually saved transaction state for session {SessionId}", saleSessionId);
                
                await _loggingService.LogInfoAsync(
                    $"Manually saved transaction state for session {saleSessionId}",
                    LogCategory.System,
                    transactionData.DeviceId);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving transaction state for session {SessionId}", saleSessionId);
            
            await _loggingService.LogErrorAsync(
                $"Error saving transaction state for session {saleSessionId}: {ex.Message}",
                LogCategory.System,
                transactionData.DeviceId,
                ex);
            
            return false;
        }
    }

    public async Task<TransactionState?> RestoreTransactionStateAsync(Guid saleSessionId)
    {
        try
        {
            // First check active cache
            if (_activeStates.TryGetValue(saleSessionId, out var cachedState))
            {
                _logger.LogDebug("Restored transaction state from cache for session {SessionId}", saleSessionId);
                return cachedState;
            }
            
            // Check database
            var saleSession = await _context.SaleSessions
                .FirstOrDefaultAsync(ss => ss.Id == saleSessionId && !ss.IsDeleted);
            
            if (saleSession?.SessionData != null)
            {
                var transactionState = JsonSerializer.Deserialize<TransactionState>(saleSession.SessionData);
                
                if (transactionState != null)
                {
                    // Add to active cache
                    _activeStates.TryAdd(saleSessionId, transactionState);
                    
                    _logger.LogInformation("Restored transaction state from database for session {SessionId}", saleSessionId);
                    
                    await _loggingService.LogInfoAsync(
                        $"Restored transaction state for session {saleSessionId}",
                        LogCategory.System,
                        transactionState.DeviceId);
                    
                    return transactionState;
                }
            }
            
            _logger.LogWarning("No transaction state found for session {SessionId}", saleSessionId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring transaction state for session {SessionId}", saleSessionId);
            
            await _loggingService.LogErrorAsync(
                $"Error restoring transaction state for session {saleSessionId}: {ex.Message}",
                LogCategory.System,
                Guid.NewGuid(), // Default device ID since we don't have it
                ex);
            
            return null;
        }
    }

    public async Task<List<TransactionState>> GetUnsavedTransactionStatesAsync(Guid? userId = null, Guid? deviceId = null)
    {
        try
        {
            var query = _context.SaleSessions
                .Where(ss => !ss.IsDeleted && 
                           ss.State == SessionState.Active && 
                           ss.SessionData != null);
            
            if (userId.HasValue)
            {
                query = query.Where(ss => ss.UserId == userId.Value);
            }
            
            if (deviceId.HasValue)
            {
                query = query.Where(ss => ss.DeviceId == deviceId.Value);
            }
            
            var saleSessions = await query.ToListAsync();
            var unsavedStates = new List<TransactionState>();
            
            foreach (var session in saleSessions)
            {
                try
                {
                    var transactionState = JsonSerializer.Deserialize<TransactionState>(session.SessionData!);
                    if (transactionState != null && !transactionState.IsCompleted)
                    {
                        unsavedStates.Add(transactionState);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize transaction state for session {SessionId}", session.Id);
                }
            }
            
            _logger.LogInformation("Found {Count} unsaved transaction states", unsavedStates.Count);
            return unsavedStates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unsaved transaction states");
            return new List<TransactionState>();
        }
    }

    public async Task<bool> MarkTransactionAsCompletedAsync(Guid saleSessionId)
    {
        try
        {
            // Update active cache
            if (_activeStates.TryGetValue(saleSessionId, out var cachedState))
            {
                cachedState.IsCompleted = true;
                cachedState.LastSavedAt = DateTime.UtcNow;
                
                await SaveTransactionStateInternalAsync(saleSessionId, cachedState);
            }
            
            // Update database
            var saleSession = await _context.SaleSessions
                .FirstOrDefaultAsync(ss => ss.Id == saleSessionId && !ss.IsDeleted);
            
            if (saleSession != null)
            {
                if (saleSession.SessionData != null)
                {
                    var transactionState = JsonSerializer.Deserialize<TransactionState>(saleSession.SessionData);
                    if (transactionState != null)
                    {
                        transactionState.IsCompleted = true;
                        transactionState.LastSavedAt = DateTime.UtcNow;
                        
                        saleSession.SessionData = JsonSerializer.Serialize(transactionState);
                        saleSession.State = SessionState.Completed;
                        saleSession.LastModified = DateTime.UtcNow;
                        
                        await _context.SaveChangesAsync();
                    }
                }
                
                // Stop auto-save for this session
                await StopAutoSaveAsync(saleSessionId);
                
                // Remove from active cache
                _activeStates.TryRemove(saleSessionId, out _);
                
                _logger.LogInformation("Marked transaction as completed for session {SessionId}", saleSessionId);
                
                await _loggingService.LogInfoAsync(
                    $"Marked transaction as completed for session {saleSessionId}",
                    LogCategory.System,
                    saleSession.DeviceId);
                
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking transaction as completed for session {SessionId}", saleSessionId);
            return false;
        }
    }

    public async Task<int> ClearOldTransactionStatesAsync(int olderThanDays = 7)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
            
            var oldSessions = await _context.SaleSessions
                .Where(ss => ss.LastModified < cutoffDate && 
                           (ss.State == SessionState.Completed || ss.State == SessionState.Cancelled))
                .ToListAsync();
            
            foreach (var session in oldSessions)
            {
                session.SessionData = null; // Clear the session data but keep the session record
            }
            
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Cleared transaction state data for {Count} old sessions", oldSessions.Count);
            
            await _loggingService.LogInfoAsync(
                $"Cleared transaction state data for {oldSessions.Count} old sessions",
                LogCategory.System,
                Guid.NewGuid());
            
            return oldSessions.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing old transaction states");
            return 0;
        }
    }

    public async Task<bool> StartAutoSaveAsync(Guid saleSessionId, int intervalSeconds = 30)
    {
        try
        {
            // Stop existing timer if any
            await StopAutoSaveAsync(saleSessionId);
            
            var timer = new Timer(async _ => await AutoSaveCallback(saleSessionId), 
                                null, 
                                TimeSpan.FromSeconds(intervalSeconds), 
                                TimeSpan.FromSeconds(intervalSeconds));
            
            _autoSaveTimers.TryAdd(saleSessionId, timer);
            
            _logger.LogInformation("Started auto-save for session {SessionId} with {Interval}s interval", 
                                 saleSessionId, intervalSeconds);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting auto-save for session {SessionId}", saleSessionId);
            return false;
        }
    }

    public async Task<bool> StopAutoSaveAsync(Guid saleSessionId)
    {
        try
        {
            if (_autoSaveTimers.TryRemove(saleSessionId, out var timer))
            {
                timer.Dispose();
                _logger.LogDebug("Stopped auto-save for session {SessionId}", saleSessionId);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping auto-save for session {SessionId}", saleSessionId);
            return false;
        }
    }

    private async Task<bool> SaveTransactionStateInternalAsync(Guid saleSessionId, TransactionState transactionData)
    {
        try
        {
            var saleSession = await _context.SaleSessions
                .FirstOrDefaultAsync(ss => ss.Id == saleSessionId && !ss.IsDeleted);
            
            if (saleSession == null)
            {
                _logger.LogWarning("Sale session {SessionId} not found for transaction state save", saleSessionId);
                return false;
            }
            
            // Serialize transaction state
            var serializedData = JsonSerializer.Serialize(transactionData, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            saleSession.SessionData = serializedData;
            saleSession.LastModified = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving transaction state internally for session {SessionId}", saleSessionId);
            return false;
        }
    }

    private async Task AutoSaveCallback(Guid saleSessionId)
    {
        try
        {
            if (_activeStates.TryGetValue(saleSessionId, out var transactionState))
            {
                await AutoSaveTransactionStateAsync(saleSessionId, transactionState);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in auto-save callback for session {SessionId}", saleSessionId);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Dispose all auto-save timers
            foreach (var timer in _autoSaveTimers.Values)
            {
                timer.Dispose();
            }
            _autoSaveTimers.Clear();
            
            _disposed = true;
        }
    }
}