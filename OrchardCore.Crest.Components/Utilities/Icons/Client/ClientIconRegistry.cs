namespace Crest.Icons;

public sealed class ClientIconRegistry
{
    private readonly Dictionary<string, ResolvedIcon> _icons = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Changes only when a supplied icon batch changes the locally resolved SVG data.
    /// Consumers can use it as a cheap invalidation token without re-fetching icons.
    /// </summary>
    public long Version { get; private set; }

    public void Register(IconPack? pack)
    {
        if (pack?.Icons is null)
        {
            return;
        }

        var changed = false;
        foreach (var item in pack.Icons.Values)
        {
            var resolved = new ResolvedIcon(item.Library, item.Version, item.Name, item.SvgMarkup);
            if (!_icons.TryGetValue(item.Key, out var existing) || existing != resolved)
            {
                _icons[item.Key] = resolved;
                changed = true;
            }
        }

        if (changed)
        {
            Version++;
        }
    }

    public bool TryResolve(string? key, out ResolvedIcon icon)
    {
        icon = default!;
        var normalizedKey = NormalizeKey(key);
        if (normalizedKey is null || !_icons.TryGetValue(normalizedKey, out var resolved))
        {
            return false;
        }

        icon = resolved;
        return true;
    }

    private static string? NormalizeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        // Public Crest/Iconify notation: @provider:prefix:name.
        // Packs stay keyed by the provider-neutral internal registry form.
        if (key[0] == '@')
        {
            var parts = key[1..].Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length == 3 && parts.All(part => !string.IsNullOrWhiteSpace(part)))
            {
                return $"{parts[0]}.{parts[1]}/current/default/{parts[2]}".ToLowerInvariant();
            }
        }

        return key;
    }
}
