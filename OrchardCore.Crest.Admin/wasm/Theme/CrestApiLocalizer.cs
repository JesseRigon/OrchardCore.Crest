using System.Globalization;
using Crest.Admin.Api;
using Crest.Components.Primitives;

namespace Crest.Admin.Theme;

// .Admin's own UI strings (as opposed to Crest.Components' compiled-in CrestStrings.resx)
// deliberately do NOT live in a .resx satellite assembly baked into the WASM bundle -
// that would need a recompile/redeploy to edit, unlike every other piece of translatable
// content in Orchard. Instead this fetches the resolved .po catalog (see
// CrestLocalizationController.GetStrings, server-side) for the active culture and plugs
// into the SAME Localizer/ILocalizer extension seam OrchardCore.Crest.Components already
// defines - .Admin components call the identical Localize(nameof(...)) pattern
// Crest.Components' own validators use, just resolved against this catalog instead of a
// compiled resource.
//
// This only overrides Crest.Admin's own string keys (see plans/user-localization.md
// phase 5) - it deliberately does NOT shadow Crest.Components' existing CrestStrings
// keys, so a .Admin page component and a Crest.Components primitive it hosts can each
// keep using their own catalog without collision.
public sealed class CrestApiLocalizer(IApi api) : ILocalizer
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private string? _activeCultureName;

    // ILocalizer.Get is synchronous (Blazor renders synchronously) - the catalog for the
    // active culture must already be loaded before any component renders. Call this once
    // after the active culture is known (see DisplayManager), and again whenever it
    // changes. Get() returns null (falling back to the compiled default) for any key not
    // yet loaded, rather than blocking a render on a network call.
    public async Task LoadAsync(CultureInfo culture)
    {
        var name = culture.Name;
        await _lock.WaitAsync();
        try
        {
            _activeCultureName = name;
            if (_cache.ContainsKey(name))
            {
                return;
            }

            var strings = await api.Crest.Rest.Localization.GetStringsAsync(name);
            _cache[name] = strings ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        finally
        {
            _lock.Release();
        }
    }

    public string? Get(string key, CultureInfo culture)
    {
        var name = _activeCultureName ?? culture.Name;
        return _cache.TryGetValue(name, out var strings) && strings.TryGetValue(key, out var value)
            ? value
            : null;
    }

    // Convenience entry point for .Admin's own Pages/Components, which are plain
    // @page-routed Razor components (not CrestComponent-derived, so they have no
    // Localize(key) instance method to call). Inject CrestApiLocalizer directly and call
    // T("SomeKey", "English fallback text") - the fallback is both the compiled default
    // AND the string a translator sees as the source text to translate from.
    public string T(string key, string fallback) =>
        _activeCultureName is { } culture && _cache.TryGetValue(culture, out var strings) && strings.TryGetValue(key, out var value)
            ? value
            : fallback;
}
