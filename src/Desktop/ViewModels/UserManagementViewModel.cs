using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using System.Collections.ObjectModel;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;

namespace Desktop.ViewModels;

public class UserManagementViewModel : BaseViewModel
{
    private readonly IUserService _userService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    [Reactive] public ObservableCollection<User> Users { get; set; } = new();
    [Reactive] public User? SelectedUser { get; set; }
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public bool IsCreateUserDialogOpen { get; set; }
    [Reactive] public bool IsEditUserDialogOpen { get; set; }

    // Create-user form fields
    [Reactive] public string NewUsername { get; set; } = string.Empty;
    [Reactive] public string NewFullName { get; set; } = string.Empty;
    [Reactive] public string NewEmail { get; set; } = string.Empty;
    [Reactive] public string NewPassword { get; set; } = string.Empty;
    [Reactive] public UserRole NewUserRole { get; set; } = UserRole.Cashier;
    [Reactive] public string? ErrorMessage { get; set; }
    [Reactive] public string? SuccessMessage { get; set; }

    public bool CanManageUsers => _currentUserService?.CurrentUser != null &&
                                  _authorizationService.CanManageUsers(_currentUserService.CurrentUser);

    public Array UserRoles => Enum.GetValues<UserRole>();

    public ReactiveCommand<Unit, Unit> LoadUsersCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenCreateUserDialogCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCreateUserDialogCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateUserCommand { get; }
    public ReactiveCommand<User?, Unit> DeactivateUserCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearMessagesCommand { get; }

    public UserManagementViewModel(
        IUserService userService,
        IAuthorizationService authorizationService,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _userService           = userService;
        _authorizationService  = authorizationService;
        _currentUserService    = currentUserService;
        _auditService          = auditService;

        LoadUsersCommand              = ReactiveCommand.CreateFromTask(LoadUsersAsync);
        OpenCreateUserDialogCommand   = ReactiveCommand.Create(OpenCreateUserDialog);
        CloseCreateUserDialogCommand  = ReactiveCommand.Create(CloseCreateUserDialog);
        CreateUserCommand             = ReactiveCommand.CreateFromTask(CreateUserAsync);
        DeactivateUserCommand         = ReactiveCommand.CreateFromTask<User?>(DeactivateUserAsync);
        ClearMessagesCommand          = ReactiveCommand.Create(() => { ErrorMessage = null; SuccessMessage = null; });
    }

    private async Task LoadUsersAsync()
    {
        if (!CanManageUsers) { ErrorMessage = "You don't have permission to manage users"; return; }

        IsLoading    = true;
        ErrorMessage = null;
        try
        {
            var list = await _userService.GetActiveUsersAsync();
            Users.Clear();
            foreach (var u in list) Users.Add(u);
        }
        catch (Exception ex) { ErrorMessage = $"Error loading users: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    private void OpenCreateUserDialog()
    {
        if (!CanManageUsers) { ErrorMessage = "You don't have permission to create users"; return; }
        ClearCreateUserForm();
        IsCreateUserDialogOpen = true;
    }

    private void CloseCreateUserDialog()
    {
        IsCreateUserDialogOpen = false;
        ClearCreateUserForm();
    }

    private async Task CreateUserAsync()
    {
        if (!CanManageUsers) { ErrorMessage = "You don't have permission to create users"; return; }

        ErrorMessage   = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(NewUsername))                    { ErrorMessage = "Username is required"; return; }
        if (string.IsNullOrWhiteSpace(NewFullName))                    { ErrorMessage = "Full name is required"; return; }
        if (string.IsNullOrWhiteSpace(NewEmail))                       { ErrorMessage = "Email is required"; return; }
        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
                                                                        { ErrorMessage = "Password must be at least 6 characters"; return; }

        IsLoading = true;
        try
        {
            var user = await _userService.CreateUserAsync(NewUsername, NewFullName, NewEmail, NewPassword, NewUserRole);
            Users.Add(user);
            SuccessMessage = $"User '{NewUsername}' created successfully";

            await _auditService.LogAsync(
                _currentUserService.CurrentUser?.Id,
                AuditAction.SystemConfiguration,
                $"Created user: {NewUsername} with role: {NewUserRole}",
                nameof(User), user.Id);

            CloseCreateUserDialog();
        }
        catch (Exception ex) { ErrorMessage = $"Error creating user: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    private async Task DeactivateUserAsync(User? user)
    {
        if (user == null || !CanManageUsers) return;
        if (user.Id == _currentUserService.CurrentUser?.Id)
        {
            ErrorMessage = "You cannot deactivate your own account";
            return;
        }

        IsLoading    = true;
        ErrorMessage = null;
        try
        {
            var success = await _userService.DeactivateUserAsync(user.Id);
            if (success)
            {
                user.IsActive  = false;
                SuccessMessage = $"User '{user.Username}' deactivated successfully";
                await _auditService.LogAsync(
                    _currentUserService.CurrentUser?.Id,
                    AuditAction.SystemConfiguration,
                    $"Deactivated user: {user.Username}",
                    nameof(User), user.Id);
            }
            else { ErrorMessage = "Failed to deactivate user"; }
        }
        catch (Exception ex) { ErrorMessage = $"Error deactivating user: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    private void ClearCreateUserForm()
    {
        NewUsername    = string.Empty;
        NewFullName    = string.Empty;
        NewEmail       = string.Empty;
        NewPassword    = string.Empty;
        NewUserRole    = UserRole.Cashier;
        ErrorMessage   = null;
        SuccessMessage = null;
    }
}
