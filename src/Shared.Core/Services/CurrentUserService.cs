using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of current user service for managing authentication context
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private User? _currentUser;
    private UserSession? _currentSession;
    private readonly ISessionService _sessionService;
    private readonly Guid _deviceId;

    public CurrentUserService(ISessionService sessionService)
    {
        _sessionService = sessionService;
        _deviceId = Guid.NewGuid(); // In a real implementation, this would come from device configuration
    }

    public User? CurrentUser => _currentUser;
    public UserSession? CurrentSession => _currentSession;
    public bool IsAuthenticated => _currentUser != null && _currentSession != null;

    public event EventHandler<AuthenticationStateChangedEventArgs>? AuthenticationStateChanged;

    public void SetCurrentUser(User user, UserSession session)
    {
        var wasAuthenticated = IsAuthenticated;
        _currentUser = user;
        _currentSession = session;

        if (!wasAuthenticated || _currentUser?.Id != user.Id)
        {
            AuthenticationStateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs
            {
                User = user,
                IsAuthenticated = true
            });
        }
    }

    public void ClearCurrentUser()
    {
        var wasAuthenticated = IsAuthenticated;
        var previousUser = _currentUser;
        
        _currentUser = null;
        _currentSession = null;

        if (wasAuthenticated)
        {
            AuthenticationStateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs
            {
                User = previousUser,
                IsAuthenticated = false
            });
        }
    }

    public async Task UpdateActivityAsync()
    {
        if (_currentSession != null)
        {
            await _sessionService.UpdateSessionActivityAsync(_currentSession.SessionToken);
        }
    }

    public async Task<bool> IsSessionExpiredAsync(int inactivityTimeoutMinutes = 30)
    {
        if (_currentSession == null)
            return true;

        return await _sessionService.IsSessionExpiredAsync(_currentSession.SessionToken, inactivityTimeoutMinutes);
    }

    public Guid GetDeviceId() => _deviceId;

    public Guid? GetUserId() => _currentUser?.Id;

    public string? GetUsername() => _currentUser?.Username;

    public UserRole GetUserRole() => _currentUser?.Role ?? UserRole.Cashier;
}