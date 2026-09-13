using OrchardCore.ContentManagement.Metadata;

namespace Crest.ContentGroups;

/// <summary>The built-in entry kind: a content type, covering every item of it.</summary>
public sealed class ContentTypeGroupEntryResolver(IContentDefinitionManager contentDefinitionManager) : IContentGroupEntryResolver
{
    public string Kind => CrestContentGroupEntryKinds.Type;

    public async Task<CrestContentGroupResolvedEntry?> ResolveAsync(string key, CancellationToken cancellationToken = default)
    {
        var definition = await contentDefinitionManager.GetTypeDefinitionAsync(key);
        return definition is null
            ? null
            : new CrestContentGroupResolvedEntry(Kind, definition.Name, definition.DisplayName, [definition.Name], []);
    }
}
