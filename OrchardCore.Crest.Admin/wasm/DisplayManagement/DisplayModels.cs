namespace Crest.Admin.DisplayManagement;

public sealed record DisplayMenu(string Name, DisplayMenuItem[] Items, DisplayMenuSeparator[] Separators, DisplaySidebarSettings SidebarSettings)
{
    public static DisplayMenu Empty(string name) => new(name, [], [], DisplaySidebarSettings.Default);
}

public sealed record DisplayMenuSeparator(string Key, string? ParentKey, int Order);

public sealed class DisplaySidebarSettings
{
    public bool Collapsible { get; init; } = true;
    public int ExpansionDurationMilliseconds { get; init; } = 500;
    public string[] TierIndents { get; init; } = ["0rem", "0.75rem", "1.25rem", "1.75rem"];
    public string[] TierBackgrounds { get; init; } = ["transparent", "transparent", "var(--rz-base-100, color-mix(in srgb, var(--rz-base-background-color) 88%, var(--rz-text-color) 12%))", "transparent"];
    public bool[] TierSeparators { get; init; } = [true, false, false];
    public string[] TierBaseSizes { get; init; } = ["1rem", "0.95rem", "0.9rem"];

    public static DisplaySidebarSettings Default { get; } = new();
}

public sealed record DisplayIcon(string? Key, string Library, string? Version, string? Style, string Name, string? SvgMarkup);

public sealed record DisplayMenuItem(
    string Text,
    string Key,
    string? Id,
    string? Href,
    string? Url,
    string? Target,
    string? Position,
    DisplayIcon? Icon,
    string[] Classes,
    DisplayMenuItem[] Items)
{
    public string? Link => !string.IsNullOrWhiteSpace(Href) ? Href : Url;
    public string StableKey => Key;
    public bool HasLink => !string.IsNullOrWhiteSpace(Link);
    public bool HasChildren => Items.Length > 0;
}
