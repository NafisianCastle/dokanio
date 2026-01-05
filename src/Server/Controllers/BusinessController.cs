using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Core.DTOs;
using Shared.Core.Services;
using System.Security.Claims;

namespace Server.Controllers;

/// <summary>
/// Controller for business and shop management operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BusinessController : ControllerBase
{
    private readonly IBusinessManagementService _businessService;
    private readonly ILogger<BusinessController> _logger;

    public BusinessController(IBusinessManagementService businessService, ILogger<BusinessController> logger)
    {
        _businessService = businessService;
        _logger = logger;
    }

    #region Business Management

    /// <summary>
    /// Creates a new business
    /// </summary>
    /// <param name="request">Business creation request</param>
    /// <returns>Created business response</returns>
    [HttpPost]
    public async Task<ActionResult<SyncApiResult<BusinessResponse>>> CreateBusiness([FromBody] CreateBusinessRequest request)
    {
        try
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized(new SyncApiResult<BusinessResponse>
                {
                    Success = false,
                    Message = "Invalid user token",
                    StatusCode = 401
                });
            }

            // Set the owner ID from the authenticated user
            request.OwnerId = userId.Value;

            var business = await _businessService.CreateBusinessAsync(request);

            _logger.LogInformation("Business {BusinessId} created by user {UserId}", business.Id, userId);

            return Ok(new SyncApiResult<BusinessResponse>
            {
                Success = true,
                Message = "Business created successfully",
                StatusCode = 200,
                Data = business
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating business for user {UserId}", GetUserIdFromToken());
            return StatusCode(500, new SyncApiResult<BusinessResponse>
            {
                Success = false,
                Message = "Internal server error during business creation",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Updates an existing business
    /// </summary>
    /// <param name="id">Business ID</param>
    /// <param name="request">Business update request</param>
    /// <returns>Updated business response</returns>
    [HttpPut("{id}")]
    public async Task<ActionResult<SyncApiResult<BusinessResponse>>> UpdateBusiness(Guid id, [FromBody] UpdateBusinessRequest request)
    {
        try
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized(new SyncApiResult<BusinessResponse>
                {
                    Success = false,
                    Message = "Invalid user token",
                    StatusCode = 401
                });
            }

            request.Id = id;
            var business = await _businessService.UpdateBusinessAsync(request);

            _logger.LogInformation("Business {BusinessId} updated by user {UserId}", id, userId);

            return Ok(new SyncApiResult<BusinessResponse>
            {
                Success = true,
                Message = "Business updated successfully",
                StatusCode = 200,
                Data = business
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating business {BusinessId} by user {UserId}", id, GetUserIdFromToken());
            return StatusCode(500, new SyncApiResult<BusinessResponse>
            {
                Success = false,
                Message = "Internal server error during business update",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Gets a business by ID
    /// </summary>
    /// <param name="id">Business ID</param>
    /// <returns>Business response</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<SyncApiResult<BusinessResponse>>> GetBusiness(Guid id)
    {
        try
        {
            var business = await _businessService.GetBusinessByIdAsync(id);
            if (business == null)
            {
                return NotFound(new SyncApiResult<BusinessResponse>
                {
                    Success = false,
                    Message = "Business not found",
                    StatusCode = 404
                });
            }

            return Ok(new SyncApiResult<BusinessResponse>
            {
                Success = true,
                Message = "Business retrieved successfully",
                StatusCode = 200,
                Data = business
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving business {BusinessId}", id);
            return StatusCode(500, new SyncApiResult<BusinessResponse>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Gets all businesses owned by the authenticated user
    /// </summary>
    /// <returns>Collection of business responses</returns>
    [HttpGet]
    public async Task<ActionResult<SyncApiResult<IEnumerable<BusinessResponse>>>> GetMyBusinesses()
    {
        try
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized(new SyncApiResult<IEnumerable<BusinessResponse>>
                {
                    Success = false,
                    Message = "Invalid user token",
                    StatusCode = 401
                });
            }

            var businesses = await _businessService.GetBusinessesByOwnerAsync(userId.Value);

            return Ok(new SyncApiResult<IEnumerable<BusinessResponse>>
            {
                Success = true,
                Message = "Businesses retrieved successfully",
                StatusCode = 200,
                Data = businesses
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving businesses for user {UserId}", GetUserIdFromToken());
            return StatusCode(500, new SyncApiResult<IEnumerable<BusinessResponse>>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Deletes a business (soft delete)
    /// </summary>
    /// <param name="id">Business ID</param>
    /// <returns>Deletion result</returns>
    [HttpDelete("{id}")]
    public async Task<ActionResult<SyncApiResult<bool>>> DeleteBusiness(Guid id)
    {
        try
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized(new SyncApiResult<bool>
                {
                    Success = false,
                    Message = "Invalid user token",
                    StatusCode = 401
                });
            }

            var result = await _businessService.DeleteBusinessAsync(id, userId.Value);
            if (!result)
            {
                return NotFound(new SyncApiResult<bool>
                {
                    Success = false,
                    Message = "Business not found or access denied",
                    StatusCode = 404
                });
            }

            _logger.LogInformation("Business {BusinessId} deleted by user {UserId}", id, userId);

            return Ok(new SyncApiResult<bool>
            {
                Success = true,
                Message = "Business deleted successfully",
                StatusCode = 200,
                Data = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting business {BusinessId} by user {UserId}", id, GetUserIdFromToken());
            return StatusCode(500, new SyncApiResult<bool>
            {
                Success = false,
                Message = "Internal server error during business deletion",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    #endregion

    #region Shop Management

    /// <summary>
    /// Creates a new shop
    /// </summary>
    /// <param name="request">Shop creation request</param>
    /// <returns>Created shop response</returns>
    [HttpPost("shops")]
    public async Task<ActionResult<SyncApiResult<ShopResponse>>> CreateShop([FromBody] CreateShopRequest request)
    {
        try
        {
            var shop = await _businessService.CreateShopAsync(request);

            _logger.LogInformation("Shop {ShopId} created for business {BusinessId}", shop.Id, request.BusinessId);

            return Ok(new SyncApiResult<ShopResponse>
            {
                Success = true,
                Message = "Shop created successfully",
                StatusCode = 200,
                Data = shop
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating shop for business {BusinessId}", request.BusinessId);
            return StatusCode(500, new SyncApiResult<ShopResponse>
            {
                Success = false,
                Message = "Internal server error during shop creation",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Updates an existing shop
    /// </summary>
    /// <param name="id">Shop ID</param>
    /// <param name="request">Shop update request</param>
    /// <returns>Updated shop response</returns>
    [HttpPut("shops/{id}")]
    public async Task<ActionResult<SyncApiResult<ShopResponse>>> UpdateShop(Guid id, [FromBody] UpdateShopRequest request)
    {
        try
        {
            request.Id = id;
            var shop = await _businessService.UpdateShopAsync(request);

            _logger.LogInformation("Shop {ShopId} updated", id);

            return Ok(new SyncApiResult<ShopResponse>
            {
                Success = true,
                Message = "Shop updated successfully",
                StatusCode = 200,
                Data = shop
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating shop {ShopId}", id);
            return StatusCode(500, new SyncApiResult<ShopResponse>
            {
                Success = false,
                Message = "Internal server error during shop update",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Gets a shop by ID
    /// </summary>
    /// <param name="id">Shop ID</param>
    /// <returns>Shop response</returns>
    [HttpGet("shops/{id}")]
    public async Task<ActionResult<SyncApiResult<ShopResponse>>> GetShop(Guid id)
    {
        try
        {
            var shop = await _businessService.GetShopByIdAsync(id);
            if (shop == null)
            {
                return NotFound(new SyncApiResult<ShopResponse>
                {
                    Success = false,
                    Message = "Shop not found",
                    StatusCode = 404
                });
            }

            return Ok(new SyncApiResult<ShopResponse>
            {
                Success = true,
                Message = "Shop retrieved successfully",
                StatusCode = 200,
                Data = shop
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shop {ShopId}", id);
            return StatusCode(500, new SyncApiResult<ShopResponse>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Gets all shops for a business
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <returns>Collection of shop responses</returns>
    [HttpGet("{businessId}/shops")]
    public async Task<ActionResult<SyncApiResult<IEnumerable<ShopResponse>>>> GetShopsByBusiness(Guid businessId)
    {
        try
        {
            var shops = await _businessService.GetShopsByBusinessAsync(businessId);

            return Ok(new SyncApiResult<IEnumerable<ShopResponse>>
            {
                Success = true,
                Message = "Shops retrieved successfully",
                StatusCode = 200,
                Data = shops
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shops for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<IEnumerable<ShopResponse>>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Deletes a shop (soft delete)
    /// </summary>
    /// <param name="id">Shop ID</param>
    /// <returns>Deletion result</returns>
    [HttpDelete("shops/{id}")]
    public async Task<ActionResult<SyncApiResult<bool>>> DeleteShop(Guid id)
    {
        try
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized(new SyncApiResult<bool>
                {
                    Success = false,
                    Message = "Invalid user token",
                    StatusCode = 401
                });
            }

            var result = await _businessService.DeleteShopAsync(id, userId.Value);
            if (!result)
            {
                return NotFound(new SyncApiResult<bool>
                {
                    Success = false,
                    Message = "Shop not found or access denied",
                    StatusCode = 404
                });
            }

            _logger.LogInformation("Shop {ShopId} deleted by user {UserId}", id, userId);

            return Ok(new SyncApiResult<bool>
            {
                Success = true,
                Message = "Shop deleted successfully",
                StatusCode = 200,
                Data = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting shop {ShopId} by user {UserId}", id, GetUserIdFromToken());
            return StatusCode(500, new SyncApiResult<bool>
            {
                Success = false,
                Message = "Internal server error during shop deletion",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    #endregion

    #region Configuration Management

    /// <summary>
    /// Gets business configuration
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <returns>Business configuration</returns>
    [HttpGet("{businessId}/configuration")]
    public async Task<ActionResult<SyncApiResult<BusinessConfiguration>>> GetBusinessConfiguration(Guid businessId)
    {
        try
        {
            var configuration = await _businessService.GetBusinessConfigurationAsync(businessId);

            return Ok(new SyncApiResult<BusinessConfiguration>
            {
                Success = true,
                Message = "Business configuration retrieved successfully",
                StatusCode = 200,
                Data = configuration
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configuration for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<BusinessConfiguration>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Updates business configuration
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="configuration">New configuration</param>
    /// <returns>Update result</returns>
    [HttpPut("{businessId}/configuration")]
    public async Task<ActionResult<SyncApiResult<bool>>> UpdateBusinessConfiguration(Guid businessId, [FromBody] BusinessConfiguration configuration)
    {
        try
        {
            var result = await _businessService.UpdateBusinessConfigurationAsync(businessId, configuration);

            _logger.LogInformation("Business configuration updated for business {BusinessId}", businessId);

            return Ok(new SyncApiResult<bool>
            {
                Success = true,
                Message = "Business configuration updated successfully",
                StatusCode = 200,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating configuration for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<bool>
            {
                Success = false,
                Message = "Internal server error during configuration update",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Gets shop configuration
    /// </summary>
    /// <param name="shopId">Shop ID</param>
    /// <returns>Shop configuration</returns>
    [HttpGet("shops/{shopId}/configuration")]
    public async Task<ActionResult<SyncApiResult<ShopConfiguration>>> GetShopConfiguration(Guid shopId)
    {
        try
        {
            var configuration = await _businessService.GetShopConfigurationAsync(shopId);

            return Ok(new SyncApiResult<ShopConfiguration>
            {
                Success = true,
                Message = "Shop configuration retrieved successfully",
                StatusCode = 200,
                Data = configuration
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configuration for shop {ShopId}", shopId);
            return StatusCode(500, new SyncApiResult<ShopConfiguration>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Updates shop configuration
    /// </summary>
    /// <param name="shopId">Shop ID</param>
    /// <param name="configuration">New configuration</param>
    /// <returns>Update result</returns>
    [HttpPut("shops/{shopId}/configuration")]
    public async Task<ActionResult<SyncApiResult<bool>>> UpdateShopConfiguration(Guid shopId, [FromBody] ShopConfiguration configuration)
    {
        try
        {
            var result = await _businessService.UpdateShopConfigurationAsync(shopId, configuration);

            _logger.LogInformation("Shop configuration updated for shop {ShopId}", shopId);

            return Ok(new SyncApiResult<bool>
            {
                Success = true,
                Message = "Shop configuration updated successfully",
                StatusCode = 200,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating configuration for shop {ShopId}", shopId);
            return StatusCode(500, new SyncApiResult<bool>
            {
                Success = false,
                Message = "Internal server error during configuration update",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    #endregion

    private Guid? GetUserIdFromToken()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("user_id");
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }
}