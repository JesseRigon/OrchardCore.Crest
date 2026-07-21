namespace BlazingOrchard.Icons;

public interface IIconProvider
{
    string Id { get; }

    string Name { get; }

    ValueTask<IReadOnlyList<IconLibraryDescriptor>> GetLibrariesAsync(CancellationToken cancellationToken = default);

    ValueTask<IconAssetDefinition?> ResolveAsync(IconKey key, CancellationToken cancellationToken = default);

    ValueTask<IconAssetDefinition?> ResolveDeclarationAsync(string declaration, CancellationToken cancellationToken = default);

    ValueTask<IconSearchResult> SearchAsync(IconSearchRequest request, CancellationToken cancellationToken = default);

    ValueTask<string> GetVersionAsync(CancellationToken cancellationToken = default);
}
