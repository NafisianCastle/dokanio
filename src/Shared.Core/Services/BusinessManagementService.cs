using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// Service implementation for managing businesses and shops in the multi-tenant POS system
/// </summary>
public class BusinessManagementService : IBusinessManagementService
{
    private readonly IBusinessRepository _businessRepository;
    private readonly IShopRepository _shopRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<BusinessManagementService> _logger;

    public BusinessManagementService(
        IBusinessRepository businessRepository,
        IShopRepository shopRepository,
        IUserRepository userRepository,
        ILogger<BusinessManagementService> logger)
    {
        _businessRepository = businessRepository;
        _shopRepository = shopRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    #region Business Management

    public async Task<BusinessResponse> CreateBusinessAsync(CreateBusinessRequest request)
    {
        _logger.LogInformation("Creating business: {BusinessName} for owner: {OwnerId}", request.Name, request.OwnerId);

        // Validate owner exists
        var owner = await _userRepository.GetByIdAsync(request.OwnerId);
        if (owner == null)
        {
            throw new ArgumentException($"Owner with ID {request.OwnerId} not found");
        }

        // Check if business name is unique for this owner
        var isUnique = await _businessRepository.IsBusinessNameUniqueAsync(request.Name, request.OwnerId);
        if (!isUnique)
        {
            throw new InvalidOperationException($"Business name '{request.Name}' already exists for this owner");
        }

        // Create business entity
        var business = new Business
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            OwnerId = request.OwnerId,
            Description = request.Description,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            TaxId = request.TaxId,
            Configuration = request.Configuration,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(), // TODO: Get from current device context
            SyncStatus = SyncStatus.NotSynced
        };

        await _businessRepository.AddAsync(business);
        await _businessRepository.SaveChangesAsync();

        _logger.LogInformation("Business created successfully: {BusinessId}", business.Id);

        return await MapToBusinessResponseAsync(business);
    }

    public async Task<BusinessResponse> UpdateBusinessAsync(UpdateBusinessRequest request)
    {
        _logger.LogInformation("Updating business: {BusinessId}", request.Id);

        var business = await _businessRepository.GetByIdAsync(request.Id);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {request.Id} not found");
        }

        // Check if business name is unique for this owner (excluding current business)
        var isUnique = await _businessRepository.IsBusinessNameUniqueAsync(request.Name, business.OwnerId, request.Id);
        if (!isUnique)
        {
            throw new InvalidOperationException($"Business name '{request.Name}' already exists for this owner");
        }

        // Update business properties
        business.Name = request.Name;
        business.Description = request.Description;
        business.Address = request.Address;
        business.Phone = request.Phone;
        business.Email = request.Email;
        business.TaxId = request.TaxId;
        business.Configuration = request.Configuration;
        business.IsActive = request.IsActive;
        business.UpdatedAt = DateTime.UtcNow;
        business.SyncStatus = SyncStatus.NotSynced;

        await _businessRepository.UpdateAsync(business);
        await _businessRepository.SaveChangesAsync();

        _logger.LogInformation("Business updated successfully: {BusinessId}", business.Id);

        return await MapToBusinessResponseAsync(business);
    }

    public async Task<BusinessResponse?> GetBusinessByIdAsync(Guid businessId)
    {
        var business = await _businessRepository.GetBusinessWithShopsAsync(businessId);
        if (business == null)
            return null;

        return await MapToBusinessResponseAsync(business);
    }

    public async Task<IEnumerable<BusinessResponse>> GetBusinessesByOwnerAsync(Guid ownerId)
    {
        var businesses = await _businessRepository.GetBusinessesByOwnerAsync(ownerId);
        var responses = new List<BusinessResponse>();

        foreach (var business in businesses)
        {
            responses.Add(await MapToBusinessResponseAsync(business));
        }

        return responses;
    }

    public async Task<IEnumerable<BusinessResponse>> GetBusinessesByTypeAsync(BusinessType businessType)
    {
        var businesses = await _businessRepository.GetBusinessesByTypeAsync(businessType);
        var responses = new List<BusinessResponse>();

        foreach (var business in businesses)
        {
            responses.Add(await MapToBusinessResponseAsync(business));
        }

        return responses;
    }

    public async Task<bool> DeleteBusinessAsync(Guid businessId, Guid userId)
    {
        _logger.LogInformation("Deleting business: {BusinessId} by user: {UserId}", businessId, userId);

        var business = await _businessRepository.GetByIdAsync(businessId);
        if (business == null)
            return false;

        // Verify user has permission to delete this business
        if (business.OwnerId != userId)
        {
            _logger.LogWarning("User {UserId} attempted to delete business {BusinessId} they don't own", userId, businessId);
            return false;
        }

        await _businessRepository.DeleteAsync(businessId);
        await _businessRepository.SaveChangesAsync();

        _logger.LogInformation("Business deleted successfully: {BusinessId}", businessId);
        return true;
    }

    #endregion

    #region Shop Management

    public async Task<ShopResponse> CreateShopAsync(CreateShopRequest request)
    {
        _logger.LogInformation("Creating shop: {ShopName} for business: {BusinessId}", request.Name, request.BusinessId);

        // Validate business exists
        var business = await _businessRepository.GetByIdAsync(request.BusinessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {request.BusinessId} not found");
        }

        // Check if shop name is unique within the business
        var isUnique = await _shopRepository.IsShopNameUniqueAsync(request.Name, request.BusinessId);
        if (!isUnique)
        {
            throw new InvalidOperationException($"Shop name '{request.Name}' already exists in this business");
        }

        // Create shop entity
        var shop = new Shop
        {
            Id = Guid.NewGuid(),
            BusinessId = request.BusinessId,
            Name = request.Name,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            Configuration = request.Configuration,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(), // TODO: Get from current device context
            SyncStatus = SyncStatus.NotSynced
        };

        await _shopRepository.AddAsync(shop);
        await _shopRepository.SaveChangesAsync();

        _logger.LogInformation("Shop created successfully: {ShopId}", shop.Id);

        return await MapToShopResponseAsync(shop);
    }

    public async Task<ShopResponse> UpdateShopAsync(UpdateShopRequest request)
    {
        _logger.LogInformation("Updating shop: {ShopId}", request.Id);

        var shop = await _shopRepository.GetShopWithBusinessAsync(request.Id);
        if (shop == null)
        {
            throw new ArgumentException($"Shop with ID {request.Id} not found");
        }

        // Check if shop name is unique within the business (excluding current shop)
        var isUnique = await _shopRepository.IsShopNameUniqueAsync(request.Name, shop.BusinessId, request.Id);
        if (!isUnique)
        {
            throw new InvalidOperationException($"Shop name '{request.Name}' already exists in this business");
        }

        // Update shop properties
        shop.Name = request.Name;
        shop.Address = request.Address;
        shop.Phone = request.Phone;
        shop.Email = request.Email;
        shop.Configuration = request.Configuration;
        shop.IsActive = request.IsActive;
        shop.UpdatedAt = DateTime.UtcNow;
        shop.SyncStatus = SyncStatus.NotSynced;

        await _shopRepository.UpdateAsync(shop);
        await _shopRepository.SaveChangesAsync();

        _logger.LogInformation("Shop updated successfully: {ShopId}", shop.Id);

        return await MapToShopResponseAsync(shop);
    }

    public async Task<ShopResponse?> GetShopByIdAsync(Guid shopId)
    {
        var shop = await _shopRepository.GetShopWithBusinessAsync(shopId);
        if (shop == null)
            return null;

        return await MapToShopResponseAsync(shop);
    }

    public async Task<IEnumerable<ShopResponse>> GetShopsByBusinessAsync(Guid businessId)
    {
        var shops = await _shopRepository.GetShopsByBusinessAsync(businessId);
        var responses = new List<ShopResponse>();

        foreach (var shop in shops)
        {
            responses.Add(await MapToShopResponseAsync(shop));
        }

        return responses;
    }

    public async Task<IEnumerable<ShopResponse>> GetShopsByBusinessAndUserAsync(Guid businessId, Guid userId)
    {
        var shops = await _shopRepository.GetShopsByBusinessAndUserAsync(businessId, userId);
        var responses = new List<ShopResponse>();

        foreach (var shop in shops)
        {
            responses.Add(await MapToShopResponseAsync(shop));
        }

        return responses;
    }

    public async Task<bool> DeleteShopAsync(Guid shopId, Guid userId)
    {
        _logger.LogInformation("Deleting shop: {ShopId} by user: {UserId}", shopId, userId);

        var shop = await _shopRepository.GetShopWithBusinessAsync(shopId);
        if (shop == null)
            return false;

        // Verify user has permission to delete this shop
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || (shop.Business.OwnerId != userId && user.Role != UserRole.BusinessOwner))
        {
            _logger.LogWarning("User {UserId} attempted to delete shop {ShopId} without permission", userId, shopId);
            return false;
        }

        await _shopRepository.DeleteAsync(shopId);
        await _shopRepository.SaveChangesAsync();

        _logger.LogInformation("Shop deleted successfully: {ShopId}", shopId);
        return true;
    }

    #endregion

    #region Configuration Management

    public async Task<BusinessConfiguration> GetBusinessConfigurationAsync(Guid businessId)
    {
        var business = await _businessRepository.GetByIdAsync(businessId);
        if (business == null)
        {
            throw new ArgumentException($"Business with ID {businessId} not found");
        }

        if (string.IsNullOrEmpty(business.Configuration))
        {
            return await GetDefaultBusinessConfigurationAsync(business.Type);
        }

        try
        {
            var config = JsonSerializer.Deserialize<BusinessConfiguration>(business.Configuration);
            return config ?? await GetDefaultBusinessConfigurationAsync(business.Type);
        }
        catch (JsonException)
        {
            _logger.LogWarning("Invalid JSON configuration for business {BusinessId}, returning default", businessId);
            return await GetDefaultBusinessConfigurationAsync(business.Type);
        }
    }

    public async Task<bool> UpdateBusinessConfigurationAsync(Guid businessId, BusinessConfiguration configuration)
    {
        var business = await _businessRepository.GetByIdAsync(businessId);
        if (business == null)
            return false;

        // Validate configuration
        var validationResult = await ValidateBusinessTypeConfigurationAsync(business.Type, configuration);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException($"Invalid configuration: {string.Join(", ", validationResult.Errors)}");
        }

        business.Configuration = JsonSerializer.Serialize(configuration);
        business.UpdatedAt = DateTime.UtcNow;
        business.SyncStatus = SyncStatus.NotSynced;

        await _businessRepository.UpdateAsync(business);
        await _businessRepository.SaveChangesAsync();

        return true;
    }

    public async Task<ShopConfiguration> GetShopConfigurationAsync(Guid shopId)
    {
        var shop = await _shopRepository.GetShopWithBusinessAsync(shopId);
        if (shop == null)
        {
            throw new ArgumentException($"Shop with ID {shopId} not found");
        }

        if (string.IsNullOrEmpty(shop.Configuration))
        {
            return await GetDefaultShopConfigurationAsync(shop.BusinessId);
        }

        try
        {
            var config = JsonSerializer.Deserialize<ShopConfiguration>(shop.Configuration);
            return config ?? await GetDefaultShopConfigurationAsync(shop.BusinessId);
        }
        catch (JsonException)
        {
            _logger.LogWarning("Invalid JSON configuration for shop {ShopId}, returning default", shopId);
            return await GetDefaultShopConfigurationAsync(shop.BusinessId);
        }
    }

    public async Task<bool> UpdateShopConfigurationAsync(Guid shopId, ShopConfiguration configuration)
    {
        var shop = await _shopRepository.GetByIdAsync(shopId);
        if (shop == null)
            return false;

        // Validate configuration
        var validationResult = await ValidateShopConfigurationAsync(shopId, configuration);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException($"Invalid configuration: {string.Join(", ", validationResult.Errors)}");
        }

        shop.Configuration = JsonSerializer.Serialize(configuration);
        shop.UpdatedAt = DateTime.UtcNow;
        shop.SyncStatus = SyncStatus.NotSynced;

        await _shopRepository.UpdateAsync(shop);
        await _shopRepository.SaveChangesAsync();

        return true;
    }

    #endregion

    #region Business Type Validation

    public async Task<BusinessValidationResult> ValidateBusinessTypeConfigurationAsync(BusinessType businessType, BusinessConfiguration configuration)
    {
        var result = new BusinessValidationResult { IsValid = true };

        // Validate based on business type
        switch (businessType)
        {
            case BusinessType.Pharmacy:
                if (!configuration.TypeSettings.EnableExpiryTracking)
                {
                    result.Warnings.Add("Expiry tracking is recommended for pharmacy businesses");
                }
                if (!configuration.TypeSettings.EnableBatchTracking)
                {
                    result.Warnings.Add("Batch tracking is recommended for pharmacy businesses");
                }
                break;

            case BusinessType.Grocery:
                if (!configuration.TypeSettings.EnableWeightBasedPricing)
                {
                    result.Warnings.Add("Weight-based pricing is recommended for grocery businesses");
                }
                if (!configuration.TypeSettings.EnableVolumeMeasurements)
                {
                    result.Warnings.Add("Volume measurements are recommended for grocery businesses");
                }
                break;
        }

        // Validate tax rate
        if (configuration.DefaultTaxRate < 0 || configuration.DefaultTaxRate > 1)
        {
            result.IsValid = false;
            result.Errors.Add("Tax rate must be between 0 and 1 (0% to 100%)");
        }

        return await Task.FromResult(result);
    }

    public async Task<ShopValidationResult> ValidateShopConfigurationAsync(Guid shopId, ShopConfiguration configuration)
    {
        var result = new ShopValidationResult { IsValid = true };

        var shop = await _shopRepository.GetShopWithBusinessAsync(shopId);
        if (shop == null)
        {
            result.IsValid = false;
            result.Errors.Add("Shop not found");
            return result;
        }

        // Validate tax rate if specified
        if (configuration.TaxRate < 0 || configuration.TaxRate > 1)
        {
            result.IsValid = false;
            result.Errors.Add("Tax rate must be between 0 and 1 (0% to 100%)");
        }

        // Validate pricing rules
        if (configuration.PricingRules.MaxDiscountPercentage < 0 || configuration.PricingRules.MaxDiscountPercentage > 1)
        {
            result.IsValid = false;
            result.Errors.Add("Maximum discount percentage must be between 0 and 1 (0% to 100%)");
        }

        // Validate inventory settings
        if (configuration.InventorySettings.LowStockThreshold < 0)
        {
            result.IsValid = false;
            result.Errors.Add("Low stock threshold must be non-negative");
        }

        if (configuration.InventorySettings.ExpiryAlertDays < 0)
        {
            result.IsValid = false;
            result.Errors.Add("Expiry alert days must be non-negative");
        }

        return result;
    }

    public async Task<BusinessConfiguration> GetDefaultBusinessConfigurationAsync(BusinessType businessType)
    {
        var config = new BusinessConfiguration
        {
            Currency = "USD",
            DefaultTaxRate = 0.0m,
            TypeSettings = new BusinessTypeSettings()
        };

        switch (businessType)
        {
            case BusinessType.Pharmacy:
                config.TypeSettings.EnableExpiryTracking = true;
                config.TypeSettings.EnableBatchTracking = true;
                config.TypeSettings.RequiredAttributes = new List<string> { "ExpiryDate", "Manufacturer", "BatchNumber" };
                config.TypeSettings.OptionalAttributes = new List<string> { "GenericName", "Dosage" };
                break;

            case BusinessType.Grocery:
                config.TypeSettings.EnableWeightBasedPricing = true;
                config.TypeSettings.EnableVolumeMeasurements = true;
                config.TypeSettings.RequiredAttributes = new List<string> { "Unit" };
                config.TypeSettings.OptionalAttributes = new List<string> { "Weight", "Volume" };
                break;

            case BusinessType.SuperShop:
                config.TypeSettings.EnableWeightBasedPricing = true;
                config.TypeSettings.EnableVolumeMeasurements = true;
                config.TypeSettings.OptionalAttributes = new List<string> { "Weight", "Volume", "Unit" };
                break;

            case BusinessType.GeneralRetail:
            case BusinessType.Custom:
            default:
                // Minimal configuration for general retail
                break;
        }

        return await Task.FromResult(config);
    }

    public async Task<ShopConfiguration> GetDefaultShopConfigurationAsync(Guid businessId)
    {
        var business = await _businessRepository.GetByIdAsync(businessId);
        var businessConfig = business != null ? await GetBusinessConfigurationAsync(businessId) : new BusinessConfiguration();

        return await Task.FromResult(new ShopConfiguration
        {
            Currency = businessConfig.Currency,
            TaxRate = businessConfig.DefaultTaxRate,
            PricingRules = new PricingRules
            {
                AllowPriceOverride = false,
                MaxDiscountPercentage = 0.1m, // 10% default
                EnableDynamicPricing = false
            },
            InventorySettings = new InventorySettings
            {
                EnableLowStockAlerts = true,
                LowStockThreshold = 10,
                EnableAutoReorder = false,
                ExpiryAlertDays = 30
            }
        });
    }

    #endregion

    #region Custom Attributes

    public async Task<IEnumerable<string>> GetRequiredProductAttributesAsync(BusinessType businessType)
    {
        var config = await GetDefaultBusinessConfigurationAsync(businessType);
        return config.TypeSettings.RequiredAttributes;
    }

    public async Task<IEnumerable<string>> GetOptionalProductAttributesAsync(BusinessType businessType)
    {
        var config = await GetDefaultBusinessConfigurationAsync(businessType);
        return config.TypeSettings.OptionalAttributes;
    }

    public async Task<BusinessValidationResult> ValidateProductAttributesAsync(BusinessType businessType, BusinessTypeAttributes attributes)
    {
        var result = new BusinessValidationResult { IsValid = true };
        var requiredAttributes = await GetRequiredProductAttributesAsync(businessType);

        switch (businessType)
        {
            case BusinessType.Pharmacy:
                if (requiredAttributes.Contains("ExpiryDate") && !attributes.ExpiryDate.HasValue)
                {
                    result.IsValid = false;
                    result.Errors.Add("Expiry date is required for pharmacy products");
                }
                if (requiredAttributes.Contains("Manufacturer") && string.IsNullOrEmpty(attributes.Manufacturer))
                {
                    result.IsValid = false;
                    result.Errors.Add("Manufacturer is required for pharmacy products");
                }
                if (requiredAttributes.Contains("BatchNumber") && string.IsNullOrEmpty(attributes.BatchNumber))
                {
                    result.IsValid = false;
                    result.Errors.Add("Batch number is required for pharmacy products");
                }
                // Validate expiry date is in the future
                if (attributes.ExpiryDate.HasValue && attributes.ExpiryDate.Value <= DateTime.UtcNow)
                {
                    result.IsValid = false;
                    result.Errors.Add("Expiry date must be in the future");
                }
                break;

            case BusinessType.Grocery:
                if (requiredAttributes.Contains("Unit") && string.IsNullOrEmpty(attributes.Unit))
                {
                    result.IsValid = false;
                    result.Errors.Add("Unit is required for grocery products");
                }
                // Validate weight is positive if specified
                if (attributes.Weight.HasValue && attributes.Weight.Value <= 0)
                {
                    result.IsValid = false;
                    result.Errors.Add("Weight must be positive");
                }
                break;
        }

        return result;
    }

    #endregion

    #region Private Helper Methods

    private async Task<BusinessResponse> MapToBusinessResponseAsync(Business business)
    {
        var owner = business.Owner ?? await _userRepository.GetByIdAsync(business.OwnerId);
        
        return new BusinessResponse
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
            UpdatedAt = business.UpdatedAt,
            OwnerName = owner?.FullName ?? "Unknown",
            Shops = business.Shops?.Select(s => new ShopResponse
            {
                Id = s.Id,
                BusinessId = s.BusinessId,
                Name = s.Name,
                Address = s.Address,
                Phone = s.Phone,
                Email = s.Email,
                Configuration = s.Configuration,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                BusinessName = business.Name,
                BusinessType = business.Type,
                ProductCount = s.Products?.Count(p => !p.IsDeleted) ?? 0,
                InventoryCount = s.Inventory?.Count(i => !i.IsDeleted) ?? 0
            }).ToList() ?? new List<ShopResponse>()
        };
    }

    private async Task<ShopResponse> MapToShopResponseAsync(Shop shop)
    {
        var business = shop.Business ?? await _businessRepository.GetByIdAsync(shop.BusinessId);
        
        return new ShopResponse
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
            UpdatedAt = shop.UpdatedAt,
            BusinessName = business?.Name ?? "Unknown",
            BusinessType = business?.Type ?? BusinessType.GeneralRetail,
            ProductCount = shop.Products?.Count(p => !p.IsDeleted) ?? 0,
            InventoryCount = shop.Inventory?.Count(i => !i.IsDeleted) ?? 0
        };
    }

    #endregion
}