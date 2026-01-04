using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.Entities;
using Shared.Core.Repositories;

namespace Mobile.ViewModels;

public partial class DailySalesViewModel : BaseViewModel
{
    private readonly ISaleRepository _saleRepository;

    public DailySalesViewModel(ISaleRepository saleRepository)
    {
        _saleRepository = saleRepository;
        Title = "Daily Sales";
        Sales = new ObservableCollection<Sale>();
        SelectedDate = DateTime.Today;
    }

    [ObservableProperty]
    private ObservableCollection<Sale> sales;

    [ObservableProperty]
    private DateTime selectedDate;

    [ObservableProperty]
    private decimal dailyTotal;

    [ObservableProperty]
    private int salesCount;

    [RelayCommand]
    private async Task LoadDailySales()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ClearError();

            // Load sales for selected date
            var salesForDate = await _saleRepository.GetSalesByDateRangeAsync(
                SelectedDate.Date, 
                SelectedDate.Date.AddDays(1));

            Sales.Clear();
            foreach (var sale in salesForDate.OrderByDescending(s => s.CreatedAt))
            {
                Sales.Add(sale);
            }

            // Calculate daily total and count
            DailyTotal = await _saleRepository.GetDailySalesAsync(SelectedDate);
            SalesCount = await _saleRepository.GetDailySalesCountAsync(SelectedDate);
        }
        catch (Exception ex)
        {
            SetError($"Failed to load daily sales: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PreviousDay()
    {
        SelectedDate = SelectedDate.AddDays(-1);
        await LoadDailySales();
    }

    [RelayCommand]
    private async Task NextDay()
    {
        if (SelectedDate.Date < DateTime.Today)
        {
            SelectedDate = SelectedDate.AddDays(1);
            await LoadDailySales();
        }
    }

    [RelayCommand]
    private async Task SelectToday()
    {
        SelectedDate = DateTime.Today;
        await LoadDailySales();
    }

    [RelayCommand]
    private async Task ViewSaleDetails(Sale sale)
    {
        if (sale == null) return;

        // Navigate to sale details or show popup
        var details = $"Invoice: {sale.InvoiceNumber}\n" +
                     $"Amount: ${sale.TotalAmount:F2}\n" +
                     $"Payment: {sale.PaymentMethod}\n" +
                     $"Time: {sale.CreatedAt:HH:mm}";

        await Shell.Current.DisplayAlert("Sale Details", details, "OK");
    }

    public async Task Initialize()
    {
        await LoadDailySales();
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        _ = Task.Run(async () => await LoadDailySales());
    }
}