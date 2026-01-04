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
        
        // Initialize database
        Task.Run(async () =>
        {
            try
            {
                var serviceProvider = Handler?.MauiContext?.Services;
                if (serviceProvider != null)
                {
                    using var scope = serviceProvider.CreateScope();
                    var migrationService = scope.ServiceProvider.GetRequiredService<IDatabaseMigrationService>();
                    await migrationService.InitializeDatabaseAsync();
                }
            }
            catch (Exception ex)
            {
                var serviceProvider = Handler?.MauiContext?.Services;
                var logger = serviceProvider?.GetService<ILogger<App>>();
                logger?.LogError(ex, "Failed to initialize database");
            }
        });
    }
}