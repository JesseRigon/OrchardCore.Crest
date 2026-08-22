using System.Globalization;
using System.Net.Http.Json;
using Crest.Components.Primitives;

namespace Crest.Components.Theme;

// .Admin's own UI strings (as opposed to Crest.Components' compiled-in CrestStrings.resx)
// deliberately do NOT live in a .resx satellite assembly baked into the WASM bundle -
// that would need a recompile/redeploy to edit, unlike every other piece of translatable
// content in Orchard. Instead this fetches the resolved .po catalog (see
// CrestLocalizationController.GetStrings, server-side) for the active culture and plugs
// into the SAME Localizer/ILocalizer extension seam OrchardCore.Crest.Components already
// defines. Keys are invariant literals (T["Some text"], native Orchard style - see
// docs/localization.md): the literal is simultaneously the key and the fallback, so an
// untranslated string renders itself, and the same literal shares its translation with
// every other pipeline keyed on it (store, shipped module .po catalogs).
//
// This only overrides caller-specific string keys (see plans/user-localization.md phase
// 5/6) - it deliberately does NOT shadow Crest.Components' existing CrestStrings keys, so
// a page component and a Crest.Components primitive it hosts can each keep using their own
// catalog without collision.
//
// Lives in Crest.Components (not Crest.Admin) because every WASM client project below
// Crest.Admin in the dependency graph - Crest.Icons, Accounting.BlazorWasm, and any future
// module's blazor-wasm project - needs to localize its own strings too, and none of them
// can reference Crest.Admin (Crest.Admin depends on them, not the other way around).
// Deliberately depends on a plain HttpClient rather than Crest.Admin.Api.IApi - the
// strings endpoint is unauthenticated GET-only, so the antiforgery-token machinery IApi's
// other members need is unnecessary baggage here.
public sealed class CrestApiLocalizer(HttpClient http) : ILocalizer
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

            var strings = await GetStringsAsync(name);
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

    // Native Orchard-style lookup for plain @page-routed Razor components (not
    // CrestComponent-derived, so they have no Localize(key) instance method to call).
    // Inject CrestApiLocalizer as T and write T["Some text"] - the invariant literal IS
    // the translation key, exactly like S["..."] in server-side Orchard code, and doubles
    // as the rendered fallback when the culture holds no translation for it. Identity is
    // never carried by these literals (menu items keep UniqueId for that); they are purely
    // translation keys.
    public string this[string text] =>
        _activeCultureName is { } culture && _cache.TryGetValue(culture, out var strings) && strings.TryGetValue(text, out var value)
            ? value
            : text;

    // Format-string form, mirroring S["Signed in as {0}", name]: the literal is resolved
    // first, then formatted with the current culture.
    public string this[string text, params object?[] args] =>
        string.Format(CultureInfo.CurrentCulture, this[text], args);

    // Cookie/credential inclusion is configured once, at HttpClient registration time
    // (see Crest.Admin/wasm/Program.cs's CrestAntiforgeryHandler-wrapped HttpClient), not
    // per-request here - that keeps this class portable across WASM and server-rendered
    // execution contexts. The WASM-only per-request BrowserRequestCredentials extension
    // (Microsoft.AspNetCore.Components.WebAssembly.Http) doesn't exist outside a browser
    // host and would break under Static SSR/InteractiveServer.
    private async Task<Dictionary<string, string>?> GetStringsAsync(string culture)
    {
        using var response = await http.GetAsync($"api/crest/localization/strings?culture={Uri.EscapeDataString(culture)}");
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<Dictionary<string, string>>()
            : null;
    }
}
