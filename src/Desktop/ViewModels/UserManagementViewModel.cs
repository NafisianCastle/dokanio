using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace Desktop.ViewModels;

/// <summary>
/// View model for user management
/// </summary>
public partial class UserManagementViewModel : BaseViewModel
{
    private readonly IUserService _userService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    [ObservableProperty]
    private ObservableCollection<User> users = new();

    [ObservableProperty]
    private User? selectedUser;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isCreateUserDialogOpen;

    [ObservableProperty]
    private bool isEditUserDialogOpen;

    // Create user properties
    [ObservableProperty]
    [Required(ErrorMessage = "Username is required")]
    private string newUsername = string.Empty;

    [ObservableProperty]
    [Required(ErrorMessage = "Full name is required")]
    private string newFullName = string.Empty;

    [ObservableProperty]
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    private string newEmail = string.Empty;

    [ObservableProperty]
    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    private string newPassword = string.Empty;

    [ObservableProperty]
    private UserRole newUserRole = UserRole.Cashier;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? successMessage;

    public UserManagementViewModel(
        IUserService userService,
        IAuthorizationService authorizationService,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _userService = userService;
        _authorizationService = authorizationService;
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    public bool CanManageUsers => _currentUserService.CurrentUser != null && 
                                  _authorizationService.CanManageUsers(_currentUserService.CurrentUser);

    [RelayCommand]
    private async Task LoadUsersAsync()
    {
        if (!CanManageUsers)
        {
            ErrorMessage = "You don't have permission to manage users";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var userList = await _userService.GetActiveUsersAsync();
            Users.Clear();
            foreach (var user in userList)
            {
                Users.Add(user);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading users: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenCreateUserDialog()
    {
        if (!CanManageUsers)
        {
            ErrorMessage = "You don't have permission to create users";
            return;
        }

        ClearCreateUserForm();
        IsCreateUserDialogOpen = true;
    }

    [RelayCommand]
    private void CloseCreateUserDialog()
    {
        IsCreateUserDialogOpen = false;
        ClearCreateUserForm();
    }

    [RelayCommand]
    private async Task CreateUserAsync()
    {
        if (!CanManageUsers)
        {
            ErrorMessage = "You don't have permission to create users";
            return;
        }

        ErrorMessage = null;
        SuccessMessage = null;

        // Validate input
        if (string.IsNullOrWhiteSpace(NewUsername))
        {
            ErrorMessage = "Username is required";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewFullName))
        {
            ErrorMessage = "Full name is required";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewEmail))
        {
            ErrorMessage = "Email is required";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
        {
            ErrorMessage = "Password must be at least 6 characters";
            return;
        }

        IsLoading = true;

        try
        {
            var user = await _userService.CreateUserAsync(
                NewUsername,
                NewFullName,
                NewEmail,
                NewPassword,
                NewUserRole);

            Users.Add(user);
            SuccessMessage = $"User '{NewUsername}' created successfully";
            
            await _auditService.LogAsync(
                _currentUserService.CurrentUser?.Id,
                AuditAction.SystemConfiguration,
                $"Created user: {NewUsername} with role: {NewUserRole}",
                nameof(User),
                user.Id);

            CloseCreateUserDialog();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error creating user: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeactivateUserAsync(User? user)
    {
        if (user == null || !CanManageUsers)
            return;

        if (user.Id == _currentUserService.CurrentUser?.Id)
        {
            ErrorMessage = "You cannot deactivate your own account";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var success = await _userService.DeactivateUserAsync(user.Id);
            if (success)
            {
                user.IsActive = false;
                SuccessMessage = $"User '{user.Username}' deactivated successfully";
                
                await _auditService.LogAsync(
                    _currentUserService.CurrentUser?.Id,
                    AuditAction.SystemConfiguration,
                    $"Deactivated user: {user.Username}",
                    nameof(User),
                    user.Id);
            }
            else
            {
                ErrorMessage = "Failed to deactivate user";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deactivating user: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearMessages()
    {
        ErrorMessage = null;
        SuccessMessage = null;
    }

    private void ClearCreateUserForm()
    {
        NewUsername = string.Empty;
        NewFullName = string.Empty;
        NewEmail = string.Empty;
        NewPassword = string.Empty;
        NewUserRole = UserRole.Cashier;
        ErrorMessage = null;
        SuccessMessage = null;
    }

    public Array UserRoles => Enum.GetValues<UserRole>();
}