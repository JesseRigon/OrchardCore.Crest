using Crest.Fields;
using Crest.Models;
using Crest.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentFields.Indexing.SQL;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using YesSql.Indexes;

namespace Crest.Indexing;

// Index rows for OptionPickerField selections.
//
// PARTITIONED PER CONTENT TYPE: each partition is its own index CLASS, and therefore
// its own SQL table, so a tenant's transactions never share a table with its parties.
// A business writing ~1M transactions a year would otherwise pile tens of millions of
// rows into one table over a decade. Content types (not modules) are the partition
// unit because types are the stable thing - ownership of a type can move between
// modules over time.
//
// Adding a partition is a declaration plus a migration, not new mapping code: derive
// an index class and register a provider bound to the content types it covers.
//
// Index tables are inherently tenant-scoped - TablePrefix is a per-tenant shell
// setting applied at YesSql store configuration, so tenants get physically separate
// (or prefixed) tables. There is no tenant discriminator column and no cross-tenant
// query path.
public abstract class OptionPickerFieldIndexBase : ContentFieldIndex
{
    /// <summary>Which source the picker drew from, e.g.
    /// "contentpartlist:transaction.status".</summary>
    public string SourceKey { get; set; }

    /// <summary>One row per selected id, so a multi-select is found by a plain
    /// equality predicate.</summary>
    public string SelectedId { get; set; }

    /// <summary>
    /// The option's technical key, when the source has one distinct from its id.
    /// Code compares KEYS (the identity rule), so this is what makes
    /// "WHERE SourceKey = ... AND SelectedKey = 'Posted'" possible without joining
    /// back to resolve the id. Null for entity sources like users, whose id is their
    /// identity.
    /// </summary>
    public string SelectedKey { get; set; }
}

/// <summary>The default partition: every content type without a dedicated one.</summary>
public class OptionPickerFieldIndex : OptionPickerFieldIndexBase;

/// <summary>
/// Maps content items to option-picker index rows. Derive one per partition and give
/// it the content types it owns; the shared logic below never changes.
/// </summary>
public abstract class OptionPickerFieldIndexProviderBase<TIndex>(IServiceProvider serviceProvider) : ContentFieldIndexProvider
    where TIndex : OptionPickerFieldIndexBase, new()
{
    private readonly HashSet<string> _ignoredTypes = [];
    private IContentDefinitionManager _contentDefinitionManager;
    private OptionPickerFieldKeyResolver _keyResolver;

    /// <summary>The content types this partition indexes. An empty set means "every
    /// type not claimed by another partition".</summary>
    protected abstract IReadOnlySet<string> PartitionContentTypes { get; }

    /// <summary>Content types claimed by other partitions, which the default
    /// partition must therefore skip.</summary>
    protected virtual IReadOnlySet<string> ExcludedContentTypes => new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<TIndex>()
            .Map(async contentItem =>
            {
                // Skip soft-deleted items, matching stock field index providers.
                if (!contentItem.Published && !contentItem.Latest)
                {
                    return null;
                }

                if (!OwnsContentType(contentItem.ContentType) || _ignoredTypes.Contains(contentItem.ContentType))
                {
                    return null;
                }

                // Lazy: IContentDefinitionManager cannot be injected here (ISession
                // cyclic dependency), same workaround the stock providers use.
                _contentDefinitionManager ??= serviceProvider.GetRequiredService<IContentDefinitionManager>();

                var typeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);
                if (typeDefinition is null)
                {
                    // Orphaned items (e.g. widgets of a removed layer) have no definition.
                    _ignoredTypes.Add(contentItem.ContentType);
                    return null;
                }

                var fieldDefinitions = typeDefinition
                    .Parts.SelectMany(part => part.PartDefinition.Fields.Where(field => field.FieldDefinition.Name == nameof(OptionPickerField)))
                    .ToArray();

                if (fieldDefinitions.Length == 0)
                {
                    _ignoredTypes.Add(contentItem.ContentType);
                    return null;
                }

                var rows = new List<TIndex>();

                foreach (var pair in fieldDefinitions.GetContentFields<OptionPickerField>(contentItem))
                {
                    var ids = (pair.Field.SelectedIds ?? []).Where(id => !string.IsNullOrEmpty(id)).ToArray();
                    if (ids.Length == 0)
                    {
                        continue;
                    }

                    // Resolved in ONE batched call per field: code compares keys, so
                    // storing them here is what lets a query filter by key without
                    // joining back through the source. Null where the source has no
                    // key distinct from the id (users and other entity sources).
                    _keyResolver ??= serviceProvider.GetRequiredService<OptionPickerFieldKeyResolver>();
                    var keys = await _keyResolver.ResolveKeysAsync(pair.Field);

                    for (var index = 0; index < ids.Length; index++)
                    {
                        rows.Add(new TIndex
                        {
                            Latest = contentItem.Latest,
                            Published = contentItem.Published,
                            ContentItemId = contentItem.ContentItemId,
                            ContentItemVersionId = contentItem.ContentItemVersionId,
                            ContentType = contentItem.ContentType,
                            ContentPart = pair.Definition.ContentTypePartDefinition.Name,
                            ContentField = pair.Definition.Name,
                            SourceKey = pair.Field.SourceKey ?? string.Empty,
                            SelectedId = ids[index],
                            SelectedKey = index < keys.Count ? keys[index] : null,
                        });
                    }
                }

                return rows;
            });
    }

    private bool OwnsContentType(string contentType) =>
        PartitionContentTypes.Count > 0
            ? PartitionContentTypes.Contains(contentType)
            : !ExcludedContentTypes.Contains(contentType);
}

/// <summary>Provider for the catch-all partition.</summary>
public class OptionPickerFieldIndexProvider(IServiceProvider serviceProvider, CrestOptionIndexPartitions partitions)
    : OptionPickerFieldIndexProviderBase<OptionPickerFieldIndex>(serviceProvider)
{
    protected override IReadOnlySet<string> PartitionContentTypes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlySet<string> ExcludedContentTypes { get; } = partitions.DedicatedContentTypes;
}

/// <summary>
/// The content types that have their own index partition. Modules add to this at
/// startup; the catch-all partition skips whatever is registered here.
/// </summary>
public sealed class CrestOptionIndexPartitions
{
    private readonly HashSet<string> _contentTypes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> DedicatedContentTypes => _contentTypes;

    public void Add(params string[] contentTypes)
    {
        foreach (var contentType in contentTypes)
        {
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                _contentTypes.Add(contentType);
            }
        }
    }
}
