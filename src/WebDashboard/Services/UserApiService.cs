using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Repositories;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace WebDashboard.Services;

public class UserApiService : IUserApiService
{
    private readonly IUserRepository _userRepository;
    private readonly IBusinessRepository _businessRepository;
    private readonly ILogger<UserApiService> _logger;

    public UserApiService(
        IUserRepository userRepository,
        IBusinessRepository businessRepository,
        ILogger<UserApiService> logger)
    {
        _userRepository = userRepository;
        _businessRepository = businessRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<UserDto>> GetUsersByBusinessAsync(Guid businessId)
    {
        try
        {
            _logger.LogInformation("Getting users for business {BusinessId}", businessId);

            var users = await _userRepository.FindAsync(u => u.BusinessId == businessId && u.IsActive);
            
            return users.Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                FirstName = u.FullName.Split(' ').FirstOrDefault() ?? "",
                LastName = u.FullName.Split(' ').Skip(1).FirstOrDefault() ?? "",
                Role = u.Role,
                BusinessId = u.BusinessId,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users for business {BusinessId}", businessId);
            throw;
        }
    }

    public async Task<UserDto> GetUserByIdAsync(Guid userId)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new ArgumentException($"User with ID {userId} not found");
            }

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FullName.Split(' ').FirstOrDefault() ?? "",
                LastName = user.FullName.Split(' ').Skip(1).FirstOrDefault() ?? "",
                Role = user.Role,
                BusinessId = user.BusinessId,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", userId);
            throw;
        }
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
    {
        try
        {
            _logger.LogInformation("Creating new user {Username} for business {BusinessId}", request.Username, request.BusinessId);

            // Verify business exists
            if (!request.BusinessId.HasValue)
            {
                throw new ArgumentException("BusinessId is required");
            }
            
            var business = await _businessRepository.GetByIdAsync(request.BusinessId.Value);
            if (business == null)
            {
                throw new ArgumentException($"Business with ID {request.BusinessId} not found");
            }

            // Check if username already exists
            var existingUser = await _userRepository.FindAsync(u => u.Username == request.Username);
            if (existingUser.Any())
            {
                throw new ArgumentException($"Username {request.Username} already exists");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                Email = request.Email,
                FullName = $"{request.FirstName} {request.LastName}".Trim(),
                Role = request.Role,
                BusinessId = request.BusinessId.Value,
                PasswordHash = "temp_hash", // In real implementation, this would be properly hashed
                Salt = "temp_salt",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                DeviceId = Guid.NewGuid(), // TODO: Get from current device context
                SyncStatus = SyncStatus.NotSynced
            };

            await _userRepository.AddAsync(user);

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Role = user.Role,
                BusinessId = user.BusinessId,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Username}", request.Username);
            throw;
        }
    }

    public async Task<UserDto> UpdateUserAsync(UpdateUserRequest request)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(request.Id);
            if (user == null)
            {
                throw new ArgumentException($"User with ID {request.Id} not found");
            }

            user.Email = request.Email;
            user.FullName = $"{request.FirstName} {request.LastName}".Trim();
            user.IsActive = request.IsActive;
            user.UpdatedAt = DateTime.UtcNow;
            user.SyncStatus = SyncStatus.NotSynced;

            await _userRepository.UpdateAsync(user);

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Role = user.Role,
                BusinessId = user.BusinessId,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", request.Id);
            throw;
        }
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new ArgumentException($"User with ID {userId} not found");
            }

            // Soft delete
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            user.SyncStatus = SyncStatus.NotSynced;

            await _userRepository.UpdateAsync(user);

            _logger.LogInformation("User {UserId} soft deleted", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", userId);
            throw;
        }
    }

    public async Task<UserDto> AssignUserRoleAsync(Guid userId, AssignRoleRequest request)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new ArgumentException($"User with ID {userId} not found");
            }

            // Verify business exists if changing business assignment
            if (request.BusinessId.HasValue)
            {
                var business = await _businessRepository.GetByIdAsync(request.BusinessId.Value);
                if (business == null)
                {
                    throw new ArgumentException($"Business with ID {request.BusinessId} not found");
                }
                user.BusinessId = request.BusinessId.Value;
            }

            user.Role = request.Role;
            user.UpdatedAt = DateTime.UtcNow;
            user.SyncStatus = SyncStatus.NotSynced;

            await _userRepository.UpdateAsync(user);

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FullName.Split(' ').FirstOrDefault() ?? "",
                LastName = user.FullName.Split(' ').Skip(1).FirstOrDefault() ?? "",
                Role = user.Role,
                BusinessId = user.BusinessId,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role to user {UserId}", userId);
            throw;
        }
    }
}