using Crest.Fields;
using Crest.Models;
using Crest.Settings;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace Crest.Services;

/// <summary>
/// Turns a parent field's RAW values into COMPARISON values. A scalar parent (text,
/// numeric, boolean) already stores what logic compares, so its values pass through.
/// An OptionPickerField parent stores ids - per the identity rule content stores ids
/// while CODE COMPARES KEYS - so its selection is resolved through its own source's
/// GetByIdsAsync and a column is read from the rows (Key by default). This is the
/// seam both cascading filters and field-visibility conditions resolve through.
/// </summary>
public sealed class OptionParentValueResolver(
    IEnumerable<IOptionSourceProvider> sourceProviders,
    IContentDefinitionManager contentDefinitionManager)
{
    /// <summary>The column name meaning "the option's technical key" (the default).</summary>
    public const string KeyColumn = "Key";

    /// <summary>
    /// Resolves the comparison values <paramref name="rawValues"/> stand for. Paths
    /// are "Field" (a field on the content type's implicit part) or "Part.Field".
    /// An unknown parent field, or a picker parent whose provider is missing,
    /// resolves to EMPTY - failing closed, so a dependent restriction never silently
    /// widens into "matches everything".
    /// </summary>
    public async Task<IReadOnlyList<string>> ResolveAsync(
        string contentType,
        string path,
        string? column,
        IReadOnlyList<string> rawValues,
        CancellationToken cancellationToken = default)
    {
        if (rawValues.Count == 0)
        {
            return rawValues;
        }

        var field = await FindFieldAsync(contentType, path);
        if (field is null)
        {
            return [];
        }

        if (!string.Equals(field.FieldDefinition?.Name, nameof(OptionPickerField), StringComparison.Ordinal))
        {
            // Scalar parents: the stored value IS the comparison value.
            return rawValues;
        }

        var sourceKey = field.GetSettings<OptionPickerFieldSettings>()?.SourceKey;
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            return [];
        }

        var (providerKey, qualifier) = CrestOptionSourceKeys.Split(sourceKey);
        var provider = sourceProviders.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, providerKey, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            return [];
        }

        var wantsKey = IsKeyColumn(column);
        var rows = await provider.GetByIdsAsync(qualifier, rawValues, wantsKey ? [] : [column!], cancellationToken);
        return Project(rows, column);
    }

    /// <summary>
    /// Projects resolved parent rows to the comparison column. Pure so the id-to-key
    /// translation - the crux of the cascade fix - is testable without providers.
    /// Null or "Key" reads the option's technical Key, falling back to the row id
    /// for entity sources whose id is their identity.
    /// </summary>
    internal static IReadOnlyList<string> Project(IReadOnlyList<OptionRow> rows, string? column)
    {
        var wantsKey = IsKeyColumn(column);

        return [.. rows
            .Select(row => wantsKey
                ? (string.IsNullOrWhiteSpace(row.Key) ? row.Id : row.Key)
                : (row.Values.TryGetValue(column!, out var value) ? value : null))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static bool IsKeyColumn(string? column) =>
        string.IsNullOrWhiteSpace(column) || string.Equals(column, KeyColumn, StringComparison.OrdinalIgnoreCase);

    private async Task<ContentPartFieldDefinition?> FindFieldAsync(string contentType, string path)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length is 0 or > 2)
        {
            return null;
        }

        // "Field" addresses the content type's implicit part (named after the type),
        // matching how attachments and editor state name their siblings.
        var partName = segments.Length == 2 ? segments[0] : contentType;
        var partDefinition = await contentDefinitionManager.GetPartDefinitionAsync(partName);

        return partDefinition?.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, segments[^1], StringComparison.OrdinalIgnoreCase));
    }
}
