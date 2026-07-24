using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace OnceDeOro.Services;

/// <summary>
/// Panel de administración contra la API del servidor. Sólo responde a los emails
/// configurados como admin (Admin__Emails); para cualquier otro, el servidor
/// devuelve 403 y acá <see cref="IsAdmin"/> queda en false.
/// </summary>
public sealed class AdminService
{
    private readonly HttpClient _http;
    private readonly AuthService _auth;

    public AdminService(HttpClient http, AuthService auth) { _http = http; _auth = auth; }

    public bool IsAdmin { get; private set; }
    public bool Checked { get; private set; }

    /// <summary>Un usuario tal como lo muestra el panel (coincide con AdminUserRow del servidor).</summary>
    public sealed record AdminUser(
        string Id, string Email, string DisplayName, DateTime CreatedAt, DateTime LastLoginAt,
        bool Banned, string ClubName, int Money, int MatchesPlayed, int Wins, int Honours, int Progress);

    private HttpRequestMessage Req(HttpMethod m, string url)
    {
        var r = new HttpRequestMessage(m, url);
        r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _auth.Token);
        return r;
    }

    /// <summary>Pregunta al servidor si esta cuenta es admin. Silencioso si no hay sesión.</summary>
    public async Task<bool> CheckAdminAsync()
    {
        Checked = true;
        if (!_auth.IsLoggedIn) { IsAdmin = false; return false; }
        try
        {
            var resp = await _http.SendAsync(Req(HttpMethod.Get, "api/admin/me"));
            if (!resp.IsSuccessStatusCode) { IsAdmin = false; return false; }
            var d = await resp.Content.ReadFromJsonAsync<Dictionary<string, bool>>();
            IsAdmin = d is not null && d.TryGetValue("isAdmin", out var v) && v;
        }
        catch { IsAdmin = false; }
        return IsAdmin;
    }

    public async Task<List<AdminUser>?> UsersAsync()
    {
        try
        {
            var resp = await _http.SendAsync(Req(HttpMethod.Get, "api/admin/users"));
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<List<AdminUser>>();
        }
        catch { return null; }
    }

    public Task<int?> AdjustMoneyAsync(string id, int delta) => MoneyPost($"api/admin/users/{id}/money", new { delta });
    public Task<int?> SetMoneyAsync(string id, int money) => MoneyPost($"api/admin/users/{id}/set-money", new { money });

    private async Task<int?> MoneyPost(string url, object body)
    {
        try
        {
            var req = Req(HttpMethod.Post, url);
            req.Content = JsonContent.Create(body);
            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            var d = await resp.Content.ReadFromJsonAsync<Dictionary<string, int>>();
            return d is not null && d.TryGetValue("money", out var v) ? v : null;
        }
        catch { return null; }
    }

    public async Task<bool> SetBanAsync(string id, bool banned)
    {
        try
        {
            var req = Req(HttpMethod.Post, $"api/admin/users/{id}/ban");
            req.Content = JsonContent.Create(new { banned });
            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
