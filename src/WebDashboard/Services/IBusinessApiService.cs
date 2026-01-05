using Shared.Core.DTOs;

namespace WebDashboard.Services;

public interface IBusinessApiService
{
    Task<IEnumerable<BusinessResponse>> GetBusinessesByOwnerAsync(Guid ownerId);
    Task<BusinessResponse> GetBusinessByIdAsync(Guid businessId);
    Task<BusinessResponse> CreateBusinessAsync(CreateBusinessRequest request);
    Task<BusinessResponse> UpdateBusinessAsync(UpdateBusinessRequest request);
    Task DeleteBusinessAsync(Guid businessId, Guid ownerId);
    
    Task<IEnumerable<ShopResponse>> GetShopsByBusinessAsync(Guid businessId);
    Task<ShopResponse> GetShopByIdAsync(Guid shopId);
    Task<ShopResponse> CreateShopAsync(CreateShopRequest request);
    Task<ShopResponse> UpdateShopAsync(UpdateShopRequest request);
    Task DeleteShopAsync(Guid shopId, Guid ownerId);
}