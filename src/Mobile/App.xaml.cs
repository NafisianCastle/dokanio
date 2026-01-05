using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Services;

namespace Mobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell();
        
        // Initialize database and check authentication
        Task.Run(async () =>
        {
            try
            {
                var serviceProvider = Handler?.MauiContext?.Services;
                if (serviceProvider != null)
                {
                    using var scope = serviceProvider.CreateScope();
                    
                    // Initialize database
                    var migrationService = scope.ServiceProvider.GetRequiredService<IDatabaseMigrationService>();
                    await migrationService.InitializeDatabaseAsync();
                    
                    // Check authentication status and navigate accordingly
                    var currentUserService = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
                    
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        if (currentUserService.IsAuthenticated)
                        {
                            await Shell.Current.GoToAsync("//main");
                        }
                        else
                        {
                            await Shell.Current.GoToAsync("//login");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                var serviceProvider = Handler?.MauiContext?.Services;
                var logger = serviceProvider?.GetService<ILogger<App>>();
                logger?.LogError(ex, "Failed to initialize application");
                
                // Navigate to login on error
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Shell.Current.GoToAsync("//login");
                });
            }
        });
    }
}