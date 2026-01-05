using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using System.Security.Cryptography;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of session management service
/// </summary>
public class SessionService : ISessionService
{
    private readonly IUserSessionRepository _sessionRepository;
    private readonly IAuditService _auditService;
    private readonly IUserRepository _userRepository;

    public SessionService(IUserSessionRepository sessionRepository, IAuditService auditService, IUserRepository userRepository)
    {
        _sessionRepository = sessionRepository;
        _auditService = auditService;
        _userRepository = userRepository;
    }

    public async Task<UserSession> CreateSessionAsync(Guid userId, string? ipAddress = null, string? userAgent = null)
    {
        var sessionToken = GenerateSessionToken();

        var session = new UserSession
        {
            UserId = userId,
            SessionToken = sessionToken,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            IsActive = true,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        await _sessionRepository.AddAsync(session);
        await _sessionRepository.SaveChangesAsync();

        return session;
    }

    public async Task<bool> UpdateSessionActivityAsync(string sessionToken)
    {
        var session = await _sessionRepository.GetByTokenAsync(sessionToken);
        if (session == null || !session.IsActive || session.EndedAt.HasValue)
            return false;

        session.LastActivityAt = DateTime.UtcNow;
        await _sessionRepository.UpdateAsync(session);
        await _sessionRepository.SaveChangesAsync();

        return true;
    }

    public async Task<bool> EndSessionAsync(string sessionToken)
    {
        var session = await _sessionRepository.GetByTokenAsync(sessionToken);
        if (session == null || !session.IsActive)
            return false;

        session.IsActive = false;
        session.EndedAt = DateTime.UtcNow;

        await _sessionRepository.UpdateAsync(session);
        await _sessionRepository.SaveChangesAsync();

        await _auditService.LogAsync(
            session.UserId,
            Enums.AuditAction.Logout,
            "User session ended");

        return true;
    }

    public async Task<UserSession?> GetActiveSessionAsync(string sessionToken)
    {
        var session = await _sessionRepository.GetByTokenAsync(sessionToken);
        if (session == null || !session.IsActive || session.EndedAt.HasValue)
            return null;

        return session;
    }

    public async Task<bool> IsSessionExpiredAsync(string sessionToken, UserRole? userRole = null)
    {
        var session = await _sessionRepository.GetByTokenAsync(sessionToken);
        if (session == null || !session.IsActive || session.EndedAt.HasValue)
            return true;

        // Get role-based timeout
        var timeoutMinutes = GetRoleBasedTimeoutMinutes(userRole);
        var cutoffTime = DateTime.UtcNow.AddMinutes(-timeoutMinutes);
        return session.LastActivityAt < cutoffTime;
    }

    public async Task<int> EndExpiredSessionsWithRoleBasedTimeoutsAsync()
    {
        var activeSessions = await _sessionRepository.GetAllActiveSessionsAsync();
        var expiredCount = 0;

        foreach (var session in activeSessions)
        {
            // Get user role for timeout calculation
            var user = await _userRepository.GetByIdAsync(session.UserId);
            var userRole = user?.Role;

            if (await IsSessionExpiredAsync(session.SessionToken, userRole))
            {
                session.IsActive = false;
                session.EndedAt = DateTime.UtcNow;
                await _sessionRepository.UpdateAsync(session);

                await _auditService.LogAsync(
                    session.UserId,
                    Enums.AuditAction.Logout,
                    $"Session expired due to role-based timeout ({GetRoleBasedTimeoutMinutes(userRole)} minutes)");

                expiredCount++;
            }
        }

        if (expiredCount > 0)
        {
            await _sessionRepository.SaveChangesAsync();
        }

        return expiredCount;
    }

    /// <summary>
    /// Gets role-based session timeout in minutes
    /// </summary>
    private int GetRoleBasedTimeoutMinutes(UserRole? userRole)
    {
        return userRole switch
        {
            UserRole.BusinessOwner => 120,      // 2 hours - longer for business owners
            UserRole.ShopManager => 90,        // 1.5 hours - moderate for managers
            UserRole.Administrator => 120,     // 2 hours - longer for admins
            UserRole.Manager => 90,            // 1.5 hours - moderate for managers
            UserRole.Supervisor => 60,         // 1 hour - standard for supervisors
            UserRole.InventoryStaff => 45,     // 45 minutes - shorter for inventory staff
            UserRole.Cashier => 30,            // 30 minutes - shortest for cashiers
            _ => 30                            // Default 30 minutes
        };
    }

    public async Task<bool> IsSessionExpiredAsync(string sessionToken, int inactivityTimeoutMinutes = 30)
    {
        var session = await _sessionRepository.GetByTokenAsync(sessionToken);
        if (session == null || !session.IsActive || session.EndedAt.HasValue)
            return true;

        var cutoffTime = DateTime.UtcNow.AddMinutes(-inactivityTimeoutMinutes);
        return session.LastActivityAt < cutoffTime;
    }

    public async Task<int> EndExpiredSessionsAsync(int inactivityTimeoutMinutes = 30)
    {
        return await _sessionRepository.EndExpiredSessionsAsync(inactivityTimeoutMinutes);
    }

    public async Task<IEnumerable<UserSession>> GetUserActiveSessionsAsync(Guid userId)
    {
        return await _sessionRepository.GetActiveSessionsByUserIdAsync(userId);
    }

    public async Task<int> EndAllUserSessionsAsync(Guid userId)
    {
        var count = await _sessionRepository.EndAllUserSessionsAsync(userId);

        if (count > 0)
        {
            await _auditService.LogAsync(
                userId,
                Enums.AuditAction.Logout,
                $"All user sessions ended ({count} sessions)");
        }

        return count;
    }

    private string GenerateSessionToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}