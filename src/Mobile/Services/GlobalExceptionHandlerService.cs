using Microsoft.Extensions.Logging;
using Shared.Core.Services;

namespace Mobile.Services;

/// <summary>
/// Global exception handler service for the Mobile application
/// Provides comprehensive exception handling with user-friendly error notifications
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
    /// Handles unhandled exceptions in the mobile application
    /// </summary>
    /// <param name="exception">The unhandled exception</param>
    /// <param name="context">Context where the exception occurred</param>
    /// <returns>Task representing the handling operation</returns>
    public async Task HandleUnhandledExceptionAsync(Exception exception, string context = "Mobile Application")
    {
        try
        {
            _logger.LogError(exception, "Unhandled exception in mobile application: {Context}", context);

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
                    
                    // Show success notification to user
                    await ShowRecoverySuccessNotificationAsync(errorResponse, recoveryResult);
                    return;
                }
                else
                {
                    _logger.LogWarning("Automatic recovery failed for exception in {Context}: {RecoveryMessage}", 
                        context, recoveryResult.Message);
                }
            }

            // Show error notification to user
            await ShowErrorNotificationAsync(errorResponse);
        }
        catch (Exception handlingException)
        {
            // Fallback error handling
            _logger.LogCritical(handlingException, "Exception handler failed while processing exception: {OriginalException}", exception.Message);
            
            // Show basic error notification
            await ShowFallbackErrorNotificationAsync(exception);
        }
    }

    /// <summary>
    /// Shows a user-friendly error notification
    /// </summary>
    /// <param name="errorResponse">The error response to display</param>
    private async Task ShowErrorNotificationAsync(ErrorResponse errorResponse)
    {
        try
        {
            // In a real implementation, this would show a MAUI alert or toast notification
            // For now, we'll use a simple console output as a placeholder
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    // This would be replaced with actual MAUI alert dialog
                    Console.WriteLine("=== MOBILE ERROR NOTIFICATION ===");
                    Console.WriteLine($"Title: Error Occurred");
                    Console.WriteLine($"Message: {errorResponse.Message}");
                    
                    if (!string.IsNullOrEmpty(errorResponse.DetailedMessage))
                    {
                        Console.WriteLine($"Details: {errorResponse.DetailedMessage}");
                    }

                    if (errorResponse.RecoveryAction != null && !errorResponse.RecoveryAction.IsAutomatic)
                    {
                        Console.WriteLine($"What you can do: {errorResponse.RecoveryAction.Description}");
                        
                        if (errorResponse.RecoveryAction.Steps.Any())
                        {
                            Console.WriteLine("Steps to try:");
                            foreach (var step in errorResponse.RecoveryAction.Steps.Take(3)) // Limit for mobile
                            {
                                Console.WriteLine($"  • {step}");
                            }
                        }
                    }

                    Console.WriteLine($"Error Code: {errorResponse.ErrorCode}");
                    Console.WriteLine("================================");

                    // In a real implementation, you would show an actual alert:
                    // await Application.Current.MainPage.DisplayAlert(
                    //     "Error Occurred",
                    //     errorResponse.Message,
                    //     "OK");
                }
                catch (Exception notificationException)
                {
                    _logger.LogError(notificationException, "Failed to show error notification on main thread");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show error notification");
        }
    }

    /// <summary>
    /// Shows a recovery success notification to the user
    /// </summary>
    /// <param name="errorResponse">The original error response</param>
    /// <param name="recoveryResult">The recovery result</param>
    private async Task ShowRecoverySuccessNotificationAsync(ErrorResponse errorResponse, RecoveryResult recoveryResult)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    Console.WriteLine("=== MOBILE RECOVERY SUCCESS ===");
                    Console.WriteLine($"✓ Issue resolved automatically");
                    Console.WriteLine($"Problem: {errorResponse.Message}");
                    Console.WriteLine($"Solution: {recoveryResult.Message}");
                    Console.WriteLine($"Time taken: {recoveryResult.RecoveryDuration.TotalSeconds:F1}s");
                    Console.WriteLine("==============================");

                    // In a real implementation, you would show a toast or brief alert:
                    // await Application.Current.MainPage.DisplayAlert(
                    //     "Issue Resolved",
                    //     $"The system automatically fixed the issue: {errorResponse.Message}",
                    //     "OK");
                }
                catch (Exception notificationException)
                {
                    _logger.LogError(notificationException, "Failed to show recovery success notification on main thread");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show recovery success notification");
        }
    }

    /// <summary>
    /// Shows a fallback error notification when the main error handling fails
    /// </summary>
    /// <param name="exception">The original exception</param>
    private async Task ShowFallbackErrorNotificationAsync(Exception exception)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    Console.WriteLine("=== MOBILE CRITICAL ERROR ===");
                    Console.WriteLine("A critical error occurred.");
                    Console.WriteLine($"Type: {exception.GetType().Name}");
                    Console.WriteLine($"Message: {exception.Message}");
                    Console.WriteLine("Please restart the app if problems persist.");
                    Console.WriteLine("=============================");

                    // In a real implementation:
                    // await Application.Current.MainPage.DisplayAlert(
                    //     "Critical Error",
                    //     "A critical error occurred. Please restart the app if problems persist.",
                    //     "OK");
                }
                catch
                {
                    // Last resort - do nothing to prevent infinite loops
                }
            });
        }
        catch
        {
            // Last resort - do nothing to prevent infinite loops
        }
    }
}