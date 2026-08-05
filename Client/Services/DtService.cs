using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using OnceDeOro.Models;

namespace OnceDeOro.Services;

/// <summary>Estado del Modo DT (carrera de entrenador). Se guarda en su propia clave.</summary>
public sealed class DtService
{
    private const string Key = "once-de-oro:dt:v1";
    private readonly IJSRuntime _js;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public DtService(IJSRuntime js) { _js = js; }

    public DtManager? Current { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync()
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("localStorage.getItem", Key);
            if (!string.IsNullOrWhiteSpace(raw))
                Current = JsonSerializer.Deserialize<DtManager>(raw, Json);
        }
        catch { Current = null; }
        Changed?.Invoke();
    }

    public void Start(string name, string surname, string nation, int age, DtLicense lic, DtBackground bg, List<string>? languages = null)
    {
        Current = DtEngine.NewCareer(name, surname, nation, age, lic, bg, languages);
        Save();
    }

    public void AdvanceWeek() { if (Current is not null) { DtEngine.AdvanceWeek(Current); Save(); } }
    public void AdvanceToNextMatch() { if (Current is not null) { DtEngine.AdvanceToNextMatch(Current); Save(); } }
    public void SimRest() { if (Current is not null) { DtEngine.SimRestOfSeason(Current); Save(); } }
    public void SetTraining(string plan, string intensity) { if (Current is not null) { DtEngine.SetTraining(Current, plan, intensity); Save(); } }
    public void AnswerPress(DtOption opt) { if (Current is not null) { DtEngine.AnswerPress(Current, opt); Save(); } }
    public void SetFormation(string f) { if (Current is not null) { DtEngine.SetFormation(Current, f); Save(); } }
    public void SetTactics(string m, string t, string p, string b, string l) { if (Current is not null) { DtEngine.SetTactics(Current, m, t, p, b, l); Save(); } }
    public void AutoLineup() { if (Current is not null) { DtEngine.AutoLineup(Current); Save(); } }
    public void ToggleStarter(string id) { if (Current is not null) { DtEngine.ToggleStarter(Current, id); Save(); } }
    public void Sell(string id) { if (Current is not null) { MarketMsg = DtEngine.SellPlayer(Current, id); Save(); } }
    public void Renew(string id) { if (Current is not null) { MarketMsg = DtEngine.RenewPlayer(Current, id); Save(); } }

    public void Choose(DtOption opt)
    {
        if (Current is null) return;
        DtEngine.Choose(Current, opt);
        Save();
    }

    // ---- Oficina / economía ----
    public void Reinforce(int m) { if (Current is not null) { DtEngine.Reinforce(Current, m); Save(); } }
    public void Upgrade(string which) { if (Current is not null && DtEngine.UpgradeFacility(Current, which)) Save(); }
    public void Loan(int m) { if (Current is not null) { DtEngine.RequestLoan(Current, m); Save(); } }

    // ---- Mercado / scouting ----
    public string MarketMsg { get; private set; } = "";
    public void Scout(Position? pos, int minMedia) { if (Current is not null) { DtEngine.Scout(Current, pos, minMedia); MarketMsg = ""; Save(); } }
    public void Sign(string prospectId, int offerM) { if (Current is not null) { MarketMsg = DtEngine.Sign(Current, prospectId, offerM); Save(); } }

    public async Task ResetAsync()
    {
        Current = null;
        try { await _js.InvokeVoidAsync("localStorage.removeItem", Key); } catch { }
        Changed?.Invoke();
    }

    private async void Save()
    {
        try { await _js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(Current, Json)); }
        catch { }
        Changed?.Invoke();
    }
}
