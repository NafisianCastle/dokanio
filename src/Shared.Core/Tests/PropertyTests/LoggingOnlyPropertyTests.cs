using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Services;
using Xunit;

namespace Shared.Core.Tests.PropertyTests;

public class LoggingOnlyPropertyTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;
    private readonly IComprehensiveLoggingService _loggingService;

    public LoggingOnlyPropertyTests()
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

    [Property]
    public bool ComprehensiveLogging(NonEmptyString message, int categoryInt, int levelInt)
    {
        // Feature: offline-first-pos, Property 21: For any log message with valid category and level, the system should persist the log entry to Local_Storage with all required information
        // **Validates: Requirements 12.5**
        
        // Clear the database for each test
        ClearDatabase();
        
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var logMessage = message.Get;
        
        // Ensure valid enum values
        var category = (LogCategory)(Math.Abs(categoryInt) % Enum.GetValues<LogCategory>().Length);
        var level = (Services.LogLevel)(Math.Abs(levelInt) % Enum.GetValues<Services.LogLevel>().Length);
        
        var additionalData = new { TestProperty = "TestValue", Timestamp = DateTime.UtcNow };
        
        try
        {
            // Test logging at the specified level
            Task logTask = level switch
            {
                Services.LogLevel.Debug => _loggingService.LogDebugAsync(logMessage, category, deviceId, userId, additionalData),
                Services.LogLevel.Information => _loggingService.LogInfoAsync(logMessage, category, deviceId, userId, additionalData),
                Services.LogLevel.Warning => _loggingService.LogWarningAsync(logMessage, category, deviceId, userId, additionalData),
                Services.LogLevel.Error => _loggingService.LogErrorAsync(logMessage, category, deviceId, new Exception("Test exception"), userId, additionalData),
                Services.LogLevel.Critical => _loggingService.LogCriticalAsync(logMessage, category, deviceId, new Exception("Test critical exception"), userId, additionalData),
                _ => _loggingService.LogInfoAsync(logMessage, category, deviceId, userId, additionalData)
            };
            
            logTask.Wait();
            
            // Verify that the log entry was persisted to Local_Storage
            var logEntries = _context.SystemLogs.Where(log => log.Message == logMessage).ToList();
            
            if (logEntries.Count != 1)
            {
                return false; // Should have exactly one log entry
            }
            
            var logEntry = logEntries.First();
            
            // Verify all required information is present and correct
            if (logEntry.Level != level)
            {
                return false; // Log level should match
            }
            
            if (logEntry.Category != category)
            {
                return false; // Log category should match
            }
            
            if (logEntry.DeviceId != deviceId)
            {
                return false; // Device ID should match
            }
            
            if (logEntry.UserId != userId)
            {
                return false; // User ID should match
            }
            
            if (string.IsNullOrEmpty(logEntry.AdditionalData))
            {
                return false; // Additional data should be serialized and stored
            }
            
            // For error and critical levels, exception details should be present
            if ((level == Services.LogLevel.Error || level == Services.LogLevel.Critical) && string.IsNullOrEmpty(logEntry.ExceptionDetails))
            {
                return false; // Exception details should be present for error/critical logs
            }
            
            // Verify that the log entry has a valid timestamp
            if (logEntry.CreatedAt == default || logEntry.CreatedAt > DateTime.UtcNow.AddMinutes(1))
            {
                return false; // Timestamp should be valid and recent
            }
            
            return true; // Comprehensive logging is working correctly
        }
        catch
        {
            return false;
        }
    }

    private void ClearDatabase()
    {
        // Clear system logs
        _context.SystemLogs.ExecuteDelete();
    }

    public void Dispose()
    {
        _context?.Database.CloseConnection();
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}