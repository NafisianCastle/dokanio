using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Shared.Core.DTOs;
using Shared.Core.Services;

namespace Mobile.ViewModels;

/// <summary>
/// Mobile ViewModel for configuration management
/// </summary>
public class ConfigurationViewModel : BaseViewModel
{
    private readonly IConfigurationService _configurationService;
    private readonly ICurrentUserService _currentUserService;

    // User preferences (primary focus for mobile)
    private UserPreferences _userPreferences = new();

    // Barcode scanner settings (important for mobile)
    private BarcodeScannerSettings _barcodeScannerSettings = new();

    // Performance settings (mobile-optimized)
    private PerformanceSettings _performanceSettings = new();

    // UI state
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private int _selectedTabIndex;

    public ConfigurationViewModel(
        IConfigurationService configurationService,
        ICurrentUserService currentUserService)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));

        // Initialize commands
        LoadConfigurationsCommand = new Command(async () => await LoadConfigurationsAsync());
        SaveUserPreferencesCommand = new Command(async () => await SaveUserPreferencesAsync());
        SaveBarcodeScannerCommand = new Command(async () => await SaveBarcodeScannerAsync());
        SavePerformanceCommand = new Command(async () => await SavePerformanceAsync());
        ResetToDefaultsCommand = new Command(async () => await ResetToDefaultsAsync());

        // Load configurations on initialization
        _ = Task.Run(LoadConfigurationsAsync);
    }

    #region Properties

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

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    // Available options for mobile UI
    public ObservableCollection<string> AvailableThemes { get; } = new()
    {
        "Light", "Dark", "Auto"
    };

    public ObservableCollection<string> AvailableLanguages { get; } = new()
    {
        "en", "es", "fr", "de", "it", "pt", "zh", "ja", "ko"
    };

    public ObservableCollection<int> AvailableFontSizes { get; } = new()
    {
        12, 14, 16, 18, 20, 22, 24
    };

    public ObservableCollection<string> AvailableScannerTypes { get; } = new()
    {
        "Camera", "Bluetooth"
    };

    public ObservableCollection<string> AvailableScanRegions { get; } = new()
    {
        "Center", "FullScreen"
    };

    #endregion

    #region Commands

    public ICommand LoadConfigurationsCommand { get; }
    public ICommand SaveUserPreferencesCommand { get; }
    public ICommand SaveBarcodeScannerCommand { get; }
    public ICommand SavePerformanceCommand { get; }
    public ICommand ResetToDefaultsCommand { get; }

    #endregion

    #region Methods

    private async Task LoadConfigurationsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading configurations...";

            var currentUser = _currentUserService.CurrentUser;

            if (currentUser != null)
            {
                // Load user preferences
                UserPreferences = await _configurationService.GetUserPreferencesAsync(currentUser.Id);
            }

            // Load device/system settings
            BarcodeScannerSettings = await _configurationService.GetBarcodeScannerSettingsAsync();
            PerformanceSettings = await _configurationService.GetPerformanceSettingsAsync();

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

    private async Task SaveUserPreferencesAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Saving user preferences...";

            var currentUser = _currentUserService.CurrentUser;
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

            // Reset key mobile configurations to defaults
            await _configurationService.ResetConfigurationAsync("BarcodeScanner.ScannerEnabled");
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

    #endregion
}