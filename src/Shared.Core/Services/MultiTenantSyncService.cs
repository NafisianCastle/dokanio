using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of multi-tenant synchronization service
/// Extends existing sync capabilities with business and shop-level data isolation
/// </summary>
public class MultiTenantSyncService : IMultiTenantSyncService
{
    private readonly PosDbContext _context;
    private readonly ISyncEngine _syncEngine;
    private readonly ISyncApiClient _apiClient;
    private readonly IBusinessRepository _businessRepository;
    private readonly IShopRepository _shopRepository;
    private readonly ILogger<MultiTenantSyncService> _logger;
    private readonly SyncConfiguration _configuration;

    public MultiTenantSyncService(
        PosDbContext context,
        ISyncEngine syncEngine,
        ISyncApiClient apiClient,
        IBusinessRepository businessRepository,
        IShopRepository shopRepository,
        ILogger<MultiTenantSyncService> logger,
        SyncConfiguration configuration)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _syncEngine = syncEngine ?? throw new ArgumentNullException(nameof(syncEngine));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _businessRepository = businessRepository ?? throw new ArgumentNullException(nameof(businessRepository));
        _shopRepository = shopRepository ?? throw new ArgumentNullException(nameof(shopRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Synchronizes all data for a specific business
    /// </summary>
    public async Task<MultiTenantSyncResult> SyncBusinessDataAsync(Guid businessId)
    {
        var result = new MultiTenantSyncResult
        {
            BusinessId = businessId,
            SyncTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting business data synchronization for business {BusinessId}", businessId);

            // Validate business exists and user has access
            var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
            if (business == null)
            {
                result.Success = false;
                result.Message = "Business not found";
                return result;
            }

            // Validate tenant isolation for business data
            var isolationValid = await ValidateTenantIsolationAsync(businessId, business);
            if (!isolationValid)
            {
                result.Success = false;
                result.Message = "Tenant isolation validation failed";
                result.IsolationViolations.Add(new TenantIsolationViolation
                {
                    EntityType = nameof(Business),
                    EntityId = businessId,
                    ExpectedBusinessId = businessId,
                    ActualBusinessId = business.Id,
                    ViolationType = "Business Access Violation",
                    Description = "Business data access validation failed"
                });
                return result;
            }

            // Sync business metadata first
            var metadataResult = await SyncBusinessMetadataAsync(businessId);
            result.BusinessRecordsSynced += metadataResult.BusinessRecordsSynced;

            // Sync all shops in the business
            var shopSyncResults = new List<MultiTenantSyncResult>();
            foreach (var shop in business.Shops.Where(s => s.IsActive))
            {
                var shopResult = await SyncShopDataAsync(shop.Id);
                shopSyncResults.Add(shopResult);
                result.ShopRecordsSynced += shopResult.ShopRecordsSynced;
                result.ItemsSynced += shopResult.ItemsSynced;
            }

            // Check for conflicts and resolve them
            var conflicts = await DetectBusinessConflictsAsync(businessId);
            if (conflicts.Any())
            {
                var conflictResult = await ResolveDataConflictsAsync(conflicts.ToArray());
                result.ConflictsResolved = conflictResult.ConflictsResolved;
                
                if (!conflictResult.Success)
                {
                    result.Errors.AddRange(conflictResult.Errors);
                }
            }

            // Aggregate results
            result.Success = shopSyncResults.All(r => r.Success) && metadataResult.Success;
            result.RecordsUploaded = shopSyncResults.Sum(r => r.RecordsUploaded) + metadataResult.RecordsUploaded;
            result.RecordsDownloaded = shopSyncResults.Sum(r => r.RecordsDownloaded) + metadataResult.RecordsDownloaded;

            // Collect business-specific metrics
            result.BusinessSpecificMetrics["TotalShops"] = business.Shops.Count;
            result.BusinessSpecificMetrics["ActiveShops"] = business.Shops.Count(s => s.IsActive);
            result.BusinessSpecificMetrics["BusinessType"] = business.Type.ToString();

            if (!result.Success)
            {
                var errors = shopSyncResults.Where(r => !r.Success).SelectMany(r => r.Errors);
                result.Errors.AddRange(errors);
                result.Message = $"Business sync completed with errors: {string.Join("; ", result.Errors)}";
            }
            else
            {
                result.Message = $"Successfully synced business with {business.Shops.Count} shops";
            }

            _logger.LogInformation("Business data synchronization completed for {BusinessId}. Success: {Success}, Records: {Records}",
                businessId, result.Success, result.ItemsSynced);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during business data synchronization for {BusinessId}", businessId);
            result.Success = false;
            result.Message = ex.Message;
            result.Errors.Add(ex.ToString());
            return result;
        }
    }

    /// <summary>
    /// Synchronizes data for a specific shop
    /// </summary>
    public async Task<MultiTenantSyncResult> SyncShopDataAsync(Guid shopId)
    {
        var result = new MultiTenantSyncResult
        {
            ShopId = shopId,
            SyncTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogDebug("Starting shop data synchronization for shop {ShopId}", shopId);

            // Get shop with business information
            var shop = await _shopRepository.GetShopWithBusinessAsync(shopId);
            if (shop == null)
            {
                result.Success = false;
                result.Message = "Shop not found";
                return result;
            }

            result.BusinessId = shop.BusinessId;

            // Validate tenant isolation
            var isolationValid = await ValidateTenantIsolationAsync(shop.BusinessId, shop);
            if (!isolationValid)
            {
                result.Success = false;
                result.Message = "Tenant isolation validation failed for shop";
                result.IsolationViolations.Add(new TenantIsolationViolation
                {
                    EntityType = nameof(Shop),
                    EntityId = shopId,
                    ExpectedBusinessId = shop.BusinessId,
                    ActualBusinessId = shop.BusinessId,
                    ViolationType = "Shop Access Violation",
                    Description = "Shop data access validation failed"
                });
                return result;
            }

            // Sync shop-specific data with business context
            var shopSyncResult = await SyncShopSpecificDataAsync(shop);
            result.ShopRecordsSynced = shopSyncResult.ItemsSynced;
            result.ItemsSynced = shopSyncResult.ItemsSynced;
            result.RecordsUploaded = shopSyncResult.RecordsUploaded;
            result.RecordsDownloaded = shopSyncResult.RecordsDownloaded;
            result.Success = shopSyncResult.Success;

            if (!result.Success)
            {
                result.Errors.AddRange(shopSyncResult.Errors);
                result.Message = shopSyncResult.Message;
            }
            else
            {
                result.Message = $"Successfully synced shop {shop.Name}";
            }

            // Add shop-specific metrics
            result.BusinessSpecificMetrics["ShopName"] = shop.Name;
            result.BusinessSpecificMetrics["BusinessType"] = shop.Business.Type.ToString();

            _logger.LogDebug("Shop data synchronization completed for {ShopId}. Success: {Success}",
                shopId, result.Success);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during shop data synchronization for {ShopId}", shopId);
            result.Success = false;
            result.Message = ex.Message;
            result.Errors.Add(ex.ToString());
            return result;
        }
    }

    /// <summary>
    /// Resolves data conflicts using business-specific rules
    /// </summary>
    public async Task<ConflictResolutionResult> ResolveDataConflictsAsync(DataConflict[] conflicts)
    {
        var result = new ConflictResolutionResult();

        try
        {
            _logger.LogInformation("Resolving {ConflictCount} data conflicts", conflicts.Length);

            foreach (var conflict in conflicts)
            {
                try
                {
                    var resolved = await ResolveIndividualConflictAsync(conflict);
                    if (resolved)
                    {
                        result.ConflictsResolved++;
                        result.ResolutionStrategies.Add($"{conflict.EntityType}:{conflict.Type}");
                    }
                    else
                    {
                        result.ConflictsRemaining++;
                        result.Errors.Add($"Failed to resolve conflict for {conflict.EntityType} {conflict.EntityId}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resolving conflict for {EntityType} {EntityId}", 
                        conflict.EntityType, conflict.EntityId);
                    result.ConflictsRemaining++;
                    result.Errors.Add($"Exception resolving {conflict.EntityType}: {ex.Message}");
                }
            }

            result.Success = result.ConflictsRemaining == 0;
            
            _logger.LogInformation("Conflict resolution completed. Resolved: {Resolved}, Remaining: {Remaining}",
                result.ConflictsResolved, result.ConflictsRemaining);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during conflict resolution");
            result.Success = false;
            result.Errors.Add(ex.Message);
            return result;
        }
    }

    /// <summary>
    /// Validates that data belongs to the correct tenant
    /// </summary>
    public async Task<bool> ValidateTenantIsolationAsync(Guid businessId, object data)
    {
        try
        {
            // Validate based on entity type
            switch (data)
            {
                case Business business:
                    return business.Id == businessId;
                
                case Shop shop:
                    return shop.BusinessId == businessId;
                
                case Product product:
                    var productShop = await _context.Shops
                        .Where(s => s.Id == product.ShopId && s.BusinessId == businessId)
                        .FirstOrDefaultAsync();
                    return productShop != null;
                
                case Sale sale:
                    var saleShop = await _context.Shops
                        .Where(s => s.Id == sale.ShopId && s.BusinessId == businessId)
                        .FirstOrDefaultAsync();
                    return saleShop != null;
                
                case Stock stock:
                    var stockProduct = await _context.Products
                        .Include(p => p.Shop)
                        .Where(p => p.Id == stock.ProductId && p.Shop.BusinessId == businessId)
                        .FirstOrDefaultAsync();
                    return stockProduct != null;
                
                case User user:
                    return user.BusinessId == businessId;
                
                default:
                    _logger.LogWarning("Unknown entity type for tenant validation: {EntityType}", data.GetType().Name);
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating tenant isolation for business {BusinessId}", businessId);
            return false;
        }
    }

    /// <summary>
    /// Synchronizes business and shop metadata
    /// </summary>
    public async Task<MultiTenantSyncResult> SyncBusinessMetadataAsync(Guid businessId)
    {
        var result = new MultiTenantSyncResult
        {
            BusinessId = businessId,
            SyncTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogDebug("Syncing business metadata for {BusinessId}", businessId);

            // Get business and shops that need syncing
            var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
            if (business == null)
            {
                result.Success = false;
                result.Message = "Business not found";
                return result;
            }

            var unsyncedBusinessData = new List<object>();
            
            // Check if business needs syncing
            if (business.SyncStatus != SyncStatus.Synced)
            {
                unsyncedBusinessData.Add(business);
            }

            // Check shops that need syncing
            var unsyncedShops = business.Shops.Where(s => s.SyncStatus != SyncStatus.Synced).ToList();
            unsyncedBusinessData.AddRange(unsyncedShops);

            if (!unsyncedBusinessData.Any())
            {
                result.Success = true;
                result.Message = "No metadata to sync";
                return result;
            }

            // Create metadata sync request
            var metadataRequest = new BusinessMetadataSyncRequest
            {
                BusinessId = businessId,
                DeviceId = _configuration.DeviceId,
                Business = business.SyncStatus != SyncStatus.Synced ? MapBusinessToDto(business) : null,
                Shops = unsyncedShops.Select(MapShopToDto).ToList()
            };

            // Upload metadata to server
            var uploadResult = await _apiClient.UploadBusinessMetadataAsync(metadataRequest);
            
            if (uploadResult.Success)
            {
                // Mark as synced
                business.SyncStatus = SyncStatus.Synced;
                business.ServerSyncedAt = DateTime.UtcNow;
                
                foreach (var shop in unsyncedShops)
                {
                    shop.SyncStatus = SyncStatus.Synced;
                    shop.ServerSyncedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                result.Success = true;
                result.BusinessRecordsSynced = unsyncedBusinessData.Count;
                result.RecordsUploaded = unsyncedBusinessData.Count;
                result.Message = $"Synced {unsyncedBusinessData.Count} metadata records";
            }
            else
            {
                result.Success = false;
                result.Message = uploadResult.Message;
                result.Errors.AddRange(uploadResult.Errors);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing business metadata for {BusinessId}", businessId);
            result.Success = false;
            result.Message = ex.Message;
            result.Errors.Add(ex.ToString());
            return result;
        }
    }

    /// <summary>
    /// Gets synchronization status for all shops in a business
    /// </summary>
    public async Task<IEnumerable<ShopSyncStatus>> GetBusinessSyncStatusAsync(Guid businessId)
    {
        try
        {
            var shops = await _shopRepository.GetShopsByBusinessAsync(businessId);
            var statusList = new List<ShopSyncStatus>();

            foreach (var shop in shops)
            {
                var status = new ShopSyncStatus
                {
                    ShopId = shop.Id,
                    ShopName = shop.Name,
                    LastSyncTime = shop.ServerSyncedAt,
                    IsOnline = true, // This would be determined by connectivity service
                    PendingUploads = await GetPendingUploadsCountAsync(shop.Id),
                    PendingDownloads = 0, // This would be determined by server comparison
                    HealthStatus = DetermineHealthStatus(shop)
                };

                statusList.Add(status);
            }

            return statusList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting business sync status for {BusinessId}", businessId);
            return Enumerable.Empty<ShopSyncStatus>();
        }
    }

    /// <summary>
    /// Performs bulk synchronization for multiple shops
    /// </summary>
    public async Task<BulkSyncResult> BulkSyncShopsAsync(IEnumerable<Guid> shopIds)
    {
        var startTime = DateTime.UtcNow;
        var result = new BulkSyncResult
        {
            TotalShops = shopIds.Count()
        };

        try
        {
            _logger.LogInformation("Starting bulk sync for {ShopCount} shops", result.TotalShops);

            var syncTasks = shopIds.Select(async shopId =>
            {
                try
                {
                    return await SyncShopDataAsync(shopId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in bulk sync for shop {ShopId}", shopId);
                    return new MultiTenantSyncResult
                    {
                        ShopId = shopId,
                        Success = false,
                        Message = ex.Message,
                        Errors = { ex.ToString() }
                    };
                }
            });

            var syncResults = await Task.WhenAll(syncTasks);
            result.Results.AddRange(syncResults);

            result.SuccessfulSyncs = syncResults.Count(r => r.Success);
            result.FailedSyncs = syncResults.Count(r => !r.Success);
            result.Success = result.FailedSyncs == 0;
            result.TotalDuration = DateTime.UtcNow - startTime;

            _logger.LogInformation("Bulk sync completed. Success: {Successful}, Failed: {Failed}, Duration: {Duration}",
                result.SuccessfulSyncs, result.FailedSyncs, result.TotalDuration);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk sync operation");
            result.Success = false;
            result.TotalDuration = DateTime.UtcNow - startTime;
            return result;
        }
    }

    #region Private Helper Methods

    private async Task<SyncResult> SyncShopSpecificDataAsync(Shop shop)
    {
        // This would use the existing sync engine but with shop-specific filtering
        // For now, delegate to the existing sync engine
        return await _syncEngine.SyncAllAsync();
    }

    private async Task<List<DataConflict>> DetectBusinessConflictsAsync(Guid businessId)
    {
        var conflicts = new List<DataConflict>();

        try
        {
            // This would implement conflict detection logic
            // For now, return empty list
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting conflicts for business {BusinessId}", businessId);
        }

        return conflicts;
    }

    private async Task<bool> ResolveIndividualConflictAsync(DataConflict conflict)
    {
        try
        {
            // Implement business-specific conflict resolution rules
            switch (conflict.Type)
            {
                case ConflictType.UpdateConflict:
                    return await ResolveUpdateConflictAsync(conflict);
                
                case ConflictType.DeleteConflict:
                    return await ResolveDeleteConflictAsync(conflict);
                
                case ConflictType.CreateConflict:
                    return await ResolveCreateConflictAsync(conflict);
                
                case ConflictType.BusinessRuleViolation:
                    return await ResolveBusinessRuleViolationAsync(conflict);
                
                case ConflictType.TenantIsolationViolation:
                    return await ResolveTenantIsolationViolationAsync(conflict);
                
                default:
                    _logger.LogWarning("Unknown conflict type: {ConflictType}", conflict.Type);
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving individual conflict {ConflictId}", conflict.Id);
            return false;
        }
    }

    private async Task<bool> ResolveUpdateConflictAsync(DataConflict conflict)
    {
        // Implement last-write-wins for most entities, server-wins for prices
        switch (conflict.EntityType)
        {
            case nameof(Product):
                // Server wins for product prices and details
                return await ApplyServerDataAsync(conflict);
            
            case nameof(Stock):
                // Recalculate from transactions (server authoritative)
                return await ApplyServerDataAsync(conflict);
            
            default:
                // Last write wins based on timestamp
                return conflict.ServerTimestamp > conflict.LocalTimestamp 
                    ? await ApplyServerDataAsync(conflict)
                    : await ApplyLocalDataAsync(conflict);
        }
    }

    private async Task<bool> ResolveDeleteConflictAsync(DataConflict conflict)
    {
        // Server wins for delete conflicts
        return await ApplyServerDataAsync(conflict);
    }

    private async Task<bool> ResolveCreateConflictAsync(DataConflict conflict)
    {
        // Merge both records if possible, otherwise server wins
        return await ApplyServerDataAsync(conflict);
    }

    private async Task<bool> ResolveBusinessRuleViolationAsync(DataConflict conflict)
    {
        // Apply business-specific rules
        return await ApplyServerDataAsync(conflict);
    }

    private async Task<bool> ResolveTenantIsolationViolationAsync(DataConflict conflict)
    {
        // Log security violation and reject the data
        _logger.LogWarning("Tenant isolation violation detected: {Conflict}", JsonSerializer.Serialize(conflict));
        return false;
    }

    private async Task<bool> ApplyServerDataAsync(DataConflict conflict)
    {
        // Apply server data to local database
        // Implementation would depend on entity type
        await Task.CompletedTask;
        return true;
    }

    private async Task<bool> ApplyLocalDataAsync(DataConflict conflict)
    {
        // Keep local data and mark for upload
        // Implementation would depend on entity type
        await Task.CompletedTask;
        return true;
    }

    private async Task<int> GetPendingUploadsCountAsync(Guid shopId)
    {
        try
        {
            var pendingSales = await _context.Sales
                .Where(s => s.ShopId == shopId && s.SyncStatus != SyncStatus.Synced)
                .CountAsync();

            var pendingProducts = await _context.Products
                .Where(p => p.ShopId == shopId && p.SyncStatus != SyncStatus.Synced)
                .CountAsync();

            return pendingSales + pendingProducts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending uploads count for shop {ShopId}", shopId);
            return 0;
        }
    }

    private SyncHealthStatus DetermineHealthStatus(Shop shop)
    {
        if (shop.ServerSyncedAt == null)
            return SyncHealthStatus.Warning;

        var timeSinceLastSync = DateTime.UtcNow - shop.ServerSyncedAt.Value;
        
        if (timeSinceLastSync > TimeSpan.FromHours(24))
            return SyncHealthStatus.Error;
        
        if (timeSinceLastSync > TimeSpan.FromHours(2))
            return SyncHealthStatus.Warning;
        
        return SyncHealthStatus.Healthy;
    }

    private BusinessDto MapBusinessToDto(Business business)
    {
        return new BusinessDto
        {
            Id = business.Id,
            Name = business.Name,
            Type = business.Type,
            OwnerId = business.OwnerId,
            Description = business.Description,
            Address = business.Address,
            Phone = business.Phone,
            Email = business.Email,
            TaxId = business.TaxId,
            Configuration = business.Configuration,
            IsActive = business.IsActive,
            CreatedAt = business.CreatedAt,
            UpdatedAt = business.UpdatedAt
        };
    }

    private ShopDto MapShopToDto(Shop shop)
    {
        return new ShopDto
        {
            Id = shop.Id,
            BusinessId = shop.BusinessId,
            Name = shop.Name,
            Address = shop.Address,
            Phone = shop.Phone,
            Email = shop.Email,
            Configuration = shop.Configuration,
            IsActive = shop.IsActive,
            CreatedAt = shop.CreatedAt,
            UpdatedAt = shop.UpdatedAt
        };
    }

    #endregion
}