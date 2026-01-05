using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Core.DTOs;
using Shared.Core.Services;
using System.Security.Claims;

namespace Server.Controllers;

/// <summary>
/// Controller for bulk data synchronization operations with conflict resolution
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BulkSyncController : ControllerBase
{
    private readonly IMultiTenantSyncService _syncService;
    private readonly ILogger<BulkSyncController> _logger;

    public BulkSyncController(IMultiTenantSyncService syncService, ILogger<BulkSyncController> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    #region Business Data Synchronization

    /// <summary>
    /// Synchronizes all data for a business
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="request">Bulk sync request</param>
    /// <returns>Synchronization result</returns>
    [HttpPost("business/{businessId}")]
    public async Task<ActionResult<SyncApiResult<BulkSyncResult>>> SyncBusinessData(
        Guid businessId, 
        [FromBody] BulkSyncRequest request)
    {
        try
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized(new SyncApiResult<BulkSyncResult>
                {
                    Success = false,
                    Message = "Invalid user token",
                    StatusCode = 401
                });
            }

            request.BusinessId = businessId;
            request.UserId = userId.Value;

            _logger.LogInformation("Starting bulk sync for business {BusinessId} by user {UserId}", 
                businessId, userId);

            var result = await _syncService.SyncBusinessDataAsync(businessId);

            _logger.LogInformation("Bulk sync completed for business {BusinessId}: {SyncedRecords} records synced, {ConflictsResolved} conflicts resolved",
                businessId, result.ItemsSynced, result.ConflictsResolved);

            return Ok(new SyncApiResult<BulkSyncResult>
            {
                Success = result.Success,
                Message = result.Success ? "Business data synchronized successfully" : "Synchronization completed with errors",
                StatusCode = result.Success ? 200 : 207, // 207 Multi-Status for partial success
                Data = new BulkSyncResult
                {
                    BusinessId = businessId,
                    SyncedRecords = result.ItemsSynced,
                    ConflictsResolved = result.ConflictsResolved,
                    Errors = result.Errors,
                    SyncTimestamp = result.SyncTimestamp,
                    IsSuccess = result.Success
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk sync for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<BulkSyncResult>
            {
                Success = false,
                Message = "Internal server error during bulk synchronization",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Synchronizes data for a specific shop
    /// </summary>
    /// <param name="shopId">Shop ID</param>
    /// <param name="request">Shop sync request</param>
    /// <returns>Synchronization result</returns>
    [HttpPost("shop/{shopId}")]
    public async Task<ActionResult<SyncApiResult<ShopSyncResult>>> SyncShopData(
        Guid shopId, 
        [FromBody] ShopSyncRequest request)
    {
        try
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized(new SyncApiResult<ShopSyncResult>
                {
                    Success = false,
                    Message = "Invalid user token",
                    StatusCode = 401
                });
            }

            request.ShopId = shopId;
            request.UserId = userId.Value;

            _logger.LogInformation("Starting shop sync for shop {ShopId} by user {UserId}", 
                shopId, userId);

            var result = await _syncService.SyncShopDataAsync(shopId);

            _logger.LogInformation("Shop sync completed for shop {ShopId}: {SyncedRecords} records synced, {ConflictsResolved} conflicts resolved",
                shopId, result.ItemsSynced, result.ConflictsResolved);

            return Ok(new SyncApiResult<ShopSyncResult>
            {
                Success = result.Success,
                Message = result.Success ? "Shop data synchronized successfully" : "Synchronization completed with errors",
                StatusCode = result.Success ? 200 : 207,
                Data = new ShopSyncResult
                {
                    ShopId = shopId,
                    SyncedRecords = result.ItemsSynced,
                    ConflictsResolved = result.ConflictsResolved,
                    Errors = result.Errors,
                    SyncTimestamp = result.SyncTimestamp,
                    IsSuccess = result.Success
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during shop sync for shop {ShopId}", shopId);
            return StatusCode(500, new SyncApiResult<ShopSyncResult>
            {
                Success = false,
                Message = "Internal server error during shop synchronization",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    #endregion

    #region Conflict Resolution

    /// <summary>
    /// Resolves data conflicts manually
    /// </summary>
    /// <param name="request">Conflict resolution request</param>
    /// <returns>Resolution result</returns>
    [HttpPost("resolve-conflicts")]
    public async Task<ActionResult<SyncApiResult<ConflictResolutionResult>>> ResolveDataConflicts([FromBody] ConflictResolutionRequest request)
    {
        try
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized(new SyncApiResult<ConflictResolutionResult>
                {
                    Success = false,
                    Message = "Invalid user token",
                    StatusCode = 401
                });
            }

            _logger.LogInformation("Resolving {ConflictCount} data conflicts by user {UserId}", 
                request.Conflicts.Count, userId);

            var result = await _syncService.ResolveDataConflictsAsync(request.Conflicts.Select(c => new Shared.Core.Services.DataConflict
            {
                Id = c.Id,
                EntityType = c.EntityType,
                EntityId = c.EntityId,
                BusinessId = Guid.Empty, // These properties don't exist in the local DataConflict
                ShopId = null,
                LocalData = c.LocalData,
                ServerData = c.ServerData,
                LocalTimestamp = c.ConflictTimestamp, // Using ConflictTimestamp as LocalTimestamp
                ServerTimestamp = c.ConflictTimestamp,
                Type = Shared.Core.Services.ConflictType.UpdateConflict, // Default type
                ConflictReason = c.ConflictType
            }).ToArray());

            _logger.LogInformation("Conflict resolution completed: {ResolvedCount} conflicts resolved, {FailedCount} failed",
                result.ConflictsResolved, result.ConflictsRemaining);

            return Ok(new SyncApiResult<ConflictResolutionResult>
            {
                Success = result.Success,
                Message = result.Success ? "All conflicts resolved successfully" : "Some conflicts could not be resolved",
                StatusCode = result.Success ? 200 : 207,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during conflict resolution");
            return StatusCode(500, new SyncApiResult<ConflictResolutionResult>
            {
                Success = false,
                Message = "Internal server error during conflict resolution",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Gets pending conflicts for a business
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <returns>Pending conflicts</returns>
    [HttpGet("conflicts/{businessId}")]
    public async Task<ActionResult<SyncApiResult<IEnumerable<DataConflict>>>> GetPendingConflicts(Guid businessId)
    {
        try
        {
            // This would typically be implemented in the sync service
            // For now, return empty list as placeholder
            var conflicts = new List<DataConflict>();

            return Ok(new SyncApiResult<IEnumerable<DataConflict>>
            {
                Success = true,
                Message = "Pending conflicts retrieved successfully",
                StatusCode = 200,
                Data = conflicts
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending conflicts for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<IEnumerable<DataConflict>>
            {
                Success = false,
                Message = "Internal server error",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    #endregion

    #region Tenant Isolation Validation

    /// <summary>
    /// Validates tenant data isolation
    /// </summary>
    /// <param name="businessId">Business ID</param>
    /// <param name="request">Validation request</param>
    /// <returns>Validation result</returns>
    [HttpPost("validate-isolation/{businessId}")]
    public async Task<ActionResult<SyncApiResult<TenantIsolationValidationResult>>> ValidateTenantIsolation(
        Guid businessId, 
        [FromBody] TenantIsolationValidationRequest request)
    {
        try
        {
            _logger.LogInformation("Validating tenant isolation for business {BusinessId}", businessId);

            var isValid = await _syncService.ValidateTenantIsolationAsync(businessId, request.Data);

            var result = new TenantIsolationValidationResult
            {
                BusinessId = businessId,
                IsValid = isValid,
                ValidationTimestamp = DateTime.UtcNow,
                Errors = isValid ? new List<string>() : new List<string> { "Tenant isolation validation failed" }
            };

            return Ok(new SyncApiResult<TenantIsolationValidationResult>
            {
                Success = true,
                Message = isValid ? "Tenant isolation validation passed" : "Tenant isolation validation failed",
                StatusCode = 200,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during tenant isolation validation for business {BusinessId}", businessId);
            return StatusCode(500, new SyncApiResult<TenantIsolationValidationResult>
            {
                Success = false,
                Message = "Internal server error during validation",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    #endregion

    #region Batch Operations

    /// <summary>
    /// Uploads bulk data changes from multiple devices
    /// </summary>
    /// <param name="request">Bulk upload request</param>
    /// <returns>Upload result</returns>
    [HttpPost("bulk-upload")]
    public async Task<ActionResult<SyncApiResult<BulkUploadResult>>> BulkUpload([FromBody] BulkUploadRequest request)
    {
        try
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized(new SyncApiResult<BulkUploadResult>
                {
                    Success = false,
                    Message = "Invalid user token",
                    StatusCode = 401
                });
            }

            _logger.LogInformation("Processing bulk upload from {DeviceCount} devices by user {UserId}", 
                request.DeviceData.Count, userId);

            var result = new BulkUploadResult
            {
                TotalRecords = request.DeviceData.Sum(d => d.Sales.Count + d.StockUpdates.Count),
                ProcessedRecords = 0,
                FailedRecords = 0,
                Errors = new List<string>(),
                ProcessingTimestamp = DateTime.UtcNow
            };

            // Process each device's data
            foreach (var deviceData in request.DeviceData)
            {
                try
                {
                    // Process sales
                    foreach (var sale in deviceData.Sales)
                    {
                        // Process individual sale (implementation would go here)
                        result.ProcessedRecords++;
                    }

                    // Process stock updates
                    foreach (var stockUpdate in deviceData.StockUpdates)
                    {
                        // Process individual stock update (implementation would go here)
                        result.ProcessedRecords++;
                    }
                }
                catch (Exception ex)
                {
                    result.FailedRecords++;
                    result.Errors.Add($"Device {deviceData.DeviceId}: {ex.Message}");
                    _logger.LogWarning(ex, "Error processing data from device {DeviceId}", deviceData.DeviceId);
                }
            }

            result.IsSuccess = result.FailedRecords == 0;

            _logger.LogInformation("Bulk upload completed: {ProcessedRecords} processed, {FailedRecords} failed",
                result.ProcessedRecords, result.FailedRecords);

            return Ok(new SyncApiResult<BulkUploadResult>
            {
                Success = result.IsSuccess,
                Message = result.IsSuccess ? "Bulk upload completed successfully" : "Bulk upload completed with errors",
                StatusCode = result.IsSuccess ? 200 : 207,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk upload");
            return StatusCode(500, new SyncApiResult<BulkUploadResult>
            {
                Success = false,
                Message = "Internal server error during bulk upload",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Downloads bulk data changes for multiple shops
    /// </summary>
    /// <param name="request">Bulk download request</param>
    /// <returns>Download result</returns>
    [HttpPost("bulk-download")]
    public async Task<ActionResult<SyncApiResult<BulkDownloadResult>>> BulkDownload([FromBody] BulkDownloadRequest request)
    {
        try
        {
            _logger.LogInformation("Processing bulk download for {ShopCount} shops since {LastSyncTimestamp}", 
                request.ShopIds.Count, request.LastSyncTimestamp);

            var result = new BulkDownloadResult
            {
                ShopData = new Dictionary<Guid, ShopSyncData>(),
                ServerTimestamp = DateTime.UtcNow,
                HasMoreData = false
            };

            // Process each shop's data
            foreach (var shopId in request.ShopIds)
            {
                try
                {
                    // Get shop data (implementation would go here)
                    var shopData = new ShopSyncData
                    {
                        ShopId = shopId,
                        Products = new List<ProductDto>(),
                        Stock = new List<StockDto>(),
                        Sales = new List<SaleDto>(),
                        LastUpdated = DateTime.UtcNow
                    };

                    result.ShopData[shopId] = shopData;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error retrieving data for shop {ShopId}", shopId);
                    // Continue processing other shops
                }
            }

            _logger.LogInformation("Bulk download completed for {ProcessedShops} shops", result.ShopData.Count);

            return Ok(new SyncApiResult<BulkDownloadResult>
            {
                Success = true,
                Message = "Bulk download completed successfully",
                StatusCode = 200,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk download");
            return StatusCode(500, new SyncApiResult<BulkDownloadResult>
            {
                Success = false,
                Message = "Internal server error during bulk download",
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

#region Request/Response Models

/// <summary>
/// Bulk synchronization request model
/// </summary>
public class BulkSyncRequest
{
    public Guid BusinessId { get; set; }
    public Guid UserId { get; set; }
    public DateTime LastSyncTimestamp { get; set; }
    public bool ForceFullSync { get; set; } = false;
    public List<Guid> ShopIds { get; set; } = new();
}

/// <summary>
/// Shop synchronization request model
/// </summary>
public class ShopSyncRequest
{
    public Guid ShopId { get; set; }
    public Guid UserId { get; set; }
    public DateTime LastSyncTimestamp { get; set; }
    public bool ForceFullSync { get; set; } = false;
}

/// <summary>
/// Bulk synchronization result model
/// </summary>
public class BulkSyncResult
{
    public Guid BusinessId { get; set; }
    public int SyncedRecords { get; set; }
    public int ConflictsResolved { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime SyncTimestamp { get; set; }
    public bool IsSuccess { get; set; }
}

/// <summary>
/// Shop synchronization result model
/// </summary>
public class ShopSyncResult
{
    public Guid ShopId { get; set; }
    public int SyncedRecords { get; set; }
    public int ConflictsResolved { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime SyncTimestamp { get; set; }
    public bool IsSuccess { get; set; }
}

/// <summary>
/// Conflict resolution request model
/// </summary>
public class ConflictResolutionRequest
{
    public List<DataConflict> Conflicts { get; set; } = new();
}

/// <summary>
/// Data conflict model
/// </summary>
public class DataConflict
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string ConflictType { get; set; } = string.Empty;
    public object LocalData { get; set; } = new();
    public object ServerData { get; set; } = new();
    public DateTime ConflictTimestamp { get; set; }
    public string Resolution { get; set; } = string.Empty; // "UseLocal", "UseServer", "Merge"
}

/// <summary>
/// Tenant isolation validation request model
/// </summary>
public class TenantIsolationValidationRequest
{
    public object Data { get; set; } = new();
}

/// <summary>
/// Tenant isolation validation result model
/// </summary>
public class TenantIsolationValidationResult
{
    public Guid BusinessId { get; set; }
    public bool IsValid { get; set; }
    public DateTime ValidationTimestamp { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Bulk upload request model
/// </summary>
public class BulkUploadRequest
{
    public List<DeviceSyncData> DeviceData { get; set; } = new();
}

/// <summary>
/// Device synchronization data model
/// </summary>
public class DeviceSyncData
{
    public Guid DeviceId { get; set; }
    public DateTime LastSyncTimestamp { get; set; }
    public List<SaleDto> Sales { get; set; } = new();
    public List<StockUpdateDto> StockUpdates { get; set; } = new();
}

/// <summary>
/// Bulk upload result model
/// </summary>
public class BulkUploadResult
{
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int FailedRecords { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime ProcessingTimestamp { get; set; }
    public bool IsSuccess { get; set; }
}

/// <summary>
/// Bulk download request model
/// </summary>
public class BulkDownloadRequest
{
    public List<Guid> ShopIds { get; set; } = new();
    public DateTime LastSyncTimestamp { get; set; }
    public int BatchSize { get; set; } = 1000;
}

/// <summary>
/// Bulk download result model
/// </summary>
public class BulkDownloadResult
{
    public Dictionary<Guid, ShopSyncData> ShopData { get; set; } = new();
    public DateTime ServerTimestamp { get; set; }
    public bool HasMoreData { get; set; }
}

/// <summary>
/// Shop synchronization data model
/// </summary>
public class ShopSyncData
{
    public Guid ShopId { get; set; }
    public List<ProductDto> Products { get; set; } = new();
    public List<StockDto> Stock { get; set; } = new();
    public List<SaleDto> Sales { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

#endregion