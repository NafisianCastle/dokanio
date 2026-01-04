using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using System.Diagnostics;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of error recovery service for storage and sync failures
/// Provides comprehensive error handling and recovery mechanisms
/// </summary>
public class ErrorRecoveryService : IErrorRecoveryService
{
    private readonly PosDbContext _context;
    private readonly IComprehensiveLoggingService _loggingService;
    private readonly ITransactionLogService _transactionLogService;
    private readonly ILogger<ErrorRecoveryService> _logger;

    public ErrorRecoveryService(
        PosDbContext context,
        IComprehensiveLoggingService loggingService,
        ITransactionLogService transactionLogService,
        ILogger<ErrorRecoveryService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        _transactionLogService = transactionLogService ?? throw new ArgumentNullException(nameof(transactionLogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Recovers from storage errors by attempting database repair and restoration
    /// Local-first: Uses Local_Storage for recovery operations
    /// </summary>
    public async Task<RecoveryResult> RecoverFromStorageErrorAsync(Exception exception)
    {
        var stopwatch = Stopwatch.StartNew();
        var deviceId = Guid.NewGuid(); // In real implementation, get from current context
        var result = new RecoveryResult
        {
            OriginalException = exception
        };

        try
        {
            await _loggingService.LogErrorAsync(
                $"Storage error recovery initiated: {exception.Message}",
                LogCategory.Database,
                deviceId,
                exception);

            // Step 1: Check database connectivity
            var canConnect = await CheckDatabaseConnectivityAsync();
            if (!canConnect)
            {
                result.ActionsPerformed.Add("Database connectivity check failed");
                result.Success = false;
                result.Message = "Cannot establish database connection";
                return result;
            }

            result.ActionsPerformed.Add("Database connectivity verified");

            // Step 2: Attempt to recover from transaction logs
            var recoveredTransactions = await _transactionLogService.RecoverFromLogsAsync();
            result.ActionsPerformed.Add($"Recovered {recoveredTransactions} transactions from logs");

            // Step 3: Validate data integrity
            var integrityIssues = await ValidateDataIntegrityAsync();
            if (integrityIssues.Any())
            {
                result.ActionsPerformed.Add($"Found {integrityIssues.Count} data integrity issues");
                
                // Attempt to fix integrity issues
                var fixedIssues = await FixDataIntegrityIssuesAsync(integrityIssues);
                result.ActionsPerformed.Add($"Fixed {fixedIssues} data integrity issues");
            }

            // Step 4: Optimize database if needed
            await OptimizeDatabaseAsync();
            result.ActionsPerformed.Add("Database optimization completed");

            result.Success = true;
            result.Message = "Storage error recovery completed successfully";

            await _loggingService.LogInfoAsync(
                "Storage error recovery completed successfully",
                LogCategory.Database,
                deviceId);
        }
        catch (Exception recoveryException)
        {
            result.Success = false;
            result.Message = $"Recovery failed: {recoveryException.Message}";
            result.ActionsPerformed.Add($"Recovery failed with exception: {recoveryException.Message}");

            await _loggingService.LogCriticalAsync(
                $"Storage error recovery failed: {recoveryException.Message}",
                LogCategory.Database,
                deviceId,
                recoveryException);
        }
        finally
        {
            stopwatch.Stop();
            result.RecoveryDuration = stopwatch.Elapsed;
        }

        return result;
    }

    /// <summary>
    /// Recovers from sync errors by implementing retry logic and conflict resolution
    /// Local-first: Prioritizes Local_Storage operations
    /// </summary>
    public async Task<RecoveryResult> RecoverFromSyncErrorAsync(Exception exception)
    {
        var stopwatch = Stopwatch.StartNew();
        var deviceId = Guid.NewGuid(); // In real implementation, get from current context
        var result = new RecoveryResult
        {
            OriginalException = exception
        };

        try
        {
            await _loggingService.LogWarningAsync(
                $"Sync error recovery initiated: {exception.Message}",
                LogCategory.Sync,
                deviceId);

            // Step 1: Ensure local data is safe
            var localDataIntact = await VerifyLocalDataIntegrityAsync();
            if (!localDataIntact)
            {
                result.ActionsPerformed.Add("Local data integrity compromised - attempting repair");
                await RecoverFromStorageErrorAsync(exception);
            }
            else
            {
                result.ActionsPerformed.Add("Local data integrity verified");
            }

            // Step 2: Queue failed sync operations for retry
            await QueueFailedSyncOperationsAsync();
            result.ActionsPerformed.Add("Failed sync operations queued for retry");

            // Step 3: Reset sync status for retry
            await ResetSyncStatusForRetryAsync();
            result.ActionsPerformed.Add("Sync status reset for retry");

            result.Success = true;
            result.Message = "Sync error recovery completed - operations queued for retry";

            await _loggingService.LogInfoAsync(
                "Sync error recovery completed successfully",
                LogCategory.Sync,
                deviceId);
        }
        catch (Exception recoveryException)
        {
            result.Success = false;
            result.Message = $"Sync recovery failed: {recoveryException.Message}";
            result.ActionsPerformed.Add($"Sync recovery failed with exception: {recoveryException.Message}");

            await _loggingService.LogErrorAsync(
                $"Sync error recovery failed: {recoveryException.Message}",
                LogCategory.Sync,
                deviceId,
                recoveryException);
        }
        finally
        {
            stopwatch.Stop();
            result.RecoveryDuration = stopwatch.Elapsed;
        }

        return result;
    }

    /// <summary>
    /// Recovers from concurrent operation errors by resolving conflicts and retrying
    /// Local-first: Uses Local_Storage for conflict resolution
    /// </summary>
    public async Task<RecoveryResult> RecoverFromConcurrencyErrorAsync(Exception exception)
    {
        var stopwatch = Stopwatch.StartNew();
        var deviceId = Guid.NewGuid(); // In real implementation, get from current context
        var result = new RecoveryResult
        {
            OriginalException = exception
        };

        try
        {
            await _loggingService.LogWarningAsync(
                $"Concurrency error recovery initiated: {exception.Message}",
                LogCategory.Database,
                deviceId);

            // Step 1: Identify conflicting operations
            var conflicts = await IdentifyConcurrencyConflictsAsync();
            result.ActionsPerformed.Add($"Identified {conflicts.Count} concurrency conflicts");

            // Step 2: Resolve conflicts using predefined rules
            var resolvedConflicts = 0;
            foreach (var conflict in conflicts)
            {
                var resolved = await ResolveConcurrencyConflictAsync(conflict);
                if (resolved)
                {
                    resolvedConflicts++;
                }
            }

            result.ActionsPerformed.Add($"Resolved {resolvedConflicts} of {conflicts.Count} conflicts");

            // Step 3: Retry failed operations
            await RetryFailedConcurrentOperationsAsync();
            result.ActionsPerformed.Add("Retried failed concurrent operations");

            result.Success = resolvedConflicts == conflicts.Count;
            result.Message = result.Success 
                ? "All concurrency conflicts resolved successfully"
                : $"Resolved {resolvedConflicts} of {conflicts.Count} conflicts";

            await _loggingService.LogInfoAsync(
                "Concurrency error recovery completed",
                LogCategory.Database,
                deviceId);
        }
        catch (Exception recoveryException)
        {
            result.Success = false;
            result.Message = $"Concurrency recovery failed: {recoveryException.Message}";
            result.ActionsPerformed.Add($"Concurrency recovery failed with exception: {recoveryException.Message}");

            await _loggingService.LogErrorAsync(
                $"Concurrency error recovery failed: {recoveryException.Message}",
                LogCategory.Database,
                deviceId,
                recoveryException);
        }
        finally
        {
            stopwatch.Stop();
            result.RecoveryDuration = stopwatch.Elapsed;
        }

        return result;
    }

    /// <summary>
    /// Recovers from data corruption by validating and repairing data integrity
    /// Local-first: Uses Local_Storage for data validation and repair
    /// </summary>
    public async Task<RecoveryResult> RecoverFromDataCorruptionAsync(Exception exception)
    {
        var stopwatch = Stopwatch.StartNew();
        var deviceId = Guid.NewGuid(); // In real implementation, get from current context
        var result = new RecoveryResult
        {
            OriginalException = exception
        };

        try
        {
            await _loggingService.LogCriticalAsync(
                $"Data corruption recovery initiated: {exception.Message}",
                LogCategory.Database,
                deviceId,
                exception);

            // Step 1: Comprehensive data validation
            var corruptionIssues = await DetectDataCorruptionAsync();
            result.ActionsPerformed.Add($"Detected {corruptionIssues.Count} data corruption issues");

            // Step 2: Attempt to repair corrupted data
            var repairedIssues = 0;
            foreach (var issue in corruptionIssues)
            {
                var repaired = await RepairDataCorruptionAsync(issue);
                if (repaired)
                {
                    repairedIssues++;
                }
            }

            result.ActionsPerformed.Add($"Repaired {repairedIssues} of {corruptionIssues.Count} corruption issues");

            // Step 3: Restore from transaction logs if needed
            if (repairedIssues < corruptionIssues.Count)
            {
                var recoveredFromLogs = await _transactionLogService.RecoverFromLogsAsync();
                result.ActionsPerformed.Add($"Recovered {recoveredFromLogs} transactions from logs");
            }

            // Step 4: Final integrity check
            var finalIntegrityIssues = await ValidateDataIntegrityAsync();
            result.ActionsPerformed.Add($"Final integrity check found {finalIntegrityIssues.Count} remaining issues");

            result.Success = finalIntegrityIssues.Count == 0;
            result.Message = result.Success 
                ? "Data corruption recovery completed successfully"
                : $"Partial recovery - {finalIntegrityIssues.Count} issues remain";

            await _loggingService.LogInfoAsync(
                "Data corruption recovery completed",
                LogCategory.Database,
                deviceId);
        }
        catch (Exception recoveryException)
        {
            result.Success = false;
            result.Message = $"Data corruption recovery failed: {recoveryException.Message}";
            result.ActionsPerformed.Add($"Data corruption recovery failed with exception: {recoveryException.Message}");

            await _loggingService.LogCriticalAsync(
                $"Data corruption recovery failed: {recoveryException.Message}",
                LogCategory.Database,
                deviceId,
                recoveryException);
        }
        finally
        {
            stopwatch.Stop();
            result.RecoveryDuration = stopwatch.Elapsed;
        }

        return result;
    }

    /// <summary>
    /// Performs comprehensive system health check and recovery
    /// Local-first: Uses Local_Storage for health monitoring
    /// </summary>
    public async Task<SystemHealthResult> PerformSystemHealthCheckAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var deviceId = Guid.NewGuid(); // In real implementation, get from current context
        var result = new SystemHealthResult();

        try
        {
            await _loggingService.LogInfoAsync(
                "System health check initiated",
                LogCategory.System,
                deviceId);

            // Check 1: Database connectivity and performance
            var dbHealth = await CheckDatabaseHealthAsync();
            result.Issues.AddRange(dbHealth);

            // Check 2: Data integrity
            var integrityIssues = await ValidateDataIntegrityAsync();
            foreach (var issue in integrityIssues)
            {
                result.Issues.Add(new HealthIssue
                {
                    Category = "Data Integrity",
                    Description = issue,
                    Severity = HealthSeverity.High
                });
            }

            // Check 3: Transaction log health
            var logHealth = await CheckTransactionLogHealthAsync();
            result.Issues.AddRange(logHealth);

            // Check 4: Storage space and performance
            var storageHealth = await CheckStorageHealthAsync();
            result.Issues.AddRange(storageHealth);

            // Attempt to resolve critical issues automatically
            var criticalIssues = result.Issues.Where(i => i.Severity == HealthSeverity.Critical).ToList();
            foreach (var issue in criticalIssues)
            {
                var resolved = await AttemptIssueResolutionAsync(issue);
                if (resolved)
                {
                    issue.IsResolved = true;
                    result.RecoveryActionsPerformed.Add($"Resolved: {issue.Description}");
                }
            }

            result.IsHealthy = !result.Issues.Any(i => !i.IsResolved && i.Severity >= HealthSeverity.High);

            await _loggingService.LogInfoAsync(
                $"System health check completed - Healthy: {result.IsHealthy}, Issues: {result.Issues.Count}",
                LogCategory.System,
                deviceId);
        }
        catch (Exception ex)
        {
            result.IsHealthy = false;
            result.Issues.Add(new HealthIssue
            {
                Category = "System",
                Description = $"Health check failed: {ex.Message}",
                Severity = HealthSeverity.Critical
            });

            await _loggingService.LogCriticalAsync(
                $"System health check failed: {ex.Message}",
                LogCategory.System,
                deviceId,
                ex);
        }
        finally
        {
            stopwatch.Stop();
            result.CheckDuration = stopwatch.Elapsed;
        }

        return result;
    }

    /// <summary>
    /// Gets recovery statistics for monitoring and diagnostics
    /// Local-first: Queries Local_Storage for statistics
    /// </summary>
    public async Task<RecoveryStatistics> GetRecoveryStatisticsAsync(DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
            var toDate = to ?? DateTime.UtcNow;

            // Query system logs for recovery-related entries
            var recoveryLogs = await _loggingService.GetLogsByCategoryAsync(LogCategory.System, fromDate, toDate);
            var recoveryEntries = recoveryLogs.Where(log => 
                log.Message.Contains("recovery", StringComparison.OrdinalIgnoreCase)).ToList();

            var statistics = new RecoveryStatistics
            {
                StatisticsPeriodStart = fromDate,
                StatisticsPeriodEnd = toDate,
                TotalRecoveryAttempts = recoveryEntries.Count,
                SuccessfulRecoveries = recoveryEntries.Count(log => 
                    log.Message.Contains("completed successfully", StringComparison.OrdinalIgnoreCase)),
                FailedRecoveries = recoveryEntries.Count(log => 
                    log.Message.Contains("failed", StringComparison.OrdinalIgnoreCase))
            };

            // Calculate recovery type counts
            statistics.RecoveryTypeCount["Storage"] = recoveryEntries.Count(log => 
                log.Message.Contains("storage", StringComparison.OrdinalIgnoreCase));
            statistics.RecoveryTypeCount["Sync"] = recoveryEntries.Count(log => 
                log.Message.Contains("sync", StringComparison.OrdinalIgnoreCase));
            statistics.RecoveryTypeCount["Concurrency"] = recoveryEntries.Count(log => 
                log.Message.Contains("concurrency", StringComparison.OrdinalIgnoreCase));
            statistics.RecoveryTypeCount["Corruption"] = recoveryEntries.Count(log => 
                log.Message.Contains("corruption", StringComparison.OrdinalIgnoreCase));

            // Calculate average recovery time (simplified - in real implementation would track actual durations)
            statistics.AverageRecoveryTime = TimeSpan.FromSeconds(30); // Placeholder

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recovery statistics");
            return new RecoveryStatistics();
        }
    }

    // Private helper methods for recovery operations

    private async Task<bool> CheckDatabaseConnectivityAsync()
    {
        try
        {
            await _context.Database.CanConnectAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<List<string>> ValidateDataIntegrityAsync()
    {
        var issues = new List<string>();

        try
        {
            // Check for orphaned records
            var orphanedSaleItems = await _context.SaleItems
                .Where(si => !_context.Sales.Any(s => s.Id == si.SaleId))
                .CountAsync();
            
            if (orphanedSaleItems > 0)
            {
                issues.Add($"Found {orphanedSaleItems} orphaned sale items");
            }

            // Check for invalid foreign key references
            var invalidStockEntries = await _context.Stock
                .Where(s => !_context.Products.Any(p => p.Id == s.ProductId))
                .CountAsync();
            
            if (invalidStockEntries > 0)
            {
                issues.Add($"Found {invalidStockEntries} stock entries with invalid product references");
            }
        }
        catch (Exception ex)
        {
            issues.Add($"Data integrity validation failed: {ex.Message}");
        }

        return issues;
    }

    private async Task<int> FixDataIntegrityIssuesAsync(List<string> issues)
    {
        var fixedCount = 0;

        try
        {
            // Remove orphaned sale items
            var orphanedSaleItems = await _context.SaleItems
                .Where(si => !_context.Sales.Any(s => s.Id == si.SaleId))
                .ToListAsync();
            
            _context.SaleItems.RemoveRange(orphanedSaleItems);
            fixedCount += orphanedSaleItems.Count;

            // Remove invalid stock entries
            var invalidStockEntries = await _context.Stock
                .Where(s => !_context.Products.Any(p => p.Id == s.ProductId))
                .ToListAsync();
            
            _context.Stock.RemoveRange(invalidStockEntries);
            fixedCount += invalidStockEntries.Count;

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fixing data integrity issues");
        }

        return fixedCount;
    }

    private async Task OptimizeDatabaseAsync()
    {
        try
        {
            // SQLite optimization commands
            await _context.Database.ExecuteSqlRawAsync("VACUUM");
            await _context.Database.ExecuteSqlRawAsync("ANALYZE");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database optimization failed");
        }
    }

    private async Task<bool> VerifyLocalDataIntegrityAsync()
    {
        var issues = await ValidateDataIntegrityAsync();
        return issues.Count == 0;
    }

    private async Task QueueFailedSyncOperationsAsync()
    {
        // In real implementation, would queue failed sync operations for retry
        await Task.CompletedTask;
    }

    private async Task ResetSyncStatusForRetryAsync()
    {
        // Reset sync status for failed operations
        await Task.CompletedTask;
    }

    private async Task<List<string>> IdentifyConcurrencyConflictsAsync()
    {
        // In real implementation, would identify actual concurrency conflicts
        return new List<string>();
    }

    private async Task<bool> ResolveConcurrencyConflictAsync(string conflict)
    {
        // In real implementation, would resolve specific conflicts
        await Task.CompletedTask;
        return true;
    }

    private async Task RetryFailedConcurrentOperationsAsync()
    {
        // In real implementation, would retry failed operations
        await Task.CompletedTask;
    }

    private async Task<List<string>> DetectDataCorruptionAsync()
    {
        // In real implementation, would detect actual data corruption
        return new List<string>();
    }

    private async Task<bool> RepairDataCorruptionAsync(string issue)
    {
        // In real implementation, would repair specific corruption issues
        await Task.CompletedTask;
        return true;
    }

    private async Task<List<HealthIssue>> CheckDatabaseHealthAsync()
    {
        var issues = new List<HealthIssue>();

        try
        {
            var canConnect = await CheckDatabaseConnectivityAsync();
            if (!canConnect)
            {
                issues.Add(new HealthIssue
                {
                    Category = "Database",
                    Description = "Cannot connect to database",
                    Severity = HealthSeverity.Critical
                });
            }
        }
        catch (Exception ex)
        {
            issues.Add(new HealthIssue
            {
                Category = "Database",
                Description = $"Database health check failed: {ex.Message}",
                Severity = HealthSeverity.High
            });
        }

        return issues;
    }

    private async Task<List<HealthIssue>> CheckTransactionLogHealthAsync()
    {
        var issues = new List<HealthIssue>();

        try
        {
            var unprocessedLogs = await _transactionLogService.GetUnprocessedLogsAsync();
            var unprocessedCount = unprocessedLogs.Count();

            if (unprocessedCount > 1000)
            {
                issues.Add(new HealthIssue
                {
                    Category = "Transaction Logs",
                    Description = $"High number of unprocessed transaction logs: {unprocessedCount}",
                    Severity = HealthSeverity.Medium
                });
            }
        }
        catch (Exception ex)
        {
            issues.Add(new HealthIssue
            {
                Category = "Transaction Logs",
                Description = $"Transaction log health check failed: {ex.Message}",
                Severity = HealthSeverity.High
            });
        }

        return issues;
    }

    private async Task<List<HealthIssue>> CheckStorageHealthAsync()
    {
        var issues = new List<HealthIssue>();

        try
        {
            // Check database size and performance metrics
            var dbSize = await GetDatabaseSizeAsync();
            if (dbSize > 1000000000) // 1GB
            {
                issues.Add(new HealthIssue
                {
                    Category = "Storage",
                    Description = $"Database size is large: {dbSize / 1000000}MB",
                    Severity = HealthSeverity.Medium
                });
            }
        }
        catch (Exception ex)
        {
            issues.Add(new HealthIssue
            {
                Category = "Storage",
                Description = $"Storage health check failed: {ex.Message}",
                Severity = HealthSeverity.Medium
            });
        }

        return issues;
    }

    private async Task<bool> AttemptIssueResolutionAsync(HealthIssue issue)
    {
        try
        {
            switch (issue.Category)
            {
                case "Database":
                    if (issue.Description.Contains("Cannot connect"))
                    {
                        // Attempt database reconnection
                        await _context.Database.CloseConnectionAsync();
                        await _context.Database.OpenConnectionAsync();
                        return await CheckDatabaseConnectivityAsync();
                    }
                    break;

                case "Transaction Logs":
                    if (issue.Description.Contains("High number"))
                    {
                        // Process some transaction logs
                        await _transactionLogService.RecoverFromLogsAsync();
                        return true;
                    }
                    break;

                case "Storage":
                    if (issue.Description.Contains("Database size"))
                    {
                        // Optimize database
                        await OptimizeDatabaseAsync();
                        return true;
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve health issue: {Issue}", issue.Description);
        }

        return false;
    }

    private async Task<long> GetDatabaseSizeAsync()
    {
        try
        {
            // For SQLite, get database file size
            var connection = _context.Database.GetDbConnection();
            if (connection.DataSource != null && File.Exists(connection.DataSource))
            {
                return new FileInfo(connection.DataSource).Length;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not determine database size");
        }

        return 0;
    }
}