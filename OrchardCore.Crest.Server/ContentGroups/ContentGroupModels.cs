using OrchardCore.Data.Documents;

namespace Crest.ContentGroups;

/// <summary>
/// Content groups: named collections of content SOURCES (a content type, an option
/// list, later other kinds) that the admin surfaces group by - "Members", "Parties",
/// "Reference data". Groups are declared by modules through
/// <see cref="IContentGroupProvider"/> (many modules may feed one group, like admin
/// menu providers feeding one section) and reshaped by the tenant through
/// <see cref="CrestContentGroupsDocument"/>. The item itself carries nothing: the
/// group is a property of the KIND of content, never of an instance.
/// </summary>
public static class CrestContentGroupEntryKinds
{
    /// <summary>A content type, by name.</summary>
    public const string Type = "type";

    /// <summary>A Content Part List, by its logical key (resolved by the lists feature).</summary>
    public const string List = "list";
}

/// <summary>Who put a group or entry there: module code or the tenant.</summary>
public static class CrestContentGroupSources
{
    public const string Module = "Module";
    public const string Tenant = "Tenant";
}

/// <summary>A reference to one content source inside a group.</summary>
public sealed record CrestContentGroupEntryRef(string Kind, string Key)
{
    public bool Matches(string kind, string key) =>
        string.Equals(Kind, kind, StringComparison.OrdinalIgnoreCase) && string.Equals(Key, key, StringComparison.OrdinalIgnoreCase);
}

/// <summary>What a module declared (before tenant overrides are applied).</summary>
public sealed record CrestContentGroupDefinition(string Key, string DisplayName, int Position, IReadOnlyList<CrestContentGroupEntryRef> Entries);

/// <summary>An entry resolved to what it actually covers: the content types and/or
/// the specific content items its kind maps to. Either list may be empty.</summary>
public sealed record CrestContentGroupResolvedEntry(
    string Kind,
    string Key,
    string DisplayName,
    IReadOnlyList<string> ContentTypes,
    IReadOnlyList<string> ContentItemIds);

/// <summary>The effective group as the API exposes it.</summary>
public sealed record CrestContentGroupModel(
    string Key,
    string DisplayName,
    int Position,
    bool Hidden,
    string Source,
    IReadOnlyList<CrestContentGroupEntryModel> Entries);

public sealed record CrestContentGroupEntryModel(string Kind, string Key, string DisplayName, string Source, bool Resolved);

/// <summary>What a group filter expands to on the content-items query.</summary>
public sealed record CrestContentGroupFilter(IReadOnlyList<string> ContentTypes, IReadOnlyList<string> ContentItemIds)
{
    public bool IsEmpty => ContentTypes.Count == 0 && ContentItemIds.Count == 0;
}

/// <summary>Tenant-level switches for content groups (site settings).</summary>
public sealed class CrestContentGroupsSettings
{
    /// <summary>When on, every visible group gets its own Content-menu entry linking
    /// to the content-items page filtered by that group - alongside (not instead of)
    /// the per-type entries, so a tenant can work by type, by group, or both.</summary>
    public bool AutoMenuPages { get; set; }
}

/// <summary>
/// The tenant's reshaping of provider-declared groups: renames, reordering, hiding,
/// entries added or removed, and wholly tenant-owned groups. Provider defaults are
/// re-derived from code on every read, so a module's later changes still surface
/// and a tenant's edits survive them - the same relationship the admin-menu layout
/// document has to navigation providers.
/// </summary>
public sealed class CrestContentGroupsDocument : Document
{
    public Dictionary<string, CrestContentGroupOverride> Groups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CrestContentGroupOverride
{
    /// <summary>True for a group the tenant created (no provider declares it).</summary>
    public bool TenantOwned { get; set; }

    public string? DisplayName { get; set; }

    public int? Position { get; set; }

    public bool Hidden { get; set; }

    public List<CrestContentGroupEntryRef> Added { get; set; } = [];

    /// <summary>Provider-declared entries the tenant took out of this group.</summary>
    public List<CrestContentGroupEntryRef> Removed { get; set; } = [];
}
