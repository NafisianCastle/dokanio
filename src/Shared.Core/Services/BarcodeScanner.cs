namespace Shared.Core.Services;

/// <summary>
/// Default implementation of IBarcodeScanner with graceful error handling
/// Provides mock implementation for testing and fallback behavior
/// </summary>
public class BarcodeScanner : IBarcodeScanner
{
    private bool _isInitialized = false;
    private bool _isConnected = false;
    private bool _isContinuousScanning = false;
    private string? _currentScannerId;
    private readonly List<ScannerInfo> _availableScanners = new();
    private readonly ScannerConfiguration _configuration;

    public event EventHandler<BarcodeScannedEventArgs>? BarcodeScanned;
    public event EventHandler<ScannerStatusChangedEventArgs>? StatusChanged;

    public BarcodeScanner(ScannerConfiguration? configuration = null)
    {
        _configuration = configuration ?? new ScannerConfiguration();
        InitializeAvailableScanners();
    }

    public async Task<bool> InitializeAsync()
    {
        try
        {
            // Simulate initialization process
            await Task.Delay(100);
            
            _isInitialized = true;
            
            StatusChanged?.Invoke(this, new ScannerStatusChangedEventArgs
            {
                IsConnected = false,
                Status = "Initialized"
            });
            
            return true;
        }
        catch (Exception)
        {
            _isInitialized = false;
            StatusChanged?.Invoke(this, new ScannerStatusChangedEventArgs
            {
                IsConnected = false,
                Status = "Initialization failed",
                Error = ScannerError.UnknownError
            });
            return false;
        }
    }

    public async Task<string?> ScanAsync()
    {
        try
        {
            if (!_isInitialized || !_isConnected)
            {
                return null;
            }

            // Simulate scanning process with timeout
            var timeoutTask = Task.Delay(_configuration.ScanTimeout);
            var scanTask = SimulateScanOperation();
            
            var completedTask = await Task.WhenAny(scanTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                // Timeout occurred
                return null;
            }
            
            return await scanTask;
        }
        catch (Exception)
        {
            StatusChanged?.Invoke(this, new ScannerStatusChangedEventArgs
            {
                IsConnected = _isConnected,
                Status = "Scan failed",
                Error = ScannerError.UnknownError
            });
            return null;
        }
    }

    public async Task<bool> StartContinuousScanAsync()
    {
        try
        {
            if (!_isInitialized || !_isConnected)
            {
                return false;
            }

            _isContinuousScanning = true;
            
            // Start continuous scanning in background
            _ = Task.Run(async () =>
            {
                while (_isContinuousScanning)
                {
                    var barcode = await SimulateScanOperation();
                    if (!string.IsNullOrEmpty(barcode))
                    {
                        BarcodeScanned?.Invoke(this, new BarcodeScannedEventArgs
                        {
                            Barcode = barcode,
                            Format = "EAN13",
                            Quality = ScanQuality.Good
                        });
                    }
                    
                    await Task.Delay(1000); // Wait before next scan attempt
                }
            });
            
            return true;
        }
        catch (Exception)
        {
            _isContinuousScanning = false;
            return false;
        }
    }

    public async Task StopContinuousScanAsync()
    {
        _isContinuousScanning = false;
        await Task.Delay(10); // Allow background task to stop
    }

    public async Task<bool> IsAvailableAsync()
    {
        await Task.Delay(10);
        return _isInitialized && _isConnected;
    }

    public async Task<IEnumerable<ScannerInfo>> GetAvailableScannersAsync()
    {
        // Simulate discovering scanners
        await Task.Delay(50);
        return _availableScanners.AsReadOnly();
    }

    public async Task<bool> ConnectToScannerAsync(string scannerId)
    {
        try
        {
            if (!_isInitialized)
            {
                return false;
            }

            // Simulate connection process
            await Task.Delay(100);
            
            var scanner = _availableScanners.FirstOrDefault(s => s.Id == scannerId);
            if (scanner == null)
            {
                return false;
            }

            _currentScannerId = scannerId;
            _isConnected = true;
            scanner.IsConnected = true;
            scanner.Status = "Connected";

            StatusChanged?.Invoke(this, new ScannerStatusChangedEventArgs
            {
                IsConnected = true,
                Status = "Connected"
            });

            return true;
        }
        catch (Exception)
        {
            _isConnected = false;
            StatusChanged?.Invoke(this, new ScannerStatusChangedEventArgs
            {
                IsConnected = false,
                Status = "Connection failed",
                Error = ScannerError.CommunicationError
            });
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        await Task.Delay(10);
        
        if (_isContinuousScanning)
        {
            await StopContinuousScanAsync();
        }
        
        if (_currentScannerId != null)
        {
            var scanner = _availableScanners.FirstOrDefault(s => s.Id == _currentScannerId);
            if (scanner != null)
            {
                scanner.IsConnected = false;
                scanner.Status = "Disconnected";
            }
        }

        _isConnected = false;
        _currentScannerId = null;

        StatusChanged?.Invoke(this, new ScannerStatusChangedEventArgs
        {
            IsConnected = false,
            Status = "Disconnected"
        });
    }

    private void InitializeAvailableScanners()
    {
        // Add some mock scanners for testing
        _availableScanners.AddRange(new[]
        {
            new ScannerInfo
            {
                Id = "camera-scanner-001",
                Name = "Camera Scanner",
                Type = "Camera",
                IsConnected = false,
                Status = "Available"
            },
            new ScannerInfo
            {
                Id = "usb-scanner-001",
                Name = "USB Barcode Scanner",
                Type = "USB",
                IsConnected = false,
                Status = "Available"
            },
            new ScannerInfo
            {
                Id = "bluetooth-scanner-001",
                Name = "Bluetooth Scanner",
                Type = "Bluetooth",
                IsConnected = false,
                Status = "Available"
            }
        });
    }

    private async Task<string?> SimulateScanOperation()
    {
        // Simulate scanning delay
        await Task.Delay(500);
        
        // For testing purposes, return a mock barcode occasionally
        var random = new Random();
        if (random.Next(1, 5) == 1) // 25% chance of successful scan
        {
            // Generate a mock EAN13 barcode
            var mockBarcode = $"123456789{random.Next(1000, 9999)}";
            return mockBarcode;
        }
        
        return null; // No barcode scanned
    }
}