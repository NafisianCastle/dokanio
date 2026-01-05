using Shared.Core.DTOs;

namespace WebDashboard.Services;

public interface IUserApiService
{
    Task<IEnumerable<UserDto>> GetUsersByBusinessAsync(Guid businessId);
    Task<UserDto> GetUserByIdAsync(Guid userId);
    Task<UserDto> CreateUserAsync(CreateUserRequest request);
    Task<UserDto> UpdateUserAsync(UpdateUserRequest request);
    Task DeleteUserAsync(Guid userId);
    Task<UserDto> AssignUserRoleAsync(Guid userId, AssignRoleRequest request);
}