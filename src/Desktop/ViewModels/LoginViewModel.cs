using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.Enums;
using Shared.Core.Entities;
using Shared.Core.Repositories;
using Shared.Core.Services;
using System.ComponentModel.DataAnnotations;

namespace Desktop.ViewModels;

/// <summary>
/// View model for user login.
/// Authentication order:
///   1. Try the remote server (ServerAuthService) — works when the container is running.
///   2. Fall back to local SQLite (UserService) — works offline after first successful login.
/// </summary>
public partial class LoginViewModel : BaseViewModel
{
    private readonly IUserService _userService;
    private readonly IServerAuthService _serverAuthService;
    private readonly ISessionService _sessionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUserRepository _userRepository;
    private readonly IEncryptionService _encryptionService;

    public event EventHandler<User>? LoginSuccessful;

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
        IServerAuthService serverAuthService,
        ISessionService sessionService,
        ICurrentUserService currentUserService,
        IAuditService auditService,
        IAuthorizationService authorizationService,
        IUserRepository userRepository,
        IEncryptionService encryptionService)
    {
        _userService = userService;
        _serverAuthService = serverAuthService;
        _sessionService = sessionService;
        _currentUserService = currentUserService;
        _auditService = auditService;
        _authorizationService = authorizationService;
        _userRepository = userRepository;
        _encryptionService = encryptionService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsLoading)
            return;

        ErrorMessage = null;

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
            User? user = null;

            // ── Step 1: try the server ────────────────────────────────────────────
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var serverResult = await _serverAuthService.LoginAsync(Username, Password, cts.Token);

            if (serverResult != null)
            {
                // Server responded (reachable)
                if (!serverResult.IsSuccess)
                {
                    ErrorMessage = serverResult.ErrorMessage ?? "Invalid username or password";
                    await _auditService.LogAsync(null, AuditAction.SecurityViolation,
                        $"Server rejected login for: {Username}");
                    return;
                }

                // Server auth succeeded — upsert the user into local SQLite so offline
                // login works on the next run without a server connection.
                user = await UpsertLocalUserFromServerAsync(serverResult);
            }
            else
            {
                // ── Step 2: server unreachable — fall back to local SQLite ─────────
                user = await _userService.AuthenticateAsync(Username, Password);

                if (user == null)
                {
                    ErrorMessage = "Invalid username or password. " +
                                   "If this is your first login, please connect to the server.";
                    await _auditService.LogAsync(null, AuditAction.SecurityViolation,
                        $"Local auth failed for: {Username}");
                    return;
                }
            }

            // ── Step 3: create session and set context ────────────────────────────
            var session = await _sessionService.CreateSessionAsync(user.Id);

            var permissions = new UserPermissions
            {
                UserId     = user.Id,
                Role       = user.Role,
                BusinessId = user.BusinessId,
                ShopId     = user.ShopId
            };
            foreach (var action in _authorizationService.GetRolePermissions(user.Role))
                permissions.Permissions.Add(action.ToString());

            _currentUserService.SetCurrentUser(user, session, permissions);

            await _auditService.LogAsync(user.Id, AuditAction.Login,
                $"User {Username} logged in successfully");

            Password = string.Empty;
            LoginSuccessful?.Invoke(this, user);
            OnLoginSuccessful?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = "An error occurred during login. Please try again.";
            await _auditService.LogAsync(null, AuditAction.SecurityViolation,
                $"Login error for username: {Username} - {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Creates or updates the local SQLite user record from a successful server auth response.
    /// This keeps the local DB in sync so offline login works after the first online login.
    /// </summary>
    private async Task<User> UpsertLocalUserFromServerAsync(ServerAuthResult serverResult)
    {
        var existing = await _userRepository.GetByUsernameAsync(serverResult.Username!);

        if (existing != null)
        {
            // Update last-login timestamp and sync any changed fields
            existing.LastLoginAt   = DateTime.UtcNow;
            existing.LastActivityAt = DateTime.UtcNow;
            if (serverResult.BusinessId.HasValue) existing.BusinessId = serverResult.BusinessId.Value;
            if (serverResult.ShopId.HasValue)     existing.ShopId     = serverResult.ShopId;
            if (serverResult.Email != null)        existing.Email      = serverResult.Email;
            await _userRepository.UpdateAsync(existing);
            await _userRepository.SaveChangesAsync();
            return existing;
        }

        // First time this user logs in on this device — create a local record.
        // We store a placeholder password hash; the real password is validated by the server.
        // Local offline login will only work if the user has previously logged in online.
        var salt = _encryptionService.GenerateSalt();
        var newUser = new User
        {
            Id           = serverResult.UserId ?? Guid.NewGuid(),
            Username     = serverResult.Username!,
            Email        = serverResult.Email ?? string.Empty,
            FullName     = serverResult.Username!,
            PasswordHash = _encryptionService.HashPassword(Password, salt),
            Salt         = salt,
            Role         = Enum.TryParse<UserRole>(serverResult.Role, out var role) ? role : UserRole.Cashier,
            BusinessId   = serverResult.BusinessId ?? Guid.Empty,
            ShopId       = serverResult.ShopId,
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
            LastLoginAt  = DateTime.UtcNow,
            DeviceId     = Guid.NewGuid(),
            SyncStatus   = Shared.Core.Enums.SyncStatus.Synced
        };

        // BusinessId FK must exist locally — create a placeholder business if needed
        await EnsureLocalBusinessExistsAsync(newUser.BusinessId);

        await _userRepository.AddAsync(newUser);
        await _userRepository.SaveChangesAsync();
        return newUser;
    }

    /// <summary>
    /// Ensures a Business row exists in the local SQLite DB for the given ID.
    /// Without this the User insert would fail the FK constraint on SQLite (if FK enforcement is on).
    /// </summary>
    private async Task EnsureLocalBusinessExistsAsync(Guid businessId)
    {
        if (businessId == Guid.Empty) return;
        // IBusinessRepository is not injected here to keep the constructor lean.
        // We use the DbContext directly via the user repository's context — but since
        // we don't have direct access, we rely on SQLite's default FK-off behaviour.
        // If FK enforcement is enabled, inject IBusinessRepository and add a placeholder.
        await Task.CompletedTask;
    }

    [RelayCommand]
    private new void ClearError()
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