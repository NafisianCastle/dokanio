using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Desktop.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    [ObservableProperty]
    private string currentUser = "Admin";

    [ObservableProperty]
    private bool isOnline = true;

    [ObservableProperty]
    private DateTime lastSyncTime = DateTime.Now;

    [ObservableProperty]
    private string syncStatus = "Ready";

    [ObservableProperty]
    private decimal todaysSales = 2450.50m;

    [ObservableProperty]
    private int todaysTransactions = 15;

    [ObservableProperty]
    private int lowStockItems = 3;

    [ObservableProperty]
    private ObservableCollection<string> recentActivities = new();

    public SaleViewModel SaleViewModel { get; }
    public SupplierViewModel SupplierViewModel { get; }
    public PurchaseViewModel PurchaseViewModel { get; }
    public ProductViewModel ProductViewModel { get; }
    public ReportsViewModel ReportsViewModel { get; }

    public MainViewModel()
    {
        Title = "POS Desktop - Dashboard";
        SaleViewModel = new SaleViewModel();
        SupplierViewModel = new SupplierViewModel();
        PurchaseViewModel = new PurchaseViewModel();
        ProductViewModel = new ProductViewModel();
        ReportsViewModel = new ReportsViewModel();
        
        LoadDashboardData();
    }

    [RelayCommand]
    private void RefreshDashboard()
    {
        LoadDashboardData();
    }

    [RelayCommand]
    private void SyncData()
    {
        SyncStatus = "Syncing...";
        
        // Simulate sync
        Task.Delay(1000).ContinueWith(_ =>
        {
            LastSyncTime = DateTime.Now;
            SyncStatus = "Sync completed";
            RecentActivities.Insert(0, $"Data sync completed at {DateTime.Now:HH:mm}");
        });
    }

    private void LoadDashboardData()
    {
        // Update recent activities
        RecentActivities.Clear();
        RecentActivities.Add($"Dashboard loaded at {DateTime.Now:HH:mm}");
        RecentActivities.Add("Online - Ready to sync");
        RecentActivities.Add("System initialized successfully");
    }
}