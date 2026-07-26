using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using OnceDeOro.Models;

namespace OnceDeOro.Services;

/// <summary>
/// Estado del simulador de carrera del jugador. Se guarda aparte de la partida
/// del manager (clave propia en localStorage), porque es un modo distinto.
/// </summary>
public sealed class CareerService
{
    private const string Key = "once-de-oro:career:v1";
    private readonly IJSRuntime _js;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public CareerService(IJSRuntime js) { _js = js; }

    public CareerPlayer? Current { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync()
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("localStorage.getItem", Key);
            if (!string.IsNullOrWhiteSpace(raw))
                Current = JsonSerializer.Deserialize<CareerPlayer>(raw, Json);
        }
        catch { Current = null; }
        Changed?.Invoke();
    }

    public void Start(string surname, int number, Foot foot, string nation, Position pos, int decisionEvery)
    {
        Current = CareerEngine.NewCareer(surname, number, foot, nation, pos, decisionEvery);
        Save();
    }

    public void Choose(CareerOption opt)
    {
        if (Current is null || Current.Retired) return;
        CareerEngine.Choose(Current, opt);
        Save();
    }

    /// <summary>Borra la carrera actual para empezar otra.</summary>
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
