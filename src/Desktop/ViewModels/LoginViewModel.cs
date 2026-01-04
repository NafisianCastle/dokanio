using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.Services;
using Shared.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Desktop.ViewModels;

/// <summary>
/// View model for user login
/// </summary>
public partial class LoginViewModel : BaseViewModel
{
    private readonly IUserService _userService;
    private readonly ISessionService _sessionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    [ObservableProperty]
    [Required(ErrorMessage = "Username is required")]
    private string username = string.Empty;

    [ObservableProperty]
    [Required(ErrorMessage = "Password is required")]
    private string password = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool rememberMe;

    public LoginViewModel(
        IUserService userService,
        ISessionService sessionService,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _userService = userService;
        _sessionService = sessionService;
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsLoading)
            return;

        ErrorMessage = null;
        
        // Validate input
        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "Username is required";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Password is required";
            return;
        }

        IsLoading = true;

        try
        {
            // Authenticate user
            var user = await _userService.AuthenticateAsync(Username, Password);
            if (user == null)
            {
                ErrorMessage = "Invalid username or password";
                await _auditService.LogAsync(
                    null,
                    AuditAction.SecurityViolation,
                    $"Failed login attempt for username: {Username}");
                return;
            }

            // Create session
            var session = await _sessionService.CreateSessionAsync(user.Id);
            
            // Set current user context
            _currentUserService.SetCurrentUser(user, session);

            // Log successful login
            await _auditService.LogAsync(
                user.Id,
                AuditAction.Login,
                $"User {Username} logged in successfully");

            // Clear password for security
            Password = string.Empty;

            // Navigate to main application (this would be handled by the view)
            OnLoginSuccessful?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = "An error occurred during login. Please try again.";
            await _auditService.LogAsync(
                null,
                AuditAction.SecurityViolation,
                $"Login error for username: {Username} - {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearError()
    {
        ErrorMessage = null;
    }

    /// <summary>
    /// Event fired when login is successful
    /// </summary>
    public Action? OnLoginSuccessful { get; set; }

    partial void OnUsernameChanged(string value)
    {
        ErrorMessage = null;
    }

    partial void OnPasswordChanged(string value)
    {
        ErrorMessage = null;
    }
}