using Microsoft.Extensions.Logging;
using Shared.Core.DependencyInjection;
using Mobile.ViewModels;
using Mobile.Views;
using Mobile.Services;

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

        // Register Shared.Core services
        builder.Services.AddSharedCore("Data Source=pos.db");

        // Register Mobile-specific services
        builder.Services.AddSingleton<BackgroundSyncService>();

        // Register ViewModels
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<ProductListViewModel>();
        builder.Services.AddTransient<SaleViewModel>();
        builder.Services.AddTransient<DailySalesViewModel>();
        builder.Services.AddTransient<BarcodeScannerViewModel>();

        // Register Views
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<ProductListPage>();
        builder.Services.AddTransient<SalePage>();
        builder.Services.AddTransient<DailySalesPage>();
        builder.Services.AddTransient<BarcodeScannerPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}