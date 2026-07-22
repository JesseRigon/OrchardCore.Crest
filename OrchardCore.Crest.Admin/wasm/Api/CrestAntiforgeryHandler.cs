using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Crest.Admin.Api;

/// <summary>
/// Adds Orchard's antiforgery request token to every unsafe same-origin Crest
/// request. Authentication continues to use the browser's Orchard cookie.
/// </summary>
public sealed class CrestAntiforgeryHandler : DelegatingHandler, ICrestAntiforgeryTokenStore
{
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private CrestAntiforgeryToken? _token;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        if (RequiresAntiforgeryToken(request))
        {
            var token = await GetTokenAsync(cancellationToken);
            request.Headers.TryAddWithoutValidation(token.HeaderName, token.RequestToken);
        }

        return await base.SendAsync(request, cancellationToken);
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

            using var request = new HttpRequestMessage(HttpMethod.Get, "api/crest/antiforgery/token");
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

public sealed record CrestAntiforgeryToken(string HeaderName, string RequestToken);
