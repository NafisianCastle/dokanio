using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.Models;
using System.Collections.ObjectModel;

namespace Desktop.ViewModels;

public partial class ReportsViewModel : BaseViewModel
{
    [ObservableProperty]
    private DateTime fromDate = DateTime.Today.AddDays(-30);

    [ObservableProperty]
    private DateTime toDate = DateTime.Today;

    [ObservableProperty]
    private string selectedReportType = "Sales Summary";

    public ObservableCollection<SalesReportItem> SalesData { get; } = new();
    public ObservableCollection<Product> ExpiringProducts { get; } = new();
    public ObservableCollection<StockReportItem> LowStockItems { get; } = new();

    public List<string> ReportTypes { get; } = new()
    {
        "Sales Summary",
        "Product Performance",
        "Expiring Products",
        "Low Stock Alert",
        "Purchase Summary"
    };

    public decimal TotalSales => SalesData.Sum(s => s.Amount);
    public int TotalTransactions => SalesData.Sum(s => s.TransactionCount);
    public decimal AverageTransaction => TotalTransactions > 0 ? TotalSales / TotalTransactions : 0;

    public ReportsViewModel()
    {
        Title = "Reports & Analytics";
        LoadSampleData();
    }

    [RelayCommand]
    private async Task GenerateReport()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            await Task.Delay(1000); // Simulate report generation

            switch (SelectedReportType)
            {
                case "Sales Summary":
                    GenerateSalesReport();
                    break;
                case "Expiring Products":
                    GenerateExpiringProductsReport();
                    break;
                case "Low Stock Alert":
                    GenerateLowStockReport();
                    break;
                default:
                    GenerateSalesReport();
                    break;
            }

            OnPropertyChanged(nameof(TotalSales));
            OnPropertyChanged(nameof(TotalTransactions));
            OnPropertyChanged(nameof(AverageTransaction));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error generating report: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportReport()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            await Task.Delay(1500); // Simulate export

            // In real app, this would export to Excel/PDF
            ErrorMessage = $"Report exported successfully to Downloads folder";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error exporting report: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void GenerateSalesReport()
    {
        SalesData.Clear();

        // Generate sample sales data for the date range
        var random = new Random();
        var currentDate = FromDate;

        while (currentDate <= ToDate)
        {
            var dailySales = random.Next(5, 25); // 5-25 transactions per day
            var dailyAmount = random.Next(1000, 5000) + (decimal)random.NextDouble() * 1000;

            SalesData.Add(new SalesReportItem
            {
                Date = currentDate,
                TransactionCount = dailySales,
                Amount = Math.Round(dailyAmount, 2)
            });

            currentDate = currentDate.AddDays(1);
        }
    }

    private void GenerateExpiringProductsReport()
    {
        ExpiringProducts.Clear();

        // Sample expiring products
        var expiringProducts = new List<Product>
        {
            new() { Name = "Aspirin 75mg", ExpiryDate = DateTime.Today.AddDays(15), StockQuantity = 50, BatchNumber = "BATCH002" },
            new() { Name = "Cough Syrup", ExpiryDate = DateTime.Today.AddDays(25), StockQuantity = 30, BatchNumber = "BATCH004" },
            new() { Name = "Antibiotic Cream", ExpiryDate = DateTime.Today.AddDays(10), StockQuantity = 20, BatchNumber = "BATCH005" }
        };

        foreach (var product in expiringProducts)
        {
            ExpiringProducts.Add(product);
        }
    }

    private void GenerateLowStockReport()
    {
        LowStockItems.Clear();

        // Sample low stock items
        var lowStockItems = new List<StockReportItem>
        {
            new() { ProductName = "Paracetamol 500mg", CurrentStock = 5, MinimumStock = 20, Category = "Medicine" },
            new() { ProductName = "Bandages", CurrentStock = 8, MinimumStock = 50, Category = "Medical Supply" },
            new() { ProductName = "Thermometer", CurrentStock = 2, MinimumStock = 10, Category = "Medical Device" }
        };

        foreach (var item in lowStockItems)
        {
            LowStockItems.Add(item);
        }
    }

    private void LoadSampleData()
    {
        GenerateSalesReport();
        GenerateExpiringProductsReport();
        GenerateLowStockReport();
    }
}

public class SalesReportItem
{
    public DateTime Date { get; set; }
    public int TransactionCount { get; set; }
    public decimal Amount { get; set; }
}

public class StockReportItem
{
    public string ProductName { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int MinimumStock { get; set; }
    public string Category { get; set; } = string.Empty;
    public int StockDeficit => MinimumStock - CurrentStock;
}