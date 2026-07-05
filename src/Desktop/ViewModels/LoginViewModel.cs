using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using Shared.Core.Enums;
using Shared.Core.Entities;
using Shared.Core.Repositories;
using Shared.Core.Services;

namespace Desktop.ViewModels;

/// <summary>
/// View model for user login.
/// Authentication order:
///   1. Try the remote server (ServerAuthService) — works when the container is running.
///   2. Fall back to local SQLite (UserService) — works offline after first successful login.
/// </summary>
public class LoginViewModel : BaseViewModel
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
    public Action? OnLoginSuccessful { get; set; }

    [Reactive] public string Username { get; set; } = string.Empty;
    [Reactive] public string Password { get; set; } = string.Empty;
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public string? ErrorMessage { get; set; }
    [Reactive] public bool RememberMe { get; set; }

    public ReactiveCommand<Unit, Unit> LoginCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearErrorCommand { get; }

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

        // Clear error when username or password changes
        this.WhenAnyValue(x => x.Username).Subscribe(_ => ErrorMessage = null);
        this.WhenAnyValue(x => x.Password).Subscribe(_ => ErrorMessage = null);

        var canLogin = this.WhenAnyValue(x => x.IsLoading, loading => !loading);

        LoginCommand = ReactiveCommand.CreateFromTask(LoginAsync, canLogin);
        ClearErrorCommand = ReactiveCommand.Create(() => { ErrorMessage = null; });
    }

    private async Task LoginAsync()
    {
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
                if (!serverResult.IsSuccess)
                {
                    ErrorMessage = serverResult.ErrorMessage ?? "Invalid username or password";
                    await _auditService.LogAsync(null, AuditAction.SecurityViolation,
                        $"Server rejected login for: {Username}");
                    return;
                }

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

    private async Task<User> UpsertLocalUserFromServerAsync(ServerAuthResult serverResult)
    {
        var existing = await _userRepository.GetByUsernameAsync(serverResult.Username!);

        if (existing != null)
        {
            existing.LastLoginAt    = DateTime.UtcNow;
            existing.LastActivityAt = DateTime.UtcNow;
            if (serverResult.BusinessId.HasValue) existing.BusinessId = serverResult.BusinessId.Value;
            if (serverResult.ShopId.HasValue)     existing.ShopId     = serverResult.ShopId;
            if (serverResult.Email != null)        existing.Email      = serverResult.Email;
            await _userRepository.UpdateAsync(existing);
            await _userRepository.SaveChangesAsync();
            return existing;
        }

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

        await EnsureLocalBusinessExistsAsync(newUser.BusinessId);
        await _userRepository.AddAsync(newUser);
        await _userRepository.SaveChangesAsync();
        return newUser;
    }

    private static Task EnsureLocalBusinessExistsAsync(Guid businessId)
    {
        if (businessId == Guid.Empty) return Task.CompletedTask;
        // Relies on SQLite FK-off default; inject IBusinessRepository if FK enforcement is on.
        return Task.CompletedTask;
    }
}
