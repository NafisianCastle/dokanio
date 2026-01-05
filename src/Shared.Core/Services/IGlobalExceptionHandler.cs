using Shared.Core.Services;

namespace Shared.Core.Services;

/// <summary>
/// Service for comprehensive global exception handling across all applications
/// Provides user-friendly error conversion, structured logging, and recovery suggestions
/// </summary>
public interface IGlobalExceptionHandler
{
    /// <summary>
    /// Handles an exception with comprehensive logging and user-friendly error conversion
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="context">Context information about where the exception occurred</param>
    /// <param name="deviceId">Device ID where the exception occurred</param>
    /// <param name="userId">User ID if available</param>
    /// <returns>Error response with user-friendly message and recovery suggestions</returns>
    Task<ErrorResponse> HandleExceptionAsync(Exception exception, string context, Guid deviceId, Guid? userId = null);
    
    /// <summary>
    /// Converts a technical exception to a user-friendly error message
    /// </summary>
    /// <param name="exception">The exception to convert</param>
    /// <returns>User-friendly error information</returns>
    Task<UserFriendlyError> ConvertToUserFriendlyErrorAsync(Exception exception);
    
    /// <summary>
    /// Logs an exception with structured context information
    /// </summary>
    /// <param name="exception">The exception to log</param>
    /// <param name="context">Context information</param>
    /// <param name="deviceId">Device ID where the exception occurred</param>
    /// <param name="userId">User ID if available</param>
    /// <param name="metadata">Additional metadata</param>
    /// <returns>Task representing the logging operation</returns>
    Task LogExceptionAsync(Exception exception, string context, Guid deviceId, Guid? userId = null, Dictionary<string, object>? metadata = null);
    
    /// <summary>
    /// Suggests recovery actions for a given exception
    /// </summary>
    /// <param name="exception">The exception to analyze</param>
    /// <param name="context">Context where the exception occurred</param>
    /// <returns>Recovery action suggestions</returns>
    Task<RecoveryAction> SuggestRecoveryActionAsync(Exception exception, string context);
    
    /// <summary>
    /// Attempts automatic recovery for recoverable exceptions
    /// </summary>
    /// <param name="exception">The exception to recover from</param>
    /// <param name="context">Context information</param>
    /// <param name="deviceId">Device ID</param>
    /// <returns>Recovery result</returns>
    Task<RecoveryResult> AttemptAutomaticRecoveryAsync(Exception exception, string context, Guid deviceId);
}

/// <summary>
/// Represents a comprehensive error response
/// </summary>
public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? DetailedMessage { get; set; }
    public string? TraceId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public RecoveryAction? RecoveryAction { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents user-friendly error information
/// </summary>
public class UserFriendlyError
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? DetailedExplanation { get; set; }
    public ErrorSeverity Severity { get; set; }
    public List<string> PossibleCauses { get; set; } = new();
    public List<string> UserActions { get; set; } = new();
    public bool IsRecoverable { get; set; }
    public string? HelpUrl { get; set; }
}

/// <summary>
/// Represents recovery action suggestions
/// </summary>
public class RecoveryAction
{
    public string ActionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = new();
    public bool IsAutomatic { get; set; }
    public bool RequiresUserConfirmation { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Error severity levels for user-friendly errors
/// </summary>
public enum ErrorSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}