using Shared.Core.Entities;

namespace Shared.Core.Services;

/// <summary>
/// Default implementation of IPrinterService with graceful error handling
/// Provides mock implementation for testing and fallback behavior
/// </summary>
public class PrinterService : IPrinterService
{
    private readonly IReceiptService _receiptService;
    private bool _isConnected = false;
    private string? _currentPrinterId;
    private readonly List<PrinterInfo> _availablePrinters = new();

    public event EventHandler<PrinterStatusChangedEventArgs>? StatusChanged;

    public PrinterService(IReceiptService receiptService)
    {
        _receiptService = receiptService;
        InitializeAvailablePrinters();
    }

    public async Task<bool> IsConnectedAsync()
    {
        // Simulate checking printer connection
        await Task.Delay(10);
        return _isConnected;
    }

    public async Task<PrintResult> PrintReceiptAsync(Sale sale)
    {
        try
        {
            // Validate receipt data first
            var validationResult = await _receiptService.ValidateReceiptDataAsync(sale);
            if (!validationResult.IsValid)
            {
                return new PrintResult
                {
                    Success = false,
                    Message = $"Invalid receipt data: {string.Join(", ", validationResult.Errors)}",
                    Error = PrintError.InvalidData
                };
            }

            // Check if printer is connected
            if (!_isConnected)
            {
                return new PrintResult
                {
                    Success = false,
                    Message = "Printer not connected",
                    Error = PrintError.NotConnected
                };
            }

            // Generate receipt content
            var receiptContent = await _receiptService.GenerateReceiptAsync(sale);
            
            // Simulate printing process
            await Task.Delay(100); // Simulate print time
            
            // In a real implementation, this would send the receipt to the actual printer
            // For now, we'll simulate successful printing
            
            return new PrintResult
            {
                Success = true,
                Message = "Receipt printed successfully"
            };
        }
        catch (Exception ex)
        {
            return new PrintResult
            {
                Success = false,
                Message = $"Print failed: {ex.Message}",
                Error = PrintError.UnknownError
            };
        }
    }

    public async Task<PrintResult> PrintTestPageAsync()
    {
        try
        {
            if (!_isConnected)
            {
                return new PrintResult
                {
                    Success = false,
                    Message = "Printer not connected",
                    Error = PrintError.NotConnected
                };
            }

            // Generate test receipt
            var testReceipt = await _receiptService.GenerateTestReceiptAsync();
            
            // Simulate printing test page
            await Task.Delay(50);
            
            return new PrintResult
            {
                Success = true,
                Message = "Test page printed successfully"
            };
        }
        catch (Exception ex)
        {
            return new PrintResult
            {
                Success = false,
                Message = $"Test print failed: {ex.Message}",
                Error = PrintError.UnknownError
            };
        }
    }

    public async Task<IEnumerable<PrinterInfo>> GetAvailablePrintersAsync()
    {
        // Simulate discovering printers
        await Task.Delay(50);
        return _availablePrinters.AsReadOnly();
    }

    public async Task<bool> ConnectToPrinterAsync(string printerId)
    {
        try
        {
            // Simulate connection process
            await Task.Delay(100);
            
            var printer = _availablePrinters.FirstOrDefault(p => p.Id == printerId);
            if (printer == null)
            {
                return false;
            }

            _currentPrinterId = printerId;
            _isConnected = true;
            printer.IsConnected = true;
            printer.Status = "Connected";

            // Raise status changed event
            StatusChanged?.Invoke(this, new PrinterStatusChangedEventArgs
            {
                IsConnected = true,
                Status = "Connected"
            });

            return true;
        }
        catch (Exception)
        {
            _isConnected = false;
            StatusChanged?.Invoke(this, new PrinterStatusChangedEventArgs
            {
                IsConnected = false,
                Status = "Connection failed",
                Error = PrintError.CommunicationError
            });
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        await Task.Delay(10);
        
        if (_currentPrinterId != null)
        {
            var printer = _availablePrinters.FirstOrDefault(p => p.Id == _currentPrinterId);
            if (printer != null)
            {
                printer.IsConnected = false;
                printer.Status = "Disconnected";
            }
        }

        _isConnected = false;
        _currentPrinterId = null;

        StatusChanged?.Invoke(this, new PrinterStatusChangedEventArgs
        {
            IsConnected = false,
            Status = "Disconnected"
        });
    }

    private void InitializeAvailablePrinters()
    {
        // Add some mock printers for testing
        _availablePrinters.AddRange(new[]
        {
            new PrinterInfo
            {
                Id = "bluetooth-thermal-001",
                Name = "Bluetooth Thermal Printer",
                Type = "Bluetooth",
                IsConnected = false,
                Status = "Available"
            },
            new PrinterInfo
            {
                Id = "usb-thermal-001",
                Name = "USB Thermal Printer",
                Type = "USB",
                IsConnected = false,
                Status = "Available"
            }
        });
    }
}