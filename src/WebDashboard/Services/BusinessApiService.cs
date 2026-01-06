using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Services;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Shared.Core.Entities;
using WebDashboard.Models;

namespace WebDashboard.Services;

public class BusinessApiService : IBusinessApiService
{
    private readonly IBusinessRepository _businessRepository;
    private readonly IShopRepository _shopRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockRepository _stockRepository;
    private readonly ILogger<BusinessApiService> _logger;

    public BusinessApiService(
        IBusinessRepository businessRepository,
        IShopRepository shopRepository,
        IProductRepository productRepository,
        IStockRepository stockRepository,
        ILogger<BusinessApiService> logger)
    {
        _businessRepository = businessRepository;
        _shopRepository = shopRepository;
        _productRepository = productRepository;
        _stockRepository = stockRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<BusinessResponse>> GetBusinessesByOwnerAsync(Guid ownerId)
    {
        try
        {
            _logger.LogInformation("Getting businesses for owner {OwnerId}", ownerId);

            var businesses = await _businessRepository.FindAsync(b => b.OwnerId == ownerId && b.IsActive);
            var businessResponses = new List<BusinessResponse>();

            foreach (var business in businesses)
            {
                var shops = await _shopRepository.FindAsync(s => s.BusinessId == business.Id && s.IsActive);
                
                businessResponses.Add(new BusinessResponse
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
                    IsActive = business.IsActive,
                    CreatedAt = business.CreatedAt,
                    UpdatedAt = business.UpdatedAt,
                    Shops = shops.Select(s => new ShopResponse
                    {
                        Id = s.Id,
                        Name = s.Name,
                        ProductCount = 0 // Will be calculated if needed
                    }).ToList()
                });
            }

            return businessResponses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting businesses for owner {OwnerId}", ownerId);
            throw;
        }
    }

    public async Task<BusinessResponse> GetBusinessByIdAsync(Guid businessId)
    {
        try
        {
            var business = await _businessRepository.GetByIdAsync(businessId);
            if (business == null)
            {
                throw new ArgumentException($"Business with ID {businessId} not found");
            }

            var shops = await _shopRepository.FindAsync(s => s.BusinessId == businessId && s.IsActive);
            var shopResponses = new List<ShopResponse>();

            foreach (var shop in shops)
            {
                var products = await _productRepository.FindAsync(p => p.ShopId == shop.Id);
                var productCount = products.Count();
                shopResponses.Add(new ShopResponse
                {
                    Id = shop.Id,
                    Name = shop.Name,
                    ProductCount = productCount
                });
            }

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
                IsActive = business.IsActive,
                CreatedAt = business.CreatedAt,
                UpdatedAt = business.UpdatedAt,
                Shops = shopResponses
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting business {BusinessId}", businessId);
            throw;
        }
    }

    public async Task<BusinessResponse> CreateBusinessAsync(CreateBusinessRequest request)
    {
        try
        {
            _logger.LogInformation("Creating new business {Name} for owner {OwnerId}", request.Name, request.OwnerId);

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
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                DeviceId = Guid.NewGuid(), // TODO: Get from current device context
                SyncStatus = SyncStatus.NotSynced
            };

            await _businessRepository.AddAsync(business);

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
                IsActive = business.IsActive,
                CreatedAt = business.CreatedAt,
                UpdatedAt = business.UpdatedAt,
                Shops = new List<ShopResponse>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating business {Name}", request.Name);
            throw;
        }
    }

    public async Task<BusinessResponse> UpdateBusinessAsync(UpdateBusinessRequest request)
    {
        try
        {
            var business = await _businessRepository.GetByIdAsync(request.Id);
            if (business == null)
            {
                throw new ArgumentException($"Business with ID {request.Id} not found");
            }

            business.Name = request.Name;
            business.Description = request.Description;
            business.Address = request.Address;
            business.Phone = request.Phone;
            business.Email = request.Email;
            business.TaxId = request.TaxId;
            business.IsActive = request.IsActive;
            business.UpdatedAt = DateTime.UtcNow;
            business.SyncStatus = SyncStatus.NotSynced;

            await _businessRepository.UpdateAsync(business);

            var shops = await _shopRepository.FindAsync(s => s.BusinessId == business.Id && s.IsActive);

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
                IsActive = business.IsActive,
                CreatedAt = business.CreatedAt,
                UpdatedAt = business.UpdatedAt,
                Shops = shops.Select(s => new ShopResponse
                {
                    Id = s.Id,
                    Name = s.Name,
                    ProductCount = 0
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating business {BusinessId}", request.Id);
            throw;
        }
    }

    public async Task DeleteBusinessAsync(Guid businessId, Guid ownerId)
    {
        try
        {
            var business = await _businessRepository.GetByIdAsync(businessId);
            if (business == null || business.OwnerId != ownerId)
            {
                throw new ArgumentException($"Business with ID {businessId} not found or not owned by user {ownerId}");
            }

            // Soft delete
            business.IsActive = false;
            business.UpdatedAt = DateTime.UtcNow;
            business.SyncStatus = SyncStatus.NotSynced;

            await _businessRepository.UpdateAsync(business);

            _logger.LogInformation("Business {BusinessId} soft deleted by owner {OwnerId}", businessId, ownerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting business {BusinessId}", businessId);
            throw;
        }
    }

    public async Task<IEnumerable<ShopResponse>> GetShopsByBusinessAsync(Guid businessId)
    {
        try
        {
            var business = await _businessRepository.GetByIdAsync(businessId);
            if (business == null)
            {
                throw new ArgumentException($"Business with ID {businessId} not found");
            }

            var shops = await _shopRepository.FindAsync(s => s.BusinessId == businessId && s.IsActive);
            var shopResponses = new List<ShopResponse>();

            foreach (var shop in shops)
            {
                var products = await _productRepository.FindAsync(p => p.ShopId == shop.Id);
                var productCount = products.Count();
                var stockItems = await _stockRepository.FindAsync(s => s.ShopId == shop.Id);
                var inventoryCount = stockItems.Sum(s => (int)s.Quantity);

                shopResponses.Add(new ShopResponse
                {
                    Id = shop.Id,
                    BusinessId = shop.BusinessId,
                    Name = shop.Name,
                    Address = shop.Address,
                    Phone = shop.Phone,
                    Email = shop.Email,
                    BusinessName = business.Name,
                    BusinessType = business.Type,
                    ProductCount = productCount,
                    InventoryCount = inventoryCount,
                    IsActive = shop.IsActive,
                    CreatedAt = shop.CreatedAt,
                    UpdatedAt = shop.UpdatedAt
                });
            }

            return shopResponses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shops for business {BusinessId}", businessId);
            throw;
        }
    }

    public async Task<ShopResponse> GetShopByIdAsync(Guid shopId)
    {
        try
        {
            var shop = await _shopRepository.GetByIdAsync(shopId);
            if (shop == null)
            {
                throw new ArgumentException($"Shop with ID {shopId} not found");
            }

            var business = await _businessRepository.GetByIdAsync(shop.BusinessId);
            var products = await _productRepository.FindAsync(p => p.ShopId == shopId);
            var productCount = products.Count();
            var stockItems = await _stockRepository.FindAsync(s => s.ShopId == shopId);
            var inventoryCount = stockItems.Sum(s => (int)s.Quantity);

            return new ShopResponse
            {
                Id = shop.Id,
                BusinessId = shop.BusinessId,
                Name = shop.Name,
                Address = shop.Address,
                Phone = shop.Phone,
                Email = shop.Email,
                BusinessName = business?.Name ?? "Unknown Business",
                BusinessType = business?.Type ?? BusinessType.GeneralRetail,
                ProductCount = productCount,
                InventoryCount = inventoryCount,
                IsActive = shop.IsActive,
                CreatedAt = shop.CreatedAt,
                UpdatedAt = shop.UpdatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shop {ShopId}", shopId);
            throw;
        }
    }

    public async Task<ShopResponse> CreateShopAsync(CreateShopRequest request)
    {
        try
        {
            var business = await _businessRepository.GetByIdAsync(request.BusinessId);
            if (business == null)
            {
                throw new ArgumentException($"Business with ID {request.BusinessId} not found");
            }

            var shop = new Shop
            {
                Id = Guid.NewGuid(),
                BusinessId = request.BusinessId,
                Name = request.Name,
                Address = request.Address,
                Phone = request.Phone,
                Email = request.Email,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                DeviceId = Guid.NewGuid(), // TODO: Get from current device context
                SyncStatus = SyncStatus.NotSynced
            };

            await _shopRepository.AddAsync(shop);

            return new ShopResponse
            {
                Id = shop.Id,
                BusinessId = shop.BusinessId,
                Name = shop.Name,
                Address = shop.Address,
                Phone = shop.Phone,
                Email = shop.Email,
                BusinessName = business.Name,
                BusinessType = business.Type,
                ProductCount = 0,
                InventoryCount = 0,
                IsActive = shop.IsActive,
                CreatedAt = shop.CreatedAt,
                UpdatedAt = shop.UpdatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating shop {Name}", request.Name);
            throw;
        }
    }

    public async Task<ShopResponse> UpdateShopAsync(UpdateShopRequest request)
    {
        try
        {
            var shop = await _shopRepository.GetByIdAsync(request.Id);
            if (shop == null)
            {
                throw new ArgumentException($"Shop with ID {request.Id} not found");
            }

            var business = await _businessRepository.GetByIdAsync(shop.BusinessId);

            shop.Name = request.Name;
            shop.Address = request.Address;
            shop.Phone = request.Phone;
            shop.Email = request.Email;
            shop.IsActive = request.IsActive;
            shop.UpdatedAt = DateTime.UtcNow;
            shop.SyncStatus = SyncStatus.NotSynced;

            await _shopRepository.UpdateAsync(shop);

            var products = await _productRepository.FindAsync(p => p.ShopId == shop.Id);
            var productCount = products.Count();
            var stockItems = await _stockRepository.FindAsync(s => s.ShopId == shop.Id);
            var inventoryCount = stockItems.Sum(s => (int)s.Quantity);

            return new ShopResponse
            {
                Id = shop.Id,
                BusinessId = shop.BusinessId,
                Name = shop.Name,
                Address = shop.Address,
                Phone = shop.Phone,
                Email = shop.Email,
                BusinessName = business?.Name ?? "Unknown Business",
                BusinessType = business?.Type ?? BusinessType.GeneralRetail,
                ProductCount = productCount,
                InventoryCount = inventoryCount,
                IsActive = shop.IsActive,
                CreatedAt = shop.CreatedAt,
                UpdatedAt = shop.UpdatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating shop {ShopId}", request.Id);
            throw;
        }
    }

    public async Task DeleteShopAsync(Guid shopId, Guid ownerId)
    {
        try
        {
            var shop = await _shopRepository.GetByIdAsync(shopId);
            if (shop == null)
            {
                throw new ArgumentException($"Shop with ID {shopId} not found");
            }

            var business = await _businessRepository.GetByIdAsync(shop.BusinessId);
            if (business == null || business.OwnerId != ownerId)
            {
                throw new ArgumentException($"Shop with ID {shopId} not found or not owned by user {ownerId}");
            }

            // Soft delete
            shop.IsActive = false;
            shop.UpdatedAt = DateTime.UtcNow;
            shop.SyncStatus = SyncStatus.NotSynced;

            await _shopRepository.UpdateAsync(shop);

            _logger.LogInformation("Shop {ShopId} soft deleted by owner {OwnerId}", shopId, ownerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting shop {ShopId}", shopId);
            throw;
        }
    }
}