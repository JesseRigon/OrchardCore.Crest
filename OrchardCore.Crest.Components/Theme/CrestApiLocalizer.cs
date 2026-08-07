using System.Globalization;
using System.Net.Http.Json;
using Crest.Components.Primitives;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Crest.Components.Theme;

// .Admin's own UI strings (as opposed to Crest.Components' compiled-in CrestStrings.resx)
// deliberately do NOT live in a .resx satellite assembly baked into the WASM bundle -
// that would need a recompile/redeploy to edit, unlike every other piece of translatable
// content in Orchard. Instead this fetches the resolved .po catalog (see
// CrestLocalizationController.GetStrings, server-side) for the active culture and plugs
// into the SAME Localizer/ILocalizer extension seam OrchardCore.Crest.Components already
// defines - callers use the identical Localize(nameof(...)) pattern Crest.Components' own
// validators use, just resolved against this catalog instead of a compiled resource.
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

    // Convenience entry point for plain @page-routed Razor components (not
    // CrestComponent-derived, so they have no Localize(key) instance method to call).
    // Inject CrestApiLocalizer directly and call T("SomeKey", "English fallback text") -
    // the fallback is both the compiled default AND the string a translator sees as the
    // source text to translate from.
    public string T(string key, string fallback) =>
        _activeCultureName is { } culture && _cache.TryGetValue(culture, out var strings) && strings.TryGetValue(key, out var value)
            ? value
            : fallback;

    private async Task<Dictionary<string, string>?> GetStringsAsync(string culture)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/crest/localization/strings?culture={Uri.EscapeDataString(culture)}");
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        using var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<Dictionary<string, string>>()
            : null;
    }
}
