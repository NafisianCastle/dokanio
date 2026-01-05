using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Shared.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Shared.Core.Services
{
    /// <summary>
    /// Implementation of system monitoring service for multi-tenant POS system
    /// </summary>
    public class SystemMonitoringService : ISystemMonitoringService
    {
        private readonly ILogger<SystemMonitoringService> _logger;
        private readonly PosDbContext _dbContext;
        private readonly List<SystemAlert> _activeAlerts = new();
        private readonly List<SystemEvent> _recentEvents = new();

        public SystemMonitoringService(
            ILogger<SystemMonitoringService> logger,
            PosDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task<SystemHealthStatus> GetSystemHealthAsync()
        {
            try
            {
                var healthStatus = new SystemHealthStatus
                {
                    LastChecked = DateTime.UtcNow,
                    Components = new Dictionary<string, ComponentHealth>()
                };

                // Check database connectivity
                var dbHealth = await CheckDatabaseHealthAsync();
                healthStatus.Components["Database"] = dbHealth;

                // Check memory usage
                var memoryHealth = CheckMemoryHealth();
                healthStatus.Components["Memory"] = memoryHealth;

                // Check disk space
                var diskHealth = CheckDiskHealth();
                healthStatus.Components["Disk"] = diskHealth;

                // Overall health
                healthStatus.IsHealthy = healthStatus.Components.Values.All(c => c.IsHealthy);
                healthStatus.Status = healthStatus.IsHealthy ? "Healthy" : "Unhealthy";

                _logger.LogInformation("System health check completed. Status: {Status}", healthStatus.Status);
                return healthStatus;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking system health");
                return new SystemHealthStatus
                {
                    IsHealthy = false,
                    Status = "Error",
                    LastChecked = DateTime.UtcNow
                };
            }
        }

        public async Task<BusinessHealthStatus> GetBusinessHealthAsync(int businessId)
        {
            try
            {
                var business = await _dbContext.Businesses
                    .Include(b => b.Shops)
                    .Include(b => b.Users)
                    .FirstOrDefaultAsync(b => b.Id == businessId);

                if (business == null)
                {
                    return new BusinessHealthStatus
                    {
                        BusinessId = businessId,
                        IsHealthy = false
                    };
                }

                var activeUsers = business.Users.Count(u => u.LastLoginDate > DateTime.UtcNow.AddDays(-7));
                var activeShops = business.Shops.Count(s => s.IsActive);

                var lastActivity = await _dbContext.Sales
                    .Where(s => s.BusinessId == businessId)
                    .OrderByDescending(s => s.SaleDate)
                    .Select(s => s.SaleDate)
                    .FirstOrDefaultAsync();

                return new BusinessHealthStatus
                {
                    BusinessId = businessId,
                    IsHealthy = activeUsers > 0 && activeShops > 0,
                    ActiveUsers = activeUsers,
                    ActiveShops = activeShops,
                    LastActivity = lastActivity
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking business health for business {BusinessId}", businessId);
                return new BusinessHealthStatus
                {
                    BusinessId = businessId,
                    IsHealthy = false
                };
            }
        }

        public async Task<SystemMetrics> GetSystemMetricsAsync()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var totalMemory = GC.GetTotalMemory(false);
                var workingSet = process.WorkingSet64;

                var totalRequests = await _dbContext.Sales.CountAsync();
                var recentErrors = _recentEvents.Count(e => e.Severity == "Error" && e.Timestamp > DateTime.UtcNow.AddHours(-1));

                return new SystemMetrics
                {
                    CpuUsagePercent = GetCpuUsage(),
                    MemoryUsagePercent = (double)workingSet / (1024 * 1024 * 1024) * 100, // Convert to GB percentage
                    DiskUsagePercent = GetDiskUsage(),
                    ActiveConnections = GetActiveConnections(),
                    TotalRequests = totalRequests,
                    AverageResponseTime = CalculateAverageResponseTime(),
                    ErrorCount = recentErrors,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system metrics");
                return new SystemMetrics { Timestamp = DateTime.UtcNow };
            }
        }

        public async Task<BusinessMetrics> GetBusinessMetricsAsync(int businessId)
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                
                var dailySales = await _dbContext.Sales
                    .Where(s => s.BusinessId == businessId && s.SaleDate.Date == today)
                    .CountAsync();

                var dailyRevenue = await _dbContext.Sales
                    .Where(s => s.BusinessId == businessId && s.SaleDate.Date == today)
                    .SumAsync(s => s.TotalAmount);

                var activeUsers = await _dbContext.Users
                    .Where(u => u.BusinessId == businessId && u.LastLoginDate > DateTime.UtcNow.AddDays(-7))
                    .CountAsync();

                var totalProducts = await _dbContext.Products
                    .Where(p => p.BusinessId == businessId)
                    .CountAsync();

                var lowStockItems = await _dbContext.Products
                    .Where(p => p.BusinessId == businessId && p.StockQuantity < 10)
                    .CountAsync();

                var lastSyncTime = await _dbContext.SyncLogs
                    .Where(sl => sl.BusinessId == businessId)
                    .OrderByDescending(sl => sl.SyncTime)
                    .Select(sl => sl.SyncTime)
                    .FirstOrDefaultAsync();

                return new BusinessMetrics
                {
                    BusinessId = businessId,
                    DailySales = dailySales,
                    DailyRevenue = dailyRevenue,
                    ActiveUsers = activeUsers,
                    TotalProducts = totalProducts,
                    LowStockItems = lowStockItems,
                    LastSyncTime = lastSyncTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting business metrics for business {BusinessId}", businessId);
                return new BusinessMetrics { BusinessId = businessId };
            }
        }

        public async Task RecordSystemEventAsync(SystemEvent systemEvent)
        {
            try
            {
                systemEvent.Timestamp = DateTime.UtcNow;
                _recentEvents.Add(systemEvent);

                // Keep only recent events (last 24 hours)
                _recentEvents.RemoveAll(e => e.Timestamp < DateTime.UtcNow.AddDays(-1));

                // Create alert for critical events
                if (systemEvent.Severity == "Critical" || systemEvent.Severity == "Error")
                {
                    var alert = new SystemAlert
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = systemEvent.EventType,
                        Description = systemEvent.Message,
                        Severity = systemEvent.Severity,
                        Status = "Active",
                        CreatedAt = systemEvent.Timestamp,
                        BusinessId = systemEvent.BusinessId
                    };
                    _activeAlerts.Add(alert);
                }

                _logger.LogInformation("System event recorded: {EventType} - {Message}", 
                    systemEvent.EventType, systemEvent.Message);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording system event");
            }
        }

        public async Task<IEnumerable<SystemAlert>> GetActiveAlertsAsync()
        {
            try
            {
                // Remove resolved alerts older than 7 days
                _activeAlerts.RemoveAll(a => a.ResolvedAt.HasValue && a.ResolvedAt < DateTime.UtcNow.AddDays(-7));
                
                return await Task.FromResult(_activeAlerts.Where(a => a.Status == "Active"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active alerts");
                return new List<SystemAlert>();
            }
        }

        public async Task<DataIsolationStatus> CheckDataIsolationAsync()
        {
            try
            {
                var violations = new List<DataIsolationViolation>();

                // Check for cross-business data leaks in sales
                var crossBusinessSales = await _dbContext.Sales
                    .Join(_dbContext.Products, s => s.ProductId, p => p.Id, (s, p) => new { Sale = s, Product = p })
                    .Where(sp => sp.Sale.BusinessId != sp.Product.BusinessId)
                    .CountAsync();

                if (crossBusinessSales > 0)
                {
                    violations.Add(new DataIsolationViolation
                    {
                        Type = "CrossBusinessSales",
                        Description = $"Found {crossBusinessSales} sales with products from different businesses",
                        TableName = "Sales",
                        DetectedAt = DateTime.UtcNow
                    });
                }

                // Check for users with access to multiple businesses
                var multiBusinessUsers = await _dbContext.Users
                    .GroupBy(u => u.Email)
                    .Where(g => g.Select(u => u.BusinessId).Distinct().Count() > 1)
                    .CountAsync();

                if (multiBusinessUsers > 0)
                {
                    violations.Add(new DataIsolationViolation
                    {
                        Type = "MultiBusinessUsers",
                        Description = $"Found {multiBusinessUsers} users with access to multiple businesses",
                        TableName = "Users",
                        DetectedAt = DateTime.UtcNow
                    });
                }

                return new DataIsolationStatus
                {
                    IsIntact = violations.Count == 0,
                    ViolationCount = violations.Count,
                    Violations = violations,
                    LastChecked = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking data isolation");
                return new DataIsolationStatus
                {
                    IsIntact = false,
                    LastChecked = DateTime.UtcNow
                };
            }
        }

        public async Task<AIAnalyticsHealth> GetAIAnalyticsHealthAsync()
        {
            try
            {
                // Simulate AI analytics health check
                var processedJobs = _recentEvents.Count(e => e.EventType == "AIAnalyticsProcessed" && e.Timestamp > DateTime.UtcNow.AddHours(-24));
                var failedJobs = _recentEvents.Count(e => e.EventType == "AIAnalyticsFailed" && e.Timestamp > DateTime.UtcNow.AddHours(-24));

                var lastProcessed = _recentEvents
                    .Where(e => e.EventType == "AIAnalyticsProcessed")
                    .OrderByDescending(e => e.Timestamp)
                    .FirstOrDefault()?.Timestamp ?? DateTime.MinValue;

                var isHealthy = failedJobs < 5 && lastProcessed > DateTime.UtcNow.AddHours(-2);

                return await Task.FromResult(new AIAnalyticsHealth
                {
                    IsHealthy = isHealthy,
                    Status = isHealthy ? "Healthy" : "Degraded",
                    ProcessedJobs = processedJobs,
                    FailedJobs = failedJobs,
                    LastProcessedAt = lastProcessed,
                    ModelMetrics = new Dictionary<string, object>
                    {
                        ["accuracy"] = 0.85,
                        ["precision"] = 0.82,
                        ["recall"] = 0.88
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking AI analytics health");
                return new AIAnalyticsHealth
                {
                    IsHealthy = false,
                    Status = "Error"
                };
            }
        }

        private async Task<ComponentHealth> CheckDatabaseHealthAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
                stopwatch.Stop();

                return new ComponentHealth
                {
                    IsHealthy = true,
                    Status = "Connected",
                    ResponseTime = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new ComponentHealth
                {
                    IsHealthy = false,
                    Status = "Disconnected",
                    ResponseTime = stopwatch.Elapsed,
                    ErrorMessage = ex.Message
                };
            }
        }

        private ComponentHealth CheckMemoryHealth()
        {
            try
            {
                var totalMemory = GC.GetTotalMemory(false);
                var memoryMB = totalMemory / (1024 * 1024);
                var isHealthy = memoryMB < 1024; // Less than 1GB

                return new ComponentHealth
                {
                    IsHealthy = isHealthy,
                    Status = isHealthy ? "Normal" : "High",
                    ResponseTime = TimeSpan.Zero
                };
            }
            catch (Exception ex)
            {
                return new ComponentHealth
                {
                    IsHealthy = false,
                    Status = "Error",
                    ErrorMessage = ex.Message
                };
            }
        }

        private ComponentHealth CheckDiskHealth()
        {
            try
            {
                var drives = DriveInfo.GetDrives();
                var systemDrive = drives.FirstOrDefault(d => d.Name == Path.GetPathRoot(Environment.SystemDirectory));
                
                if (systemDrive != null)
                {
                    var freeSpacePercent = (double)systemDrive.AvailableFreeSpace / systemDrive.TotalSize * 100;
                    var isHealthy = freeSpacePercent > 10; // More than 10% free space

                    return new ComponentHealth
                    {
                        IsHealthy = isHealthy,
                        Status = isHealthy ? "Normal" : "Low Space",
                        ResponseTime = TimeSpan.Zero
                    };
                }

                return new ComponentHealth
                {
                    IsHealthy = true,
                    Status = "Unknown",
                    ResponseTime = TimeSpan.Zero
                };
            }
            catch (Exception ex)
            {
                return new ComponentHealth
                {
                    IsHealthy = false,
                    Status = "Error",
                    ErrorMessage = ex.Message
                };
            }
        }

        private double GetCpuUsage()
        {
            // Simplified CPU usage calculation
            var process = Process.GetCurrentProcess();
            return process.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount / 1000.0 * 100;
        }

        private double GetDiskUsage()
        {
            try
            {
                var drives = DriveInfo.GetDrives();
                var systemDrive = drives.FirstOrDefault(d => d.Name == Path.GetPathRoot(Environment.SystemDirectory));
                
                if (systemDrive != null)
                {
                    return (double)(systemDrive.TotalSize - systemDrive.AvailableFreeSpace) / systemDrive.TotalSize * 100;
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private int GetActiveConnections()
        {
            // Simplified active connections count
            return _dbContext.ChangeTracker.Entries().Count();
        }

        private double CalculateAverageResponseTime()
        {
            // Simplified average response time calculation
            var recentEvents = _recentEvents.Where(e => e.Timestamp > DateTime.UtcNow.AddMinutes(-5));
            return recentEvents.Any() ? 150.0 : 0.0; // Mock 150ms average
        }
    }
}