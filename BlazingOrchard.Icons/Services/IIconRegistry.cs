namespace BlazingOrchard.Icons;

public interface IIconRegistry
{
    ValueTask<IReadOnlyList<IconLibraryDescriptor>> GetLibrariesAsync(CancellationToken cancellationToken = default);

    ValueTask<IconAssetDefinition?> ResolveAsync(IconKey key, CancellationToken cancellationToken = default);

    ValueTask<IconAssetDefinition?> ResolveDeclarationAsync(string declaration, CancellationToken cancellationToken = default);

    ValueTask<IconSearchResult> SearchAsync(IconSearchRequest request, CancellationToken cancellationToken = default);

    ValueTask<IconPack> BuildPackAsync(IEnumerable<IconKey> keys, CancellationToken cancellationToken = default);
}
