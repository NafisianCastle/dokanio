using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Events;
using Shared.Core.Integration;
using Shared.Core.Plugins;
using Shared.Core.Services;

namespace Shared.Core.Architecture;

/// <summary>
/// Configuration for multi-tenant services
/// </summary>
public class MultiTenantServiceConfiguration
{
    /// <summary>
    /// Enable plugin system
    /// </summary>
    public bool EnablePluginSystem { get; set; } = true;
    
    /// <summary>
    /// Plugin directory path
    /// </summary>
    public string PluginDirectory { get; set; } = "plugins";
    
    /// <summary>
    /// Enable event-driven architecture
    /// </summary>
    public bool EnableEventBus { get; set; } = true;
    
    /// <summary>
    /// Enable external analytics integration
    /// </summary>
    public bool EnableAnalyticsIntegration { get; set; } = false;
    
    /// <summary>
    /// Enable external AI service integration
    /// </summary>
    public bool EnableAIIntegration { get; set; } = false;
    
    /// <summary>
    /// Analytics API configuration
    /// </summary>
    public AnalyticsApiConfiguration AnalyticsApi { get; set; } = new();
    
    /// <summary>
    /// AI service configuration
    /// </summary>
    public AIServiceConfiguration AIService { get; set; } = new();
    
    /// <summary>
    /// Event bus configuration
    /// </summary>
    public EventBusConfiguration EventBus { get; set; } = new();
}

/// <summary>
/// Configuration for analytics API integration
/// </summary>
public class AnalyticsApiConfiguration
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Configuration for AI service integration
/// </summary>
public class AIServiceConfiguration
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);
    public string ModelVersion { get; set; } = "v1";
}

/// <summary>
/// Configuration for event bus
/// </summary>
public class EventBusConfiguration
{
    public bool EnablePersistence { get; set; } = false;
    public string PersistenceConnectionString { get; set; } = string.Empty;
    public int MaxConcurrentHandlers { get; set; } = 10;
    public TimeSpan HandlerTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Extension methods for configuring multi-tenant services
/// </summary>
public static class MultiTenantServiceExtensions
{
    /// <summary>
    /// Adds extensible architecture services to the service collection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Multi-tenant service configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddExtensibleArchitecture(
        this IServiceCollection services, 
        MultiTenantServiceConfiguration? configuration = null)
    {
        configuration ??= new MultiTenantServiceConfiguration();
        
        // Register configuration
        services.AddSingleton(configuration);
        
        // Add plugin system
        if (configuration.EnablePluginSystem)
        {
            services.AddPluginSystem(configuration.PluginDirectory);
        }
        
        // Add event bus
        if (configuration.EnableEventBus)
        {
            services.AddEventBus(configuration.EventBus);
        }
        
        // Add analytics integration
        if (configuration.EnableAnalyticsIntegration)
        {
            services.AddAnalyticsIntegration(configuration.AnalyticsApi);
        }
        
        // Add AI service integration
        if (configuration.EnableAIIntegration)
        {
            services.AddAIServiceIntegration(configuration.AIService);
        }
        
        return services;
    }
    
    /// <summary>
    /// Adds plugin system services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="pluginDirectory">Plugin directory path</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddPluginSystem(this IServiceCollection services, string pluginDirectory)
    {
        services.AddSingleton<IPluginManager, PluginManager>();
        
        // Register plugin initialization service
        services.AddHostedService<PluginInitializationService>(provider =>
            new PluginInitializationService(
                provider.GetRequiredService<IPluginManager>(),
                provider.GetRequiredService<ILogger<PluginInitializationService>>(),
                pluginDirectory));
        
        return services;
    }
    
    /// <summary>
    /// Adds event bus services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Event bus configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddEventBus(this IServiceCollection services, EventBusConfiguration configuration)
    {
        services.AddSingleton<IEventBus, EventBus>();
        services.AddSingleton(configuration);
        
        // Register default event handlers
        services.AddScoped<IEventHandler<SaleCompletedEvent>, SaleCompletedEventHandler>();
        services.AddScoped<IEventHandler<InventoryUpdatedEvent>, InventoryUpdatedEventHandler>();
        services.AddScoped<IEventHandler<LowStockDetectedEvent>, LowStockDetectedEventHandler>();
        services.AddScoped<IEventHandler<ProductExpiryWarningEvent>, ProductExpiryWarningEventHandler>();
        
        return services;
    }
    
    /// <summary>
    /// Adds analytics integration services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Analytics API configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAnalyticsIntegration(this IServiceCollection services, AnalyticsApiConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.AddHttpClient<IAnalyticsApiClient, AnalyticsApiClient>(client =>
        {
            client.BaseAddress = new Uri(configuration.BaseUrl);
            client.Timeout = configuration.Timeout;
            client.DefaultRequestHeaders.Add("X-API-Key", configuration.ApiKey);
        });
        
        return services;
    }
    
    /// <summary>
    /// Adds AI service integration services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">AI service configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAIServiceIntegration(this IServiceCollection services, AIServiceConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.AddHttpClient<IAIServiceClient, AIServiceClient>(client =>
        {
            client.BaseAddress = new Uri(configuration.BaseUrl);
            client.Timeout = configuration.Timeout;
            client.DefaultRequestHeaders.Add("X-API-Key", configuration.ApiKey);
            client.DefaultRequestHeaders.Add("X-Model-Version", configuration.ModelVersion);
        });
        
        return services;
    }
}