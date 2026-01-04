using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.Services;

namespace Mobile.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly ISyncEngine _syncEngine;
    private readonly IConnectivityService _connectivityService;

    public MainViewModel(ISyncEngine syncEngine, IConnectivityService connectivityService)
    {
        _syncEngine = syncEngine;
        _connectivityService = connectivityService;
        Title = "Offline POS";
        
        // Start background sync
        _ = Task.Run(async () => await _syncEngine.StartBackgroundSyncAsync());
    }

    [ObservableProperty]
    private bool isOnline;

    [ObservableProperty]
    private DateTime lastSyncTime;

    [ObservableProperty]
    private string syncStatus = "Ready";

    [RelayCommand]
    private async Task NavigateToProductList()
    {
        await Shell.Current.GoToAsync("//products");
    }

    [RelayCommand]
    private async Task NavigateToNewSale()
    {
        await Shell.Current.GoToAsync("//sale");
    }

    [RelayCommand]
    private async Task NavigateToDailySales()
    {
        await Shell.Current.GoToAsync("//dailysales");
    }

    [RelayCommand]
    private async Task NavigateToScanner()
    {
        await Shell.Current.GoToAsync("//scanner");
    }

    [RelayCommand]
    private async Task ManualSync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            SyncStatus = "Syncing...";
            
            var result = await _syncEngine.SyncAllAsync();
            
            if (result.Success)
            {
                SyncStatus = "Sync completed";
                LastSyncTime = DateTime.Now;
            }
            else
            {
                SyncStatus = $"Sync failed: {result.Message}";
                SetError(result.Message ?? "Sync failed");
            }
        }
        catch (Exception ex)
        {
            SyncStatus = "Sync failed";
            SetError($"Sync error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task CheckConnectivity()
    {
        IsOnline = _connectivityService.IsConnected;
    }
}