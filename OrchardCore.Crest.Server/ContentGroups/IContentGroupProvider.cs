namespace Crest.ContentGroups;

/// <summary>
/// A module's declaration of the content groups it contributes to. Register next to
/// the module's navigation provider. Contributions to the same group KEY from several
/// modules merge; the first declared display name and the lowest position win, so a
/// module that only ADDS entries to another module's group passes the key alone.
/// </summary>
public interface IContentGroupProvider
{
    void Build(ContentGroupBuilder builder);
}

public sealed class ContentGroupBuilder
{
    private readonly Dictionary<string, MutableGroup> _groups = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Declares or joins a group. <paramref name="displayName"/> and
    /// <paramref name="position"/> only matter for the first declaration.</summary>
    public ContentGroupEntriesBuilder Group(string key, string? displayName = null, int? position = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A content group needs a key.", nameof(key));
        }

        if (!_groups.TryGetValue(key, out var group))
        {
            group = new MutableGroup(key.Trim());
            _groups[group.Key] = group;
        }

        if (!string.IsNullOrWhiteSpace(displayName) && group.DisplayName is null)
        {
            group.DisplayName = displayName.Trim();
        }

        if (position is not null && (group.Position is null || position < group.Position))
        {
            group.Position = position;
        }

        return new ContentGroupEntriesBuilder(group);
    }

    public IReadOnlyList<CrestContentGroupDefinition> Build() =>
        _groups.Values
            .Select(group => new CrestContentGroupDefinition(
                group.Key,
                group.DisplayName ?? group.Key,
                group.Position ?? 0,
                group.Entries.Distinct().ToArray()))
            .ToArray();

    internal sealed class MutableGroup(string key)
    {
        public string Key { get; } = key;
        public string? DisplayName { get; set; }
        public int? Position { get; set; }
        public List<CrestContentGroupEntryRef> Entries { get; } = [];
    }
}

public sealed class ContentGroupEntriesBuilder
{
    private readonly ContentGroupBuilder.MutableGroup _group;

    internal ContentGroupEntriesBuilder(ContentGroupBuilder.MutableGroup group) => _group = group;

    public ContentGroupEntriesBuilder Entry(string kind, string key)
    {
        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A content group entry needs a kind and a key.");
        }

        var entry = new CrestContentGroupEntryRef(kind.Trim().ToLowerInvariant(), key.Trim());
        if (!_group.Entries.Any(existing => existing.Matches(entry.Kind, entry.Key)))
        {
            _group.Entries.Add(entry);
        }

        return this;
    }

    public ContentGroupEntriesBuilder Type(string contentType) => Entry(CrestContentGroupEntryKinds.Type, contentType);

    public ContentGroupEntriesBuilder Types(params string[] contentTypes)
    {
        foreach (var contentType in contentTypes)
        {
            Type(contentType);
        }

        return this;
    }

    public ContentGroupEntriesBuilder List(string listKey) => Entry(CrestContentGroupEntryKinds.List, listKey);

    public ContentGroupEntriesBuilder Lists(params string[] listKeys)
    {
        foreach (var listKey in listKeys)
        {
            List(listKey);
        }

        return this;
    }
}

/// <summary>
/// Turns an entry reference into what it covers. One resolver per entry KIND; the
/// "type" kind ships here, "list" ships with the Content Part Lists feature, and a
/// module can add a kind of its own without the group system knowing about it.
/// </summary>
public interface IContentGroupEntryResolver
{
    string Kind { get; }

    /// <summary>Null when the key does not exist on this tenant (the entry stays in
    /// the group as unresolved so a module's declaration is never silently lost).</summary>
    Task<CrestContentGroupResolvedEntry?> ResolveAsync(string key, CancellationToken cancellationToken = default);
}
