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
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int ItemsSynced { get; set; }
    public DateTime SyncTime { get; set; }
}

public class SyncProgressEventArgs : EventArgs
{
    public string Message { get; set; } = string.Empty;
    public int Progress { get; set; }
    public bool IsCompleted { get; set; }
}