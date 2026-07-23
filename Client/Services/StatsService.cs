using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace OnceDeOro.Services;

/// <summary>Números públicos del contador de jugadores (lo que devuelve /api/stats).</summary>
public sealed record GlobalStats(long Players, long Matches, long Accounts,
                                 long Active7d, Dictionary<string, long> ByLang);

/// <summary>
/// El contador anónimo de jugadores. Genera UNA vez un UUID de navegador (que no
/// tiene nada que ver con vos: ni email, ni nombre) y lo manda al arrancar junto
/// con cuántos partidos jugaste. El servidor sólo lleva conteos agregados.
/// Si el servidor no tiene base configurada, todo esto simplemente no hace nada.
/// </summary>
public sealed class StatsService
{
    private const string VisitorKey = "once-de-oro:visitor:v1";
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly Loc _loc;

    public StatsService(HttpClient http, IJSRuntime js, Loc loc)
    {
        _http = http; _js = js; _loc = loc;
    }

    /// <summary>UUID del navegador; lo crea la primera vez y lo guarda.</summary>
    private async Task<string> VisitorIdAsync()
    {
        try
        {
            var id = await _js.InvokeAsync<string?>("localStorage.getItem", VisitorKey);
            if (!string.IsNullOrWhiteSpace(id)) return id!;
            id = Guid.NewGuid().ToString("N");
            await _js.InvokeVoidAsync("localStorage.setItem", VisitorKey, id);
            return id;
        }
        catch { return ""; }
    }

    /// <summary>Avisa "acá hay un jugador" con sus totales. Silencioso ante cualquier error.</summary>
    public async Task PingAsync(long matches)
    {
        try
        {
            var id = await VisitorIdAsync();
            if (string.IsNullOrEmpty(id)) return;
            await _http.PostAsJsonAsync("api/stats/ping", new { VisitorId = id, Matches = matches, Lang = _loc.Lang });
        }
        catch { /* sin servidor, seguimos jugando igual */ }
    }

    public async Task<GlobalStats?> GetAsync()
    {
        try { return await _http.GetFromJsonAsync<GlobalStats>("api/stats"); }
        catch { return null; }
    }
}
