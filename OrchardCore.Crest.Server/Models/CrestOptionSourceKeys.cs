namespace Crest.Models;

/// <summary>
/// Source keys name which provider a picker draws from, and are stored in field
/// settings and on the field itself. Option List sources are qualified by the list's
/// logical key, so one provider serves every list.
/// </summary>
public static class CrestOptionSourceKeys
{
    /// <summary>Provider key for tenant-editable Content Part Lists.</summary>
    public const string ContentPartListProvider = "contentpartlist";

    /// <summary>Builds the source key for an Option List, e.g.
    /// "contentpartlist:pricing.modifier-kind".</summary>
    public static string ForContentPartList(string listKey) => $"{ContentPartListProvider}:{listKey}";

    /// <summary>Splits a source key into its provider key and the provider-specific
    /// remainder ("contentpartlist:pricing.side" -> "contentpartlist", "pricing.side").</summary>
    public static (string Provider, string Qualifier) Split(string? sourceKey)
    {
        var value = sourceKey?.Trim() ?? string.Empty;
        var separator = value.IndexOf(':');
        return separator < 0
            ? (value, string.Empty)
            : (value[..separator], value[(separator + 1)..]);
    }
}
