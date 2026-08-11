using System.Net.Http.Json;
using Crest.Admin.Api;
using Microsoft.AspNetCore.Http;

namespace Crest.Services;

/// <summary>
/// Phase 8: the server-side counterpart of the WASM CrestAntiforgeryHandler, for the
/// SSR/InteractiveServer phases of InteractiveAuto. Admin components keep calling
/// api/crest/* over the same scoped HttpClient abstraction in every render context;
/// here the browser isn't making the request, so the current user's identity has to be
/// forwarded explicitly: the incoming request's Cookie header (Orchard auth cookie +
/// antiforgery cookie + culture cookie) is captured once per scope and attached to
/// every outgoing loopback request, and the antiforgery request token is fetched
/// through the same forwarded cookie exactly like the WASM handler does - no browser
/// APIs involved.
///
/// Scope capture: IHttpContextAccessor covers both server contexts - during static SSR
/// it's the page request itself; during an InteractiveServer circuit it's the SignalR
/// connection request, which carries the same cookies. Captured lazily at first send
/// and cached for the scope's lifetime, so a long-lived circuit keeps working with the
/// cookies it connected with.
/// </summary>
public sealed class CrestForwardedAuthHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler, ICrestAntiforgeryTokenStore
{
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private CrestAntiforgeryToken? _token;
    private string? _cookieHeader;
    private bool _cookieCaptured;

    public Uri? BaseAddress { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ForwardCookies(request);

        if (RequiresAntiforgeryToken(request))
        {
            var token = await GetTokenAsync(cancellationToken);
            request.Headers.TryAddWithoutValidation(token.HeaderName, token.RequestToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    public void Clear() => _token = null;

    private void ForwardCookies(HttpRequestMessage request)
    {
        if (!_cookieCaptured)
        {
            _cookieHeader = httpContextAccessor.HttpContext?.Request.Headers.Cookie.ToString();
            _cookieCaptured = true;
        }

        if (!string.IsNullOrEmpty(_cookieHeader) && !request.Headers.Contains("Cookie"))
        {
            request.Headers.TryAddWithoutValidation("Cookie", _cookieHeader);
        }
    }

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
            ForwardCookies(request);
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

/// <summary>
/// Server-side ICrestCultureCookieWriter: deliberately does nothing. The culture cookie
/// is written exclusively by the browser (crest.theme.js via the WASM handler); the
/// server only reads it, through the RequestLocalizationOptions pipeline
/// (CrestCultureCookieOptionsConfiguration). See docs/localization.md.
/// </summary>
public sealed class CrestNoOpCultureCookieWriter : ICrestCultureCookieWriter
{
    public void SetCultureCookieContext(CultureCookieContext? context)
    {
    }
}
