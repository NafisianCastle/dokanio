using Desktop.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.DependencyInjection;
using Shared.Core.Services;

namespace Desktop.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDesktopServices(
        this IServiceCollection services,
        string connectionString,
        IConfiguration? configuration = null)
    {
        // Add shared core services (SQLite, repositories, business logic)
        services.AddSharedCore(connectionString);

        // ── Server auth HTTP client ───────────────────────────────────────────────
        // Reads BaseUrl from appsettings.json → "Server:BaseUrl", falls back to
        // the POS_SERVER_URL environment variable, then to localhost:5000.
        var serverBaseUrl =
            configuration?["Server:BaseUrl"]
            ?? Environment.GetEnvironmentVariable("POS_SERVER_URL")
            ?? "http://localhost:5000";

        var authTimeoutSeconds = 5;
        if (int.TryParse(configuration?["Server:AuthTimeoutSeconds"], out var t))
            authTimeoutSeconds = t;

        services.AddHttpClient<IServerAuthService, ServerAuthService>(client =>
        {
            client.BaseAddress = new Uri(serverBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(authTimeoutSeconds);
        });

        // Add desktop-specific exception handling
        services.AddScoped<GlobalExceptionHandlerService>();

        // Add ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<SaleViewModel>();
        services.AddTransient<SaleTabContainerViewModel>();
        services.AddTransient<ProductViewModel>();
        services.AddTransient<PurchaseViewModel>();
        services.AddTransient<SupplierViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<UserManagementViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<BusinessManagementViewModel>();
        services.AddTransient<AdvancedReportsViewModel>();
        services.AddTransient<AIInventoryViewModel>();
        services.AddTransient<ConfigurationViewModel>();
        // Note: BarcodeScannerWindowViewModel requires sessionId and shopId at construction time,
        // so it is instantiated directly in the view code-behind rather than via DI.

        return services;
    }
    
    /// <summary>
    /// Initializes the desktop application with multi-business support
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    /// <returns>Initialization result</returns>
    public static async Task<bool> InitializeDesktopApplicationAsync(this IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var startupService = serviceProvider.GetRequiredService<IMultiBusinessStartupService>();
        
        try
        {
            logger.LogInformation("Initializing desktop application with multi-business support");
            
            // Initialize the system
            var initResult = await startupService.InitializeSystemAsync();
            if (!initResult.IsSuccess)
            {
                logger.LogError("Desktop application initialization failed: {Errors}", 
                    string.Join(", ", initResult.Errors));
                return false;
            }
            
            // Validate system readiness
            var readinessResult = await startupService.ValidateSystemReadinessAsync();
            if (!readinessResult.IsReady)
            {
                logger.LogWarning("Desktop application has readiness issues: {Issues}", 
                    string.Join(", ", readinessResult.BlockingIssues));
                
                // Continue if only non-blocking issues
                if (readinessResult.BlockingIssues.Any())
                {
                    return false;
                }
            }
            
            logger.LogInformation("Desktop application initialized successfully in {Duration}ms", 
                initResult.TotalInitializationTime.TotalMilliseconds);
            
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Critical error during desktop application initialization");
            return false;
        }
    }
}