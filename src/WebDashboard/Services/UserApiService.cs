using Shared.Core.DTOs;

namespace WebDashboard.Services;

public class UserApiService : IUserApiService
{
    private readonly HttpClient _httpClient;

    public UserApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<UserDto>> GetUsersByBusinessAsync(Guid businessId)
    {
        // Return mock data for demo
        await Task.Delay(100); // Simulate async operation
        return new List<UserDto>
        {
            new UserDto
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                Email = "admin@demo.com",
                FirstName = "Admin",
                LastName = "User",
                Role = global::Shared.Core.Enums.UserRole.BusinessOwner,
                BusinessId = businessId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
    }

    public async Task<UserDto> GetUserByIdAsync(Guid userId)
    {
        await Task.Delay(100);
        return new UserDto
        {
            Id = userId,
            Username = "demo",
            Email = "demo@example.com",
            FirstName = "Demo",
            LastName = "User",
            Role = global::Shared.Core.Enums.UserRole.BusinessOwner,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
    {
        await Task.Delay(100);
        return new UserDto
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = request.Role,
            BusinessId = request.BusinessId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public async Task<UserDto> UpdateUserAsync(UpdateUserRequest request)
    {
        await Task.Delay(100);
        return new UserDto
        {
            Id = request.Id,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            IsActive = request.IsActive,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        await Task.Delay(100);
        // Mock deletion
    }

    public async Task<UserDto> AssignUserRoleAsync(Guid userId, AssignRoleRequest request)
    {
        await Task.Delay(100);
        return new UserDto
        {
            Id = userId,
            Role = request.Role,
            BusinessId = request.BusinessId,
            UpdatedAt = DateTime.UtcNow
        };
    }
}