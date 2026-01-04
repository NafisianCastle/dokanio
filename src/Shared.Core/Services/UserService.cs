using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of user management service
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly IAuditService _auditService;

    public UserService(
        IUserRepository userRepository,
        IEncryptionService encryptionService,
        IAuditService auditService)
    {
        _userRepository = userRepository;
        _encryptionService = encryptionService;
        _auditService = auditService;
    }

    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        var user = await _userRepository.GetByUsernameAsync(username);
        if (user == null || !user.IsActive)
        {
            await _auditService.LogAsync(
                null,
                AuditAction.SecurityViolation,
                $"Failed login attempt for username: {username}");
            return null;
        }

        if (!_encryptionService.VerifyPassword(password, user.PasswordHash, user.Salt))
        {
            await _auditService.LogAsync(
                user.Id,
                AuditAction.SecurityViolation,
                $"Invalid password for user: {username}");
            return null;
        }

        // Update last login time
        user.LastLoginAt = DateTime.UtcNow;
        user.LastActivityAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        await _auditService.LogAsync(
            user.Id,
            AuditAction.Login,
            $"User {username} logged in successfully");

        return user;
    }

    public async Task<User> CreateUserAsync(string username, string fullName, string email, string password, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty", nameof(username));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be empty", nameof(password));

        if (await _userRepository.UsernameExistsAsync(username))
            throw new InvalidOperationException($"Username '{username}' already exists");

        if (await _userRepository.EmailExistsAsync(email))
            throw new InvalidOperationException($"Email '{email}' already exists");

        var salt = _encryptionService.GenerateSalt();
        var passwordHash = _encryptionService.HashPassword(password, salt);

        var user = new User
        {
            Username = username,
            FullName = fullName,
            Email = email,
            PasswordHash = passwordHash,
            Salt = salt,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();

        await _auditService.LogAsync(
            null,
            AuditAction.SystemConfiguration,
            $"Created new user: {username} with role: {role}",
            nameof(User),
            user.Id);

        return user;
    }

    public async Task<User> UpdateUserAsync(User user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        await _auditService.LogAsync(
            user.Id,
            AuditAction.SystemConfiguration,
            $"Updated user: {user.Username}",
            nameof(User),
            user.Id);

        return user;
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || !user.IsActive)
            return false;

        if (!_encryptionService.VerifyPassword(currentPassword, user.PasswordHash, user.Salt))
        {
            await _auditService.LogAsync(
                userId,
                AuditAction.SecurityViolation,
                "Failed password change attempt - invalid current password");
            return false;
        }

        var newSalt = _encryptionService.GenerateSalt();
        var newPasswordHash = _encryptionService.HashPassword(newPassword, newSalt);

        user.PasswordHash = newPasswordHash;
        user.Salt = newSalt;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        await _auditService.LogAsync(
            userId,
            AuditAction.SystemConfiguration,
            "Password changed successfully");

        return true;
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _userRepository.GetByIdAsync(userId);
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        return await _userRepository.GetByUsernameAsync(username);
    }

    public async Task<IEnumerable<User>> GetActiveUsersAsync()
    {
        return await _userRepository.GetActiveUsersAsync();
    }

    public async Task<bool> DeactivateUserAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return false;

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        await _auditService.LogAsync(
            userId,
            AuditAction.SystemConfiguration,
            $"Deactivated user: {user.Username}",
            nameof(User),
            user.Id);

        return true;
    }
}