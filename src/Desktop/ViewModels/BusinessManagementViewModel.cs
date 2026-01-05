using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace Desktop.ViewModels;

/// <summary>
/// View model for business and shop management
/// </summary>
public partial class BusinessManagementViewModel : BaseViewModel
{
    private readonly IBusinessManagementService _businessManagementService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    [ObservableProperty]
    private ObservableCollection<BusinessResponse> businesses = new();

    [ObservableProperty]
    private ObservableCollection<ShopResponse> shops = new();

    [ObservableProperty]
    private BusinessResponse? selectedBusiness;

    [ObservableProperty]
    private ShopResponse? selectedShop;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isCreateBusinessDialogOpen;

    [ObservableProperty]
    private bool isCreateShopDialogOpen;

    [ObservableProperty]
    private bool isEditBusinessDialogOpen;

    [ObservableProperty]
    private bool isEditShopDialogOpen;

    // Create business properties
    [ObservableProperty]
    [Required(ErrorMessage = "Business name is required")]
    private string newBusinessName = string.Empty;

    [ObservableProperty]
    private string newBusinessDescription = string.Empty;

    [ObservableProperty]
    private string newBusinessAddress = string.Empty;

    [ObservableProperty]
    private string newBusinessPhone = string.Empty;

    [ObservableProperty]
    private string newBusinessEmail = string.Empty;

    [ObservableProperty]
    private string newBusinessTaxId = string.Empty;

    [ObservableProperty]
    private BusinessType newBusinessType = BusinessType.GeneralRetail;

    // Create shop properties
    [ObservableProperty]
    [Required(ErrorMessage = "Shop name is required")]
    private string newShopName = string.Empty;

    [ObservableProperty]
    private string newShopAddress = string.Empty;

    [ObservableProperty]
    private string newShopPhone = string.Empty;

    [ObservableProperty]
    private string newShopEmail = string.Empty;

    [ObservableProperty]
    private string? successMessage;

    public BusinessManagementViewModel(
        IBusinessManagementService businessManagementService,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _businessManagementService = businessManagementService;
        _currentUserService = currentUserService;
        _auditService = auditService;
        Title = "Business Management";
    }

    public bool CanManageBusinesses => _currentUserService.CurrentUser?.Role == UserRole.BusinessOwner;

    public Array BusinessTypes => Enum.GetValues<BusinessType>();

    [RelayCommand]
    private async Task LoadBusinessesAsync()
    {
        if (!CanManageBusinesses)
        {
            SetError("You don't have permission to manage businesses");
            return;
        }

        IsLoading = true;
        ClearError();

        try
        {
            var currentUser = _currentUserService.CurrentUser;
            if (currentUser == null)
            {
                SetError("User not authenticated");
                return;
            }

            var businessList = await _businessManagementService.GetBusinessesByOwnerAsync(currentUser.Id);
            Businesses.Clear();
            foreach (var business in businessList)
            {
                Businesses.Add(business);
            }

            // Load shops for the first business if available
            if (Businesses.Any())
            {
                SelectedBusiness = Businesses.First();
                await LoadShopsForBusinessAsync(SelectedBusiness.Id);
            }
        }
        catch (Exception ex)
        {
            SetError($"Error loading businesses: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadShopsForBusinessAsync(Guid businessId)
    {
        IsLoading = true;
        ClearError();

        try
        {
            var shopList = await _businessManagementService.GetShopsByBusinessAsync(businessId);
            Shops.Clear();
            foreach (var shop in shopList)
            {
                Shops.Add(shop);
            }
        }
        catch (Exception ex)
        {
            SetError($"Error loading shops: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenCreateBusinessDialog()
    {
        if (!CanManageBusinesses)
        {
            SetError("You don't have permission to create businesses");
            return;
        }

        ClearCreateBusinessForm();
        IsCreateBusinessDialogOpen = true;
    }

    [RelayCommand]
    private void CloseCreateBusinessDialog()
    {
        IsCreateBusinessDialogOpen = false;
        ClearCreateBusinessForm();
    }

    [RelayCommand]
    private async Task CreateBusinessAsync()
    {
        if (!CanManageBusinesses)
        {
            SetError("You don't have permission to create businesses");
            return;
        }

        ClearError();
        SuccessMessage = null;

        // Validate input
        if (string.IsNullOrWhiteSpace(NewBusinessName))
        {
            SetError("Business name is required");
            return;
        }

        IsLoading = true;

        try
        {
            var currentUser = _currentUserService.CurrentUser;
            if (currentUser == null)
            {
                SetError("User not authenticated");
                return;
            }

            var request = new CreateBusinessRequest
            {
                Name = NewBusinessName,
                Type = NewBusinessType,
                OwnerId = currentUser.Id,
                Description = NewBusinessDescription,
                Address = NewBusinessAddress,
                Phone = NewBusinessPhone,
                Email = NewBusinessEmail,
                TaxId = NewBusinessTaxId,
                Configuration = System.Text.Json.JsonSerializer.Serialize(
                    await _businessManagementService.GetDefaultBusinessConfigurationAsync(NewBusinessType))
            };

            var business = await _businessManagementService.CreateBusinessAsync(request);
            Businesses.Add(business);
            SuccessMessage = $"Business '{NewBusinessName}' created successfully";

            await _auditService.LogAsync(
                currentUser.Id,
                AuditAction.SystemConfiguration,
                $"Created business: {NewBusinessName} of type: {NewBusinessType}",
                nameof(Business),
                business.Id);

            CloseCreateBusinessDialog();
        }
        catch (Exception ex)
        {
            SetError($"Error creating business: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenCreateShopDialog()
    {
        if (SelectedBusiness == null)
        {
            SetError("Please select a business first");
            return;
        }

        if (!CanManageBusinesses)
        {
            SetError("You don't have permission to create shops");
            return;
        }

        ClearCreateShopForm();
        IsCreateShopDialogOpen = true;
    }

    [RelayCommand]
    private void CloseCreateShopDialog()
    {
        IsCreateShopDialogOpen = false;
        ClearCreateShopForm();
    }

    [RelayCommand]
    private async Task CreateShopAsync()
    {
        if (SelectedBusiness == null)
        {
            SetError("Please select a business first");
            return;
        }

        if (!CanManageBusinesses)
        {
            SetError("You don't have permission to create shops");
            return;
        }

        ClearError();
        SuccessMessage = null;

        // Validate input
        if (string.IsNullOrWhiteSpace(NewShopName))
        {
            SetError("Shop name is required");
            return;
        }

        IsLoading = true;

        try
        {
            var request = new CreateShopRequest
            {
                BusinessId = SelectedBusiness.Id,
                Name = NewShopName,
                Address = NewShopAddress,
                Phone = NewShopPhone,
                Email = NewShopEmail,
                Configuration = System.Text.Json.JsonSerializer.Serialize(
                    await _businessManagementService.GetDefaultShopConfigurationAsync(SelectedBusiness.Id))
            };

            var shop = await _businessManagementService.CreateShopAsync(request);
            Shops.Add(shop);
            SuccessMessage = $"Shop '{NewShopName}' created successfully";

            await _auditService.LogAsync(
                _currentUserService.CurrentUser?.Id,
                AuditAction.SystemConfiguration,
                $"Created shop: {NewShopName} for business: {SelectedBusiness.Name}",
                nameof(Shop),
                shop.Id);

            CloseCreateShopDialog();
        }
        catch (Exception ex)
        {
            SetError($"Error creating shop: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteBusinessAsync(BusinessResponse? business)
    {
        if (business == null || !CanManageBusinesses)
            return;

        IsLoading = true;
        ClearError();

        try
        {
            var currentUser = _currentUserService.CurrentUser;
            if (currentUser == null)
            {
                SetError("User not authenticated");
                return;
            }

            var success = await _businessManagementService.DeleteBusinessAsync(business.Id, currentUser.Id);
            if (success)
            {
                Businesses.Remove(business);
                if (SelectedBusiness?.Id == business.Id)
                {
                    SelectedBusiness = null;
                    Shops.Clear();
                }
                SuccessMessage = $"Business '{business.Name}' deleted successfully";

                await _auditService.LogAsync(
                    currentUser.Id,
                    AuditAction.SystemConfiguration,
                    $"Deleted business: {business.Name}",
                    nameof(Business),
                    business.Id);
            }
            else
            {
                SetError("Failed to delete business");
            }
        }
        catch (Exception ex)
        {
            SetError($"Error deleting business: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteShopAsync(ShopResponse? shop)
    {
        if (shop == null || !CanManageBusinesses)
            return;

        IsLoading = true;
        ClearError();

        try
        {
            var currentUser = _currentUserService.CurrentUser;
            if (currentUser == null)
            {
                SetError("User not authenticated");
                return;
            }

            var success = await _businessManagementService.DeleteShopAsync(shop.Id, currentUser.Id);
            if (success)
            {
                Shops.Remove(shop);
                SuccessMessage = $"Shop '{shop.Name}' deleted successfully";

                await _auditService.LogAsync(
                    currentUser.Id,
                    AuditAction.SystemConfiguration,
                    $"Deleted shop: {shop.Name}",
                    nameof(Shop),
                    shop.Id);
            }
            else
            {
                SetError("Failed to delete shop");
            }
        }
        catch (Exception ex)
        {
            SetError($"Error deleting shop: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearMessages()
    {
        ClearError();
        SuccessMessage = null;
    }

    partial void OnSelectedBusinessChanged(BusinessResponse? value)
    {
        if (value != null)
        {
            _ = LoadShopsForBusinessAsync(value.Id);
        }
        else
        {
            Shops.Clear();
        }
    }

    private void ClearCreateBusinessForm()
    {
        NewBusinessName = string.Empty;
        NewBusinessDescription = string.Empty;
        NewBusinessAddress = string.Empty;
        NewBusinessPhone = string.Empty;
        NewBusinessEmail = string.Empty;
        NewBusinessTaxId = string.Empty;
        NewBusinessType = BusinessType.GeneralRetail;
        ClearError();
        SuccessMessage = null;
    }

    private void ClearCreateShopForm()
    {
        NewShopName = string.Empty;
        NewShopAddress = string.Empty;
        NewShopPhone = string.Empty;
        NewShopEmail = string.Empty;
        ClearError();
        SuccessMessage = null;
    }
}