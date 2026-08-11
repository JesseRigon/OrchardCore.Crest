using Microsoft.JSInterop;

namespace Crest.Components.Primitives;

/// <summary>
/// Phase 8: guarded JS interop for code that runs during the static-SSR/prerender phase
/// of InteractiveAuto (or under CrestBlazorComponentShapeBindingResolver's HtmlRenderer),
/// where no browser is attached and any interop call throws InvalidOperationException.
/// Correctness rests on Blazor's own lifecycle: the interactive phase (circuit or WASM)
/// creates a fresh component instance and re-runs its init, this time with working
/// interop - so a browser-only effect (applying a theme, reading sessionStorage,
/// writing a cookie) that silently no-ops during prerender is *re-done properly* moments
/// later, never lost. Use this ONLY for such re-done-later effects; for interop whose
/// result changes what prerendered markup should contain, restructure the component
/// (OnAfterRenderAsync + StateHasChanged, or PersistentComponentState) instead.
/// </summary>
public static class PrerenderSafeJs
{
    /// <summary>Invoke a void JS function; silently no-op when interop is unavailable (prerender).</summary>
    public static async ValueTask TryInvokeVoidAsync(this IJSRuntime js, string identifier, params object?[]? args)
    {
        try
        {
            await js.InvokeVoidAsync(identifier, args);
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>Invoke a JS function; return <paramref name="fallback"/> when interop is unavailable (prerender).</summary>
    public static async ValueTask<TValue?> TryInvokeAsync<TValue>(this IJSRuntime js, string identifier, TValue? fallback = default, params object?[]? args)
    {
        try
        {
            return await js.InvokeAsync<TValue>(identifier, args);
        }
        catch (InvalidOperationException)
        {
            return fallback;
        }
    }
}
