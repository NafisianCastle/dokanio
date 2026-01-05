using Microsoft.Extensions.Logging;
using Shared.Core.DependencyInjection;
using Shared.Core.Services;
using Mobile.ViewModels;
using Mobile.Views;
using Mobile.Services;
using System.IO;

namespace Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Get database path for mobile
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "pos.db");
        var connectionString = $"Data Source={dbPath}";

        // Register Shared.Core services
        builder.Services.AddSharedCore(connectionString);

        // Register Mobile-specific services
        builder.Services.AddSingleton<BackgroundSyncService>();
        builder.Services.AddSingleton<IUserContextService, UserContextService>();

        // Register ViewModels
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<ProductListViewModel>();
        builder.Services.AddTransient<SaleViewModel>();
        builder.Services.AddTransient<DailySalesViewModel>();
        builder.Services.AddTransient<BarcodeScannerViewModel>();

        // Register Views
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<ProductListPage>();
        builder.Services.AddTransient<SalePage>();
        builder.Services.AddTransient<DailySalesPage>();
        builder.Services.AddTransient<BarcodeScannerPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        
        // Initialize the mobile application asynchronously
        _ = Task.Run(async () =>
        {
            try
            {
                var success = await app.Services.InitializeMobileApplicationAsync();
                if (!success)
                {
                    // Log error but don't prevent app startup
                    var logger = app.Services.GetRequiredService<ILogger<MauiProgram>>();
                    logger.LogError("Mobile application initialization failed, but app will continue");
                }
            }
            catch (Exception ex)
            {
                var logger = app.Services.GetRequiredService<ILogger<MauiProgram>>();
                logger.LogError(ex, "Error during mobile application initialization");
            }
        });

        return app;
    }
    
    /// <summary>
    /// Initializes the mobile application with multi-business support
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    /// <returns>Initialization result</returns>
    public static async Task<bool> InitializeMobileApplicationAsync(this IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<MauiProgram>>();
        var startupService = serviceProvider.GetRequiredService<IMultiBusinessStartupService>();
        
        try
        {
            logger.LogInformation("Initializing mobile application with multi-business support");
            
            // Initialize the system
            var initResult = await startupService.InitializeSystemAsync();
            if (!initResult.IsSuccess)
            {
                logger.LogError("Mobile application initialization failed: {Errors}", 
                    string.Join(", ", initResult.Errors));
                return false;
            }
            
            // Validate system readiness (more lenient for mobile)
            var readinessResult = await startupService.ValidateSystemReadinessAsync();
            if (!readinessResult.IsReady && readinessResult.BlockingIssues.Any())
            {
                logger.LogWarning("Mobile application has blocking issues: {Issues}", 
                    string.Join(", ", readinessResult.BlockingIssues));
                return false;
            }
            
            logger.LogInformation("Mobile application initialized successfully in {Duration}ms", 
                initResult.TotalInitializationTime.TotalMilliseconds);
            
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Critical error during mobile application initialization");
            return false;
        }
    }
}