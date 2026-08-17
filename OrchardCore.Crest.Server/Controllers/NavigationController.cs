using Crest.Services;
using Crest.Icons;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Navigation;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/navigation")]
public sealed class NavigationController(
    ICrestRequestAccess requestAccess) : ControllerBase
{
    [HttpGet("admin")]
    public Task<ActionResult<NavigationMenu>> GetAdminMenu() => GetMenu("admin");

    // Resolves the tenant's single CrestMenuPlacement.User AdminMenu document into a
    // NavigationMenu. See CrestProfileMenuService for why this can't go through
    // INavigationManager.BuildMenuAsync the way "admin" does.
    [HttpGet("profile")]
    public async Task<ActionResult<NavigationMenu>> GetProfileMenuAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel);
        if (access is null)
        {
            return Forbid();
        }

        var profileMenuService = access.GetRequiredService<CrestProfileMenuService>();
        return Ok(await profileMenuService.BuildAsync(User, HttpContext.RequestAborted));
    }

    [HttpGet("menus/{menuName}")]
    public async Task<ActionResult<NavigationMenu>> GetMenu(string menuName)
    {
        var access = await requestAccess.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel);
        if (access is null)
        {
            return Forbid();
        }

        var navigationManager = access.GetRequiredService<INavigationManager>();
        var layoutService = access.GetRequiredService<CrestAdminMenuLayoutService>();
        var primaryNavMenuSettingsStore = access.GetRequiredService<CrestPrimaryNavMenuSettingsStore>();
        var adminSettingsNormalizer = access.GetRequiredService<CrestAdminSettingsNormalizer>();
        var iconController = access.GetRequiredService<CrestIconController>();

        // Orchard builds, authorizes, and reduces this tree for the actual
        // request user. Apply the tenant-wide Crest layout only afterwards.
        if (string.Equals(menuName, "admin", StringComparison.OrdinalIgnoreCase))
        {
            await adminSettingsNormalizer.EnsureNewMenuEnabledAsync();
        }

        var items = await navigationManager.BuildMenuAsync(menuName, ControllerContext);

        var menu = new NavigationMenu(
            menuName,
            items.OrderBy(item => item.Position, NavigationPositionComparer.Instance)
                .Select(NavigationItem.From)
                .ToArray());

        if (string.Equals(menuName, "admin", StringComparison.OrdinalIgnoreCase))
        {
            menu = await layoutService.ApplyAsync(menu);
            menu = menu with { PrimaryNavMenuSettings = await primaryNavMenuSettingsStore.GetAsync(HttpContext.RequestAborted) };
        }

        return Ok(await iconController.ResolveMenuIconsAsync(
            menu,
            string.Equals(menuName, "admin", StringComparison.OrdinalIgnoreCase) ? CrestIconController.AdminMenuChromeIconKeys : null,
            HttpContext.RequestAborted));
    }
}

public sealed record NavigationMenu(string Name, NavigationItem[] Items, IconPack? Icons = null, NavigationSeparator[]? Separators = null, CrestPrimaryNavMenuSettings? PrimaryNavMenuSettings = null);

public sealed record NavigationSeparator(string Key, string? ParentKey, int Order);

public sealed record NavigationIcon(string? Key, string Library, string? Version, string? Style, string Name, string? SvgMarkup);

public sealed record NavigationItem(
    string Text,
    string? TextKey,
    string? Id,
    string? Href,
    string? Url,
    string? Target,
    string? Position,
    NavigationIcon? Icon,
    string[] Classes,
    NavigationItem[] Items)
{
    // Menu labels (Text) are translated and must never be part of the match key - the same
    // item resolves to different Text per admin culture. Every stock OrchardCore admin
    // navigation provider now sets a stable, culture-invariant Id, so Id is the preferred
    // match key.
    //
    // TextKey is MenuItem.Text.Name: the untranslated literal a provider passed to S["..."],
    // which OrchardCore's own NavigationManager.Merge matches on and which therefore does not
    // vary by admin culture. It is a weaker identifier than Id, because it changes whenever
    // someone rewords the caption in the provider's source, but it is present on every item
    // rather than only on those whose provider bothered to set an Id. Falling back to it lets
    // an item contributed by a third-party provider with no Id still keep a stable handle
    // across culture changes, instead of having none at all.
    public string? Key => !string.IsNullOrEmpty(Id) ? Id : TextKey;
    public string? Link => !string.IsNullOrWhiteSpace(Href) ? Href : Url;

    public static NavigationItem From(MenuItem item) =>
        new(
            item.Text.Value,
            item.Text.Name,
            item.Id,
            item.Href,
            item.Url,
            item.Target,
            item.Position,
            null,
            item.Classes.ToArray(),
            item.Items.OrderBy(child => child.Position, NavigationPositionComparer.Instance)
                .Select(From)
                .ToArray());
}

internal sealed class NavigationPositionComparer : IComparer<string?>
{
    private static readonly char[] SplitChars = ['.', ':'];

    public static NavigationPositionComparer Instance { get; } = new();

    private NavigationPositionComparer()
    {
    }

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        var xParts = GetNormalizedPosition(x).Split(SplitChars);
        var yParts = GetNormalizedPosition(y).Split(SplitChars);
        var length = Math.Min(xParts.Length, yParts.Length);

        for (var i = 0; i < length; i++)
        {
            var xIsInt = TryNormalizeKnownPartition(xParts[i], out var xPosition);
            var yIsInt = TryNormalizeKnownPartition(yParts[i], out var yPosition);

            if (!xIsInt)
            {
                xIsInt = xParts[i].Length == 0 || int.TryParse(xParts[i], out xPosition);
            }

            if (!yIsInt)
            {
                yIsInt = yParts[i].Length == 0 || int.TryParse(yParts[i], out yPosition);
            }

            if (!xIsInt && !yIsInt)
            {
                // Position strings are built almost universally upstream via
                // LocalizedString.PrefixPosition(), which bakes in the TRANSLATED display
                // text (LocalizedString.ToString() returns .Value, discarding .Name - see
                // OrchardCore.Navigation.Core's PrefixPosition(LocalizedString) overload).
                // Comparing that text would sort items differently per admin culture. Treat
                // non-numeric segments as tied instead: OrderBy is a stable sort, so ties
                // fall through to the underlying provider's original (culture-invariant)
                // registration order rather than alphabetizing translated words.
                continue;
            }

            if (!xIsInt || (yIsInt && xPosition > yPosition))
            {
                return 1;
            }

            if (!yIsInt || xPosition < yPosition)
            {
                return -1;
            }
        }

        return xParts.Length.CompareTo(yParts.Length);
    }

    private static string GetNormalizedPosition(string? value)
    {
        if (value is null)
        {
            return "before.";
        }

        var trimmed = value.Trim(':').TrimEnd('.');

        return string.IsNullOrWhiteSpace(trimmed) ? "0" : trimmed;
    }

    private static bool TryNormalizeKnownPartition(string partition, out int position)
    {
        if (partition.Equals("before", StringComparison.OrdinalIgnoreCase))
        {
            position = -9999;
            return true;
        }

        if (partition.Equals("after", StringComparison.OrdinalIgnoreCase))
        {
            position = 9999;
            return true;
        }

        position = 0;
        return false;
    }
}
