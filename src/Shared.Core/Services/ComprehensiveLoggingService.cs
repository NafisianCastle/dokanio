using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of comprehensive logging service for all system layers
/// Provides structured logging with different severity levels and categories
/// </summary>
public class ComprehensiveLoggingService : IComprehensiveLoggingService
{
    private readonly PosDbContext _context;
    private readonly ILogger<ComprehensiveLoggingService> _logger;

    public ComprehensiveLoggingService(PosDbContext context, ILogger<ComprehensiveLoggingService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Logs a debug message for development and troubleshooting
    /// Local-first: Logs to Local_Storage immediately for offline operation support
    /// </summary>
    public async Task LogDebugAsync(string message, LogCategory category, Guid deviceId, Guid? userId = null, object? additionalData = null)
    {
        await LogAsync(LogLevel.Debug, message, category, deviceId, null, userId, additionalData);
    }

    /// <summary>
    /// Logs an informational message for normal operations
    /// Local-first: Logs to Local_Storage immediately for offline operation support
    /// </summary>
    public async Task LogInfoAsync(string message, LogCategory category, Guid deviceId, Guid? userId = null, object? additionalData = null)
    {
        await LogAsync(LogLevel.Information, message, category, deviceId, null, userId, additionalData);
    }

    /// <summary>
    /// Logs a warning message for potential issues
    /// Local-first: Logs to Local_Storage immediately for offline operation support
    /// </summary>
    public async Task LogWarningAsync(string message, LogCategory category, Guid deviceId, Guid? userId = null, object? additionalData = null)
    {
        await LogAsync(LogLevel.Warning, message, category, deviceId, null, userId, additionalData);
    }

    /// <summary>
    /// Logs an error message for system errors
    /// Local-first: Logs to Local_Storage immediately for offline operation support
    /// </summary>
    public async Task LogErrorAsync(string message, LogCategory category, Guid deviceId, Exception? exception = null, Guid? userId = null, object? additionalData = null)
    {
        await LogAsync(LogLevel.Error, message, category, deviceId, exception, userId, additionalData);
    }

    /// <summary>
    /// Logs a critical error message for system-critical failures
    /// Local-first: Logs to Local_Storage immediately for offline operation support
    /// </summary>
    public async Task LogCriticalAsync(string message, LogCategory category, Guid deviceId, Exception? exception = null, Guid? userId = null, object? additionalData = null)
    {
        await LogAsync(LogLevel.Critical, message, category, deviceId, exception, userId, additionalData);
    }

    /// <summary>
    /// Core logging method that persists to Local_Storage
    /// Local-first: All logs are stored locally immediately for offline operation support
    /// </summary>
    private async Task LogAsync(LogLevel level, string message, LogCategory category, Guid deviceId, Exception? exception = null, Guid? userId = null, object? additionalData = null)
    {
        try
        {
            var logEntry = new SystemLogEntry
            {
                Level = level,
                Category = category,
                Message = message,
                DeviceId = deviceId,
                UserId = userId,
                ExceptionDetails = exception?.ToString(),
                AdditionalData = additionalData != null ? JsonSerializer.Serialize(additionalData) : null,
                CreatedAt = DateTime.UtcNow
            };

            // Local-first: Log to Local_Storage immediately
            _context.SystemLogs.Add(logEntry);
            await _context.SaveChangesAsync();

            // Also log to Microsoft.Extensions.Logging for development/debugging
            var logLevel = level switch
            {
                Services.LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
                Services.LogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
                Services.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
                Services.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
                Services.LogLevel.Critical => Microsoft.Extensions.Logging.LogLevel.Critical,
                _ => Microsoft.Extensions.Logging.LogLevel.Information
            };

            _logger.Log(logLevel, exception, "[{Category}] {Message}", category, message);
        }
        catch (Exception ex)
        {
            // Fallback to Microsoft.Extensions.Logging if database logging fails
            _logger.LogError(ex, "Failed to log message to database: {Message}", message);
        }
    }

    /// <summary>
    /// Gets log entries by category within a date range
    /// Local-first: Queries Local_Storage only for offline operation support
    /// </summary>
    public async Task<IEnumerable<SystemLogEntry>> GetLogsByCategoryAsync(LogCategory category, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var query = _context.SystemLogs.Where(log => log.Category == category);

            if (from.HasValue)
            {
                query = query.Where(log => log.CreatedAt >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(log => log.CreatedAt <= to.Value);
            }

            return await query.OrderByDescending(log => log.CreatedAt).ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs by category {Category}", category);
            return Enumerable.Empty<SystemLogEntry>();
        }
    }

    /// <summary>
    /// Gets log entries by severity level within a date range
    /// Local-first: Queries Local_Storage only for offline operation support
    /// </summary>
    public async Task<IEnumerable<SystemLogEntry>> GetLogsByLevelAsync(LogLevel level, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var query = _context.SystemLogs.Where(log => log.Level == level);

            if (from.HasValue)
            {
                query = query.Where(log => log.CreatedAt >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(log => log.CreatedAt <= to.Value);
            }

            return await query.OrderByDescending(log => log.CreatedAt).ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs by level {Level}", level);
            return Enumerable.Empty<SystemLogEntry>();
        }
    }

    /// <summary>
    /// Gets all log entries within a date range
    /// Local-first: Queries Local_Storage only for offline operation support
    /// </summary>
    public async Task<IEnumerable<SystemLogEntry>> GetLogsAsync(DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var query = _context.SystemLogs.AsQueryable();

            if (from.HasValue)
            {
                query = query.Where(log => log.CreatedAt >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(log => log.CreatedAt <= to.Value);
            }

            return await query.OrderByDescending(log => log.CreatedAt).ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all logs");
            return Enumerable.Empty<SystemLogEntry>();
        }
    }

    /// <summary>
    /// Gets error and critical log entries for system health monitoring
    /// Local-first: Queries Local_Storage only for offline operation support
    /// </summary>
    public async Task<IEnumerable<SystemLogEntry>> GetErrorLogsAsync(DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var query = _context.SystemLogs.Where(log => log.Level == LogLevel.Error || log.Level == LogLevel.Critical);

            if (from.HasValue)
            {
                query = query.Where(log => log.CreatedAt >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(log => log.CreatedAt <= to.Value);
            }

            return await query.OrderByDescending(log => log.CreatedAt).ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving error logs");
            return Enumerable.Empty<SystemLogEntry>();
        }
    }
}