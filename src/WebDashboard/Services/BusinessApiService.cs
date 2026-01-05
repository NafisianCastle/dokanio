using Shared.Core.DTOs;
using Shared.Core.Services;
using Shared.Core.Enums;
using WebDashboard.Models;

namespace WebDashboard.Services;

public class BusinessApiService : IBusinessApiService
{
    private readonly HttpClient _httpClient;

    public BusinessApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<BusinessResponse>> GetBusinessesByOwnerAsync(Guid ownerId)
    {
        await Task.Delay(100); // Simulate async operation
        
        // Return mock data for demo
        return new List<BusinessResponse>
        {
            new BusinessResponse
            {
                Id = Guid.NewGuid(),
                Name = "Demo Business",
                Type = BusinessType.GeneralRetail,
                OwnerId = ownerId,
                Description = "Demo business for testing",
                Address = "123 Demo Street",
                Phone = "555-0123",
                Email = "demo@business.com",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Shops = new List<ShopResponse>
                {
                    new ShopResponse
                    {
                        Id = Guid.NewGuid(),
                        Name = "Main Shop",
                        ProductCount = 100
                    }
                }
            }
        };
    }

    public async Task<BusinessResponse> GetBusinessByIdAsync(Guid businessId)
    {
        await Task.Delay(100);
        
        return new BusinessResponse
        {
            Id = businessId,
            Name = "Demo Business",
            Type = BusinessType.GeneralRetail,
            Description = "Demo business for testing",
            Address = "123 Demo Street",
            Phone = "555-0123",
            Email = "demo@business.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Shops = new List<ShopResponse>
            {
                new ShopResponse
                {
                    Id = Guid.NewGuid(),
                    Name = "Main Shop",
                    ProductCount = 100
                }
            }
        };
    }

    public async Task<BusinessResponse> CreateBusinessAsync(CreateBusinessRequest request)
    {
        await Task.Delay(100);
        
        return new BusinessResponse
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
            Shops = new List<ShopResponse>()
        };
    }

    public async Task<BusinessResponse> UpdateBusinessAsync(UpdateBusinessRequest request)
    {
        await Task.Delay(100);
        
        return new BusinessResponse
        {
            Id = request.Id,
            Name = request.Name,
            Description = request.Description,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            TaxId = request.TaxId,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow,
            Shops = new List<ShopResponse>()
        };
    }

    public async Task DeleteBusinessAsync(Guid businessId, Guid ownerId)
    {
        await Task.Delay(100);
        // Mock deletion
    }

    public async Task<IEnumerable<ShopResponse>> GetShopsByBusinessAsync(Guid businessId)
    {
        await Task.Delay(100);
        
        return new List<ShopResponse>
        {
            new ShopResponse
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Main Shop",
                Address = "123 Main Street",
                Phone = "555-0123",
                Email = "shop@business.com",
                BusinessName = "Demo Business",
                BusinessType = BusinessType.GeneralRetail,
                ProductCount = 100,
                InventoryCount = 500,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ShopResponse
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Branch Shop",
                Address = "456 Branch Avenue",
                Phone = "555-0124",
                Email = "branch@business.com",
                BusinessName = "Demo Business",
                BusinessType = BusinessType.GeneralRetail,
                ProductCount = 75,
                InventoryCount = 300,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
    }

    public async Task<ShopResponse> GetShopByIdAsync(Guid shopId)
    {
        await Task.Delay(100);
        
        return new ShopResponse
        {
            Id = shopId,
            BusinessId = Guid.NewGuid(),
            Name = "Demo Shop",
            Address = "123 Demo Street",
            Phone = "555-0123",
            Email = "shop@business.com",
            BusinessName = "Demo Business",
            BusinessType = BusinessType.GeneralRetail,
            ProductCount = 100,
            InventoryCount = 500,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public async Task<ShopResponse> CreateShopAsync(CreateShopRequest request)
    {
        await Task.Delay(100);
        
        return new ShopResponse
        {
            Id = Guid.NewGuid(),
            BusinessId = request.BusinessId,
            Name = request.Name,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            BusinessName = "Demo Business",
            BusinessType = BusinessType.GeneralRetail,
            ProductCount = 0,
            InventoryCount = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public async Task<ShopResponse> UpdateShopAsync(UpdateShopRequest request)
    {
        await Task.Delay(100);
        
        return new ShopResponse
        {
            Id = request.Id,
            Name = request.Name,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            BusinessName = "Demo Business",
            BusinessType = BusinessType.GeneralRetail,
            ProductCount = 100,
            InventoryCount = 500,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow
        };
    }

    public async Task DeleteShopAsync(Guid shopId, Guid ownerId)
    {
        await Task.Delay(100);
        // Mock deletion
    }
}