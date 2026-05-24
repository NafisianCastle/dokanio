using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using System.Collections.ObjectModel;
using Shared.Core.DTOs;
using Shared.Core.Services;

namespace Desktop.ViewModels;

public class ConfigurationViewModel : BaseViewModel
{
    private readonly IConfigurationService _configurationService;
    private readonly ICurrentUserService _currentUserService;

    [Reactive] public ShopPricingSettings ShopPricingSettings { get; set; } = new();
    [Reactive] public ShopTaxSettings ShopTaxSettings { get; set; } = new();
    [Reactive] public UserPreferences UserPreferences { get; set; } = new();
    [Reactive] public BarcodeScannerSettings BarcodeScannerSettings { get; set; } = new();
    [Reactive] public PerformanceSettings PerformanceSettings { get; set; } = new();
    [Reactive] public BusinessSettings BusinessSettings { get; set; } = new();
    [Reactive] public CurrencySettings CurrencySettings { get; set; } = new();
    [Reactive] public LocalizationSettings LocalizationSettings { get; set; } = new();
    [Reactive] public string StatusMessage { get; set; } = string.Empty;
    [Reactive] public string SelectedTab { get; set; } = "Shop";

    public ObservableCollection<string> AvailableThemes        { get; } = new() { "Light", "Dark", "Auto" };
    public ObservableCollection<string> AvailableLanguages     { get; } = new() { "en", "bn", "es", "fr", "de", "zh" };
    public ObservableCollection<string> AvailableFontFamilies  { get; } = new() { "Segoe UI", "Arial", "Calibri", "Tahoma" };
    public ObservableCollection<string> AvailableScannerTypes  { get; } = new() { "Camera", "USB", "Bluetooth" };
    public ObservableCollection<string> AvailableScanRegions   { get; } = new() { "Center", "FullScreen", "Custom" };

    public ReactiveCommand<Unit, Unit> LoadConfigurationsCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveShopPricingCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveShopTaxCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveUserPreferencesCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveBarcodeScannerCommand { get; }
    public ReactiveCommand<Unit, Unit> SavePerformanceCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetToDefaultsCommand { get; }
    public ReactiveCommand<Unit, Unit> InitializeDefaultsCommand { get; }

    public ConfigurationViewModel(
        IConfigurationService configurationService,
        ICurrentUserService currentUserService)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _currentUserService   = currentUserService   ?? throw new ArgumentNullException(nameof(currentUserService));
        Title = "Configuration";

        LoadConfigurationsCommand  = ReactiveCommand.CreateFromTask(LoadConfigurationsAsync);
        SaveShopPricingCommand     = ReactiveCommand.CreateFromTask(SaveShopPricingAsync);
        SaveShopTaxCommand         = ReactiveCommand.CreateFromTask(SaveShopTaxAsync);
        SaveUserPreferencesCommand = ReactiveCommand.CreateFromTask(SaveUserPreferencesAsync);
        SaveBarcodeScannerCommand  = ReactiveCommand.CreateFromTask(SaveBarcodeScannerAsync);
        SavePerformanceCommand     = ReactiveCommand.CreateFromTask(SavePerformanceAsync);
        ResetToDefaultsCommand     = ReactiveCommand.CreateFromTask(ResetToDefaultsAsync);
        InitializeDefaultsCommand  = ReactiveCommand.CreateFromTask(InitializeDefaultsAsync);

        _ = Task.Run(LoadConfigurationsAsync);
    }

    private async Task LoadConfigurationsAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading configurations...";
        try
        {
            var user = _currentUserService.CurrentUser;
            if (user?.ShopId != null)
            {
                ShopPricingSettings = await _configurationService.GetShopPricingSettingsAsync(user.ShopId.Value);
                ShopTaxSettings     = await _configurationService.GetShopTaxSettingsAsync(user.ShopId.Value);
            }
            if (user != null)
                UserPreferences = await _configurationService.GetUserPreferencesAsync(user.Id);

            BarcodeScannerSettings = await _configurationService.GetBarcodeScannerSettingsAsync();
            PerformanceSettings    = await _configurationService.GetPerformanceSettingsAsync();
            BusinessSettings       = await _configurationService.GetBusinessSettingsAsync();
            CurrencySettings       = await _configurationService.GetCurrencySettingsAsync();
            LocalizationSettings   = await _configurationService.GetLocalizationSettingsAsync();
            StatusMessage = "Configurations loaded successfully";
        }
        catch (Exception ex) { StatusMessage = $"Error loading configurations: {ex.Message}"; SetError(ex.Message); }
        finally { IsBusy = false; }
    }

    private async Task SaveShopPricingAsync()
    {
        IsBusy = true; StatusMessage = "Saving shop pricing settings...";
        try
        {
            var shopId = _currentUserService.CurrentUser?.ShopId;
            if (shopId == null) { StatusMessage = "No current shop selected"; return; }
            await _configurationService.SetShopPricingSettingsAsync(shopId.Value, ShopPricingSettings);
            StatusMessage = "Shop pricing settings saved";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; SetError(ex.Message); }
        finally { IsBusy = false; }
    }

    private async Task SaveShopTaxAsync()
    {
        IsBusy = true; StatusMessage = "Saving shop tax settings...";
        try
        {
            var shopId = _currentUserService.CurrentUser?.ShopId;
            if (shopId == null) { StatusMessage = "No current shop selected"; return; }
            await _configurationService.SetShopTaxSettingsAsync(shopId.Value, ShopTaxSettings);
            StatusMessage = "Shop tax settings saved";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; SetError(ex.Message); }
        finally { IsBusy = false; }
    }

    private async Task SaveUserPreferencesAsync()
    {
        IsBusy = true; StatusMessage = "Saving user preferences...";
        try
        {
            var user = _currentUserService.CurrentUser;
            if (user == null) { StatusMessage = "No current user"; return; }
            UserPreferences.UserId = user.Id;
            await _configurationService.SetUserPreferencesAsync(user.Id, UserPreferences);
            StatusMessage = "User preferences saved";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; SetError(ex.Message); }
        finally { IsBusy = false; }
    }

    private async Task SaveBarcodeScannerAsync()
    {
        IsBusy = true; StatusMessage = "Saving barcode scanner settings...";
        try
        {
            await _configurationService.SetBarcodeScannerSettingsAsync(BarcodeScannerSettings);
            StatusMessage = "Barcode scanner settings saved";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; SetError(ex.Message); }
        finally { IsBusy = false; }
    }

    private async Task SavePerformanceAsync()
    {
        IsBusy = true; StatusMessage = "Saving performance settings...";
        try
        {
            await _configurationService.SetPerformanceSettingsAsync(PerformanceSettings);
            StatusMessage = "Performance settings saved";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; SetError(ex.Message); }
        finally { IsBusy = false; }
    }

    private async Task ResetToDefaultsAsync()
    {
        IsBusy = true; StatusMessage = "Resetting to defaults...";
        try
        {
            await _configurationService.ResetConfigurationAsync("Currency.Code");
            await _configurationService.ResetConfigurationAsync("Tax.DefaultRate");
            await _configurationService.ResetConfigurationAsync("Performance.PageSize");
            await LoadConfigurationsAsync();
            StatusMessage = "Configurations reset to defaults";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; SetError(ex.Message); }
        finally { IsBusy = false; }
    }

    private async Task InitializeDefaultsAsync()
    {
        IsBusy = true; StatusMessage = "Initializing default configurations...";
        try
        {
            await _configurationService.InitializeDefaultConfigurationsAsync();
            await LoadConfigurationsAsync();
            StatusMessage = "Default configurations initialized";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; SetError(ex.Message); }
        finally { IsBusy = false; }
    }
}
