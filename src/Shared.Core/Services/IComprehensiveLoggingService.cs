using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Service for comprehensive logging throughout all system layers
/// Implements structured logging with different severity levels and categories
/// </summary>
public interface IComprehensiveLoggingService
{
    /// <summary>
    /// Logs a debug message for development and troubleshooting
    /// </summary>
    /// <param name="message">The debug message</param>
    /// <param name="category">The logging category</param>
    /// <param name="deviceId">Device performing the operation</param>
    /// <param name="userId">User performing the operation (if applicable)</param>
    /// <param name="additionalData">Additional structured data</param>
    Task LogDebugAsync(string message, LogCategory category, Guid deviceId, Guid? userId = null, object? additionalData = null);
    
    /// <summary>
    /// Logs an informational message for normal operations
    /// </summary>
    /// <param name="message">The informational message</param>
    /// <param name="category">The logging category</param>
    /// <param name="deviceId">Device performing the operation</param>
    /// <param name="userId">User performing the operation (if applicable)</param>
    /// <param name="additionalData">Additional structured data</param>
    Task LogInfoAsync(string message, LogCategory category, Guid deviceId, Guid? userId = null, object? additionalData = null);
    
    /// <summary>
    /// Logs a warning message for potential issues
    /// </summary>
    /// <param name="message">The warning message</param>
    /// <param name="category">The logging category</param>
    /// <param name="deviceId">Device performing the operation</param>
    /// <param name="userId">User performing the operation (if applicable)</param>
    /// <param name="additionalData">Additional structured data</param>
    Task LogWarningAsync(string message, LogCategory category, Guid deviceId, Guid? userId = null, object? additionalData = null);
    
    /// <summary>
    /// Logs an error message for system errors
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="category">The logging category</param>
    /// <param name="deviceId">Device performing the operation</param>
    /// <param name="exception">The exception that occurred (if applicable)</param>
    /// <param name="userId">User performing the operation (if applicable)</param>
    /// <param name="additionalData">Additional structured data</param>
    Task LogErrorAsync(string message, LogCategory category, Guid deviceId, Exception? exception = null, Guid? userId = null, object? additionalData = null);
    
    /// <summary>
    /// Logs a critical error message for system-critical failures
    /// </summary>
    /// <param name="message">The critical error message</param>
    /// <param name="category">The logging category</param>
    /// <param name="deviceId">Device performing the operation</param>
    /// <param name="exception">The exception that occurred (if applicable)</param>
    /// <param name="userId">User performing the operation (if applicable)</param>
    /// <param name="additionalData">Additional structured data</param>
    Task LogCriticalAsync(string message, LogCategory category, Guid deviceId, Exception? exception = null, Guid? userId = null, object? additionalData = null);
    
    /// <summary>
    /// Gets log entries by category within a date range
    /// </summary>
    /// <param name="category">The logging category</param>
    /// <param name="from">Start date</param>
    /// <param name="to">End date</param>
    /// <returns>Collection of log entries</returns>
    Task<IEnumerable<SystemLogEntry>> GetLogsByCategoryAsync(LogCategory category, DateTime? from = null, DateTime? to = null);
    
    /// <summary>
    /// Gets log entries by severity level within a date range
    /// </summary>
    /// <param name="level">The log level</param>
    /// <param name="from">Start date</param>
    /// <param name="to">End date</param>
    /// <returns>Collection of log entries</returns>
    Task<IEnumerable<SystemLogEntry>> GetLogsByLevelAsync(LogLevel level, DateTime? from = null, DateTime? to = null);
    
    /// <summary>
    /// Gets all log entries within a date range
    /// </summary>
    /// <param name="from">Start date</param>
    /// <param name="to">End date</param>
    /// <returns>Collection of log entries</returns>
    Task<IEnumerable<SystemLogEntry>> GetLogsAsync(DateTime? from = null, DateTime? to = null);
    
    /// <summary>
    /// Gets error and critical log entries for system health monitoring
    /// </summary>
    /// <param name="from">Start date</param>
    /// <param name="to">End date</param>
    /// <returns>Collection of error log entries</returns>
    Task<IEnumerable<SystemLogEntry>> GetErrorLogsAsync(DateTime? from = null, DateTime? to = null);
}

/// <summary>
/// Represents a comprehensive system log entry
/// </summary>
public class SystemLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public LogLevel Level { get; set; }
    public LogCategory Category { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid DeviceId { get; set; }
    public Guid? UserId { get; set; }
    public string? ExceptionDetails { get; set; }
    public string? AdditionalData { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Log severity levels
/// </summary>
public enum LogLevel
{
    Debug = 0,
    Information = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}

/// <summary>
/// Log categories for different system layers
/// </summary>
public enum LogCategory
{
    Database = 0,
    Sync = 1,
    Hardware = 2,
    Security = 3,
    Business = 4,
    UI = 5,
    Performance = 6,
    System = 7
}