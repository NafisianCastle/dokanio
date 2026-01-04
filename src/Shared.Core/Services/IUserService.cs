using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Service for user management and authentication
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Authenticates a user with username and password
    /// </summary>
    /// <param name="username">Username</param>
    /// <param name="password">Password</param>
    /// <returns>User if authentication successful, null otherwise</returns>
    Task<User?> AuthenticateAsync(string username, string password);
    
    /// <summary>
    /// Creates a new user
    /// </summary>
    /// <param name="username">Username</param>
    /// <param name="fullName">Full name</param>
    /// <param name="email">Email address</param>
    /// <param name="password">Password</param>
    /// <param name="role">User role</param>
    /// <returns>Created user</returns>
    Task<User> CreateUserAsync(string username, string fullName, string email, string password, UserRole role);
    
    /// <summary>
    /// Updates user information
    /// </summary>
    /// <param name="user">User to update</param>
    /// <returns>Updated user</returns>
    Task<User> UpdateUserAsync(User user);
    
    /// <summary>
    /// Changes user password
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="currentPassword">Current password</param>
    /// <param name="newPassword">New password</param>
    /// <returns>True if password changed successfully</returns>
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
    
    /// <summary>
    /// Gets user by ID
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>User if found</returns>
    Task<User?> GetUserByIdAsync(Guid userId);
    
    /// <summary>
    /// Gets user by username
    /// </summary>
    /// <param name="username">Username</param>
    /// <returns>User if found</returns>
    Task<User?> GetUserByUsernameAsync(string username);
    
    /// <summary>
    /// Gets all active users
    /// </summary>
    /// <returns>List of active users</returns>
    Task<IEnumerable<User>> GetActiveUsersAsync();
    
    /// <summary>
    /// Deactivates a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>True if deactivated successfully</returns>
    Task<bool> DeactivateUserAsync(Guid userId);
}