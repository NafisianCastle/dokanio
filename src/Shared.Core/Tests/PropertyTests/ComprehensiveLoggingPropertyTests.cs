using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Services;
using Xunit;

namespace Shared.Core.Tests.PropertyTests;

public class ComprehensiveLoggingPropertyTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;
    private readonly IComprehensiveLoggingService _loggingService;

    public ComprehensiveLoggingPropertyTests()
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
            
            // Test retrieval by category
            var categoryLogsTask = _loggingService.GetLogsByCategoryAsync(category);
            categoryLogsTask.Wait();
            var categoryLogs = categoryLogsTask.Result.ToList();
            
            if (!categoryLogs.Any(log => log.Id == logEntry.Id))
            {
                return false; // Should be retrievable by category
            }
            
            // Test retrieval by level
            var levelLogsTask = _loggingService.GetLogsByLevelAsync(level);
            levelLogsTask.Wait();
            var levelLogs = levelLogsTask.Result.ToList();
            
            if (!levelLogs.Any(log => log.Id == logEntry.Id))
            {
                return false; // Should be retrievable by level
            }
            
            // Test retrieval of all logs
            var allLogsTask = _loggingService.GetLogsAsync();
            allLogsTask.Wait();
            var allLogs = allLogsTask.Result.ToList();
            
            if (!allLogs.Any(log => log.Id == logEntry.Id))
            {
                return false; // Should be retrievable in all logs
            }
            
            // For error and critical levels, test error log retrieval
            if (level == Services.LogLevel.Error || level == Services.LogLevel.Critical)
            {
                var errorLogsTask = _loggingService.GetErrorLogsAsync();
                errorLogsTask.Wait();
                var errorLogs = errorLogsTask.Result.ToList();
                
                if (!errorLogs.Any(log => log.Id == logEntry.Id))
                {
                    return false; // Error/critical logs should be retrievable via GetErrorLogsAsync
                }
            }
            
            return true; // Comprehensive logging is working correctly
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool LoggingPersistsAcrossSystemLayers(NonEmptyString databaseMessage, NonEmptyString syncMessage, NonEmptyString hardwareMessage)
    {
        // Feature: offline-first-pos, Property 21: For any system layer (database, sync, hardware, etc.), log messages should be consistently persisted and retrievable
        // **Validates: Requirements 12.5**
        
        // Clear the database for each test
        ClearDatabase();
        
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        
        try
        {
            // Log messages from different system layers
            var databaseLogTask = _loggingService.LogInfoAsync(databaseMessage.Get, LogCategory.Database, deviceId, userId);
            var syncLogTask = _loggingService.LogWarningAsync(syncMessage.Get, LogCategory.Sync, deviceId, userId);
            var hardwareLogTask = _loggingService.LogErrorAsync(hardwareMessage.Get, LogCategory.Hardware, deviceId, new Exception("Hardware error"), userId);
            
            Task.WaitAll(databaseLogTask, syncLogTask, hardwareLogTask);
            
            // Verify all logs were persisted
            var allLogs = _context.SystemLogs.ToList();
            
            if (allLogs.Count != 3)
            {
                return false; // Should have exactly 3 log entries
            }
            
            // Verify each category has its log
            var databaseLogs = allLogs.Where(log => log.Category == LogCategory.Database).ToList();
            var syncLogs = allLogs.Where(log => log.Category == LogCategory.Sync).ToList();
            var hardwareLogs = allLogs.Where(log => log.Category == LogCategory.Hardware).ToList();
            
            if (databaseLogs.Count != 1 || syncLogs.Count != 1 || hardwareLogs.Count != 1)
            {
                return false; // Each category should have exactly one log
            }
            
            // Verify messages match
            if (databaseLogs.First().Message != databaseMessage.Get ||
                syncLogs.First().Message != syncMessage.Get ||
                hardwareLogs.First().Message != hardwareMessage.Get)
            {
                return false; // Messages should match what was logged
            }
            
            // Verify levels are correct
            if (databaseLogs.First().Level != Services.LogLevel.Information ||
                syncLogs.First().Level != Services.LogLevel.Warning ||
                hardwareLogs.First().Level != Services.LogLevel.Error)
            {
                return false; // Log levels should match what was specified
            }
            
            // Test retrieval by category works for all layers
            var retrievedDatabaseLogsTask = _loggingService.GetLogsByCategoryAsync(LogCategory.Database);
            var retrievedSyncLogsTask = _loggingService.GetLogsByCategoryAsync(LogCategory.Sync);
            var retrievedHardwareLogsTask = _loggingService.GetLogsByCategoryAsync(LogCategory.Hardware);
            
            Task.WaitAll(retrievedDatabaseLogsTask, retrievedSyncLogsTask, retrievedHardwareLogsTask);
            
            var retrievedDatabaseLogs = retrievedDatabaseLogsTask.Result.ToList();
            var retrievedSyncLogs = retrievedSyncLogsTask.Result.ToList();
            var retrievedHardwareLogs = retrievedHardwareLogsTask.Result.ToList();
            
            if (retrievedDatabaseLogs.Count != 1 || retrievedSyncLogs.Count != 1 || retrievedHardwareLogs.Count != 1)
            {
                return false; // Should be able to retrieve logs by category
            }
            
            return true; // Logging works consistently across all system layers
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool LoggingSupportsDateRangeFiltering(NonEmptyString message1, NonEmptyString message2, NonEmptyString message3)
    {
        // Feature: offline-first-pos, Property 21: For any date range query, the logging system should return only logs within the specified time period
        // **Validates: Requirements 12.5**
        
        // Clear the database for each test
        ClearDatabase();
        
        var deviceId = Guid.NewGuid();
        var baseTime = DateTime.UtcNow.AddHours(-2);
        
        try
        {
            // Create logs at different times by manually setting timestamps
            var log1 = new SystemLogEntry
            {
                Level = Services.LogLevel.Information,
                Category = LogCategory.System,
                Message = message1.Get,
                DeviceId = deviceId,
                CreatedAt = baseTime.AddMinutes(-30) // 30 minutes before base time
            };
            
            var log2 = new SystemLogEntry
            {
                Level = Services.LogLevel.Information,
                Category = LogCategory.System,
                Message = message2.Get,
                DeviceId = deviceId,
                CreatedAt = baseTime // At base time
            };
            
            var log3 = new SystemLogEntry
            {
                Level = Services.LogLevel.Information,
                Category = LogCategory.System,
                Message = message3.Get,
                DeviceId = deviceId,
                CreatedAt = baseTime.AddMinutes(30) // 30 minutes after base time
            };
            
            _context.SystemLogs.AddRange(log1, log2, log3);
            _context.SaveChanges();
            
            // Test date range filtering - get logs from base time onwards
            var fromTime = baseTime.AddMinutes(-5); // 5 minutes before base time
            var toTime = baseTime.AddMinutes(5); // 5 minutes after base time
            
            var filteredLogsTask = _loggingService.GetLogsAsync(fromTime, toTime);
            filteredLogsTask.Wait();
            var filteredLogs = filteredLogsTask.Result.ToList();
            
            // Should only return log2 (the one at base time)
            if (filteredLogs.Count != 1)
            {
                return false; // Should have exactly one log in the date range
            }
            
            if (filteredLogs.First().Message != message2.Get)
            {
                return false; // Should be the log at base time
            }
            
            // Test with no date filters - should return all logs
            var allLogsTask = _loggingService.GetLogsAsync();
            allLogsTask.Wait();
            var allLogs = allLogsTask.Result.ToList();
            
            if (allLogs.Count != 3)
            {
                return false; // Should return all 3 logs when no date filter is applied
            }
            
            // Test with only 'from' filter
            var fromOnlyLogsTask = _loggingService.GetLogsAsync(baseTime);
            fromOnlyLogsTask.Wait();
            var fromOnlyLogs = fromOnlyLogsTask.Result.ToList();
            
            if (fromOnlyLogs.Count != 2)
            {
                return false; // Should return log2 and log3 (from base time onwards)
            }
            
            // Test with only 'to' filter
            var toOnlyLogsTask = _loggingService.GetLogsAsync(null, baseTime);
            toOnlyLogsTask.Wait();
            var toOnlyLogs = toOnlyLogsTask.Result.ToList();
            
            if (toOnlyLogs.Count != 2)
            {
                return false; // Should return log1 and log2 (up to base time)
            }
            
            return true; // Date range filtering works correctly
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