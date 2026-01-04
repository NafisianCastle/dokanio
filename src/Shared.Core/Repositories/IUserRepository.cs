using Shared.Core.Entities;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository interface for User entity
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>
    /// Gets user by username
    /// </summary>
    /// <param name="username">Username to search for</param>
    /// <returns>User if found</returns>
    Task<User?> GetByUsernameAsync(string username);
    
    /// <summary>
    /// Gets user by email
    /// </summary>
    /// <param name="email">Email to search for</param>
    /// <returns>User if found</returns>
    Task<User?> GetByEmailAsync(string email);
    
    /// <summary>
    /// Gets all active users
    /// </summary>
    /// <returns>List of active users</returns>
    Task<IEnumerable<User>> GetActiveUsersAsync();
    
    /// <summary>
    /// Checks if username exists
    /// </summary>
    /// <param name="username">Username to check</param>
    /// <returns>True if username exists</returns>
    Task<bool> UsernameExistsAsync(string username);
    
    /// <summary>
    /// Checks if email exists
    /// </summary>
    /// <param name="email">Email to check</param>
    /// <returns>True if email exists</returns>
    Task<bool> EmailExistsAsync(string email);
}