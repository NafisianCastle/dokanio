using Shared.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Shared.Core.DTOs;

/// <summary>
/// User data transfer object
/// </summary>
public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public UserRole Role { get; set; }
    public Guid? BusinessId { get; set; }
    public string? BusinessName { get; set; }
    public List<Guid> ShopIds { get; set; } = new();
    public List<string> ShopNames { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Request DTO for creating a new user
/// </summary>
public class CreateUserRequest
{
    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [Required]
    public UserRole Role { get; set; }
    
    public Guid? BusinessId { get; set; }
    
    public List<Guid> ShopIds { get; set; } = new();
    
    [MaxLength(20)]
    public string? Phone { get; set; }
    
    [MaxLength(500)]
    public string? Address { get; set; }
    
    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for updating an existing user
/// </summary>
public class UpdateUserRequest
{
    [Required]
    public Guid Id { get; set; }
    
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [MaxLength(20)]
    public string? Phone { get; set; }
    
    [MaxLength(500)]
    public string? Address { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Request DTO for assigning role to user
/// </summary>
public class AssignRoleRequest
{
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    public UserRole Role { get; set; }
    
    public Guid? BusinessId { get; set; }
    
    public List<Guid> ShopIds { get; set; } = new();
}

/// <summary>
/// Login request DTO
/// </summary>
public class LoginRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    public string Password { get; set; } = string.Empty;
    
    public bool RememberMe { get; set; } = false;
}

/// <summary>
/// Authentication result DTO
/// </summary>
public class AuthenticationResult
{
    public bool IsSuccess { get; set; }
    public string? Token { get; set; }
    public UserDto? User { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public List<string> Permissions { get; set; } = new();
}

