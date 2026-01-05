using Shared.Core.DTOs;
using Shared.Core.Enums;

namespace WebDashboard.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly HttpClient _httpClient;
    private UserDto? _currentUser;

    public AuthenticationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AuthenticationResult> LoginAsync(LoginRequest request)
    {
        try
        {
            // For demo purposes, simulate authentication
            if (request.Username == "admin" && request.Password == "password")
            {
                _currentUser = new UserDto
                {
                    Id = Guid.NewGuid(),
                    BusinessId = Guid.NewGuid(),
                    Username = request.Username,
                    Email = "admin@demo.com",
                    FirstName = "Admin",
                    LastName = "User",
                    Role = UserRole.BusinessOwner,
                    LastLoginAt = DateTime.UtcNow,
                    IsActive = true
                };

                return new AuthenticationResult
                {
                    IsSuccess = true,
                    User = _currentUser,
                    Token = "demo-token",
                    ExpiresAt = DateTime.UtcNow.AddHours(8)
                };
            }

            return new AuthenticationResult
            {
                IsSuccess = false,
                ErrorMessage = "Invalid username or password"
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during login: {ex.Message}");
            return new AuthenticationResult
            {
                IsSuccess = false,
                ErrorMessage = "Login failed. Please try again."
            };
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            _currentUser = null;
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during logout: {ex.Message}");
        }
    }

    public async Task<UserDto?> GetCurrentUserAsync()
    {
        try
        {
            // For demo purposes, return a mock user if none is set
            if (_currentUser == null)
            {
                _currentUser = new UserDto
                {
                    Id = Guid.NewGuid(),
                    BusinessId = Guid.NewGuid(),
                    Username = "demo_owner",
                    Email = "owner@demo.com",
                    FirstName = "Demo",
                    LastName = "Owner",
                    Role = UserRole.BusinessOwner,
                    LastLoginAt = DateTime.UtcNow,
                    IsActive = true
                };
            }
            return await Task.FromResult(_currentUser);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting current user: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            var user = await GetCurrentUserAsync();
            return user != null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking authentication: {ex.Message}");
            return false;
        }
    }
}