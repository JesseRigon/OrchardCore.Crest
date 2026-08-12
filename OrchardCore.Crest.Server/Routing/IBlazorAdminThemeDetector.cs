using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Environment.Extensions;

namespace Crest.Routing;

// Single source of truth for "is the Blazor admin theme the tenant's currently active
// admin theme" - the same question BlazorAdminThemeMiddleware, RouteGateMatcherPolicy,
// and AdminRouteComponentTableProvider/DefaultRouteComponentTableManager each used to ask
// with their own, independently-typed logic (a raw theme-id string equality check that
// silently drifted from the real "is this theme id OR does it carry the blazor tag"
// rule the middleware alone had gotten right). Scoped: IAdminThemeService itself is
// scoped, and this is only ever consulted from within an active request/shell scope
// (the middleware's shell-host fallback path for pre-scope checks stays inline there -
// it is not "is Blazor active", it is "how do I get a scope to ask that question in").
public interface IBlazorAdminThemeDetector
{
    Task<bool> IsBlazorAdminThemeActiveAsync();
}

public sealed class BlazorAdminThemeDetector(
    IAdminThemeService adminThemeService,
    IOptions<BlazorAdminThemeOptions> options,
    ILogger<BlazorAdminThemeDetector> logger) : IBlazorAdminThemeDetector
{
    public async Task<bool> IsBlazorAdminThemeActiveAsync()
    {
        var adminThemeName = await adminThemeService.GetAdminThemeNameAsync();
        var adminTheme = await adminThemeService.GetAdminThemeAsync();
        var hasBlazorTag = HasBlazorTag(adminTheme, options.Value.BlazorThemeTag);
        var isBlazorAdminTheme = string.Equals(adminThemeName, options.Value.BlazorAdminThemeId, StringComparison.OrdinalIgnoreCase) || hasBlazorTag;

        logger.LogDebug(
            "Blazor admin theme check: selected admin theme name '{AdminThemeName}', resolved extension '{ExtensionId}', has '{Tag}' tag: {HasBlazorTag}, serving Blazor: {ServeBlazor}.",
            adminThemeName,
            adminTheme?.Id,
            options.Value.BlazorThemeTag,
            hasBlazorTag,
            isBlazorAdminTheme);

        return isBlazorAdminTheme;
    }

    private static bool HasBlazorTag(IExtensionInfo? extension, string tag) =>
        extension?.Manifest?.Tags?.Any(candidate => string.Equals(candidate, tag, StringComparison.OrdinalIgnoreCase)) == true;
}
