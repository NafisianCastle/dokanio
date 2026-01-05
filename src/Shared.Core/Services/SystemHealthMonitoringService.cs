using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of system health monitoring service
/// Provides real-time monitoring and alerting for system health issues
/// </summary>
public class SystemHealthMonitoringService : ISystemHealthMonitoringService, IDisposable
{
    private readonly IErrorRecoveryService _errorRecoveryService;
    private readonly IComprehensiveLoggingService _loggingService;
    private readonly ILogger<SystemHealthMonitoringService> _logger;
    private Timer? _monitoringTimer;
    private readonly object _lockObject = new();
    
    private bool _isMonitoring = false;
    private SystemHealthResult? _lastHealthStatus;
    private readonly List<SystemHealthResult> _healthHistory = new();

    public event EventHandler<HealthStatusChangedEventArgs>? HealthStatusChanged;
    public event EventHandler<CriticalHealthIssueEventArgs>? CriticalHealthIssueDetected;

    public SystemHealthMonitoringService(
        IErrorRecoveryService errorRecoveryService,
        IComprehensiveLoggingService loggingService,
        ILogger<SystemHealthMonitoringService> logger)
    {
        _errorRecoveryService = errorRecoveryService ?? throw new ArgumentNullException(nameof(errorRecoveryService));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts continuous system health monitoring
    /// Local-first: Monitors Local_Storage and local system health
    /// </summary>
    public async Task StartMonitoringAsync(TimeSpan monitoringInterval)
    {
        lock (_lockObject)
        {
            if (_isMonitoring)
            {
                return; // Already monitoring
            }

            _isMonitoring = true;
        }

        var deviceId = Guid.NewGuid(); // In real implementation, get from current context

        try
        {
            await _loggingService.LogInfoAsync(
                $"System health monitoring started with interval: {monitoringInterval}",
                LogCategory.System,
                deviceId);

            // Start the monitoring timer
            _monitoringTimer = new Timer(async _ => await PerformHealthCheckAsync(), null, TimeSpan.Zero, monitoringInterval);

            _logger.LogInformation("System health monitoring started with interval: {Interval}", monitoringInterval);
        }
        catch (Exception ex)
        {
            _isMonitoring = false;
            await _loggingService.LogErrorAsync(
                $"Failed to start system health monitoring: {ex.Message}",
                LogCategory.System,
                deviceId,
                ex);
            throw;
        }
    }

    /// <summary>
    /// Stops continuous system health monitoring
    /// </summary>
    public async Task StopMonitoringAsync()
    {
        lock (_lockObject)
        {
            if (!_isMonitoring)
            {
                return; // Not monitoring
            }

            _isMonitoring = false;
        }

        var deviceId = Guid.NewGuid(); // In real implementation, get from current context

        try
        {
            _monitoringTimer?.Dispose();

            await _loggingService.LogInfoAsync(
                "System health monitoring stopped",
                LogCategory.System,
                deviceId);

            _logger.LogInformation("System health monitoring stopped");
        }
        catch (Exception ex)
        {
            await _loggingService.LogErrorAsync(
                $"Error stopping system health monitoring: {ex.Message}",
                LogCategory.System,
                deviceId,
                ex);
        }
    }

    /// <summary>
    /// Gets current system health status
    /// Local-first: Uses Local_Storage for health assessment
    /// </summary>
    public async Task<SystemHealthResult> GetCurrentHealthStatusAsync()
    {
        try
        {
            var healthResult = await _errorRecoveryService.PerformSystemHealthCheckAsync();
            
            lock (_lockObject)
            {
                _lastHealthStatus = healthResult;
                _healthHistory.Add(healthResult);
                
                // Keep only last 100 health check results
                if (_healthHistory.Count > 100)
                {
                    _healthHistory.RemoveAt(0);
                }
            }

            return healthResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current health status");
            
            return new SystemHealthResult
            {
                IsHealthy = false,
                Issues = new List<HealthIssue>
                {
                    new HealthIssue
                    {
                        Category = "System",
                        Description = $"Health check failed: {ex.Message}",
                        Severity = HealthSeverity.Critical
                    }
                }
            };
        }
    }

    /// <summary>
    /// Gets system health history within a date range
    /// Local-first: Uses local health history cache
    /// </summary>
    public async Task<IEnumerable<SystemHealthResult>> GetHealthHistoryAsync(DateTime? from = null, DateTime? to = null)
    {
        await Task.CompletedTask; // Make method async

        lock (_lockObject)
        {
            var history = _healthHistory.AsEnumerable();

            if (from.HasValue)
            {
                history = history.Where(h => h.CheckTimestamp >= from.Value);
            }

            if (to.HasValue)
            {
                history = history.Where(h => h.CheckTimestamp <= to.Value);
            }

            return history.OrderByDescending(h => h.CheckTimestamp).ToList();
        }
    }

    /// <summary>
    /// Gets system performance metrics
    /// Local-first: Collects metrics from local system and Local_Storage
    /// </summary>
    public async Task<SystemPerformanceMetrics> GetPerformanceMetricsAsync()
    {
        try
        {
            var metrics = new SystemPerformanceMetrics();

            // Database response time test
            var stopwatch = Stopwatch.StartNew();
            await _errorRecoveryService.PerformSystemHealthCheckAsync();
            stopwatch.Stop();
            metrics.DatabaseResponseTime = stopwatch.Elapsed.TotalMilliseconds;

            // Memory usage
            var process = Process.GetCurrentProcess();
            metrics.MemoryUsage = process.WorkingSet64;

            // CPU usage (simplified)
            metrics.CpuUsage = GetCpuUsage();

            // Disk space (simplified)
            var diskInfo = GetDiskSpaceInfo();
            metrics.DiskSpaceUsed = diskInfo.Used;
            metrics.DiskSpaceAvailable = diskInfo.Available;

            // Transaction throughput (placeholder)
            metrics.TransactionThroughput = await GetTransactionThroughputAsync();

            // Error rate (placeholder)
            metrics.ErrorRate = await GetErrorRateAsync();

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting performance metrics");
            return new SystemPerformanceMetrics();
        }
    }

    /// <summary>
    /// Gets diagnostic information for troubleshooting
    /// Local-first: Collects diagnostic info from local system and Local_Storage
    /// </summary>
    public async Task<SystemDiagnosticInfo> GetDiagnosticInfoAsync()
    {
        try
        {
            var diagnosticInfo = new SystemDiagnosticInfo
            {
                SystemVersion = GetSystemVersion(),
                DatabaseVersion = await GetDatabaseVersionAsync(),
                Configuration = GetSystemConfiguration(),
                RecentErrors = await GetRecentErrorsAsync(),
                PerformanceMetrics = await GetPerformanceMetricsAsync(),
                CurrentIssues = (await GetCurrentHealthStatusAsync()).Issues
            };

            // Add additional diagnostic information
            diagnosticInfo.AdditionalInfo["ProcessId"] = Process.GetCurrentProcess().Id;
            diagnosticInfo.AdditionalInfo["StartTime"] = Process.GetCurrentProcess().StartTime;
            diagnosticInfo.AdditionalInfo["ThreadCount"] = Process.GetCurrentProcess().Threads.Count;
            diagnosticInfo.AdditionalInfo["HandleCount"] = Process.GetCurrentProcess().HandleCount;

            return diagnosticInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting diagnostic information");
            return new SystemDiagnosticInfo
            {
                RecentErrors = new List<string> { $"Diagnostic collection failed: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Performs a health check and raises events if status changes
    /// </summary>
    private async Task PerformHealthCheckAsync()
    {
        if (!_isMonitoring)
        {
            return;
        }

        try
        {
            var currentHealth = await GetCurrentHealthStatusAsync();
            
            SystemHealthResult? previousHealth;
            lock (_lockObject)
            {
                previousHealth = _lastHealthStatus;
                _lastHealthStatus = currentHealth;
            }

            // Check for status changes
            if (previousHealth != null && currentHealth.IsHealthy != previousHealth.IsHealthy)
            {
                HealthStatusChanged?.Invoke(this, new HealthStatusChangedEventArgs
                {
                    PreviousStatus = previousHealth,
                    CurrentStatus = currentHealth
                });
            }

            // Check for critical issues
            var criticalIssues = currentHealth.Issues.Where(i => i.Severity == HealthSeverity.Critical).ToList();
            foreach (var issue in criticalIssues)
            {
                CriticalHealthIssueDetected?.Invoke(this, new CriticalHealthIssueEventArgs
                {
                    Issue = issue,
                    RequiresImmediateAttention = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health check");
        }
    }

    // Helper methods for collecting system information

    private double GetCpuUsage()
    {
        try
        {
            // Simplified CPU usage calculation
            var process = Process.GetCurrentProcess();
            return process.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount;
        }
        catch
        {
            return 0;
        }
    }

    private (long Used, long Available) GetDiskSpaceInfo()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:");
            return (drive.TotalSize - drive.AvailableFreeSpace, drive.AvailableFreeSpace);
        }
        catch
        {
            return (0, 0);
        }
    }

    private async Task<int> GetTransactionThroughputAsync()
    {
        try
        {
            // In real implementation, would calculate actual transaction throughput
            // For now, return a placeholder value
            await Task.CompletedTask;
            return 100; // Placeholder
        }
        catch
        {
            return 0;
        }
    }

    private async Task<int> GetErrorRateAsync()
    {
        try
        {
            // Get error logs from the last hour
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);
            var errorLogs = await _loggingService.GetErrorLogsAsync(oneHourAgo);
            return errorLogs.Count();
        }
        catch
        {
            return 0;
        }
    }

    private string GetSystemVersion()
    {
        try
        {
            return Environment.OSVersion.ToString();
        }
        catch
        {
            return "Unknown";
        }
    }

    private async Task<string> GetDatabaseVersionAsync()
    {
        try
        {
            // In real implementation, would query database version
            await Task.CompletedTask;
            return "SQLite 3.x"; // Placeholder
        }
        catch
        {
            return "Unknown";
        }
    }

    private Dictionary<string, string> GetSystemConfiguration()
    {
        var config = new Dictionary<string, string>();

        try
        {
            config["MachineName"] = Environment.MachineName;
            config["ProcessorCount"] = Environment.ProcessorCount.ToString();
            config["OSVersion"] = Environment.OSVersion.ToString();
            config["CLRVersion"] = Environment.Version.ToString();
            config["WorkingDirectory"] = Environment.CurrentDirectory;
        }
        catch (Exception ex)
        {
            config["ConfigurationError"] = ex.Message;
        }

        return config;
    }

    private async Task<List<string>> GetRecentErrorsAsync()
    {
        try
        {
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);
            var errorLogs = await _loggingService.GetErrorLogsAsync(oneHourAgo);
            
            return errorLogs
                .Take(10) // Last 10 errors
                .Select(log => $"{log.CreatedAt:yyyy-MM-dd HH:mm:ss} - {log.Message}")
                .ToList();
        }
        catch
        {
            return new List<string> { "Could not retrieve recent errors" };
        }
    }

    public void Dispose()
    {
        _monitoringTimer?.Dispose();
        _isMonitoring = false;
    }
}