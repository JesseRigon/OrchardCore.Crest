namespace Crest.Routing;

public interface IRouteComponentTableManager
{
    // No themeId parameter: the manager resolves the shell's currently-active Admin+Site
    // theme pair itself (IAdminThemeService / ISiteThemeService), the same sources
    // BlazorAdminThemeMiddleware.IsBlazorAdminThemeAsync already reads. Keying by more
    // than that live pair (e.g. every installed theme) has no consumer and was
    // deliberately rejected - see docs/BlazorWeb.md's "Route reachability" section.
    Task<RouteComponentTable> GetRouteComponentTableAsync();
}
