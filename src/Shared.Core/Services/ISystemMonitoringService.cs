using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared.Core.Services
{
    /// <summary>
    /// Service for system monitoring and health checks in multi-tenant POS system
    /// </summary>
    public interface ISystemMonitoringService
    {
        /// <summary>
        /// Get overall system health status
        /// </summary>
        Task<SystemHealthStatus> GetSystemHealthAsync();

        /// <summary>
        /// Get health status for a specific business
        /// </summary>
        Task<BusinessHealthStatus> GetBusinessHealthAsync(int businessId);

        /// <summary>
        /// Get system performance metrics
        /// </summary>
        Task<SystemMetrics> GetSystemMetricsAsync();

        /// <summary>
        /// Get business-specific performance metrics
        /// </summary>
        Task<BusinessMetrics> GetBusinessMetricsAsync(int businessId);

        /// <summary>
        /// Record a system event for monitoring
        /// </summary>
        Task RecordSystemEventAsync(SystemEvent systemEvent);

        /// <summary>
        /// Get system alerts
        /// </summary>
        Task<IEnumerable<SystemAlert>> GetActiveAlertsAsync();

        /// <summary>
        /// Check multi-tenant data isolation integrity
        /// </summary>
        Task<DataIsolationStatus> CheckDataIsolationAsync();

        /// <summary>
        /// Monitor AI analytics pipeline health
        /// </summary>
        Task<AIAnalyticsHealth> GetAIAnalyticsHealthAsync();
    }

    public class SystemHealthStatus
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime LastChecked { get; set; }
        public Dictionary<string, ComponentHealth> Components { get; set; } = new();
    }

    public class ComponentHealth
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = string.Empty;
        public TimeSpan ResponseTime { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class BusinessHealthStatus
    {
        public int BusinessId { get; set; }
        public bool IsHealthy { get; set; }
        public int ActiveUsers { get; set; }
        public int ActiveShops { get; set; }
        public DateTime LastActivity { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = new();
    }

    public class SystemMetrics
    {
        public double CpuUsagePercent { get; set; }
        public double MemoryUsagePercent { get; set; }
        public double DiskUsagePercent { get; set; }
        public int ActiveConnections { get; set; }
        public int TotalRequests { get; set; }
        public double AverageResponseTime { get; set; }
        public int ErrorCount { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class BusinessMetrics
    {
        public int BusinessId { get; set; }
        public int DailySales { get; set; }
        public decimal DailyRevenue { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalProducts { get; set; }
        public int LowStockItems { get; set; }
        public DateTime LastSyncTime { get; set; }
        public Dictionary<string, object> CustomMetrics { get; set; } = new();
    }

    public class SystemEvent
    {
        public string EventType { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public int? BusinessId { get; set; }
        public int? UserId { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    public class SystemAlert
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public int? BusinessId { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class DataIsolationStatus
    {
        public bool IsIntact { get; set; }
        public int ViolationCount { get; set; }
        public List<DataIsolationViolation> Violations { get; set; } = new();
        public DateTime LastChecked { get; set; }
    }

    public class DataIsolationViolation
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int? BusinessId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
    }

    public class AIAnalyticsHealth
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = string.Empty;
        public int ProcessedJobs { get; set; }
        public int FailedJobs { get; set; }
        public DateTime LastProcessedAt { get; set; }
        public Dictionary<string, object> ModelMetrics { get; set; } = new();
    }
}