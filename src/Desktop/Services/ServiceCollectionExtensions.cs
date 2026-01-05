using Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Shared.Core.DependencyInjection;

namespace Desktop.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDesktopServices(this IServiceCollection services, string connectionString)
    {
        // Add shared core services
        services.AddSharedCore(connectionString);
        
        // Add ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<SaleViewModel>();
        services.AddTransient<ProductViewModel>();
        services.AddTransient<PurchaseViewModel>();
        services.AddTransient<SupplierViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<UserManagementViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<BusinessManagementViewModel>();
        services.AddTransient<AdvancedReportsViewModel>();
        services.AddTransient<AIInventoryViewModel>();
        
        return services;
    }
}