using Microsoft.Extensions.Logging;
using Shared.Core.Services;

namespace WebDashboard.Services;

/// <summary>
/// Global exception handler service for the WebDashboard application
/// Provides comprehensive exception handling with user-friendly error notifications for Blazor
/// </summary>
public class GlobalExceptionHandlerService
{
    private readonly IGlobalExceptionHandler _globalExceptionHandler;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GlobalExceptionHandlerService> _logger;

    public GlobalExceptionHandlerService(
        IGlobalExceptionHandler globalExceptionHandler,
        ICurrentUserService currentUserService,
        ILogger<GlobalExceptionHandlerService> logger)
    {
        _globalExceptionHandler = globalExceptionHandler ?? throw new ArgumentNullException(nameof(globalExceptionHandler));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles unhandled exceptions in the web dashboard application
    /// </summary>
    /// <param name="exception">The unhandled exception</param>
    /// <param name="context">Context where the exception occurred</param>
    /// <returns>Error information for display in the UI</returns>
    public async Task<WebErrorInfo> HandleUnhandledExceptionAsync(Exception exception, string context = "Web Dashboard")
    {
        try
        {
            _logger.LogError(exception, "Unhandled exception in web dashboard: {Context}", context);

            // Get device and user context
            var deviceId = _currentUserService.GetDeviceId();
            var userId = _currentUserService.GetUserId();

            // Use comprehensive exception handler
            var errorResponse = await _globalExceptionHandler.HandleExceptionAsync(
                exception, 
                context, 
                deviceId, 
                userId);

            // Attempt automatic recovery for recoverable exceptions
            RecoveryResult? recoveryResult = null;
            if (errorResponse.RecoveryAction?.IsAutomatic == true)
            {
                recoveryResult = await _globalExceptionHandler.AttemptAutomaticRecoveryAsync(
                    exception, 
                    context, 
                    deviceId);

                if (recoveryResult.Success)
                {
                    _logger.LogInformation("Automatic recovery successful for exception in {Context}", context);
                }
                else
                {
                    _logger.LogWarning("Automatic recovery failed for exception in {Context}: {RecoveryMessage}", 
                        context, recoveryResult.Message);
                }
            }

            // Return web-friendly error information
            return new WebErrorInfo
            {
                Title = GetErrorTitle(errorResponse),
                Message = errorResponse.Message,
                DetailedMessage = errorResponse.DetailedMessage,
                ErrorCode = errorResponse.ErrorCode,
                Timestamp = errorResponse.Timestamp,
                Severity = DetermineWebSeverity(errorResponse),
                IsRecoverable = errorResponse.RecoveryAction != null,
                RecoveryAction = errorResponse.RecoveryAction,
                RecoveryResult = recoveryResult,
                ShowToUser = true
            };
        }
        catch (Exception handlingException)
        {
            // Fallback error handling
            _logger.LogCritical(handlingException, "Exception handler failed while processing exception: {OriginalException}", exception.Message);
            
            return new WebErrorInfo
            {
                Title = "Critical Error",
                Message = "A critical error occurred while processing your request.",
                DetailedMessage = "The error handling system encountered an issue. Please refresh the page and try again.",
                ErrorCode = "HANDLER_FAILURE",
                Timestamp = DateTime.UtcNow,
                Severity = WebErrorSeverity.Critical,
                IsRecoverable = false,
                ShowToUser = true
            };
        }
    }

    /// <summary>
    /// Handles exceptions that occur in Blazor components
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="componentName">Name of the component where the exception occurred</param>
    /// <returns>Error information for display in the component</returns>
    public async Task<WebErrorInfo> HandleComponentExceptionAsync(Exception exception, string componentName)
    {
        var context = $"Blazor Component: {componentName}";
        return await HandleUnhandledExceptionAsync(exception, context);
    }

    /// <summary>
    /// Handles exceptions that occur during API calls
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="apiEndpoint">The API endpoint that failed</param>
    /// <returns>Error information for display in the UI</returns>
    public async Task<WebErrorInfo> HandleApiExceptionAsync(Exception exception, string apiEndpoint)
    {
        var context = $"API Call: {apiEndpoint}";
        return await HandleUnhandledExceptionAsync(exception, context);
    }

    private static string GetErrorTitle(ErrorResponse errorResponse)
    {
        return errorResponse.ErrorCode switch
        {
            var code when code.StartsWith("DBUPDATE") => "Data Save Error",
            var code when code.StartsWith("DBUPDATE_CONCURRENCY") => "Data Conflict",
            var code when code.StartsWith("HTTPREQUEST") => "Connection Error",
            var code when code.StartsWith("TIMEOUT") => "Operation Timeout",
            var code when code.StartsWith("UNAUTHORIZED") => "Access Denied",
            var code when code.StartsWith("ARGUMENT") => "Invalid Input",
            var code when code.StartsWith("FILENOTFOUND") => "File Not Found",
            var code when code.StartsWith("OUTOFMEMORY") => "Memory Error",
            var code when code.StartsWith("NOTIMPLEMENTED") => "Feature Unavailable",
            _ => "Unexpected Error"
        };
    }

    private static WebErrorSeverity DetermineWebSeverity(ErrorResponse errorResponse)
    {
        return errorResponse.StatusCode switch
        {
            >= 500 => WebErrorSeverity.Critical,
            >= 400 and < 500 => WebErrorSeverity.High,
            _ => WebErrorSeverity.Medium
        };
    }
}

/// <summary>
/// Web-specific error information for display in Blazor components
/// </summary>
public class WebErrorInfo
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? DetailedMessage { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public WebErrorSeverity Severity { get; set; }
    public bool IsRecoverable { get; set; }
    public bool ShowToUser { get; set; }
    public RecoveryAction? RecoveryAction { get; set; }
    public RecoveryResult? RecoveryResult { get; set; }
}

/// <summary>
/// Web-specific error severity levels
/// </summary>
public enum WebErrorSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}