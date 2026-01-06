using Microsoft.Extensions.Logging;
using Shared.Core.Services;
using System;
using System.Threading.Tasks;

namespace Desktop.Services;

/// <summary>
/// Global exception handler service for the Desktop application
/// Provides comprehensive exception handling with user-friendly error dialogs
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
    /// Handles unhandled exceptions in the desktop application
    /// </summary>
    /// <param name="exception">The unhandled exception</param>
    /// <param name="context">Context where the exception occurred</param>
    /// <returns>Task representing the handling operation</returns>
    public async Task HandleUnhandledExceptionAsync(Exception exception, string context = "Desktop Application")
    {
        try
        {
            _logger.LogError(exception, "Unhandled exception in desktop application: {Context}", context);

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
            if (errorResponse.RecoveryAction?.IsAutomatic == true)
            {
                var recoveryResult = await _globalExceptionHandler.AttemptAutomaticRecoveryAsync(
                    exception, 
                    context, 
                    deviceId);

                if (recoveryResult.Success)
                {
                    _logger.LogInformation("Automatic recovery successful for exception in {Context}", context);
                    
                    // Show success message to user
                    await ShowRecoverySuccessMessageAsync(errorResponse, recoveryResult);
                    return;
                }
                else
                {
                    _logger.LogWarning("Automatic recovery failed for exception in {Context}: {RecoveryMessage}", 
                        context, recoveryResult.Message);
                }
            }

            // Show error dialog to user
            await ShowErrorDialogAsync(errorResponse);
        }
        catch (Exception handlingException)
        {
            // Fallback error handling
            _logger.LogCritical(handlingException, "Exception handler failed while processing exception: {OriginalException}", exception.Message);
            
            // Show basic error dialog
            await ShowFallbackErrorDialogAsync(exception);
        }
    }

    /// <summary>
    /// Shows a user-friendly error dialog
    /// </summary>
    /// <param name="errorResponse">The error response to display</param>
    private async Task ShowErrorDialogAsync(ErrorResponse errorResponse)
    {
        await Task.Run(() =>
        {
            try
            {
                // In a real implementation, this would show an Avalonia dialog
                // For now, we'll use a simple console output as a placeholder
                Console.WriteLine("=== ERROR DIALOG ===");
                Console.WriteLine($"Title: Error Occurred");
                Console.WriteLine($"Message: {errorResponse.Message}");
                
                if (!string.IsNullOrEmpty(errorResponse.DetailedMessage))
                {
                    Console.WriteLine($"Details: {errorResponse.DetailedMessage}");
                }

                if (errorResponse.RecoveryAction != null)
                {
                    Console.WriteLine($"Recovery Action: {errorResponse.RecoveryAction.Description}");
                    
                    if (errorResponse.RecoveryAction.Steps.Any())
                    {
                        Console.WriteLine("Suggested Steps:");
                        foreach (var step in errorResponse.RecoveryAction.Steps)
                        {
                            Console.WriteLine($"  - {step}");
                        }
                    }
                }

                Console.WriteLine($"Error Code: {errorResponse.ErrorCode}");
                Console.WriteLine($"Timestamp: {errorResponse.Timestamp}");
                Console.WriteLine("==================");
            }
            catch (Exception dialogException)
            {
                _logger.LogError(dialogException, "Failed to show error dialog");
            }
        });
    }

    /// <summary>
    /// Shows a recovery success message to the user
    /// </summary>
    /// <param name="errorResponse">The original error response</param>
    /// <param name="recoveryResult">The recovery result</param>
    private async Task ShowRecoverySuccessMessageAsync(ErrorResponse errorResponse, RecoveryResult recoveryResult)
    {
        await Task.Run(() =>
        {
            try
            {
                Console.WriteLine("=== RECOVERY SUCCESS ===");
                Console.WriteLine($"The system successfully recovered from an error.");
                Console.WriteLine($"Original Issue: {errorResponse.Message}");
                Console.WriteLine($"Recovery Actions Performed:");
                
                foreach (var action in recoveryResult.ActionsPerformed)
                {
                    Console.WriteLine($"  - {action}");
                }
                
                Console.WriteLine($"Recovery completed in {recoveryResult.RecoveryDuration.TotalSeconds:F1} seconds");
                Console.WriteLine("========================");
            }
            catch (Exception dialogException)
            {
                _logger.LogError(dialogException, "Failed to show recovery success message");
            }
        });
    }

    /// <summary>
    /// Shows a fallback error dialog when the main error handling fails
    /// </summary>
    /// <param name="exception">The original exception</param>
    private async Task ShowFallbackErrorDialogAsync(Exception exception)
    {
        await Task.Run(() =>
        {
            try
            {
                Console.WriteLine("=== CRITICAL ERROR ===");
                Console.WriteLine("A critical error occurred and the error handling system failed.");
                Console.WriteLine($"Exception: {exception.GetType().Name}");
                Console.WriteLine($"Message: {exception.Message}");
                Console.WriteLine("Please restart the application and contact support if the problem persists.");
                Console.WriteLine("=====================");
            }
            catch
            {
                // Last resort - do nothing to prevent infinite loops
            }
        });
    }
}