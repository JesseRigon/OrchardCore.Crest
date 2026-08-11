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
}
