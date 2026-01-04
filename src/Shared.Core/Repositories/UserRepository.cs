using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.Entities;

namespace Shared.Core.Repositories;

/// <summary>
/// Repository implementation for User entity
/// </summary>
public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(PosDbContext context, ILogger<UserRepository> logger) : base(context, logger)
    {
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.Username == username && !u.IsDeleted);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
    }

    public async Task<IEnumerable<User>> GetActiveUsersAsync()
    {
        return await _context.Set<User>()
            .Where(u => u.IsActive && !u.IsDeleted)
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        return await _context.Set<User>()
            .AnyAsync(u => u.Username == username && !u.IsDeleted);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Set<User>()
            .AnyAsync(u => u.Email == email && !u.IsDeleted);
    }
}