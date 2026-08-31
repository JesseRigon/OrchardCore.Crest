using Crest.Fields;
using Crest.Models;

namespace Crest.Services;

/// <summary>
/// Resolves the technical keys behind an <see cref="OptionPickerField"/>'s stored ids.
/// Code compares keys (the identity rule), while content stores ids, so this is the
/// translation both the index and any consuming service needs.
/// </summary>
public sealed class OptionPickerFieldKeyResolver(IEnumerable<IOptionSourceProvider> providers)
{
    // "Key" is part of the provider row contract (OptionRow.Key), not something owned
    // by any single provider - the Option List provider moved to Crest.ContentPartLists, so
    // the column name is spelled here rather than referenced from it.
    private static readonly string[] KeyColumn = ["Key"];

    /// <summary>
    /// Keys for the field's selected ids, in the field's own order. Entries are null
    /// where the source identifies rows by id alone (users, content items), which is
    /// exactly what the index stores as a null SelectedKey.
    /// </summary>
    public async Task<IReadOnlyList<string?>> ResolveKeysAsync(OptionPickerField field, CancellationToken cancellationToken = default)
    {
        var ids = field.SelectedIds ?? [];
        if (ids.Length == 0)
        {
            return [];
        }

        var (providerKey, qualifier) = CrestOptionSourceKeys.Split(field.SourceKey);
        var provider = providers.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, providerKey, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            return [.. ids.Select(_ => (string?)null)];
        }

        // One batched call for every id on the field - never one call per id.
        var rows = await provider.GetByIdsAsync(qualifier, ids, KeyColumn, cancellationToken);
        var keysById = rows.ToDictionary(row => row.Id, row => row.Key, StringComparer.OrdinalIgnoreCase);

        return [.. ids.Select(id => keysById.TryGetValue(id, out var key) ? key : null)];
    }
}
