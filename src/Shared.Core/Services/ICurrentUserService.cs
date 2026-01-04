using Shared.Core.Entities;

namespace Shared.Core.Services;

/// <summary>
/// Service for managing the current authenticated user context
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current authenticated user
    /// </summary>
    User? CurrentUser { get; }
    
    /// <summary>
    /// Gets the current user session
    /// </summary>
    UserSession? CurrentSession { get; }
    
    /// <summary>
    /// Sets the current authenticated user and session
    /// </summary>
    /// <param name="user">The authenticated user</param>
    /// <param name="session">The user session</param>
    void SetCurrentUser(User user, UserSession session);
    
    /// <summary>
    /// Clears the current user context
    /// </summary>
    void ClearCurrentUser();
    
    /// <summary>
    /// Checks if a user is currently authenticated
    /// </summary>
    bool IsAuthenticated { get; }
    
    /// <summary>
    /// Updates the last activity timestamp
    /// </summary>
    Task UpdateActivityAsync();
    
    /// <summary>
    /// Checks if the current session is expired
    /// </summary>
    /// <param name="inactivityTimeoutMinutes">Inactivity timeout in minutes</param>
    /// <returns>True if session is expired</returns>
    Task<bool> IsSessionExpiredAsync(int inactivityTimeoutMinutes = 30);
    
    /// <summary>
    /// Event fired when user authentication state changes
    /// </summary>
    event EventHandler<AuthenticationStateChangedEventArgs>? AuthenticationStateChanged;
}

/// <summary>
/// Event arguments for authentication state changes
/// </summary>
public class AuthenticationStateChangedEventArgs : EventArgs
{
    public User? User { get; set; }
    public bool IsAuthenticated { get; set; }
}