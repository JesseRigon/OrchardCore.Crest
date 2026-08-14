namespace Crest;

/// <summary>
/// The contract between <see cref="BlazorAdminThemeMiddleware"/> and the theme-dispatching
/// document root (Components/App.razor). When the middleware authorizes an admin/login
/// page request and rewrites its path to the canonical (prefix-stripped) form for
/// MapRazorComponents' route table, it records the decision here so the App root knows
/// to render the Admin document (Admin Routes, admin head assets, admin base href)
/// instead of the Site one. Requests that never went through the middleware's admin
/// gate (theme not selected, non-admin paths) carry no marker and get the Site document.
/// </summary>
public static class CrestBlazorHosting
{
    /// <summary>
    /// HttpContext.Items key holding the shell base path ("/Admin", "/Login", or the
    /// tenant-configured equivalents) that served this request - becomes the admin
    /// document's &lt;base href&gt; (with a trailing slash appended).
    /// </summary>
    public const string ShellBasePathItem = "Crest.BlazorAdmin.ShellBasePath";

    /// <summary>
    /// HttpContext.Items key holding the original, un-rewritten request path (e.g.
    /// "/Admin/Features" before the middleware rewrote it to "/Features").
    /// </summary>
    public const string OriginalPathItem = "Crest.BlazorAdmin.OriginalPath";

    /// <summary>
    /// HttpContext.Items key holding the request's PathBase as it stood BEFORE the
    /// middleware shifted the shell base into it - i.e. Orchard's own layer: the
    /// tenant's RequestUrlPrefix (plus any host-level base). This is the base the
    /// tenant-root API surface (api/crest/*, the SignalR hubs) lives under, which is
    /// NOT the admin shell's own base - the WASM client needs it to compose API and
    /// hub URLs that survive URL-prefixed tenants.
    /// </summary>
    public const string TenantBasePathItem = "Crest.BlazorAdmin.TenantBasePath";
}
