using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Shared.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace Shared.Core.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AuditLoggingService"/>.
/// Covers Requirements 10.1 (timestamps), 10.2 (user info), 10.3 (change history),
/// and 10.6 (audit reports).
/// </summary>
public class AuditLoggingServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IAuditLoggingService _auditLoggingService;
    private readonly ISaleAuditLogRepository _repository;
    private readonly PosDbContext _context;
    private readonly ITestOutputHelper _output;

    // Shared test IDs
    private readonly Guid _saleId1 = Guid.NewGuid();
    private readonly Guid _saleId2 = Guid.NewGuid();
    private readonly Guid _userId1 = Guid.NewGuid();
    private readonly Guid _userId2 = Guid.NewGuid();
    private readonly Guid _shopId = Guid.NewGuid();
    private readonly Guid _deviceId = Guid.NewGuid();

    public AuditLoggingServiceTests(ITestOutputHelper output)
    {
        _output = output;

        var services = new ServiceCollection();
        // Use a unique in-memory database per test instance to avoid cross-test contamination
        services.AddLogging();
        services.AddDbContext<PosDbContext>(options =>
        {
            options.UseInMemoryDatabase($"AuditLogTest_{Guid.NewGuid()}");
            options.EnableSensitiveDataLogging(true);
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
        });
        services.AddScoped<ISaleAuditLogRepository, SaleAuditLogRepository>();
        services.AddScoped<IAuditLoggingService, AuditLoggingService>();
        _serviceProvider = services.BuildServiceProvider();

        _auditLoggingService = _serviceProvider.GetRequiredService<IAuditLoggingService>();
        _repository = _serviceProvider.GetRequiredService<ISaleAuditLogRepository>();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();

        SeedTestData().GetAwaiter().GetResult();
    }

    private async Task SeedTestData()
    {
        // Seed minimal Sale records so the shop-filter join works in GetByDateRangeAsync
        var businessId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        _context.Businesses.Add(new Business
        {
            Id = businessId,
            Name = "Test Business",
            Type = BusinessType.GeneralRetail,
            OwnerId = ownerId,
            IsActive = true
        });

        _context.Shops.Add(new Shop
        {
            Id = _shopId,
            BusinessId = businessId,
            Name = "Test Shop",
            DeviceId = _deviceId,
            IsActive = true
        });

        _context.Sales.Add(new Sale
        {
            Id = _saleId1,
            ShopId = _shopId,
            UserId = _userId1,
            InvoiceNumber = "INV-TEST-001",
            Status = SaleStatus.Active
        });

        _context.Sales.Add(new Sale
        {
            Id = _saleId2,
            ShopId = _shopId,
            UserId = _userId2,
            InvoiceNumber = "INV-TEST-002",
            Status = SaleStatus.Active
        });

        await _context.SaveChangesAsync();
    }

    public void Dispose() => _serviceProvider.Dispose();

    // =========================================================================
    // LogSaleEventAsync — persists record with correct fields (Req 10.1, 10.2)
    // =========================================================================

    [Fact]
    public async Task LogSaleEvent_ShouldPersistRecordWithCorrectFields()
    {
        // Arrange
        var description = "Sale INV-TEST-001 created";

        // Act
        await _auditLoggingService.LogSaleEventAsync(
            _saleId1, _userId1,
            SaleAuditEventType.SaleCreated,
            description,
            deviceId: _deviceId);

        // Assert
        var logs = (await _repository.GetBySaleIdAsync(_saleId1)).ToList();
        Assert.Single(logs);

        var log = logs[0];
        Assert.Equal(_saleId1, log.SaleId);
        Assert.Equal(_userId1, log.UserId);                          // Req 10.2: user info recorded
        Assert.Equal(SaleAuditEventType.SaleCreated, log.EventType); // Req 10.1: event type
        Assert.Equal(description, log.EventDescription);
        Assert.True(log.Timestamp <= DateTime.UtcNow);               // Req 10.1: timestamp present
        Assert.True(log.Timestamp > DateTime.UtcNow.AddMinutes(-1)); // timestamp is recent
        Assert.Equal(_deviceId, log.DeviceId);

        _output.WriteLine($"Audit log persisted: {log.Id}, event: {log.EventType}, user: {log.UserId}");
    }

    [Fact]
    public async Task LogSaleEvent_WithOldAndNewValues_ShouldSerializeValues()
    {
        // Arrange
        var oldValues = new { Status = "Draft" };
        var newValues = new { Status = "Completed", Total = 150.00m };

        // Act
        await _auditLoggingService.LogSaleEventAsync(
            _saleId1, _userId1,
            SaleAuditEventType.SaleCompleted,
            "Sale completed",
            oldValues: oldValues,
            newValues: newValues);

        // Assert
        var logs = (await _repository.GetBySaleIdAsync(_saleId1)).ToList();
        Assert.Single(logs);

        var log = logs[0];
        Assert.NotNull(log.OldValues);
        Assert.NotNull(log.NewValues);
        Assert.Contains("Draft", log.OldValues);
        Assert.Contains("Completed", log.NewValues);
        Assert.Contains("150", log.NewValues);

        _output.WriteLine($"OldValues: {log.OldValues}");
        _output.WriteLine($"NewValues: {log.NewValues}");
    }

    // =========================================================================
    // LogItemChangeAsync — captures old and new values (Req 10.3)
    // =========================================================================

    [Fact]
    public async Task LogItemChange_ShouldCaptureOldAndNewValues()
    {
        // Arrange
        var saleItemId = Guid.NewGuid();
        var oldValues = new { Quantity = 2, TotalPrice = 20.00m };
        var newValues = new { Quantity = 5, TotalPrice = 50.00m };

        // Act
        await _auditLoggingService.LogItemChangeAsync(
            _saleId1, saleItemId, _userId1,
            SaleAuditEventType.ItemQuantityChanged,
            oldValues, newValues);

        // Assert
        var logs = (await _repository.GetBySaleIdAsync(_saleId1)).ToList();
        Assert.Single(logs);

        var log = logs[0];
        Assert.Equal(SaleAuditEventType.ItemQuantityChanged, log.EventType); // Req 10.3: change type
        Assert.NotNull(log.OldValues);
        Assert.NotNull(log.NewValues);
        Assert.Contains("20", log.OldValues);  // old price captured
        Assert.Contains("50", log.NewValues);  // new price captured

        _output.WriteLine($"Item change logged: {log.EventType}, old: {log.OldValues}, new: {log.NewValues}");
    }

    [Fact]
    public async Task LogItemChange_ItemRemoved_ShouldHaveNullNewValues()
    {
        // Arrange
        var saleItemId = Guid.NewGuid();
        var oldValues = new { ProductId = Guid.NewGuid(), Quantity = 3, TotalPrice = 30.00m };

        // Act
        await _auditLoggingService.LogItemChangeAsync(
            _saleId1, saleItemId, _userId1,
            SaleAuditEventType.ItemRemoved,
            oldValues, newValues: null);

        // Assert
        var logs = (await _repository.GetBySaleIdAsync(_saleId1)).ToList();
        Assert.Single(logs);

        var log = logs[0];
        Assert.Equal(SaleAuditEventType.ItemRemoved, log.EventType);
        Assert.NotNull(log.OldValues);   // old state captured
        Assert.Null(log.NewValues);      // nothing after removal

        _output.WriteLine($"Item removal logged: {log.EventType}");
    }

    // =========================================================================
    // GetAuditLogsForSaleAsync — returns only logs for that sale (Req 10.1)
    // =========================================================================

    [Fact]
    public async Task GetAuditLogsForSale_ShouldReturnOnlyLogsForThatSale()
    {
        // Arrange — log events for two different sales
        await _auditLoggingService.LogSaleEventAsync(
            _saleId1, _userId1, SaleAuditEventType.SaleCreated, "Sale 1 created");
        await _auditLoggingService.LogSaleEventAsync(
            _saleId1, _userId1, SaleAuditEventType.ItemAdded, "Item added to sale 1");
        await _auditLoggingService.LogSaleEventAsync(
            _saleId2, _userId2, SaleAuditEventType.SaleCreated, "Sale 2 created");

        // Act
        var logsForSale1 = (await _auditLoggingService.GetAuditLogsForSaleAsync(_saleId1)).ToList();
        var logsForSale2 = (await _auditLoggingService.GetAuditLogsForSaleAsync(_saleId2)).ToList();

        // Assert
        Assert.Equal(2, logsForSale1.Count);
        Assert.Single(logsForSale2);
        Assert.All(logsForSale1, l => Assert.Equal(_saleId1, l.SaleId));
        Assert.All(logsForSale2, l => Assert.Equal(_saleId2, l.SaleId));

        _output.WriteLine($"Sale 1 logs: {logsForSale1.Count}, Sale 2 logs: {logsForSale2.Count}");
    }

    [Fact]
    public async Task GetAuditLogsForSale_EmptySaleId_ShouldThrowArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _auditLoggingService.GetAuditLogsForSaleAsync(Guid.Empty));
    }

    // =========================================================================
    // GetAuditLogsByUserAsync — filters by user and date range (Req 10.2)
    // =========================================================================

    [Fact]
    public async Task GetAuditLogsByUser_ShouldFilterByUserAndDateRange()
    {
        // Arrange
        var fromDate = DateTime.UtcNow.AddHours(-1);
        var toDate = DateTime.UtcNow.AddHours(1);

        await _auditLoggingService.LogSaleEventAsync(
            _saleId1, _userId1, SaleAuditEventType.SaleCreated, "User1 created sale 1");
        await _auditLoggingService.LogSaleEventAsync(
            _saleId2, _userId1, SaleAuditEventType.ItemAdded, "User1 added item to sale 2");
        await _auditLoggingService.LogSaleEventAsync(
            _saleId2, _userId2, SaleAuditEventType.SaleCreated, "User2 created sale 2");

        // Act
        var user1Logs = (await _auditLoggingService.GetAuditLogsByUserAsync(_userId1, fromDate, toDate)).ToList();
        var user2Logs = (await _auditLoggingService.GetAuditLogsByUserAsync(_userId2, fromDate, toDate)).ToList();

        // Assert
        Assert.Equal(2, user1Logs.Count);
        Assert.Single(user2Logs);
        Assert.All(user1Logs, l => Assert.Equal(_userId1, l.UserId));
        Assert.All(user2Logs, l => Assert.Equal(_userId2, l.UserId));

        _output.WriteLine($"User1 logs: {user1Logs.Count}, User2 logs: {user2Logs.Count}");
    }

    [Fact]
    public async Task GetAuditLogsByUser_OutsideDateRange_ShouldReturnEmpty()
    {
        // Arrange — log an event now
        await _auditLoggingService.LogSaleEventAsync(
            _saleId1, _userId1, SaleAuditEventType.SaleCreated, "Sale created");

        // Query a date range in the past (before the event)
        var fromDate = DateTime.UtcNow.AddDays(-10);
        var toDate = DateTime.UtcNow.AddDays(-9);

        // Act
        var logs = (await _auditLoggingService.GetAuditLogsByUserAsync(_userId1, fromDate, toDate)).ToList();

        // Assert
        Assert.Empty(logs);
    }

    // =========================================================================
    // GenerateAuditReportAsync — correct event counts and summaries (Req 10.6)
    // =========================================================================

    [Fact]
    public async Task GenerateAuditReport_ShouldReturnCorrectEventCounts()
    {
        // Arrange
        var fromDate = DateTime.UtcNow.AddHours(-1);
        var toDate = DateTime.UtcNow.AddHours(1);

        await _auditLoggingService.LogSaleEventAsync(
            _saleId1, _userId1, SaleAuditEventType.SaleCreated, "Sale 1 created");
        await _auditLoggingService.LogSaleEventAsync(
            _saleId1, _userId1, SaleAuditEventType.ItemAdded, "Item added");
        await _auditLoggingService.LogSaleEventAsync(
            _saleId1, _userId1, SaleAuditEventType.ItemAdded, "Another item added");
        await _auditLoggingService.LogSaleEventAsync(
            _saleId1, _userId1, SaleAuditEventType.SaleCompleted, "Sale completed");
        await _auditLoggingService.LogSaleEventAsync(
            _saleId2, _userId2, SaleAuditEventType.SaleCreated, "Sale 2 created");
        await _auditLoggingService.LogSaleEventAsync(
            _saleId2, _userId2, SaleAuditEventType.SaleCancelled, "Sale 2 cancelled");

        // Act
        var report = await _auditLoggingService.GenerateAuditReportAsync(fromDate, toDate);

        // Assert
        Assert.Equal(6, report.TotalEvents);
        Assert.Equal(2, report.EventsByType[SaleAuditEventType.SaleCreated]);
        Assert.Equal(2, report.EventsByType[SaleAuditEventType.ItemAdded]);
        Assert.Equal(1, report.EventsByType[SaleAuditEventType.SaleCompleted]);
        Assert.Equal(1, report.EventsByType[SaleAuditEventType.SaleCancelled]);

        // Sales summary
        Assert.Equal(2, report.SalesSummary.SalesCreated);
        Assert.Equal(1, report.SalesSummary.SalesCompleted);
        Assert.Equal(1, report.SalesSummary.SalesCancelled);
        Assert.Equal(2, report.SalesSummary.ItemsAdded);

        // Report period
        Assert.Equal(fromDate, report.ReportPeriod.StartDate);
        Assert.Equal(toDate, report.ReportPeriod.EndDate);
        Assert.True(report.GeneratedAt <= DateTime.UtcNow);

        _output.WriteLine($"Report: {report.TotalEvents} events, generated at {report.GeneratedAt}");
    }

    [Fact]
    public async Task GenerateAuditReport_ShouldIncludeTopUsers()
    {
        // Arrange
        var fromDate = DateTime.UtcNow.AddHours(-1);
        var toDate = DateTime.UtcNow.AddHours(1);

        // User1 has 3 events, User2 has 1 event
        await _auditLoggingService.LogSaleEventAsync(
            _saleId1, _userId1, SaleAuditEventType.SaleCreated, "Event 1");
        await _auditLoggingService.LogSaleEventAsync(
            _saleId1, _userId1, SaleAuditEventType.ItemAdded, "Event 2");
        await _auditLoggingService.LogSaleEventAsync(
            _saleId1, _userId1, SaleAuditEventType.SaleCompleted, "Event 3");
        await _auditLoggingService.LogSaleEventAsync(
            _saleId2, _userId2, SaleAuditEventType.SaleCreated, "Event 4");

        // Act
        var report = await _auditLoggingService.GenerateAuditReportAsync(fromDate, toDate);

        // Assert
        var topUsers = report.TopUsers.ToList();
        Assert.NotEmpty(topUsers);

        // Most active user should be first
        var mostActive = topUsers[0];
        Assert.Equal(_userId1, mostActive.UserId);
        Assert.Equal(3, mostActive.TotalEvents);

        _output.WriteLine($"Top user: {mostActive.UserId} with {mostActive.TotalEvents} events");
    }

    [Fact]
    public async Task GenerateAuditReport_FilteredByUserId_ShouldOnlyIncludeThatUser()
    {
        // Arrange
        var fromDate = DateTime.UtcNow.AddHours(-1);
        var toDate = DateTime.UtcNow.AddHours(1);

        await _auditLoggingService.LogSaleEventAsync(
            _saleId1, _userId1, SaleAuditEventType.SaleCreated, "User1 event");
        await _auditLoggingService.LogSaleEventAsync(
            _saleId2, _userId2, SaleAuditEventType.SaleCreated, "User2 event");

        // Act
        var report = await _auditLoggingService.GenerateAuditReportAsync(fromDate, toDate, userId: _userId1);

        // Assert
        Assert.Equal(1, report.TotalEvents);
        Assert.Equal(1, report.EventsByType[SaleAuditEventType.SaleCreated]);

        _output.WriteLine($"User-filtered report: {report.TotalEvents} events");
    }

    [Fact]
    public async Task GenerateAuditReport_EmptyPeriod_ShouldReturnZeroCounts()
    {
        // Arrange — no events logged
        var fromDate = DateTime.UtcNow.AddDays(-5);
        var toDate = DateTime.UtcNow.AddDays(-4);

        // Act
        var report = await _auditLoggingService.GenerateAuditReportAsync(fromDate, toDate);

        // Assert
        Assert.Equal(0, report.TotalEvents);
        Assert.Empty(report.EventsByType);
        Assert.Empty(report.TopUsers);
        Assert.Equal(0, report.SalesSummary.SalesCreated);
    }
}
