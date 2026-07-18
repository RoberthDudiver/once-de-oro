using Microsoft.AspNetCore.Components;
using OnceDeOro.Services;

namespace OnceDeOro.Components;

/// <summary>
/// Base para las páginas del juego: se re-renderiza cuando el estado cambia
/// (incluida la carga asíncrona inicial desde localStorage).
/// </summary>
public abstract class GamePageBase : ComponentBase, IDisposable
{
    [Inject] protected GameService Game { get; set; } = default!;
    [Inject] protected Loc Loc { get; set; } = default!;

    protected override void OnInitialized()
    {
        Game.Changed += OnGameChangedInternal;
        Loc.Changed += OnLocChanged;
    }

    private void OnGameChangedInternal()
    {
        OnGameChanged();
        InvokeAsync(StateHasChanged);
    }

    private void OnLocChanged() => InvokeAsync(StateHasChanged);

    /// <summary>Hook opcional: corre cuando cambia el estado del juego (p. ej. al terminar de cargar).</summary>
    protected virtual void OnGameChanged() { }

    public virtual void Dispose()
    {
        Game.Changed -= OnGameChangedInternal;
        Loc.Changed -= OnLocChanged;
    }
}
