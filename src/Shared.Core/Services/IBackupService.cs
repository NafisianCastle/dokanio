using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared.Core.Services
{
    /// <summary>
    /// Service for automated backup and disaster recovery in multi-tenant POS system
    /// </summary>
    public interface IBackupService
    {
        /// <summary>
        /// Create a full system backup
        /// </summary>
        Task<BackupResult> CreateFullBackupAsync();

        /// <summary>
        /// Create a backup for a specific business
        /// </summary>
        Task<BackupResult> CreateBusinessBackupAsync(int businessId);

        /// <summary>
        /// Create an incremental backup
        /// </summary>
        Task<BackupResult> CreateIncrementalBackupAsync(DateTime since);

        /// <summary>
        /// Restore from a backup
        /// </summary>
        Task<RestoreResult> RestoreFromBackupAsync(string backupId, RestoreOptions options);

        /// <summary>
        /// Get list of available backups
        /// </summary>
        Task<IEnumerable<BackupInfo>> GetAvailableBackupsAsync();

        /// <summary>
        /// Verify backup integrity
        /// </summary>
        Task<BackupVerificationResult> VerifyBackupAsync(string backupId);

        /// <summary>
        /// Schedule automatic backups
        /// </summary>
        Task ScheduleAutomaticBackupsAsync(BackupSchedule schedule);

        /// <summary>
        /// Get backup status and statistics
        /// </summary>
        Task<BackupStatus> GetBackupStatusAsync();

        /// <summary>
        /// Clean up old backups based on retention policy
        /// </summary>
        Task CleanupOldBackupsAsync(RetentionPolicy policy);
    }

    public class BackupResult
    {
        public bool Success { get; set; }
        public string BackupId { get; set; } = string.Empty;
        public string BackupPath { get; set; } = string.Empty;
        public long BackupSizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
        public BackupType Type { get; set; }
        public int? BusinessId { get; set; }
    }

    public class RestoreResult
    {
        public bool Success { get; set; }
        public string BackupId { get; set; } = string.Empty;
        public DateTime RestoredAt { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
        public int RecordsRestored { get; set; }
    }

    public class BackupInfo
    {
        public string BackupId { get; set; } = string.Empty;
        public string BackupPath { get; set; } = string.Empty;
        public BackupType Type { get; set; }
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? BusinessId { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
    }

    public class BackupVerificationResult
    {
        public bool IsValid { get; set; }
        public string BackupId { get; set; } = string.Empty;
        public DateTime VerifiedAt { get; set; }
        public List<string> Issues { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class BackupSchedule
    {
        public string ScheduleId { get; set; } = string.Empty;
        public BackupType Type { get; set; }
        public string CronExpression { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public int? BusinessId { get; set; }
        public RetentionPolicy RetentionPolicy { get; set; } = new();
    }

    public class BackupStatus
    {
        public DateTime LastFullBackup { get; set; }
        public DateTime LastIncrementalBackup { get; set; }
        public int TotalBackups { get; set; }
        public long TotalBackupSizeBytes { get; set; }
        public bool IsBackupRunning { get; set; }
        public string? CurrentBackupId { get; set; }
        public List<BackupSchedule> ActiveSchedules { get; set; } = new();
    }

    public class RestoreOptions
    {
        public bool RestoreData { get; set; } = true;
        public bool RestoreSchema { get; set; } = true;
        public bool RestoreUsers { get; set; } = true;
        public int? TargetBusinessId { get; set; }
        public DateTime? PointInTime { get; set; }
        public List<string> TablesToRestore { get; set; } = new();
    }

    public class RetentionPolicy
    {
        public int DailyBackupsToKeep { get; set; } = 7;
        public int WeeklyBackupsToKeep { get; set; } = 4;
        public int MonthlyBackupsToKeep { get; set; } = 12;
        public int YearlyBackupsToKeep { get; set; } = 5;
        public bool DeleteAfterDays { get; set; } = true;
        public int MaxDaysToKeep { get; set; } = 365;
    }

    public enum BackupType
    {
        Full,
        Incremental,
        Differential,
        Business,
        Schema
    }
}