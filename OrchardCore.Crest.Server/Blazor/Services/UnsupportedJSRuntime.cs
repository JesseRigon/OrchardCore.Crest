using Microsoft.JSInterop;

namespace Crest.Blazor;

// Static SSR has no browser attached, so JS interop genuinely cannot work here - but
// [Inject] IJSRuntime still requires *some* registration to exist, or component
// instantiation throws a DI error before rendering even starts (confirmed: CrestHeading,
// which never calls JS during initial render, still failed to instantiate under
// HtmlRenderer without this). Real interop attempts throw a clear, actionable error
// instead of hanging or silently no-op-ing - correct, because a component calling JS
// outside OnAfterRenderAsync (the only lifecycle stage HtmlRenderer's quiescence task
// actually reaches) is a genuine bug that needs to surface during Phase 3 component
// authoring, not be masked.
public sealed class UnsupportedJSRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw NotSupported();

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw NotSupported();

    private static InvalidOperationException NotSupported() => new(
        "JavaScript interop is not available when a component is rendered by CrestBlazorComponentShapeBindingResolver " +
        "(Static SSR via HtmlRenderer, no browser attached). Guard interop calls behind OnAfterRenderAsync, or mark the " +
        "component as an interactive island instead (see plans/blazor hybrid conversion.md, Phase 3b).");
}
