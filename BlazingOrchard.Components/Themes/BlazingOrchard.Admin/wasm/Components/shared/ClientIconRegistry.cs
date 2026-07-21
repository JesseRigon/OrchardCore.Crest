namespace BlazingOrchard.Icons;

public sealed class ClientIconRegistry
{
    private readonly Dictionary<string, ResolvedIcon> _icons = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IconPack? pack)
    {
        if (pack?.Icons is null)
        {
            return;
        }

        foreach (var item in pack.Icons.Values)
        {
            _icons[item.Key] = new ResolvedIcon(item.Library, item.Version, item.Name, item.SvgMarkup);
        }
    }

    public bool TryResolve(string? key, out ResolvedIcon icon)
    {
        icon = default!;
        if (string.IsNullOrWhiteSpace(key) || !_icons.TryGetValue(key, out var resolved))
        {
            return false;
        }

        icon = resolved;
        return true;
    }
}

public sealed record IconPack(string Version, IReadOnlyDictionary<string, IconPackItem> Icons);

public sealed record IconPackItem(
    string Key,
    string Library,
    string Version,
    string Style,
    string Name,
    string SvgMarkup,
    string? Attribution = null,
    string? License = null);
