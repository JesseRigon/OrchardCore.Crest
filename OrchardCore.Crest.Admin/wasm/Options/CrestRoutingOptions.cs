namespace Crest.Admin.Options;

// AdminPath/LoginPath hold the tenant's real, absolute admin/login URLs (e.g.
// "/backoffice"), fetched anonymously from CrestRoutingController at boot (see
// Program.cs) so components (e.g. AdminTitleBar's logout redirect, Login.razor's
// post-login redirect) can build real, navigable cross-shell URLs without knowing
// about <base href> rewriting. The Canonical*Path constants below are only the
// fallback used if that fetch fails - they match AdminOptions.AdminUrlPrefix's own
// stock default ("Admin") and UserOptions.LoginPath's own stock default ("Login"),
// not some Crest-specific "canonical shape" - every @page directive in this app is
// itself already the real page path with no prefix baked in (Blazor's Router
// resolves them relative to BaseUri, which the server rewrites to the tenant's real
// prefix - see BlazorAdminThemeMiddleware's PathBase shift).
public sealed class CrestRoutingOptions
{
    public const string CanonicalAdminPath = "/Admin";
    public const string CanonicalLoginPath = "/login";

    public string AdminPath { get; set; } = CanonicalAdminPath;
    public string LoginPath { get; set; } = CanonicalLoginPath;
}
