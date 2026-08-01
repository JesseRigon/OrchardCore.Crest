using Crest.Services;
using Crest.Icons;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Navigation;
using System.Security.Cryptography;
using System.Text;

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
            menu = await layoutService.MigrateLegacyKeysAsync(menu);
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
    string? Id,
    string? Href,
    string? Url,
    string? Target,
    string? Position,
    NavigationIcon? Icon,
    string[] Classes,
    NavigationItem[] Items,
    string? Path = null)
{
    // Menu labels (Text) are translated and must never be part of the match key - the
    // same item resolves to different Text per admin culture. Id is the only field
    // authors set explicitly for this purpose; Href/Url (a route) is culture-invariant
    // too. Path is a resource-key (LocalizedString.Name, never the translated Value)
    // structural fingerprint used only when neither Id nor a link is present, e.g. the
    // parent-only category nodes ("Configuration", "Settings") that stock OrchardCore
    // providers emit with no .Id(...) and no link.
    // If Crest is ever merged upstream into OrchardCore core, part of that merge should
    // auto-assign a stable Id to every admin MenuItem that doesn't declare one (instead
    // of leaving Id optional), and this Key fallback chain should be replaced with a
    // straight MenuItem.Id lookup.
    public string Key => !string.IsNullOrWhiteSpace(Id) ? Id : (Link ?? StableKey(Path ?? Text));
    public string? Link => !string.IsNullOrWhiteSpace(Href) ? Href : Url;

    public static NavigationItem From(MenuItem item) => From(item, null, 0);

    private static NavigationItem From(MenuItem item, string? parentPath, int index)
    {
        var path = $"{parentPath}/{item.Text.Name}#{index}";
        return new(
            item.Text.Value,
            item.Id,
            item.Href,
            item.Url,
            item.Target,
            item.Position,
            null,
            item.Classes.ToArray(),
            item.Items.OrderBy(child => child.Position, NavigationPositionComparer.Instance)
                .Select((child, childIndex) => From(child, path, childIndex))
                .ToArray(),
            path);
    }

    private static string StableKey(string input) =>
        "nav-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
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
                var result = string.Compare(x, y, StringComparison.OrdinalIgnoreCase);

                if (result != 0)
                {
                    return result;
                }

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
