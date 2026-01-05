using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Desktop.Services;
using Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Core.Services;
using System.IO;

namespace Desktop;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private GlobalExceptionHandlerService? _exceptionHandler;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Configure services
        var services = new ServiceCollection();
        
        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
        });

        // Get database path
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OfflinePOS");
        Directory.CreateDirectory(appDataPath);
        var connectionString = $"Data Source={Path.Combine(appDataPath, "pos.db")}";

        // Add desktop services (which includes shared core)
        services.AddDesktopServices(connectionString);

        _serviceProvider = services.BuildServiceProvider();

        // Set up global exception handling
        _exceptionHandler = _serviceProvider.GetRequiredService<GlobalExceptionHandlerService>();
        SetupGlobalExceptionHandling();

        // Initialize database
        Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var migrationService = scope.ServiceProvider.GetRequiredService<IDatabaseMigrationService>();
                await migrationService.InitializeDatabaseAsync();
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider.GetService<ILogger<App>>();
                logger?.LogError(ex, "Failed to initialize database");
                
                // Handle database initialization exception
                if (_exceptionHandler != null)
                {
                    await _exceptionHandler.HandleUnhandledExceptionAsync(ex, "Database Initialization");
                }
            }
        });

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow(mainViewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupGlobalExceptionHandling()
    {
        // Handle unhandled exceptions in the current AppDomain
        AppDomain.CurrentDomain.UnhandledException += async (sender, e) =>
        {
            if (e.ExceptionObject is Exception exception && _exceptionHandler != null)
            {
                await _exceptionHandler.HandleUnhandledExceptionAsync(exception, "AppDomain Unhandled Exception");
            }
        };

        // Handle unhandled exceptions in tasks
        TaskScheduler.UnobservedTaskException += async (sender, e) =>
        {
            if (_exceptionHandler != null)
            {
                await _exceptionHandler.HandleUnhandledExceptionAsync(e.Exception, "Task Unobserved Exception");
            }
            e.SetObserved(); // Prevent the process from terminating
        };
    }
}

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}