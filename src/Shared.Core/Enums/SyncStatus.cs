namespace Shared.Core.Enums;

public enum SyncStatus
{
    NotSynced = 0,
    Syncing = 1,
    Synced = 2,
    SyncFailed = 3,
    ConflictResolved = 4
}