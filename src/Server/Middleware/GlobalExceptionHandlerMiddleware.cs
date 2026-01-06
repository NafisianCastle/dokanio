using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Shared.Core.Services;

namespace Server.Middleware;

/// <summary>
/// Enhanced global middleware to handle unhandled exceptions across the server application
/// Uses comprehensive exception handling with user-friendly messages and recovery suggestions
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _env;
    private readonly IGlobalExceptionHandler _globalExceptionHandler;
    private readonly ICurrentUserService _currentUserService;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next, 
        ILogger<GlobalExceptionHandlerMiddleware> logger, 
        IHostEnvironment env,
        IGlobalExceptionHandler globalExceptionHandler,
        ICurrentUserService currentUserService)
    {
        _next = next;
        _logger = logger;
        _env = env;
        _globalExceptionHandler = globalExceptionHandler;
        _currentUserService = currentUserService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var safePath = context.Request.Path.ToString()
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty);

            _logger.LogError(ex, "An unhandled exception occurred during request processing path: {Path}", safePath);

            if (context.Response.HasStarted)
            {
                _logger.LogWarning("Response already started; cannot write error body for path: {Path}", safePath);
                throw;
            }

            context.Response.Clear();
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        try
        {
            // Get device and user context
            var deviceId = _currentUserService.GetDeviceId();
            var userId = _currentUserService.GetUserId();
            var requestContext = $"HTTP {context.Request.Method} {context.Request.Path}";

            // Use comprehensive exception handler
            var errorResponse = await _globalExceptionHandler.HandleExceptionAsync(
                exception, 
                requestContext, 
                deviceId, 
                userId);

            // Set HTTP status code
            context.Response.StatusCode = errorResponse.StatusCode;

            // Add trace ID
            errorResponse.TraceId = context.TraceIdentifier;

            // Attempt automatic recovery for recoverable exceptions
            if (errorResponse.RecoveryAction?.IsAutomatic == true)
            {
                var recoveryResult = await _globalExceptionHandler.AttemptAutomaticRecoveryAsync(
                    exception, 
                    requestContext, 
                    deviceId);

                if (recoveryResult.Success)
                {
                    errorResponse.Metadata["AutoRecovery"] = "Successful";
                    errorResponse.Metadata["RecoveryActions"] = recoveryResult.ActionsPerformed;
                }
                else
                {
                    errorResponse.Metadata["AutoRecovery"] = "Failed";
                    errorResponse.Metadata["RecoveryFailure"] = recoveryResult.Message;
                }
            }

            // Serialize and send response
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = _env.IsDevelopment()
            };

            var json = JsonSerializer.Serialize(errorResponse, jsonOptions);
            await context.Response.WriteAsync(json);
        }
        catch (Exception handlingException)
        {
            // Fallback error handling if comprehensive handler fails
            _logger.LogCritical(handlingException, "Exception handler middleware failed while processing exception: {OriginalException}", exception.Message);
            
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            
            var fallbackResponse = new
            {
                StatusCode = context.Response.StatusCode,
                Message = "An unexpected error occurred while processing your request.",
                DetailedMessage = _env.IsDevelopment() ? exception.ToString() : null,
                TraceId = context.TraceIdentifier,
                Timestamp = DateTime.UtcNow
            };

            var fallbackJson = JsonSerializer.Serialize(fallbackResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            await context.Response.WriteAsync(fallbackJson);
        }
    }
}
