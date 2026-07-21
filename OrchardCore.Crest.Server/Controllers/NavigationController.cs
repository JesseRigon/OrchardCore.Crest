using Crest.Services;
using Crest.Icons;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Navigation;
using System.Security.Cryptography;
using System.Text;

namespace Crest.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/crest/navigation")]
public sealed class NavigationController(
    INavigationManager navigationManager,
    CrestAdminMenuLayoutService layoutService,
    CrestIconController iconController) : ControllerBase
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

        return Ok(await iconController.ResolveMenuIconsAsync(menu, HttpContext.RequestAborted));
    }
}

public sealed record NavigationMenu(string Name, NavigationItem[] Items, IconPack? Icons = null);

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
        null,
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
