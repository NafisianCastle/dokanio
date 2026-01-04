using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;

namespace Mobile.ViewModels;

public partial class SaleViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ISaleService _saleService;
    private readonly IProductService _productService;
    private readonly IPrinterService _printerService;
    private readonly IReceiptService _receiptService;

    public SaleViewModel(
        ISaleService saleService, 
        IProductService productService,
        IPrinterService printerService,
        IReceiptService receiptService)
    {
        _saleService = saleService;
        _productService = productService;
        _printerService = printerService;
        _receiptService = receiptService;
        Title = "New Sale";
        SaleItems = new ObservableCollection<SaleItemViewModel>();
        PaymentMethods = Enum.GetValues<PaymentMethod>().ToList();
        SelectedPaymentMethod = PaymentMethod.Cash;
    }

    [ObservableProperty]
    private ObservableCollection<SaleItemViewModel> saleItems;

    [ObservableProperty]
    private decimal totalAmount;

    [ObservableProperty]
    private List<PaymentMethod> paymentMethods;

    [ObservableProperty]
    private PaymentMethod selectedPaymentMethod;

    [ObservableProperty]
    private string invoiceNumber = string.Empty;

    [ObservableProperty]
    private bool canCompleteSale;

    private Sale? _currentSale;

    [RelayCommand]
    private async Task AddProduct(Product product)
    {
        if (product == null) return;

        try
        {
            // Check if product already exists in sale
            var existingItem = SaleItems.FirstOrDefault(si => si.ProductId == product.Id);
            
            if (existingItem != null)
            {
                existingItem.Quantity++;
            }
            else
            {
                var saleItemViewModel = new SaleItemViewModel
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.UnitPrice,
                    Quantity = 1,
                    BatchNumber = product.BatchNumber
                };
                
                SaleItems.Add(saleItemViewModel);
            }

            CalculateTotal();
        }
        catch (Exception ex)
        {
            SetError($"Failed to add product: {ex.Message}");
        }
    }

    [RelayCommand]
    private void RemoveItem(SaleItemViewModel item)
    {
        if (item == null) return;

        SaleItems.Remove(item);
        CalculateTotal();
    }

    [RelayCommand]
    private void UpdateQuantity(SaleItemViewModel item)
    {
        if (item == null || item.Quantity <= 0)
        {
            RemoveItem(item);
            return;
        }

        CalculateTotal();
    }

    [RelayCommand]
    private async Task CompleteSale()
    {
        if (IsBusy || !CanCompleteSale) return;

        try
        {
            IsBusy = true;
            ClearError();

            // Create sale
            var deviceId = Guid.NewGuid(); // In real app, get from device service
            _currentSale = await _saleService.CreateSaleAsync(InvoiceNumber, deviceId);

            // Add items to sale
            foreach (var item in SaleItems)
            {
                await _saleService.AddItemToSaleAsync(
                    _currentSale.Id,
                    item.ProductId,
                    item.Quantity,
                    item.UnitPrice,
                    item.BatchNumber);
            }

            // Complete sale
            await _saleService.CompleteSaleAsync(_currentSale.Id, SelectedPaymentMethod);

            // Print receipt
            await PrintReceipt();

            // Clear sale
            await ClearSale();

            // Show success message
            await Shell.Current.DisplayAlert("Success", "Sale completed successfully!", "OK");
        }
        catch (Exception ex)
        {
            SetError($"Failed to complete sale: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearSale()
    {
        SaleItems.Clear();
        TotalAmount = 0;
        InvoiceNumber = GenerateInvoiceNumber();
        _currentSale = null;
        CanCompleteSale = false;
    }

    [RelayCommand]
    private async Task ScanBarcode()
    {
        await Shell.Current.GoToAsync("//scanner");
    }

    private async Task PrintReceipt()
    {
        if (_currentSale == null) return;

        try
        {
            var isConnected = await _printerService.IsConnectedAsync();
            if (!isConnected)
            {
                await Shell.Current.DisplayAlert("Warning", "Printer not connected. Receipt not printed.", "OK");
                return;
            }

            var result = await _printerService.PrintReceiptAsync(_currentSale);
            if (!result.Success)
            {
                await Shell.Current.DisplayAlert("Warning", $"Failed to print receipt: {result.Message}", "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Warning", $"Print error: {ex.Message}", "OK");
        }
    }

    private void CalculateTotal()
    {
        TotalAmount = SaleItems.Sum(item => item.Quantity * item.UnitPrice);
        CanCompleteSale = SaleItems.Any() && TotalAmount > 0;
    }

    private string GenerateInvoiceNumber()
    {
        return $"INV-{DateTime.Now:yyyyMMdd}-{DateTime.Now.Ticks % 10000:D4}";
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey("SelectedProduct") && query["SelectedProduct"] is Product product)
        {
            _ = Task.Run(async () => await AddProduct(product));
        }
        
        if (query.ContainsKey("ScannedBarcode") && query["ScannedBarcode"] is string barcode)
        {
            _ = Task.Run(async () => await AddProductByBarcode(barcode));
        }
    }

    private async Task AddProductByBarcode(string barcode)
    {
        try
        {
            var product = await _productService.GetProductByBarcodeAsync(barcode);
            if (product != null)
            {
                await AddProduct(product);
            }
            else
            {
                SetError($"Product not found for barcode: {barcode}");
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to find product: {ex.Message}");
        }
    }

    public async Task Initialize()
    {
        InvoiceNumber = GenerateInvoiceNumber();
        await ClearSale();
    }
}

public partial class SaleItemViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid productId;

    [ObservableProperty]
    private string productName = string.Empty;

    [ObservableProperty]
    private decimal unitPrice;

    [ObservableProperty]
    private int quantity;

    [ObservableProperty]
    private string? batchNumber;

    public decimal Total => Quantity * UnitPrice;
}