namespace Shared.Core.Services;

public interface ISyncEngine
{
    Task<SyncResult> SyncAllAsync();
    Task<SyncResult> SyncSalesAsync();
    Task<SyncResult> SyncProductsAsync();
    Task<SyncResult> SyncStockAsync();
    Task StartBackgroundSyncAsync();
    Task StopBackgroundSyncAsync();
    event EventHandler<SyncProgressEventArgs>? SyncProgress;
    event EventHandler<ConnectivityChangedEventArgs>? ConnectivityChanged;
}

public class SyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ItemsSynced { get; set; }
    public DateTime SyncTime { get; set; }
    public int RecordsUploaded { get; set; }
    public int RecordsDownloaded { get; set; }
    public int ConflictsResolved { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime SyncTimestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan SyncDuration { get; set; }
}

public class SyncProgressEventArgs : EventArgs
{
    public string Message { get; set; } = string.Empty;
    public int Progress { get; set; }
    public bool IsCompleted { get; set; }
}