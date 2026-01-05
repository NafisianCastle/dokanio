using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Server.Data;
using Server.Services;
using Shared.Core.DependencyInjection;
using Shared.Core.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure HTTPS and security
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
    options.HttpsPort = builder.Environment.IsDevelopment() ? 7001 : 443;
});

// Add security headers
builder.Services.AddAntiforgery();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure PostgreSQL database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=pos_server;Username=postgres;Password=postgres";

builder.Services.AddDbContext<ServerDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(30);
        npgsqlOptions.EnableRetryOnFailure(3);
    });
    options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
    options.EnableDetailedErrors(builder.Environment.IsDevelopment());
});

// Add shared core services for server
var sqliteConnectionString = builder.Configuration.GetConnectionString("SqliteConnection") 
    ?? "Data Source=server_pos.db";
builder.Services.AddSharedCore(sqliteConnectionString);

// Configure JWT authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] 
    ?? "POS-Server-Default-Secret-Key-For-Development-Only-Change-In-Production-123456789";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "POS-Server",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "POS-Devices",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("JWT authentication failed: {Exception}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var deviceId = context.Principal?.FindFirst("device_id")?.Value;
                logger.LogDebug("JWT token validated for device {DeviceId}", deviceId);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Register application services
builder.Services.AddScoped<IJwtService, JwtService>();

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    if (builder.Environment.IsDevelopment())
    {
        logging.SetMinimumLevel(LogLevel.Debug);
    }
});

var app = builder.Build();

// Initialize the server application
try
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Initializing server application with multi-business support");
    
    var startupService = app.Services.GetRequiredService<IMultiBusinessStartupService>();
    var initResult = await startupService.InitializeSystemAsync();
    
    if (!initResult.IsSuccess)
    {
        logger.LogError("Server initialization failed: {Errors}", string.Join(", ", initResult.Errors));
        // Continue anyway for development, but log the issues
    }
    else
    {
        logger.LogInformation("Server initialized successfully in {Duration}ms", 
            initResult.TotalInitializationTime.TotalMilliseconds);
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Error during server initialization");
}

// Ensure database is created and migrated
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        await context.Database.EnsureCreatedAsync();
        logger.LogInformation("Server database initialized successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error initializing server database");
        throw;
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("DevelopmentPolicy");
}
else
{
    // Production security headers
    app.UseHsts();
}

// Security middleware
app.Use(async (context, next) =>
{
    // Add security headers
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none';";
    
    await next();
});

app.UseHttpsRedirection();

// Custom authentication and authorization middleware
// app.UseMiddleware<Server.Middleware.AuthenticationMiddleware>();
// app.UseMiddleware<Server.Middleware.AuthorizationMiddleware>();

// Authentication and authorization must be in this order
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();