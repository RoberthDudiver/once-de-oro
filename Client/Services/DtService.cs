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

    public void Start(string name, string surname, string nation, int age, DtLicense lic, DtBackground bg)
    {
        Current = DtEngine.NewCareer(name, surname, nation, age, lic, bg);
        Save();
    }

    public void PlaySeason()
    {
        if (Current is null) return;
        DtEngine.PlaySeason(Current);
        Save();
    }

    public void Choose(DtOption opt)
    {
        if (Current is null) return;
        DtEngine.Choose(Current, opt);
        Save();
    }

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
