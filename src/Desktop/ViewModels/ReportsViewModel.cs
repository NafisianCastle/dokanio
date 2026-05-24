using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Repositories;
using Shared.Core.Services;

namespace Desktop.ViewModels;

public class ReportsViewModel : BaseViewModel
{
    private readonly IDashboardService _dashboardService;
    private readonly IReportService _reportService;
    private readonly IProductRepository _productRepository;
    private readonly IStockRepository _stockRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ReportsViewModel> _logger;

    [Reactive] public DateTime FromDate { get; set; } = DateTime.Today.AddDays(-30);
    [Reactive] public DateTime ToDate { get; set; } = DateTime.Today;
    [Reactive] public string SelectedReportType { get; set; } = "Sales Summary";

    public ObservableCollection<SalesReportItem> SalesData { get; } = new();
    public ObservableCollection<Shared.Core.Entities.Product> ExpiringProducts { get; } = new();
    public ObservableCollection<StockReportItem> LowStockItems { get; } = new();

    public List<string> ReportTypes { get; } = new()
    {
        "Sales Summary", "Product Performance", "Expiring Products",
        "Low Stock Alert", "Purchase Summary"
    };

    public decimal TotalSales        => SalesData.Sum(s => s.Amount);
    public int     TotalTransactions => SalesData.Sum(s => s.TransactionCount);
    public decimal AverageTransaction => TotalTransactions > 0 ? TotalSales / TotalTransactions : 0;

    public ReactiveCommand<Unit, Unit> GenerateReportCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportReportCommand { get; }

    // Design-time constructor
    public ReportsViewModel()
    {
        Title = "Reports & Analytics";
        GenerateReportCommand = ReactiveCommand.CreateFromTask(GenerateReportAsync);
        ExportReportCommand   = ReactiveCommand.CreateFromTask(ExportReportAsync);
    }

    public ReportsViewModel(
        IDashboardService dashboardService,
        IReportService reportService,
        IProductRepository productRepository,
        IStockRepository stockRepository,
        ICurrentUserService currentUserService,
        ILogger<ReportsViewModel> logger) : this()
    {
        _dashboardService   = dashboardService;
        _reportService      = reportService;
        _productRepository  = productRepository;
        _stockRepository    = stockRepository;
        _currentUserService = currentUserService;
        _logger             = logger;
    }

    private async Task GenerateReportAsync()
    {
        IsBusy = true;
        ClearError();
        try
        {
            switch (SelectedReportType)
            {
                case "Sales Summary":      await GenerateSalesReportAsync();           break;
                case "Expiring Products":  await GenerateExpiringProductsReportAsync(); break;
                case "Low Stock Alert":    await GenerateLowStockReportAsync();         break;
                default:                   await GenerateSalesReportAsync();            break;
            }
            this.RaisePropertyChanged(nameof(TotalSales));
            this.RaisePropertyChanged(nameof(TotalTransactions));
            this.RaisePropertyChanged(nameof(AverageTransaction));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report");
            SetError($"Error generating report: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private async Task ExportReportAsync()
    {
        if (_currentUserService?.CurrentUser == null) { SetError("User not authenticated"); return; }
        if (_reportService == null) return;

        IsBusy = true;
        ClearError();
        try
        {
            var request = new SalesReportRequest
            {
                DateRange  = new DateRange { StartDate = FromDate, EndDate = ToDate },
                Format     = ReportFormat.CSV,
                ReportType = SalesReportType.Summary
            };
            var reportData = await _reportService.GenerateSalesReportAsync(request);
            SuccessMessage = $"Report exported successfully ({reportData.Summary.TotalTransactions} transactions)";
            _logger.LogInformation("Report exported: {Transactions} transactions", reportData.Summary.TotalTransactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report");
            SetError($"Error exporting report: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private async Task GenerateSalesReportAsync()
    {
        SalesData.Clear();
        if (_dashboardService == null) return;
        try
        {
            var dailyData = await _dashboardService.GetDailyRevenueDataAsync(
                Guid.Empty, new DateRange { StartDate = FromDate, EndDate = ToDate });
            foreach (var d in dailyData)
                SalesData.Add(new SalesReportItem { Date = d.Date, TransactionCount = d.TransactionCount, Amount = d.Revenue });
        }
        catch (Exception ex) { _logger?.LogWarning(ex, "Could not load daily revenue data"); }
    }

    private async Task GenerateExpiringProductsReportAsync()
    {
        ExpiringProducts.Clear();
        if (_productRepository == null) return;
        var threshold = DateTime.Today.AddDays(30);
        var expiring  = await _productRepository.GetExpiringMedicinesAsync(threshold);
        foreach (var p in expiring.OrderBy(p => p.ExpiryDate)) ExpiringProducts.Add(p);
    }

    private async Task GenerateLowStockReportAsync()
    {
        LowStockItems.Clear();
        if (_stockRepository == null) return;
        try
        {
            var allStock = await _stockRepository.GetAllAsync();
            foreach (var stock in allStock.Where(s => s.Quantity <= 10 && !s.IsDeleted))
            {
                var product = _productRepository != null
                    ? await _productRepository.GetByIdAsync(stock.ProductId) : null;
                LowStockItems.Add(new StockReportItem
                {
                    ProductName  = product?.Name ?? stock.ProductId.ToString(),
                    CurrentStock = stock.Quantity,
                    MinimumStock = 10,
                    Category     = product?.Category ?? string.Empty
                });
            }
        }
        catch (Exception ex) { _logger?.LogWarning(ex, "Could not load low stock data"); }
    }
}

public class SalesReportItem
{
    public DateTime Date             { get; set; }
    public int      TransactionCount { get; set; }
    public decimal  Amount           { get; set; }
}

public class StockReportItem
{
    public string ProductName  { get; set; } = string.Empty;
    public int    CurrentStock { get; set; }
    public int    MinimumStock { get; set; }
    public string Category     { get; set; } = string.Empty;
    public int    StockDeficit => MinimumStock - CurrentStock;
}
