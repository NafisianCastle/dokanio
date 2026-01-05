using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Services;
using Shared.Core.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Shared.Core.Tests;

/// <summary>
/// Tests for the GlobalExceptionHandler service
/// </summary>
public class GlobalExceptionHandlerTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IGlobalExceptionHandler _exceptionHandler;

    public GlobalExceptionHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        _serviceProvider = services.BuildServiceProvider();
        _exceptionHandler = _serviceProvider.GetRequiredService<IGlobalExceptionHandler>();
    }

    [Fact]
    public async Task HandleExceptionAsync_WithDbUpdateException_ReturnsUserFriendlyError()
    {
        // Arrange
        var exception = new Microsoft.EntityFrameworkCore.DbUpdateException("Database update failed");
        var context = "Test Context";
        var deviceId = Guid.NewGuid();

        // Act
        var result = await _exceptionHandler.HandleExceptionAsync(exception, context, deviceId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Unable to save your changes to the database.", result.Message);
        Assert.Equal(500, result.StatusCode); // Should be InternalServerError for database issues
        Assert.Contains("DBUPDATE", result.ErrorCode);
        Assert.NotNull(result.RecoveryAction);
        Assert.True(result.RecoveryAction.IsAutomatic);
    }

    [Fact]
    public async Task ConvertToUserFriendlyErrorAsync_WithArgumentNullException_ReturnsCorrectError()
    {
        // Arrange
        var exception = new ArgumentNullException("testParameter", "Test parameter cannot be null");

        // Act
        var result = await _exceptionHandler.ConvertToUserFriendlyErrorAsync(exception);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Missing Required Information", result.Title);
        Assert.Equal("Required information is missing.", result.Message);
        Assert.Equal(ErrorSeverity.Medium, result.Severity);
        Assert.True(result.IsRecoverable);
        Assert.Contains("Fill in all required fields", result.UserActions);
    }

    [Fact]
    public async Task SuggestRecoveryActionAsync_WithHttpRequestException_ReturnsNetworkRecovery()
    {
        // Arrange
        var exception = new HttpRequestException("Network connection failed");
        var context = "API Call";

        // Act
        var result = await _exceptionHandler.SuggestRecoveryActionAsync(exception, context);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("NetworkRecovery", result.ActionType);
        Assert.True(result.IsAutomatic);
        Assert.False(result.RequiresUserConfirmation);
        Assert.Contains("Check network connectivity", result.Steps);
    }

    [Fact]
    public async Task AttemptAutomaticRecoveryAsync_WithDbUpdateException_CallsErrorRecoveryService()
    {
        // Arrange
        var exception = new Microsoft.EntityFrameworkCore.DbUpdateException("Database error");
        var context = "Database Operation";
        var deviceId = Guid.NewGuid();

        // Act
        var result = await _exceptionHandler.AttemptAutomaticRecoveryAsync(exception, context, deviceId);

        // Assert
        Assert.NotNull(result);
        // The result depends on the ErrorRecoveryService implementation
        // In the test environment, it should attempt recovery
    }

    [Fact]
    public async Task LogExceptionAsync_WithValidException_LogsSuccessfully()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var context = "Unit Test";
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var metadata = new Dictionary<string, object>
        {
            ["TestKey"] = "TestValue"
        };

        // Act & Assert - Should not throw
        await _exceptionHandler.LogExceptionAsync(exception, context, deviceId, userId, metadata);
    }

    [Fact]
    public async Task HandleExceptionAsync_WithUnknownException_ReturnsGenericError()
    {
        // Arrange
        var exception = new CustomTestException("This is a custom test exception");
        var context = "Test Context";
        var deviceId = Guid.NewGuid();

        // Act
        var result = await _exceptionHandler.HandleExceptionAsync(exception, context, deviceId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode); // Should be InternalServerError for unknown exceptions
        Assert.Contains("CUSTOMTEST", result.ErrorCode);
    }

    [Fact]
    public async Task ConvertToUserFriendlyErrorAsync_WithTimeoutException_ReturnsTimeoutError()
    {
        // Arrange
        var exception = new TimeoutException("Operation timed out");

        // Act
        var result = await _exceptionHandler.ConvertToUserFriendlyErrorAsync(exception);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Operation Timeout", result.Title);
        Assert.Equal(ErrorSeverity.Medium, result.Severity);
        Assert.True(result.IsRecoverable);
        Assert.Contains("Try the operation again", result.UserActions);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    /// <summary>
    /// Custom exception for testing unknown exception handling
    /// </summary>
    private class CustomTestException : Exception
    {
        public CustomTestException(string message) : base(message) { }
    }
}