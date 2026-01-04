namespace Shared.Core.Services;

/// <summary>
/// Default implementation of ICashDrawerService with graceful error handling
/// Provides mock implementation for testing and fallback behavior
/// </summary>
public class CashDrawerService : ICashDrawerService
{
    private bool _isConnected = false;
    private bool _isOpen = false;
    private string? _currentDrawerId;
    private readonly List<CashDrawerInfo> _availableDrawers = new();
    private readonly CashDrawerConfiguration _configuration;

    public event EventHandler<CashDrawerStatusChangedEventArgs>? StatusChanged;

    public CashDrawerService(CashDrawerConfiguration? configuration = null)
    {
        _configuration = configuration ?? new CashDrawerConfiguration();
        InitializeAvailableDrawers();
    }

    public async Task<CashDrawerResult> OpenDrawerAsync()
    {
        try
        {
            if (!_isConnected)
            {
                return new CashDrawerResult
                {
                    Success = false,
                    Message = "Cash drawer not connected",
                    Error = CashDrawerError.NotConnected
                };
            }

            if (_isOpen)
            {
                return new CashDrawerResult
                {
                    Success = true,
                    Message = "Cash drawer is already open"
                };
            }

            // Simulate opening the drawer
            await Task.Delay(100);
            
            _isOpen = true;
            
            StatusChanged?.Invoke(this, new CashDrawerStatusChangedEventArgs
            {
                Status = CashDrawerStatus.Open,
                IsConnected = true
            });

            // Auto-close if configured
            if (_configuration.AutoClose)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(_configuration.AutoCloseDelay);
                    if (_isOpen)
                    {
                        _isOpen = false;
                        StatusChanged?.Invoke(this, new CashDrawerStatusChangedEventArgs
                        {
                            Status = CashDrawerStatus.Closed,
                            IsConnected = true
                        });
                    }
                });
            }

            return new CashDrawerResult
            {
                Success = true,
                Message = "Cash drawer opened successfully"
            };
        }
        catch (Exception ex)
        {
            return new CashDrawerResult
            {
                Success = false,
                Message = $"Failed to open cash drawer: {ex.Message}",
                Error = CashDrawerError.UnknownError
            };
        }
    }

    public async Task<bool> IsConnectedAsync()
    {
        await Task.Delay(10);
        return _isConnected;
    }

    public async Task<bool> IsOpenAsync()
    {
        await Task.Delay(10);
        return _isOpen;
    }

    public async Task<CashDrawerStatus> GetStatusAsync()
    {
        await Task.Delay(10);
        
        if (!_isConnected)
        {
            return CashDrawerStatus.NotConnected;
        }
        
        return _isOpen ? CashDrawerStatus.Open : CashDrawerStatus.Closed;
    }

    public async Task<IEnumerable<CashDrawerInfo>> GetAvailableDrawersAsync()
    {
        // Simulate discovering cash drawers
        await Task.Delay(50);
        return _availableDrawers.AsReadOnly();
    }

    public async Task<bool> ConnectToDrawerAsync(string drawerId)
    {
        try
        {
            // Simulate connection process
            await Task.Delay(100);
            
            var drawer = _availableDrawers.FirstOrDefault(d => d.Id == drawerId);
            if (drawer == null)
            {
                return false;
            }

            _currentDrawerId = drawerId;
            _isConnected = true;
            _isOpen = false; // Assume drawer is closed when first connected
            drawer.IsConnected = true;
            drawer.Status = "Connected";

            StatusChanged?.Invoke(this, new CashDrawerStatusChangedEventArgs
            {
                Status = CashDrawerStatus.Closed,
                IsConnected = true
            });

            return true;
        }
        catch (Exception)
        {
            _isConnected = false;
            StatusChanged?.Invoke(this, new CashDrawerStatusChangedEventArgs
            {
                Status = CashDrawerStatus.Error,
                IsConnected = false,
                Error = CashDrawerError.CommunicationError
            });
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        await Task.Delay(10);
        
        if (_currentDrawerId != null)
        {
            var drawer = _availableDrawers.FirstOrDefault(d => d.Id == _currentDrawerId);
            if (drawer != null)
            {
                drawer.IsConnected = false;
                drawer.Status = "Disconnected";
            }
        }

        _isConnected = false;
        _isOpen = false;
        _currentDrawerId = null;

        StatusChanged?.Invoke(this, new CashDrawerStatusChangedEventArgs
        {
            Status = CashDrawerStatus.NotConnected,
            IsConnected = false
        });
    }

    private void InitializeAvailableDrawers()
    {
        // Add some mock cash drawers for testing
        _availableDrawers.AddRange(new[]
        {
            new CashDrawerInfo
            {
                Id = "usb-drawer-001",
                Name = "USB Cash Drawer",
                Type = "USB",
                IsConnected = false,
                Status = "Available"
            },
            new CashDrawerInfo
            {
                Id = "serial-drawer-001",
                Name = "Serial Cash Drawer",
                Type = "Serial",
                IsConnected = false,
                Status = "Available"
            }
        });
    }
}