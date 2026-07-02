using BlazingOrchard.Services;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Navigation;
using System.Security.Cryptography;
using System.Text;

namespace BlazingOrchard.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/blazing/navigation")]
public sealed class NavigationController(
    INavigationManager navigationManager,
    BlazingAdminMenuLayoutService layoutService) : ControllerBase
{
    [HttpGet("admin")]
    public Task<ActionResult<NavigationMenu>> GetAdminMenu() => GetMenu("admin");

    [HttpGet("menus/{menuName}")]
    public async Task<ActionResult<NavigationMenu>> GetMenu(string menuName)
    {
        var items = await navigationManager.BuildMenuAsync(menuName, ControllerContext);

        var menu = new NavigationMenu(
            menuName,
            items.OrderBy(item => item.Position, NavigationPositionComparer.Instance)
                .Select(NavigationItem.From)
                .ToArray());

        if (string.Equals(menuName, "admin", StringComparison.OrdinalIgnoreCase))
        {
            menu = await layoutService.ApplyAsync(menu);
        }

        return Ok(menu);
    }
}

public sealed record NavigationMenu(string Name, NavigationItem[] Items);

public sealed record NavigationItem(
    string Text,
    string? Id,
    string? Href,
    string? Url,
    string? Target,
    string? Position,
    string? Icon,
    string[] Classes,
    NavigationItem[] Items)
{
    public string Key => !string.IsNullOrWhiteSpace(Id) ? Id : StableKey(Text, Link);
    public string? Link => !string.IsNullOrWhiteSpace(Href) ? Href : Url;

    public static NavigationItem From(MenuItem item) => new(
        item.Text.Value,
        item.Id,
        item.Href,
        item.Url,
        item.Target,
        item.Position,
        NavigationIconResolver.Resolve(item),
        item.Classes.ToArray(),
        item.Items.OrderBy(child => child.Position, NavigationPositionComparer.Instance)
            .Select(From)
            .ToArray());

    private static string StableKey(string text, string? link)
    {
        var input = $"{text}|{link}";
        return "nav-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }
}

internal static class NavigationIconResolver
{
    private static readonly Dictionary<string, string> Icons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["accesscontrol"] = "lock",
        ["admin"] = "dashboard",
        ["adminmenus"] = "menu_open",
        ["content"] = "edit_square",
        ["contentdefinition"] = "deployed_code",
        ["contentitems"] = "article",
        ["contentparts"] = "view_module",
        ["contenttypes"] = "category",
        ["cultures"] = "translate",
        ["debugging"] = "bug_report",
        ["deployments"] = "cloud_upload",
        ["design"] = "desktop_windows",
        ["features"] = "extension",
        ["general"] = "settings_applications",
        ["indexes"] = "storage",
        ["library"] = "photo_library",
        ["localization"] = "language",
        ["media"] = "perm_media",
        ["menus"] = "menu",
        ["multitenancy"] = "business",
        ["placements"] = "low_priority",
        ["profiles"] = "tune",
        ["queries"] = "manage_search",
        ["recipes"] = "restaurant_menu",
        ["roles"] = "admin_panel_settings",
        ["search"] = "search",
        ["security"] = "lock",
        ["settings"] = "settings",
        ["shortcodes"] = "code",
        ["templates"] = "description",
        ["themes"] = "palette",
        ["tools"] = "construction",
        ["users"] = "group",
        ["widgets"] = "widgets",
        ["workflows"] = "account_tree",
        ["elsa"] = "account_tree",
        ["zones"] = "grid_view"
    };

    public static string? Resolve(MenuItem item)
    {
        foreach (var key in GetKeys(item))
        {
            if (Icons.TryGetValue(key, out var icon))
            {
                return icon;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetKeys(MenuItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Id))
        {
            yield return Normalize(item.Id);
        }

        foreach (var className in item.Classes)
        {
            if (!string.IsNullOrWhiteSpace(className))
            {
                yield return Normalize(className);
            }
        }

        if (!string.IsNullOrWhiteSpace(item.Text.Value))
        {
            yield return Normalize(item.Text.Value);
        }
    }

    private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).ToArray());
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
