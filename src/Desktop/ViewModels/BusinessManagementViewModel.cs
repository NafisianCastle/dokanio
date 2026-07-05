using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using System.Collections.ObjectModel;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;

namespace Desktop.ViewModels;

public class BusinessManagementViewModel : BaseViewModel
{
    private readonly IBusinessManagementService _businessManagementService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    [Reactive] public ObservableCollection<BusinessResponse> Businesses { get; set; } = new();
    [Reactive] public ObservableCollection<ShopResponse> Shops { get; set; } = new();
    [Reactive] public BusinessResponse? SelectedBusiness { get; set; }
    [Reactive] public ShopResponse? SelectedShop { get; set; }
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public bool IsCreateBusinessDialogOpen { get; set; }
    [Reactive] public bool IsCreateShopDialogOpen { get; set; }
    [Reactive] public bool IsEditBusinessDialogOpen { get; set; }
    [Reactive] public bool IsEditShopDialogOpen { get; set; }

    // Create-business form
    [Reactive] public string NewBusinessName { get; set; } = string.Empty;
    [Reactive] public string NewBusinessDescription { get; set; } = string.Empty;
    [Reactive] public string NewBusinessAddress { get; set; } = string.Empty;
    [Reactive] public string NewBusinessPhone { get; set; } = string.Empty;
    [Reactive] public string NewBusinessEmail { get; set; } = string.Empty;
    [Reactive] public string NewBusinessTaxId { get; set; } = string.Empty;
    [Reactive] public BusinessType NewBusinessType { get; set; } = BusinessType.GeneralRetail;

    // Create-shop form
    [Reactive] public string NewShopName { get; set; } = string.Empty;
    [Reactive] public string NewShopAddress { get; set; } = string.Empty;
    [Reactive] public string NewShopPhone { get; set; } = string.Empty;
    [Reactive] public string NewShopEmail { get; set; } = string.Empty;
    [Reactive] public string? SuccessMessage { get; set; }

    public bool CanManageBusinesses => _currentUserService.CurrentUser?.Role is
        UserRole.BusinessOwner or UserRole.Administrator or UserRole.SuperAdmin;

    public Array BusinessTypes => Enum.GetValues<BusinessType>();

    public ReactiveCommand<Unit, Unit> LoadBusinessesCommand { get; }
    public ReactiveCommand<Guid, Unit> LoadShopsForBusinessCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenCreateBusinessDialogCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCreateBusinessDialogCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateBusinessCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenCreateShopDialogCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCreateShopDialogCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateShopCommand { get; }
    public ReactiveCommand<BusinessResponse?, Unit> DeleteBusinessCommand { get; }
    public ReactiveCommand<ShopResponse?, Unit> DeleteShopCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearMessagesCommand { get; }

    public BusinessManagementViewModel(
        IBusinessManagementService businessManagementService,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _businessManagementService = businessManagementService;
        _currentUserService        = currentUserService;
        _auditService              = auditService;
        Title = "Business Management";

        LoadBusinessesCommand            = ReactiveCommand.CreateFromTask(LoadBusinessesAsync);
        LoadShopsForBusinessCommand      = ReactiveCommand.CreateFromTask<Guid>(LoadShopsForBusinessAsync);
        OpenCreateBusinessDialogCommand  = ReactiveCommand.Create(OpenCreateBusinessDialog);
        CloseCreateBusinessDialogCommand = ReactiveCommand.Create(CloseCreateBusinessDialog);
        CreateBusinessCommand            = ReactiveCommand.CreateFromTask(CreateBusinessAsync);
        OpenCreateShopDialogCommand      = ReactiveCommand.Create(OpenCreateShopDialog);
        CloseCreateShopDialogCommand     = ReactiveCommand.Create(CloseCreateShopDialog);
        CreateShopCommand                = ReactiveCommand.CreateFromTask(CreateShopAsync);
        DeleteBusinessCommand            = ReactiveCommand.CreateFromTask<BusinessResponse?>(DeleteBusinessAsync);
        DeleteShopCommand                = ReactiveCommand.CreateFromTask<ShopResponse?>(DeleteShopAsync);
        ClearMessagesCommand             = ReactiveCommand.Create(() => { ClearError(); SuccessMessage = null; });

        // React to business selection
        this.WhenAnyValue(x => x.SelectedBusiness).Subscribe(b =>
        {
            if (b != null) _ = LoadShopsForBusinessAsync(b.Id);
            else Shops.Clear();
        });
    }

    private async Task LoadBusinessesAsync()
    {
        if (!CanManageBusinesses) { SetError("You don't have permission to manage businesses"); return; }

        IsLoading = true;
        ClearError();
        try
        {
            var user = _currentUserService.CurrentUser;
            if (user == null) { SetError("User not authenticated"); return; }

            var list = await _businessManagementService.GetBusinessesByOwnerAsync(user.Id);
            Businesses.Clear();
            foreach (var b in list) Businesses.Add(b);

            if (Businesses.Any())
            {
                SelectedBusiness = Businesses.First();
                await LoadShopsForBusinessAsync(SelectedBusiness.Id);
            }
        }
        catch (Exception ex) { SetError($"Error loading businesses: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private async Task LoadShopsForBusinessAsync(Guid businessId)
    {
        IsLoading = true;
        ClearError();
        try
        {
            var list = await _businessManagementService.GetShopsByBusinessAsync(businessId);
            Shops.Clear();
            foreach (var s in list) Shops.Add(s);
        }
        catch (Exception ex) { SetError($"Error loading shops: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private void OpenCreateBusinessDialog()
    {
        if (!CanManageBusinesses) { SetError("You don't have permission to create businesses"); return; }
        ClearCreateBusinessForm();
        IsCreateBusinessDialogOpen = true;
    }

    private void CloseCreateBusinessDialog()
    {
        IsCreateBusinessDialogOpen = false;
        ClearCreateBusinessForm();
    }

    private async Task CreateBusinessAsync()
    {
        if (!CanManageBusinesses) { SetError("You don't have permission to create businesses"); return; }
        ClearError(); SuccessMessage = null;
        if (string.IsNullOrWhiteSpace(NewBusinessName)) { SetError("Business name is required"); return; }

        IsLoading = true;
        try
        {
            var user = _currentUserService.CurrentUser;
            if (user == null) { SetError("User not authenticated"); return; }

            var request = new CreateBusinessRequest
            {
                Name          = NewBusinessName,
                Type          = NewBusinessType,
                OwnerId       = user.Id,
                Description   = NewBusinessDescription,
                Address       = NewBusinessAddress,
                Phone         = NewBusinessPhone,
                Email         = NewBusinessEmail,
                TaxId         = NewBusinessTaxId,
                Configuration = System.Text.Json.JsonSerializer.Serialize(
                    await _businessManagementService.GetDefaultBusinessConfigurationAsync(NewBusinessType))
            };

            var business = await _businessManagementService.CreateBusinessAsync(request);
            Businesses.Add(business);
            SuccessMessage = $"Business '{NewBusinessName}' created successfully";

            await _auditService.LogAsync(user.Id, AuditAction.SystemConfiguration,
                $"Created business: {NewBusinessName} of type: {NewBusinessType}",
                nameof(Business), business.Id);

            CloseCreateBusinessDialog();
        }
        catch (Exception ex) { SetError($"Error creating business: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private void OpenCreateShopDialog()
    {
        if (SelectedBusiness == null) { SetError("Please select a business first"); return; }
        if (!CanManageBusinesses) { SetError("You don't have permission to create shops"); return; }
        ClearCreateShopForm();
        IsCreateShopDialogOpen = true;
    }

    private void CloseCreateShopDialog()
    {
        IsCreateShopDialogOpen = false;
        ClearCreateShopForm();
    }

    private async Task CreateShopAsync()
    {
        if (SelectedBusiness == null) { SetError("Please select a business first"); return; }
        if (!CanManageBusinesses) { SetError("You don't have permission to create shops"); return; }
        ClearError(); SuccessMessage = null;
        if (string.IsNullOrWhiteSpace(NewShopName)) { SetError("Shop name is required"); return; }

        IsLoading = true;
        try
        {
            var request = new CreateShopRequest
            {
                BusinessId    = SelectedBusiness.Id,
                Name          = NewShopName,
                Address       = NewShopAddress,
                Phone         = NewShopPhone,
                Email         = NewShopEmail,
                Configuration = System.Text.Json.JsonSerializer.Serialize(
                    await _businessManagementService.GetDefaultShopConfigurationAsync(SelectedBusiness.Id))
            };

            var shop = await _businessManagementService.CreateShopAsync(request);
            Shops.Add(shop);
            SuccessMessage = $"Shop '{NewShopName}' created successfully";

            await _auditService.LogAsync(_currentUserService.CurrentUser?.Id,
                AuditAction.SystemConfiguration,
                $"Created shop: {NewShopName} for business: {SelectedBusiness.Name}",
                nameof(Shop), shop.Id);

            CloseCreateShopDialog();
        }
        catch (Exception ex) { SetError($"Error creating shop: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private async Task DeleteBusinessAsync(BusinessResponse? business)
    {
        if (business == null || !CanManageBusinesses) return;

        IsLoading = true;
        ClearError();
        try
        {
            var user = _currentUserService.CurrentUser;
            if (user == null) { SetError("User not authenticated"); return; }

            var success = await _businessManagementService.DeleteBusinessAsync(business.Id, user.Id);
            if (success)
            {
                Businesses.Remove(business);
                if (SelectedBusiness?.Id == business.Id) { SelectedBusiness = null; Shops.Clear(); }
                SuccessMessage = $"Business '{business.Name}' deleted successfully";
                await _auditService.LogAsync(user.Id, AuditAction.SystemConfiguration,
                    $"Deleted business: {business.Name}", nameof(Business), business.Id);
            }
            else { SetError("Failed to delete business"); }
        }
        catch (Exception ex) { SetError($"Error deleting business: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private async Task DeleteShopAsync(ShopResponse? shop)
    {
        if (shop == null || !CanManageBusinesses) return;

        IsLoading = true;
        ClearError();
        try
        {
            var user = _currentUserService.CurrentUser;
            if (user == null) { SetError("User not authenticated"); return; }

            var success = await _businessManagementService.DeleteShopAsync(shop.Id, user.Id);
            if (success)
            {
                Shops.Remove(shop);
                SuccessMessage = $"Shop '{shop.Name}' deleted successfully";
                await _auditService.LogAsync(user.Id, AuditAction.SystemConfiguration,
                    $"Deleted shop: {shop.Name}", nameof(Shop), shop.Id);
            }
            else { SetError("Failed to delete shop"); }
        }
        catch (Exception ex) { SetError($"Error deleting shop: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private void ClearCreateBusinessForm()
    {
        NewBusinessName = NewBusinessDescription = NewBusinessAddress =
        NewBusinessPhone = NewBusinessEmail = NewBusinessTaxId = string.Empty;
        NewBusinessType = BusinessType.GeneralRetail;
        ClearError(); SuccessMessage = null;
    }

    private void ClearCreateShopForm()
    {
        NewShopName = NewShopAddress = NewShopPhone = NewShopEmail = string.Empty;
        ClearError(); SuccessMessage = null;
    }
}
