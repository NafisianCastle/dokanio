using Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Desktop.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        // Add ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<SaleViewModel>();
        
        return services;
    }
}