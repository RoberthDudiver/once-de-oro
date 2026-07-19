using Microsoft.JSInterop;

namespace OnceDeOro.Services;

/// <summary>
/// Sonidos del juego. Todo se genera por código con Web Audio (ver wwwroot/js/sfx.js):
/// no hay archivos de audio, así que no hay licencias ni atribuciones que cumplir.
/// Si el navegador no soporta Web Audio, cada llamada simplemente no hace nada.
/// </summary>
public sealed class Sfx
{
    private readonly IJSRuntime _js;
    public Sfx(IJSRuntime js) => _js = js;

    public bool On { get; private set; } = true;

    private async void Fire(string fn, params object?[] args)
    {
        try { await _js.InvokeVoidAsync(fn, args); } catch { /* sin audio, seguimos */ }
    }

    public void Play(string name) => Fire("odoSfx.play", name);
    public void CrowdStart() => Fire("odoSfx.crowdStart");
    public void CrowdStop() => Fire("odoSfx.crowdStop");

    /// <summary>Loop de hinchada para el modo rápido (reemplaza a los efectos).</summary>
    public void MusicStart() => Fire("odoSfx.musicStart");
    public void MusicStop() => Fire("odoSfx.musicStop");

    public async Task LoadAsync()
    {
        try { On = await _js.InvokeAsync<bool>("odoSfx.isOn"); } catch { On = false; }
    }

    public async Task<bool> ToggleAsync()
    {
        try { On = await _js.InvokeAsync<bool>("odoSfx.toggle"); } catch { }
        return On;
    }
}
