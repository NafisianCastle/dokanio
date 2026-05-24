using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// Authenticates against the remote server's /api/auth/login endpoint.
/// Returns null when the server is unreachable so callers can fall back to local auth.
/// </summary>
public class ServerAuthService : IServerAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServerAuthService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ServerAuthService(HttpClient httpClient, ILogger<ServerAuthService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ServerAuthResult?> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new { username, password };
            using var response = await _httpClient.PostAsJsonAsync("api/auth/login", payload, cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Server auth returned {StatusCode} for user {Username}", response.StatusCode, username);

                // Try to extract the error message from the response body
                string? errorMessage = null;
                try
                {
                    var errorDoc = JsonDocument.Parse(body);
                    if (errorDoc.RootElement.TryGetProperty("message", out var msg))
                        errorMessage = msg.GetString();
                }
                catch { /* ignore parse errors */ }

                return new ServerAuthResult
                {
                    IsSuccess = false,
                    ErrorMessage = errorMessage ?? "Invalid username or password"
                };
            }

            // Parse the SyncApiResult<AuthenticationResponse> envelope
            var envelope = JsonDocument.Parse(body);
            var root = envelope.RootElement;

            if (!root.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
            {
                var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "Authentication failed";
                return new ServerAuthResult { IsSuccess = false, ErrorMessage = msg };
            }

            if (!root.TryGetProperty("data", out var data))
                return new ServerAuthResult { IsSuccess = false, ErrorMessage = "Unexpected server response" };

            return new ServerAuthResult
            {
                IsSuccess = true,
                UserId    = data.TryGetProperty("userId",     out var uid)  && uid.TryGetGuid(out var g1)  ? g1  : null,
                Username  = data.TryGetProperty("username",   out var un)   ? un.GetString()               : null,
                Email     = data.TryGetProperty("email",      out var em)   ? em.GetString()               : null,
                Role      = data.TryGetProperty("role",       out var ro)   ? ro.GetString()               : null,
                BusinessId= data.TryGetProperty("businessId", out var bid)  && bid.TryGetGuid(out var g2) ? g2  : null,
                ShopId    = data.TryGetProperty("shopId",     out var sid)  && sid.ValueKind != JsonValueKind.Null && sid.TryGetGuid(out var g3) ? g3 : null,
                SessionId = data.TryGetProperty("sessionId",  out var sess) && sess.ValueKind != JsonValueKind.Null && sess.TryGetGuid(out var g4) ? g4 : null,
            };
        }
        catch (HttpRequestException ex)
        {
            // Server unreachable — caller should fall back to local auth
            _logger.LogInformation("Server unreachable for auth ({Message}), falling back to local auth", ex.Message);
            return null;
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Server auth timed out, falling back to local auth");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error during server auth, falling back to local auth");
            return null;
        }
    }
}
