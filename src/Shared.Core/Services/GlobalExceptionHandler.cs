using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of comprehensive global exception handling service
/// Provides user-friendly error conversion, structured logging, and recovery suggestions
/// </summary>
public class GlobalExceptionHandler : IGlobalExceptionHandler
{
    private readonly IComprehensiveLoggingService _loggingService;
    private readonly IErrorRecoveryService _errorRecoveryService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IComprehensiveLoggingService loggingService,
        IErrorRecoveryService errorRecoveryService,
        ILogger<GlobalExceptionHandler> logger)
    {
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        _errorRecoveryService = errorRecoveryService ?? throw new ArgumentNullException(nameof(errorRecoveryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles an exception with comprehensive logging and user-friendly error conversion
    /// </summary>
    public async Task<ErrorResponse> HandleExceptionAsync(Exception exception, string context, Guid deviceId, Guid? userId = null)
    {
        try
        {
            // Log the exception with structured context
            await LogExceptionAsync(exception, context, deviceId, userId);

            // Convert to user-friendly error
            var userFriendlyError = await ConvertToUserFriendlyErrorAsync(exception);

            // Suggest recovery actions
            var recoveryAction = await SuggestRecoveryActionAsync(exception, context);

            // Determine HTTP status code
            var statusCode = DetermineStatusCode(exception);

            // Create comprehensive error response
            var errorResponse = new ErrorResponse
            {
                StatusCode = statusCode,
                ErrorCode = GenerateErrorCode(exception),
                Message = userFriendlyError.Message,
                DetailedMessage = userFriendlyError.DetailedExplanation,
                RecoveryAction = recoveryAction,
                Metadata = new Dictionary<string, object>
                {
                    ["ExceptionType"] = exception.GetType().Name,
                    ["Context"] = context,
                    ["DeviceId"] = deviceId,
                    ["Severity"] = userFriendlyError.Severity.ToString(),
                    ["IsRecoverable"] = userFriendlyError.IsRecoverable
                }
            };

            if (userId.HasValue)
            {
                errorResponse.Metadata["UserId"] = userId.Value;
            }

            return errorResponse;
        }
        catch (Exception handlingException)
        {
            // Fallback error handling if the exception handler itself fails
            _logger.LogCritical(handlingException, "Exception handler failed while processing exception: {OriginalException}", exception.Message);
            
            return new ErrorResponse
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                ErrorCode = "HANDLER_FAILURE",
                Message = "An unexpected error occurred while processing your request.",
                DetailedMessage = "The error handling system encountered an issue. Please contact support.",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Converts a technical exception to a user-friendly error message
    /// </summary>
    public async Task<UserFriendlyError> ConvertToUserFriendlyErrorAsync(Exception exception)
    {
        await Task.CompletedTask; // For async consistency

        return exception switch
        {
            // Database-related exceptions (more specific first)
            DbUpdateConcurrencyException => new UserFriendlyError
            {
                Title = "Data Conflict",
                Message = "The data you're trying to save has been modified by another user.",
                DetailedExplanation = "Someone else has updated this information while you were working on it. Please refresh and try again.",
                Severity = ErrorSeverity.Medium,
                PossibleCauses = new List<string>
                {
                    "Another user modified the same data",
                    "Multiple devices accessing the same record",
                    "Synchronization conflicts"
                },
                UserActions = new List<string>
                {
                    "Refresh the page or screen",
                    "Re-enter your changes",
                    "Coordinate with other users if working on shared data"
                },
                IsRecoverable = true
            },

            DbUpdateException => new UserFriendlyError
            {
                Title = "Data Save Error",
                Message = "Unable to save your changes to the database.",
                DetailedExplanation = "The system encountered an issue while trying to save your data. This might be due to a connection problem or data validation issue.",
                Severity = ErrorSeverity.High,
                PossibleCauses = new List<string>
                {
                    "Network connectivity issues",
                    "Database is temporarily unavailable",
                    "Data validation constraints",
                    "Insufficient storage space"
                },
                UserActions = new List<string>
                {
                    "Check your network connection",
                    "Try saving again in a few moments",
                    "Verify that all required fields are filled correctly",
                    "Contact support if the problem persists"
                },
                IsRecoverable = true
            },

            InvalidOperationException invalidOpEx when invalidOpEx.Message.Contains("database", StringComparison.OrdinalIgnoreCase) => new UserFriendlyError
            {
                Title = "Database Operation Error",
                Message = "Unable to complete the database operation.",
                DetailedExplanation = "The system cannot perform the requested operation on the database at this time.",
                Severity = ErrorSeverity.High,
                PossibleCauses = new List<string>
                {
                    "Database connection issues",
                    "Invalid operation sequence",
                    "Database is in maintenance mode"
                },
                UserActions = new List<string>
                {
                    "Wait a moment and try again",
                    "Check your network connection",
                    "Contact support if the issue continues"
                },
                IsRecoverable = true
            },

            // Network and connectivity exceptions
            HttpRequestException httpEx => new UserFriendlyError
            {
                Title = "Network Connection Error",
                Message = "Unable to connect to the server.",
                DetailedExplanation = "The application cannot reach the server. This might be due to network connectivity issues or server maintenance.",
                Severity = ErrorSeverity.High,
                PossibleCauses = new List<string>
                {
                    "No internet connection",
                    "Server is temporarily unavailable",
                    "Firewall blocking the connection",
                    "Server maintenance in progress"
                },
                UserActions = new List<string>
                {
                    "Check your internet connection",
                    "Try again in a few minutes",
                    "Contact your network administrator",
                    "Work offline if possible"
                },
                IsRecoverable = true
            },

            TimeoutException => new UserFriendlyError
            {
                Title = "Operation Timeout",
                Message = "The operation took too long to complete.",
                DetailedExplanation = "The system waited for a response but didn't receive one within the expected time.",
                Severity = ErrorSeverity.Medium,
                PossibleCauses = new List<string>
                {
                    "Slow network connection",
                    "Server is overloaded",
                    "Large amount of data being processed",
                    "System performance issues"
                },
                UserActions = new List<string>
                {
                    "Try the operation again",
                    "Check your network speed",
                    "Break large operations into smaller parts",
                    "Contact support if timeouts persist"
                },
                IsRecoverable = true
            },

            // Authentication and authorization exceptions
            UnauthorizedAccessException => new UserFriendlyError
            {
                Title = "Access Denied",
                Message = "You don't have permission to perform this action.",
                DetailedExplanation = "Your current user account doesn't have the necessary permissions for this operation.",
                Severity = ErrorSeverity.Medium,
                PossibleCauses = new List<string>
                {
                    "Insufficient user permissions",
                    "Session has expired",
                    "Account has been deactivated",
                    "Role-based access restrictions"
                },
                UserActions = new List<string>
                {
                    "Log out and log back in",
                    "Contact your administrator for permission",
                    "Verify your account status",
                    "Use a different account with appropriate permissions"
                },
                IsRecoverable = true
            },

            // Validation exceptions (more specific first)
            ArgumentNullException => new UserFriendlyError
            {
                Title = "Missing Required Information",
                Message = "Required information is missing.",
                DetailedExplanation = "Some essential information needed to complete this operation is not provided.",
                Severity = ErrorSeverity.Medium,
                PossibleCauses = new List<string>
                {
                    "Required fields are empty",
                    "Data was not loaded properly",
                    "Form submission incomplete"
                },
                UserActions = new List<string>
                {
                    "Fill in all required fields",
                    "Refresh the page and try again",
                    "Check that all data loaded correctly"
                },
                IsRecoverable = true
            },

            ArgumentException => new UserFriendlyError
            {
                Title = "Invalid Input",
                Message = "The information provided is not valid.",
                DetailedExplanation = exception.Message,
                Severity = ErrorSeverity.Low,
                PossibleCauses = new List<string>
                {
                    "Required fields are missing",
                    "Data format is incorrect",
                    "Values are outside acceptable ranges",
                    "Invalid characters in input"
                },
                UserActions = new List<string>
                {
                    "Check all required fields are filled",
                    "Verify data formats (dates, numbers, etc.)",
                    "Remove any special characters",
                    "Follow the input guidelines provided"
                },
                IsRecoverable = true
            },

            // File and I/O exceptions
            FileNotFoundException => new UserFriendlyError
            {
                Title = "File Not Found",
                Message = "A required file could not be found.",
                DetailedExplanation = "The system is looking for a file that doesn't exist or has been moved.",
                Severity = ErrorSeverity.High,
                PossibleCauses = new List<string>
                {
                    "File has been deleted or moved",
                    "Incorrect file path",
                    "File permissions issue",
                    "Storage device disconnected"
                },
                UserActions = new List<string>
                {
                    "Check if the file exists in the expected location",
                    "Restore the file from backup if available",
                    "Contact support for assistance",
                    "Try uploading the file again"
                },
                IsRecoverable = true
            },

            DirectoryNotFoundException => new UserFriendlyError
            {
                Title = "Directory Not Found",
                Message = "A required folder could not be found.",
                DetailedExplanation = "The system cannot locate a folder it needs to complete the operation.",
                Severity = ErrorSeverity.High,
                PossibleCauses = new List<string>
                {
                    "Folder has been deleted or moved",
                    "Incorrect folder path",
                    "Permission issues",
                    "Storage device issues"
                },
                UserActions = new List<string>
                {
                    "Check if the folder exists",
                    "Create the folder if it's missing",
                    "Contact support for assistance",
                    "Check storage device connections"
                },
                IsRecoverable = true
            },

            IOException => new UserFriendlyError
            {
                Title = "File Operation Error",
                Message = "Unable to read or write file data.",
                DetailedExplanation = "The system encountered an error while trying to access file data.",
                Severity = ErrorSeverity.High,
                PossibleCauses = new List<string>
                {
                    "File is in use by another program",
                    "Insufficient disk space",
                    "File permissions issue",
                    "Storage device error"
                },
                UserActions = new List<string>
                {
                    "Close other programs that might be using the file",
                    "Free up disk space",
                    "Check file permissions",
                    "Try again in a few moments"
                },
                IsRecoverable = true
            },

            // Memory and resource exceptions
            OutOfMemoryException => new UserFriendlyError
            {
                Title = "Insufficient Memory",
                Message = "The system has run out of available memory.",
                DetailedExplanation = "The operation requires more memory than is currently available.",
                Severity = ErrorSeverity.Critical,
                PossibleCauses = new List<string>
                {
                    "Too many applications running",
                    "Processing very large amounts of data",
                    "Memory leak in the application",
                    "Insufficient system RAM"
                },
                UserActions = new List<string>
                {
                    "Close unnecessary applications",
                    "Restart the application",
                    "Process smaller amounts of data at a time",
                    "Contact support if the problem persists"
                },
                IsRecoverable = true
            },

            // Generic exceptions
            NotImplementedException => new UserFriendlyError
            {
                Title = "Feature Not Available",
                Message = "This feature is not yet implemented.",
                DetailedExplanation = "The requested functionality is planned but not yet available in this version.",
                Severity = ErrorSeverity.Medium,
                PossibleCauses = new List<string>
                {
                    "Feature is under development",
                    "Feature requires additional licensing",
                    "Feature is disabled in current configuration"
                },
                UserActions = new List<string>
                {
                    "Use alternative methods if available",
                    "Contact support for feature availability",
                    "Check for application updates",
                    "Consider upgrading your license"
                },
                IsRecoverable = false
            },

            NotSupportedException => new UserFriendlyError
            {
                Title = "Operation Not Supported",
                Message = "This operation is not supported in the current context.",
                DetailedExplanation = "The requested operation cannot be performed under the current conditions.",
                Severity = ErrorSeverity.Medium,
                PossibleCauses = new List<string>
                {
                    "Operation not supported on this platform",
                    "Current configuration doesn't allow this operation",
                    "Required components are not installed"
                },
                UserActions = new List<string>
                {
                    "Try a different approach",
                    "Check system requirements",
                    "Contact support for alternatives",
                    "Update your system if needed"
                },
                IsRecoverable = false
            },

            // Default case for unknown exceptions
            _ => new UserFriendlyError
            {
                Title = "Unexpected Error",
                Message = "An unexpected error occurred.",
                DetailedExplanation = "The system encountered an error that it doesn't recognize. This has been logged for investigation.",
                Severity = ErrorSeverity.High,
                PossibleCauses = new List<string>
                {
                    "Software bug or defect",
                    "Unexpected system state",
                    "External system failure",
                    "Data corruption"
                },
                UserActions = new List<string>
                {
                    "Try the operation again",
                    "Restart the application if the problem persists",
                    "Contact support with details of what you were doing",
                    "Check for application updates"
                },
                IsRecoverable = true
            }
        };
    }

    /// <summary>
    /// Logs an exception with structured context information
    /// </summary>
    public async Task LogExceptionAsync(Exception exception, string context, Guid deviceId, Guid? userId = null, Dictionary<string, object>? metadata = null)
    {
        try
        {
            var logCategory = DetermineLogCategory(exception, context);
            var additionalData = new Dictionary<string, object>
            {
                ["ExceptionType"] = exception.GetType().Name,
                ["StackTrace"] = exception.StackTrace ?? "No stack trace available",
                ["Context"] = context,
                ["InnerException"] = exception.InnerException?.Message ?? "None"
            };

            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    additionalData[kvp.Key] = kvp.Value;
                }
            }

            await _loggingService.LogErrorAsync(
                $"Exception in {context}: {exception.Message}",
                logCategory,
                deviceId,
                exception,
                userId,
                additionalData);
        }
        catch (Exception loggingException)
        {
            // Fallback to basic logging if structured logging fails
            _logger.LogError(loggingException, "Failed to log exception through comprehensive logging service");
            _logger.LogError(exception, "Original exception in context {Context}", context);
        }
    }

    /// <summary>
    /// Suggests recovery actions for a given exception
    /// </summary>
    public async Task<RecoveryAction> SuggestRecoveryActionAsync(Exception exception, string context)
    {
        await Task.CompletedTask; // For async consistency

        return exception switch
        {
            DbUpdateConcurrencyException => new RecoveryAction
            {
                ActionType = "ConcurrencyResolution",
                Description = "Resolve data concurrency conflict",
                Steps = new List<string>
                {
                    "Refresh data from database",
                    "Show user the current state",
                    "Allow user to merge changes",
                    "Save resolved data"
                },
                IsAutomatic = false,
                RequiresUserConfirmation = true,
                EstimatedDuration = TimeSpan.FromMinutes(2)
            },

            DbUpdateException => new RecoveryAction
            {
                ActionType = "DatabaseRecovery",
                Description = "Attempt to recover from database save error",
                Steps = new List<string>
                {
                    "Verify database connectivity",
                    "Check data integrity",
                    "Retry the save operation",
                    "If failed, queue for later sync"
                },
                IsAutomatic = true,
                RequiresUserConfirmation = false,
                EstimatedDuration = TimeSpan.FromSeconds(30)
            },

            HttpRequestException => new RecoveryAction
            {
                ActionType = "NetworkRecovery",
                Description = "Attempt to restore network connectivity",
                Steps = new List<string>
                {
                    "Check network connectivity",
                    "Retry connection with exponential backoff",
                    "Switch to offline mode if available",
                    "Queue operations for later sync"
                },
                IsAutomatic = true,
                RequiresUserConfirmation = false,
                EstimatedDuration = TimeSpan.FromMinutes(1)
            },

            TimeoutException => new RecoveryAction
            {
                ActionType = "TimeoutRecovery",
                Description = "Retry operation with extended timeout",
                Steps = new List<string>
                {
                    "Increase operation timeout",
                    "Break operation into smaller chunks if possible",
                    "Retry with exponential backoff",
                    "Show progress to user"
                },
                IsAutomatic = true,
                RequiresUserConfirmation = false,
                EstimatedDuration = TimeSpan.FromMinutes(3)
            },

            UnauthorizedAccessException => new RecoveryAction
            {
                ActionType = "AuthenticationRecovery",
                Description = "Attempt to restore user authentication",
                Steps = new List<string>
                {
                    "Check if session has expired",
                    "Prompt user to re-authenticate",
                    "Refresh authentication tokens",
                    "Retry original operation"
                },
                IsAutomatic = false,
                RequiresUserConfirmation = true,
                EstimatedDuration = TimeSpan.FromMinutes(1)
            },

            OutOfMemoryException => new RecoveryAction
            {
                ActionType = "MemoryRecovery",
                Description = "Free up system memory",
                Steps = new List<string>
                {
                    "Force garbage collection",
                    "Clear application caches",
                    "Reduce data processing batch size",
                    "Suggest application restart"
                },
                IsAutomatic = true,
                RequiresUserConfirmation = false,
                EstimatedDuration = TimeSpan.FromSeconds(15)
            },

            _ => new RecoveryAction
            {
                ActionType = "GenericRecovery",
                Description = "General error recovery",
                Steps = new List<string>
                {
                    "Log error details for analysis",
                    "Suggest user retry the operation",
                    "Provide alternative approaches if available",
                    "Escalate to support if needed"
                },
                IsAutomatic = false,
                RequiresUserConfirmation = true,
                EstimatedDuration = TimeSpan.FromMinutes(1)
            }
        };
    }

    /// <summary>
    /// Attempts automatic recovery for recoverable exceptions
    /// </summary>
    public async Task<RecoveryResult> AttemptAutomaticRecoveryAsync(Exception exception, string context, Guid deviceId)
    {
        try
        {
            return exception switch
            {
                DbUpdateConcurrencyException => await _errorRecoveryService.RecoverFromConcurrencyErrorAsync(exception),
                DbUpdateException => await _errorRecoveryService.RecoverFromStorageErrorAsync(exception),
                HttpRequestException => await _errorRecoveryService.RecoverFromSyncErrorAsync(exception),
                _ => new RecoveryResult
                {
                    Success = false,
                    Message = "No automatic recovery available for this exception type",
                    ActionsPerformed = new List<string> { "Exception logged for manual review" }
                }
            };
        }
        catch (Exception recoveryException)
        {
            await _loggingService.LogErrorAsync(
                $"Automatic recovery failed for {exception.GetType().Name}: {recoveryException.Message}",
                LogCategory.System,
                deviceId,
                recoveryException);

            return new RecoveryResult
            {
                Success = false,
                Message = "Automatic recovery failed",
                ActionsPerformed = new List<string> { "Recovery attempt failed", "Exception logged for manual review" },
                OriginalException = exception
            };
        }
    }

    // Private helper methods

    private static int DetermineStatusCode(Exception exception)
    {
        return exception switch
        {
            ArgumentNullException => (int)HttpStatusCode.BadRequest,
            ArgumentException => (int)HttpStatusCode.BadRequest,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            FileNotFoundException => (int)HttpStatusCode.NotFound,
            DirectoryNotFoundException => (int)HttpStatusCode.NotFound,
            NotImplementedException => (int)HttpStatusCode.NotImplemented,
            NotSupportedException => (int)HttpStatusCode.BadRequest,
            TimeoutException => (int)HttpStatusCode.RequestTimeout,
            HttpRequestException => (int)HttpStatusCode.ServiceUnavailable,
            DbUpdateConcurrencyException => (int)HttpStatusCode.Conflict,
            _ => (int)HttpStatusCode.InternalServerError
        };
    }

    private static string GenerateErrorCode(Exception exception)
    {
        var exceptionName = exception.GetType().Name;
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        return $"{exceptionName.ToUpper()}_{timestamp}";
    }

    private static LogCategory DetermineLogCategory(Exception exception, string context)
    {
        if (exception is DbUpdateException || exception is DbUpdateConcurrencyException || 
            exception is InvalidOperationException && exception.Message.Contains("database"))
        {
            return LogCategory.Database;
        }

        if (exception is HttpRequestException || exception is TimeoutException)
        {
            return LogCategory.Sync;
        }

        if (exception is UnauthorizedAccessException)
        {
            return LogCategory.Security;
        }

        if (exception is FileNotFoundException || exception is DirectoryNotFoundException || exception is IOException)
        {
            return LogCategory.System;
        }

        if (context.Contains("UI", StringComparison.OrdinalIgnoreCase) || 
            context.Contains("View", StringComparison.OrdinalIgnoreCase))
        {
            return LogCategory.UI;
        }

        if (context.Contains("Business", StringComparison.OrdinalIgnoreCase) || 
            context.Contains("Service", StringComparison.OrdinalIgnoreCase))
        {
            return LogCategory.Business;
        }

        return LogCategory.System;
    }
}