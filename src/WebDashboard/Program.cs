using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Shared.Core.DependencyInjection;
using Shared.Core.Services;
using WebDashboard.Services;
using WebDashboard.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Add Blazorise
builder.Services
    .AddBlazorise(options =>
    {
        options.Immediate = true;
    })
    .AddBootstrap5Providers()
    .AddFontAwesomeIcons();

// Add HTTP client
builder.Services.AddHttpClient();

// Add SignalR for real-time updates
builder.Services.AddSignalR();

// Add shared core services for web dashboard
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=dashboard_pos.db";
builder.Services.AddSharedCore(connectionString);

// Add web dashboard specific services
builder.Services.AddScoped<IDashboardApiService, DashboardApiService>();
builder.Services.AddScoped<IBusinessApiService, BusinessApiService>();
builder.Services.AddScoped<IUserApiService, UserApiService>();
builder.Services.AddScoped<WebDashboard.Services.IAuthenticationService, WebDashboard.Services.AuthenticationService>();

var app = builder.Build();

// Initialize the web dashboard application
try
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Initializing web dashboard with multi-business support");
    
    var startupService = app.Services.GetRequiredService<IMultiBusinessStartupService>();
    var initResult = await startupService.InitializeSystemAsync();
    
    if (!initResult.IsSuccess)
    {
        logger.LogWarning("Web dashboard initialization had issues: {Errors}", string.Join(", ", initResult.Errors));
        // Continue anyway for web dashboard
    }
    else
    {
        logger.LogInformation("Web dashboard initialized successfully in {Duration}ms", 
            initResult.TotalInitializationTime.TotalMilliseconds);
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Error during web dashboard initialization");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Map SignalR hubs
app.MapHub<DashboardHub>("/dashboardhub");

app.Run();