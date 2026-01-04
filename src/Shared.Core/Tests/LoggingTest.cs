using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Services;
using Xunit;

namespace Shared.Core.Tests;

public class LoggingTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;
    private readonly IComprehensiveLoggingService _loggingService;

    public LoggingTest()
    {
        var services = new ServiceCollection();
        
        // Use SQLite in-memory database for testing
        services.AddDbContext<PosDbContext>(options =>
        {
            options.UseSqlite("Data Source=:memory:", sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(30);
            });
            options.EnableSensitiveDataLogging(true);
        });
        
        // Add logging
        services.AddLogging();
        
        // Add comprehensive logging service
        services.AddScoped<IComprehensiveLoggingService, ComprehensiveLoggingService>();
        
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        _loggingService = _serviceProvider.GetRequiredService<IComprehensiveLoggingService>();
        
        // Ensure database is created and configured
        _context.Database.OpenConnection(); // Keep connection open for in-memory SQLite
        _context.Database.EnsureCreated();
        
        // Enable foreign keys for SQLite
        _context.Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON");
    }

    [Fact]
    public async Task ComprehensiveLogging_ShouldPersistLogEntry()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "Test log message";
        var category = LogCategory.System;
        var level = Services.LogLevel.Information;
        var additionalData = new { TestProperty = "TestValue" };

        // Act
        await _loggingService.LogInfoAsync(message, category, deviceId, userId, additionalData);

        // Assert
        var logEntries = _context.SystemLogs.Where(log => log.Message == message).ToList();
        Assert.Single(logEntries);
        
        var logEntry = logEntries.First();
        Assert.Equal(level, logEntry.Level);
        Assert.Equal(category, logEntry.Category);
        Assert.Equal(deviceId, logEntry.DeviceId);
        Assert.Equal(userId, logEntry.UserId);
        Assert.NotNull(logEntry.AdditionalData);
        Assert.True(logEntry.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task ComprehensiveLogging_ShouldHandleErrorsWithExceptions()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var message = "Test error message";
        var category = LogCategory.Database;
        var exception = new Exception("Test exception");

        // Act
        await _loggingService.LogErrorAsync(message, category, deviceId, exception);

        // Assert
        var logEntries = _context.SystemLogs.Where(log => log.Message == message).ToList();
        Assert.Single(logEntries);
        
        var logEntry = logEntries.First();
        Assert.Equal(Services.LogLevel.Error, logEntry.Level);
        Assert.Equal(category, logEntry.Category);
        Assert.Equal(deviceId, logEntry.DeviceId);
        Assert.NotNull(logEntry.ExceptionDetails);
        Assert.Contains("Test exception", logEntry.ExceptionDetails);
    }

    [Fact]
    public async Task ComprehensiveLogging_ShouldRetrieveLogsByCategory()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var message1 = "Database message";
        var message2 = "System message";
        
        await _loggingService.LogInfoAsync(message1, LogCategory.Database, deviceId);
        await _loggingService.LogInfoAsync(message2, LogCategory.System, deviceId);

        // Act
        var databaseLogs = await _loggingService.GetLogsByCategoryAsync(LogCategory.Database);
        var systemLogs = await _loggingService.GetLogsByCategoryAsync(LogCategory.System);

        // Assert
        Assert.Single(databaseLogs.Where(log => log.Message == message1));
        Assert.Single(systemLogs.Where(log => log.Message == message2));
        Assert.Empty(databaseLogs.Where(log => log.Message == message2));
        Assert.Empty(systemLogs.Where(log => log.Message == message1));
    }

    [Fact]
    public async Task ComprehensiveLogging_ShouldRetrieveLogsByLevel()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var infoMessage = "Info message";
        var errorMessage = "Error message";
        
        await _loggingService.LogInfoAsync(infoMessage, LogCategory.System, deviceId);
        await _loggingService.LogErrorAsync(errorMessage, LogCategory.System, deviceId);

        // Act
        var infoLogs = await _loggingService.GetLogsByLevelAsync(Services.LogLevel.Information);
        var errorLogs = await _loggingService.GetLogsByLevelAsync(Services.LogLevel.Error);

        // Assert
        Assert.Single(infoLogs.Where(log => log.Message == infoMessage));
        Assert.Single(errorLogs.Where(log => log.Message == errorMessage));
        Assert.Empty(infoLogs.Where(log => log.Message == errorMessage));
        Assert.Empty(errorLogs.Where(log => log.Message == infoMessage));
    }

    [Fact]
    public async Task ComprehensiveLogging_ShouldRetrieveErrorLogs()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var infoMessage = "Info message";
        var errorMessage = "Error message";
        var criticalMessage = "Critical message";
        
        await _loggingService.LogInfoAsync(infoMessage, LogCategory.System, deviceId);
        await _loggingService.LogErrorAsync(errorMessage, LogCategory.System, deviceId);
        await _loggingService.LogCriticalAsync(criticalMessage, LogCategory.System, deviceId);

        // Act
        var errorLogs = await _loggingService.GetErrorLogsAsync();

        // Assert
        Assert.Equal(2, errorLogs.Count()); // Should include both error and critical
        Assert.Single(errorLogs.Where(log => log.Message == errorMessage));
        Assert.Single(errorLogs.Where(log => log.Message == criticalMessage));
        Assert.Empty(errorLogs.Where(log => log.Message == infoMessage));
    }

    public void Dispose()
    {
        _context?.Database.CloseConnection();
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}