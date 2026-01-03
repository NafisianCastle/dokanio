using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Server.Services;
using Shared.Core.Services;
using System.Security.Cryptography;
using System.Text;

namespace Server.Controllers;

/// <summary>
/// Controller for device registration and authentication
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DeviceController : ControllerBase
{
    private readonly ServerDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ILogger<DeviceController> _logger;

    public DeviceController(ServerDbContext context, IJwtService jwtService, ILogger<DeviceController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new device with the system
    /// </summary>
    /// <param name="request">Device registration request</param>
    /// <returns>Registration result with API key</returns>
    [HttpPost("register")]
    public async Task<ActionResult<SyncApiResult<DeviceRegistrationResponse>>> RegisterDevice([FromBody] DeviceRegistrationRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.DeviceName))
            {
                return BadRequest(new SyncApiResult<DeviceRegistrationResponse>
                {
                    Success = false,
                    Message = "Device name is required",
                    StatusCode = 400,
                    Errors = new List<string> { "DeviceName cannot be empty" }
                });
            }

            // Check if device already exists
            var existingDevice = await _context.Devices
                .FirstOrDefaultAsync(d => d.Id == request.DeviceId);

            if (existingDevice != null)
            {
                if (existingDevice.IsActive)
                {
                    return Conflict(new SyncApiResult<DeviceRegistrationResponse>
                    {
                        Success = false,
                        Message = "Device is already registered",
                        StatusCode = 409,
                        Errors = new List<string> { "Device with this ID already exists" }
                    });
                }
                else
                {
                    // Reactivate existing device
                    existingDevice.IsActive = true;
                    existingDevice.Name = request.DeviceName;
                    existingDevice.ApiKey = GenerateApiKey();
                    
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Reactivated device {DeviceId} with name {DeviceName}", 
                        existingDevice.Id, existingDevice.Name);

                    return Ok(new SyncApiResult<DeviceRegistrationResponse>
                    {
                        Success = true,
                        Message = "Device reactivated successfully",
                        StatusCode = 200,
                        Data = new DeviceRegistrationResponse
                        {
                            DeviceId = existingDevice.Id,
                            ApiKey = existingDevice.ApiKey,
                            IsActive = existingDevice.IsActive
                        }
                    });
                }
            }

            // Create new device
            var device = new Device
            {
                Id = request.DeviceId,
                Name = request.DeviceName,
                ApiKey = GenerateApiKey(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Devices.Add(device);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Registered new device {DeviceId} with name {DeviceName}", 
                device.Id, device.Name);

            return Ok(new SyncApiResult<DeviceRegistrationResponse>
            {
                Success = true,
                Message = "Device registered successfully",
                StatusCode = 200,
                Data = new DeviceRegistrationResponse
                {
                    DeviceId = device.Id,
                    ApiKey = device.ApiKey,
                    IsActive = device.IsActive
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering device {DeviceId}", request.DeviceId);
            return StatusCode(500, new SyncApiResult<DeviceRegistrationResponse>
            {
                Success = false,
                Message = "Internal server error during device registration",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Authenticates a device and returns JWT token
    /// </summary>
    /// <param name="request">Authentication request</param>
    /// <returns>Authentication result with JWT token</returns>
    [HttpPost("authenticate")]
    public async Task<ActionResult<SyncApiResult<AuthenticationResponse>>> Authenticate([FromBody] AuthenticationRequest request)
    {
        try
        {
            if (request.DeviceId == Guid.Empty || string.IsNullOrWhiteSpace(request.ApiKey))
            {
                return BadRequest(new SyncApiResult<AuthenticationResponse>
                {
                    Success = false,
                    Message = "Device ID and API key are required",
                    StatusCode = 400,
                    Errors = new List<string> { "Invalid device credentials" }
                });
            }

            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.ApiKey == request.ApiKey && d.IsActive);

            if (device == null)
            {
                _logger.LogWarning("Authentication failed for device {DeviceId}", request.DeviceId);
                return Unauthorized(new SyncApiResult<AuthenticationResponse>
                {
                    Success = false,
                    Message = "Invalid device credentials",
                    StatusCode = 401,
                    Errors = new List<string> { "Device not found or inactive" }
                });
            }

            var token = _jwtService.GenerateToken(device);
            var refreshToken = _jwtService.GenerateRefreshToken();

            _logger.LogInformation("Device {DeviceId} authenticated successfully", device.Id);

            return Ok(new SyncApiResult<AuthenticationResponse>
            {
                Success = true,
                Message = "Authentication successful",
                StatusCode = 200,
                Data = new AuthenticationResponse
                {
                    AccessToken = token,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddHours(24), // Should match JWT expiration
                    DeviceId = device.Id
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating device {DeviceId}", request.DeviceId);
            return StatusCode(500, new SyncApiResult<AuthenticationResponse>
            {
                Success = false,
                Message = "Internal server error during authentication",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Gets device information (requires authentication)
    /// </summary>
    /// <param name="deviceId">Device ID</param>
    /// <returns>Device information</returns>
    [HttpGet("{deviceId}")]
    [Authorize]
    public async Task<ActionResult<SyncApiResult<DeviceInfo>>> GetDevice(Guid deviceId)
    {
        try
        {
            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.Id == deviceId && d.IsActive);

            if (device == null)
            {
                return NotFound(new SyncApiResult<DeviceInfo>
                {
                    Success = false,
                    Message = "Device not found",
                    StatusCode = 404
                });
            }

            return Ok(new SyncApiResult<DeviceInfo>
            {
                Success = true,
                Message = "Device information retrieved successfully",
                StatusCode = 200,
                Data = new DeviceInfo
                {
                    DeviceId = device.Id,
                    Name = device.Name,
                    IsActive = device.IsActive,
                    CreatedAt = device.CreatedAt,
                    LastSyncAt = device.LastSyncAt
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving device {DeviceId}", deviceId);
            return StatusCode(500, new SyncApiResult<DeviceInfo>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    private string GenerateApiKey()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}

/// <summary>
/// Request model for device registration
/// </summary>
public class DeviceRegistrationRequest
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
}

/// <summary>
/// Response model for device registration
/// </summary>
public class DeviceRegistrationResponse
{
    public Guid DeviceId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

/// <summary>
/// Request model for device authentication
/// </summary>
public class AuthenticationRequest
{
    public Guid DeviceId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Response model for device information
/// </summary>
public class DeviceInfo
{
    public Guid DeviceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
}