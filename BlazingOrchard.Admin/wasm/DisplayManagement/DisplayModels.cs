namespace BlazingOrchard.Admin.DisplayManagement;

public sealed record DisplayMenu(string Name, DisplayMenuItem[] Items)
{
    public static DisplayMenu Empty(string name) => new(name, []);
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
