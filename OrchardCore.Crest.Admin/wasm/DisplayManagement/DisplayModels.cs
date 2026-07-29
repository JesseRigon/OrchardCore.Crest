namespace Crest.Admin.DisplayManagement;

public sealed record DisplayMenu(string Name, DisplayMenuItem[] Items, DisplayMenuSeparator[] Separators, DisplayPrimaryNavMenuSettings PrimaryNavMenuSettings)
{
    public static DisplayMenu Empty(string name) => new(name, [], [], DisplayPrimaryNavMenuSettings.Default);
}

public sealed record DisplayMenuSeparator(string Key, string? ParentKey, int Order);

// Available options are expected to grow (more anchor corners, responsive-size-specific
// choices) — keep this an open enum rather than a bool.
public enum PrimaryNavMenuCollapseIconPosition
{
    OutsideBottomRight,
    InsideBottomLeft,
}

public sealed class DisplayPrimaryNavMenuSettings
{
    public bool Collapsible { get; init; } = true;
    public int ExpansionDurationMilliseconds { get; init; } = 500;
    public string[] TierIndents { get; init; } = ["0rem", "0.75rem", "1.25rem", "1.75rem"];
    public string[] TierBackgrounds { get; init; } = ["transparent", "transparent", "color-mix(in srgb, var(--crest-color-surface-1) 88%, var(--crest-color-text-1) 12%)", "transparent"];
    public bool[] TierSeparators { get; init; } = [true, false, false];
    public string[] TierBaseSizes { get; init; } = ["1rem", "0.95rem", "0.9rem"];
    public PrimaryNavMenuCollapseIconPosition CollapseIconPosition { get; init; } = PrimaryNavMenuCollapseIconPosition.OutsideBottomRight;

    public static DisplayPrimaryNavMenuSettings Default { get; } = new();
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
