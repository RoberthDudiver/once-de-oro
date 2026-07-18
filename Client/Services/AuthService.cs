using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using OnceDeOro.Models;

namespace OnceDeOro.Services;

public sealed record AuthResult(bool Ok, string? Error = null);

/// <summary>
/// Cuenta del jugador contra la API del servidor (MongoDB).
/// Guarda el token en localStorage para que la sesión sobreviva a recargas.
/// Si el servidor no tiene cuentas habilitadas, todo queda deshabilitado y el
/// juego sigue funcionando sólo con localStorage.
/// </summary>
public sealed class AuthService
{
    private const string TokenKey = "once-de-oro:auth:v1";

    private readonly HttpClient _http;
    private readonly IJSRuntime _js;

    /// <summary>
    /// Mismas opciones que usa el save local: los enums (TeamStyle) viajan como TEXTO.
    /// Si no, al leer la partida de la nube explota y parecería que "no hay partida".
    /// </summary>
    private static readonly System.Text.Json.JsonSerializerOptions StateJson =
        new(System.Text.Json.JsonSerializerDefaults.Web)
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };

    public AuthService(HttpClient http, IJSRuntime js) { _http = http; _js = js; }

    public string? Token { get; private set; }
    public string Email { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public bool IsLoggedIn => !string.IsNullOrEmpty(Token);

    /// <summary>false si este despliegue no tiene MongoDB configurado.</summary>
    public bool Available { get; private set; }

    public event Action? Changed;

    private sealed record Stored(string Token, string Email, string DisplayName);
    private sealed record AuthResponse(string Token, string Email, string DisplayName);
    private sealed record ApiError(string? Error);

    public async Task InitAsync()
    {
        try
        {
            var status = await _http.GetFromJsonAsync<Dictionary<string, bool>>("api/auth/status");
            Available = status is not null && status.TryGetValue("enabled", out var e) && e;
        }
        catch { Available = false; }

        try
        {
            var raw = await _js.InvokeAsync<string?>("localStorage.getItem", TokenKey);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var s = System.Text.Json.JsonSerializer.Deserialize<Stored>(raw);
                if (s is not null && !string.IsNullOrEmpty(s.Token))
                {
                    Token = s.Token; Email = s.Email; DisplayName = s.DisplayName;
                }
            }
        }
        catch { }
        Changed?.Invoke();
    }

    public async Task<AuthResult> RegisterAsync(string email, string password, string? displayName)
        => await PostAuthAsync("api/auth/register", new { email, password, displayName });

    public async Task<AuthResult> LoginAsync(string email, string password)
        => await PostAuthAsync("api/auth/login", new { email, password });

    private async Task<AuthResult> PostAuthAsync(string url, object body)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(url, body);
            if (!resp.IsSuccessStatusCode)
            {
                string msg = "No se pudo completar la operación.";
                try { msg = (await resp.Content.ReadFromJsonAsync<ApiError>())?.Error ?? msg; } catch { }
                return new AuthResult(false, msg);
            }

            var data = await resp.Content.ReadFromJsonAsync<AuthResponse>();
            if (data is null) return new AuthResult(false, "Respuesta inválida del servidor.");

            Token = data.Token; Email = data.Email; DisplayName = data.DisplayName;
            await _js.InvokeVoidAsync("localStorage.setItem", TokenKey,
                System.Text.Json.JsonSerializer.Serialize(new Stored(Token, Email, DisplayName)));
            Changed?.Invoke();
            return new AuthResult(true);
        }
        catch (Exception ex) { return new AuthResult(false, "No se pudo conectar: " + ex.Message); }
    }

    public async Task LogoutAsync()
    {
        Token = null; Email = ""; DisplayName = "";
        try { await _js.InvokeVoidAsync("localStorage.removeItem", TokenKey); } catch { }
        Changed?.Invoke();
    }

    private HttpRequestMessage Authed(HttpMethod m, string url)
    {
        var req = new HttpRequestMessage(m, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        return req;
    }

    /// <summary>
    /// Resultado de consultar la nube. Distinguir "no hay partida" de "falló la consulta"
    /// es CRÍTICO: si un error se tomara como "no hay partida", subiríamos un estado
    /// vacío encima y borraríamos el progreso del jugador.
    /// </summary>
    public sealed record FetchResult(bool Ok, GameState? State)
    {
        public static readonly FetchResult Failed = new(false, null);
        public static readonly FetchResult Empty = new(true, null);
    }

    public async Task<FetchResult> FetchStateAsync()
    {
        if (!IsLoggedIn) return FetchResult.Failed;
        try
        {
            var resp = await _http.SendAsync(Authed(HttpMethod.Get, "api/game/state"));
            if (resp.StatusCode == System.Net.HttpStatusCode.NoContent) return FetchResult.Empty;
            if (!resp.IsSuccessStatusCode) return FetchResult.Failed;

            var state = await resp.Content.ReadFromJsonAsync<GameState>(StateJson);
            return state is null ? FetchResult.Failed : new FetchResult(true, state);
        }
        catch { return FetchResult.Failed; }
    }

    /// <summary>Sube la partida a la nube. Devuelve true si quedó guardada.</summary>
    public async Task<bool> PushStateAsync(GameState state)
    {
        if (!IsLoggedIn) return false;
        try
        {
            var req = Authed(HttpMethod.Put, "api/game/state");
            req.Content = JsonContent.Create(state, options: StateJson);
            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
