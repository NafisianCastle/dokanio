using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Shared.Core.DTOs;
using Shared.Core.Services;

namespace Desktop.ViewModels;

/// <summary>
/// ViewModel for configuration management
/// </summary>
public class ConfigurationViewModel : BaseViewModel
{
    private readonly IConfigurationService _configurationService;
    private readonly ICurrentUserService _currentUserService;

    // Shop-level settings
    private ShopPricingSettings _shopPricingSettings = new();
    private ShopTaxSettings _shopTaxSettings = new();

    // User preferences
    private UserPreferences _userPreferences = new();

    // Barcode scanner settings
    private BarcodeScannerSettings _barcodeScannerSettings = new();

    // Performance settings
    private PerformanceSettings _performanceSettings = new();

    // Business settings
    private BusinessSettings _businessSettings = new();
    private CurrencySettings _currencySettings = new();
    private LocalizationSettings _localizationSettings = new();

    // UI state
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private string _selectedTab = "Shop";

    public ConfigurationViewModel(
        IConfigurationService configurationService,
        ICurrentUserService currentUserService)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));

        // Initialize commands
        LoadConfigurationsCommand = new RelayCommand(async () => await LoadConfigurationsAsync());
        SaveShopPricingCommand = new RelayCommand(async () => await SaveShopPricingAsync());
        SaveShopTaxCommand = new RelayCommand(async () => await SaveShopTaxAsync());
        SaveUserPreferencesCommand = new RelayCommand(async () => await SaveUserPreferencesAsync());
        SaveBarcodeScannerCommand = new RelayCommand(async () => await SaveBarcodeScannerAsync());
        SavePerformanceCommand = new RelayCommand(async () => await SavePerformanceAsync());
        ResetToDefaultsCommand = new RelayCommand(async () => await ResetToDefaultsAsync());
        InitializeDefaultsCommand = new RelayCommand(async () => await InitializeDefaultsAsync());

        // Load configurations on initialization
        _ = Task.Run(LoadConfigurationsAsync);
    }

    #region Properties

    public ShopPricingSettings ShopPricingSettings
    {
        get => _shopPricingSettings;
        set => SetProperty(ref _shopPricingSettings, value);
    }

    public ShopTaxSettings ShopTaxSettings
    {
        get => _shopTaxSettings;
        set => SetProperty(ref _shopTaxSettings, value);
    }

    public UserPreferences UserPreferences
    {
        get => _userPreferences;
        set => SetProperty(ref _userPreferences, value);
    }

    public BarcodeScannerSettings BarcodeScannerSettings
    {
        get => _barcodeScannerSettings;
        set => SetProperty(ref _barcodeScannerSettings, value);
    }

    public PerformanceSettings PerformanceSettings
    {
        get => _performanceSettings;
        set => SetProperty(ref _performanceSettings, value);
    }

    public BusinessSettings BusinessSettings
    {
        get => _businessSettings;
        set => SetProperty(ref _businessSettings, value);
    }

    public CurrencySettings CurrencySettings
    {
        get => _currencySettings;
        set => SetProperty(ref _currencySettings, value);
    }

    public LocalizationSettings LocalizationSettings
    {
        get => _localizationSettings;
        set => SetProperty(ref _localizationSettings, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string SelectedTab
    {
        get => _selectedTab;
        set => SetProperty(ref _selectedTab, value);
    }

    // Available options for dropdowns
    public ObservableCollection<string> AvailableThemes { get; } = new()
    {
        "Light", "Dark", "Auto"
    };

    public ObservableCollection<string> AvailableLanguages { get; } = new()
    {
        "en", "es", "fr", "de", "it", "pt", "zh", "ja", "ko"
    };

    public ObservableCollection<string> AvailableFontFamilies { get; } = new()
    {
        "Segoe UI", "Arial", "Calibri", "Tahoma", "Verdana"
    };

    public ObservableCollection<string> AvailableScannerTypes { get; } = new()
    {
        "Camera", "USB", "Bluetooth"
    };

    public ObservableCollection<string> AvailableScanRegions { get; } = new()
    {
        "Center", "FullScreen", "Custom"
    };

    #endregion

    #region Commands

    public ICommand LoadConfigurationsCommand { get; }
    public ICommand SaveShopPricingCommand { get; }
    public ICommand SaveShopTaxCommand { get; }
    public ICommand SaveUserPreferencesCommand { get; }
    public ICommand SaveBarcodeScannerCommand { get; }
    public ICommand SavePerformanceCommand { get; }
    public ICommand ResetToDefaultsCommand { get; }
    public ICommand InitializeDefaultsCommand { get; }

    #endregion

    #region Methods

    private async Task LoadConfigurationsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading configurations...";

            var currentUser = await _currentUserService.GetCurrentUserAsync();
            var currentShop = await _currentUserService.GetCurrentShopAsync();

            if (currentShop != null)
            {
                // Load shop-level settings
                ShopPricingSettings = await _configurationService.GetShopPricingSettingsAsync(currentShop.Id);
                ShopTaxSettings = await _configurationService.GetShopTaxSettingsAsync(currentShop.Id);
            }

            if (currentUser != null)
            {
                // Load user preferences
                UserPreferences = await _configurationService.GetUserPreferencesAsync(currentUser.Id);
            }

            // Load device/system settings
            BarcodeScannerSettings = await _configurationService.GetBarcodeScannerSettingsAsync();
            PerformanceSettings = await _configurationService.GetPerformanceSettingsAsync();
            BusinessSettings = await _configurationService.GetBusinessSettingsAsync();
            CurrencySettings = await _configurationService.GetCurrencySettingsAsync();
            LocalizationSettings = await _configurationService.GetLocalizationSettingsAsync();

            StatusMessage = "Configurations loaded successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading configurations: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveShopPricingAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Saving shop pricing settings...";

            var currentShop = await _currentUserService.GetCurrentShopAsync();
            if (currentShop == null)
            {
                StatusMessage = "No current shop selected";
                return;
            }

            await _configurationService.SetShopPricingSettingsAsync(currentShop.Id, ShopPricingSettings);
            StatusMessage = "Shop pricing settings saved successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving shop pricing settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveShopTaxAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Saving shop tax settings...";

            var currentShop = await _currentUserService.GetCurrentShopAsync();
            if (currentShop == null)
            {
                StatusMessage = "No current shop selected";
                return;
            }

            await _configurationService.SetShopTaxSettingsAsync(currentShop.Id, ShopTaxSettings);
            StatusMessage = "Shop tax settings saved successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving shop tax settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveUserPreferencesAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Saving user preferences...";

            var currentUser = await _currentUserService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                StatusMessage = "No current user";
                return;
            }

            UserPreferences.UserId = currentUser.Id;
            await _configurationService.SetUserPreferencesAsync(currentUser.Id, UserPreferences);
            StatusMessage = "User preferences saved successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving user preferences: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveBarcodeScannerAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Saving barcode scanner settings...";

            await _configurationService.SetBarcodeScannerSettingsAsync(BarcodeScannerSettings);
            StatusMessage = "Barcode scanner settings saved successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving barcode scanner settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SavePerformanceAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Saving performance settings...";

            await _configurationService.SetPerformanceSettingsAsync(PerformanceSettings);
            StatusMessage = "Performance settings saved successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving performance settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ResetToDefaultsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Resetting to default configurations...";

            // Reset key configurations to defaults
            await _configurationService.ResetConfigurationAsync("Currency.Code");
            await _configurationService.ResetConfigurationAsync("Tax.DefaultRate");
            await _configurationService.ResetConfigurationAsync("Performance.PageSize");

            // Reload configurations
            await LoadConfigurationsAsync();
            StatusMessage = "Configurations reset to defaults successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error resetting configurations: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task InitializeDefaultsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Initializing default configurations...";

            await _configurationService.InitializeDefaultConfigurationsAsync();
            await LoadConfigurationsAsync();
            StatusMessage = "Default configurations initialized successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error initializing default configurations: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion
}

/// <summary>
/// Simple relay command implementation
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter)
    {
        await _execute();
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}