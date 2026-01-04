using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Shared.Core.Services;
using Xunit;

namespace Shared.Core.Tests;

/// <summary>
/// Tests for security and authentication functionality
/// </summary>
public class SecurityTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;

    public SecurityTests()
    {
        var services = new ServiceCollection();
        
        // Add in-memory database
        services.AddDbContext<PosDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole());
        
        // Add security services
        services.AddScoped<IEncryptionService, EncryptionService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        
        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task EncryptionService_ShouldEncryptAndDecryptData()
    {
        // Arrange
        var encryptionService = _serviceProvider.GetRequiredService<IEncryptionService>();
        const string plainText = "Sensitive POS data";

        // Act
        var encrypted = encryptionService.Encrypt(plainText);
        var decrypted = encryptionService.Decrypt(encrypted);

        // Assert
        Assert.NotEqual(plainText, encrypted);
        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public async Task EncryptionService_ShouldHashAndVerifyPasswords()
    {
        // Arrange
        var encryptionService = _serviceProvider.GetRequiredService<IEncryptionService>();
        const string password = "SecurePassword123!";

        // Act
        var salt = encryptionService.GenerateSalt();
        var hash = encryptionService.HashPassword(password, salt);
        var isValid = encryptionService.VerifyPassword(password, hash, salt);
        var isInvalid = encryptionService.VerifyPassword("WrongPassword", hash, salt);

        // Assert
        Assert.NotEmpty(salt);
        Assert.NotEmpty(hash);
        Assert.True(isValid);
        Assert.False(isInvalid);
    }

    [Fact]
    public async Task UserService_ShouldCreateAndAuthenticateUser()
    {
        // Arrange
        var userService = _serviceProvider.GetRequiredService<IUserService>();
        const string username = "testuser";
        const string password = "TestPassword123!";
        const string fullName = "Test User";
        const string email = "test@example.com";

        // Act
        var createdUser = await userService.CreateUserAsync(username, fullName, email, password, UserRole.Cashier);
        var authenticatedUser = await userService.AuthenticateAsync(username, password);
        var failedAuth = await userService.AuthenticateAsync(username, "WrongPassword");

        // Assert
        Assert.NotNull(createdUser);
        Assert.Equal(username, createdUser.Username);
        Assert.Equal(fullName, createdUser.FullName);
        Assert.Equal(email, createdUser.Email);
        Assert.Equal(UserRole.Cashier, createdUser.Role);
        
        Assert.NotNull(authenticatedUser);
        Assert.Equal(createdUser.Id, authenticatedUser.Id);
        
        Assert.Null(failedAuth);
    }

    [Fact]
    public async Task SessionService_ShouldCreateAndManageSessions()
    {
        // Arrange
        var userService = _serviceProvider.GetRequiredService<IUserService>();
        var sessionService = _serviceProvider.GetRequiredService<ISessionService>();
        
        var user = await userService.CreateUserAsync("sessionuser", "Session User", "session@example.com", "Password123!", UserRole.Cashier);

        // Act
        var session = await sessionService.CreateSessionAsync(user.Id);
        var activeSession = await sessionService.GetActiveSessionAsync(session.SessionToken);
        var activityUpdated = await sessionService.UpdateSessionActivityAsync(session.SessionToken);
        var sessionEnded = await sessionService.EndSessionAsync(session.SessionToken);
        var endedSession = await sessionService.GetActiveSessionAsync(session.SessionToken);

        // Assert
        Assert.NotNull(session);
        Assert.NotEmpty(session.SessionToken);
        Assert.True(session.IsActive);
        
        Assert.NotNull(activeSession);
        Assert.Equal(session.Id, activeSession.Id);
        
        Assert.True(activityUpdated);
        Assert.True(sessionEnded);
        Assert.Null(endedSession);
    }

    [Fact]
    public async Task AuthorizationService_ShouldEnforceRoleBasedPermissions()
    {
        // Arrange
        var userService = _serviceProvider.GetRequiredService<IUserService>();
        var authorizationService = _serviceProvider.GetRequiredService<IAuthorizationService>();
        
        var cashier = await userService.CreateUserAsync("cashier", "Cashier User", "cashier@example.com", "Password123!", UserRole.Cashier);
        var manager = await userService.CreateUserAsync("manager", "Manager User", "manager@example.com", "Password123!", UserRole.Manager);
        var admin = await userService.CreateUserAsync("admin", "Admin User", "admin@example.com", "Password123!", UserRole.Administrator);

        // Act & Assert
        // Cashier permissions
        Assert.True(authorizationService.HasPermission(cashier, AuditAction.CreateSale));
        Assert.False(authorizationService.HasPermission(cashier, AuditAction.RefundSale));
        Assert.False(authorizationService.HasPermission(cashier, AuditAction.AccessReports));
        Assert.False(authorizationService.CanManageUsers(cashier));

        // Manager permissions
        Assert.True(authorizationService.HasPermission(manager, AuditAction.CreateSale));
        Assert.True(authorizationService.HasPermission(manager, AuditAction.RefundSale));
        Assert.True(authorizationService.HasPermission(manager, AuditAction.AccessReports));
        Assert.False(authorizationService.CanManageUsers(manager));

        // Administrator permissions
        Assert.True(authorizationService.HasPermission(admin, AuditAction.CreateSale));
        Assert.True(authorizationService.HasPermission(admin, AuditAction.RefundSale));
        Assert.True(authorizationService.HasPermission(admin, AuditAction.AccessReports));
        Assert.True(authorizationService.CanManageUsers(admin));
    }

    [Fact]
    public async Task AuditService_ShouldLogSecurityEvents()
    {
        // Arrange
        var userService = _serviceProvider.GetRequiredService<IUserService>();
        var auditService = _serviceProvider.GetRequiredService<IAuditService>();
        
        var user = await userService.CreateUserAsync("audituser", "Audit User", "audit@example.com", "Password123!", UserRole.Manager);

        // Act
        var auditLog = await auditService.LogAsync(
            user.Id,
            AuditAction.Login,
            "User logged in successfully",
            nameof(User),
            user.Id,
            null,
            null,
            "192.168.1.1",
            "Test User Agent");

        var userLogs = await auditService.GetUserAuditLogsAsync(user.Id);
        var actionLogs = await auditService.GetActionAuditLogsAsync(AuditAction.Login);

        // Assert
        Assert.NotNull(auditLog);
        Assert.Equal(user.Id, auditLog.UserId);
        Assert.Equal(AuditAction.Login, auditLog.Action);
        Assert.Equal("User logged in successfully", auditLog.Description);
        Assert.Equal("192.168.1.1", auditLog.IpAddress);

        Assert.Single(userLogs);
        Assert.Contains(actionLogs, log => log.Id == auditLog.Id);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}