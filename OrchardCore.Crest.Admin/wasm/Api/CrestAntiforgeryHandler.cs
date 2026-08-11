using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.JSInterop;

namespace Crest.Admin.Api;

/// <summary>
/// Adds Orchard's antiforgery request token to every unsafe same-origin Crest
/// request. Authentication continues to use the browser's Orchard cookie.
/// </summary>
public sealed class CrestAntiforgeryHandler(IJSInProcessRuntime js) : DelegatingHandler, ICrestAntiforgeryTokenStore, ICrestCultureCookieWriter
{
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private CrestAntiforgeryToken? _token;
    private CultureCookieContext? _cultureCookieContext;

    // Set by Program.cs to the same origin-root address the owning HttpClient itself
    // uses (see apiBaseAddress). GetTokenAsync below sends its own request directly
    // through the inner handler chain via base.SendAsync, bypassing the owning
    // HttpClient.SendAsync's usual BaseAddress + relative-URI combination step - so a
    // plain relative "api/crest/antiforgery/token" string would otherwise resolve
    // against the browser's document.baseURI (i.e. <base href>) instead, breaking
    // once that stopped being the origin root for shells served under a tenant's
    // configured AdminPath/LoginPath.
    public Uri? BaseAddress { get; set; }

    // Pushed by DisplayManager.RefreshManifestAsync every time it resolves culture (see
    // plans/user-localization.md phase 15). The cookie is per-origin but a session
    // override lives in sessionStorage (per-tab) - so between two open tabs with
    // different overrides, whichever tab last wrote the cookie "wins" for both until the
    // other tab makes its own request. Re-resolving and rewriting the cookie immediately
    // before every outgoing Crest API request (not only on manifest refresh) means each
    // tab's own requests always carry its own resolved culture, regardless of what the
    // other tab last wrote.
    public void SetCultureCookieContext(CultureCookieContext? context) => _cultureCookieContext = context;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        RewriteCultureCookie();

        if (RequiresAntiforgeryToken(request))
        {
            var token = await GetTokenAsync(cancellationToken);
            request.Headers.TryAddWithoutValidation(token.HeaderName, token.RequestToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    // Synchronous JS interop (setAdminCulture/getSessionCultureOverride/getBrowserLocale
    // in crest.theme.js are all plain, non-async functions) so this can run inline on the
    // request path with no extra await hop. Never throws/blocks a request on failure - a
    // culture-cookie write is an enhancement, not something any Crest API call should ever
    // fail over.
    private void RewriteCultureCookie()
    {
        var context = _cultureCookieContext;
        if (context is null)
        {
            return;
        }

        try
        {
            var sessionOverride = js.Invoke<string?>("crestTheme.getSessionCultureOverride", context.UserName);
            var browserLocale = js.Invoke<string?>("crestTheme.getBrowserLocale");
            var resolved = Crest.Admin.DisplayManagement.DisplayManager.ResolveCulture(
                context.CultureSelector, sessionOverride, browserLocale, context.IsUnderAdminPath);
            js.InvokeVoid("crestTheme.setAdminCulture", context.CultureSelector.CookieName, context.CultureSelector.CookiePath, resolved);
        }
        catch (JSException)
        {
        }
    }

    public void Clear() => _token = null;

    private async Task<CrestAntiforgeryToken> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null)
        {
            return _token;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null)
            {
                return _token;
            }

            var tokenUri = BaseAddress is null
                ? new Uri("api/crest/antiforgery/token", UriKind.Relative)
                : new Uri(BaseAddress, "api/crest/antiforgery/token");
            using var request = new HttpRequestMessage(HttpMethod.Get, tokenUri);
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            using var response = await base.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            _token = await response.Content.ReadFromJsonAsync<CrestAntiforgeryToken>(cancellationToken)
                ?? throw new InvalidOperationException("Orchard did not return an antiforgery token.");
            return _token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static bool RequiresAntiforgeryToken(HttpRequestMessage request) =>
        request.Method != HttpMethod.Get &&
        request.Method != HttpMethod.Head &&
        request.Method != HttpMethod.Options &&
        !string.Equals(request.RequestUri?.AbsolutePath, "/api/crest/antiforgery/token", StringComparison.OrdinalIgnoreCase);
}

public interface ICrestAntiforgeryTokenStore
{
    void Clear();
}

// Phase 8: DisplayManager's seam onto the browser-side culture-cookie rewrite, so it
// can depend on an interface instead of the concrete WASM-only CrestAntiforgeryHandler
// (whose ctor hard-requires IJSInProcessRuntime and can't be constructed server-side).
// WASM: implemented by CrestAntiforgeryHandler (rewrites the cookie via synchronous JS
// interop before each request). Server (SSR/InteractiveServer): a no-op registration -
// the server *reads* the culture cookie through RequestLocalizationOptions and never
// writes it; cookie writeback stays exclusively client-side per docs/localization.md.
public interface ICrestCultureCookieWriter
{
    void SetCultureCookieContext(CultureCookieContext? context);
}

public sealed record CrestAntiforgeryToken(string HeaderName, string RequestToken);

// Pushed into CrestAntiforgeryHandler by DisplayManager.RefreshManifestAsync (see
// plans/user-localization.md phase 15) so the handler can independently re-resolve and
// rewrite the culture cookie on every outgoing request without depending on
// DisplayManager itself (avoids a circular DI dependency - DisplayManager depends on
// IApi, which depends on this handler's owning HttpClient).
public sealed record CultureCookieContext(string? UserName, CultureSelector CultureSelector, bool IsUnderAdminPath);
