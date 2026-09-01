using Crest.Fields;
using Crest.Migrations;
using Crest.Models;
using Crest.Settings;
using OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentManagement.Records;
using YesSql;

namespace Crest.Services;

// Content Part Lists are stored as content items whose CrestContentPartListPart contains the
// Option items directly, so tenants edit them like any other content and Orchard's own
// version history records who changed what - no parallel override store, and no
// dependency on the Taxonomies module.
public sealed class CrestContentPartListService(
    ISession session,
    IContentManager contentManager,
    IContentDefinitionManager contentDefinitionManager) : ICrestContentPartListService
{
    // Per-request memo only. Sets are read repeatedly while resolving a document's
    // lines, but they are content items a tenant may edit at any time, so caching
    // beyond the request would need invalidation on save; scoped memoization gets the
    // hot-path win without that risk.
    private readonly Dictionary<string, CrestContentPartListModel?> _byKey = new(CrestContentPartListRules.KeyComparer);

    public async Task<IReadOnlyList<CrestContentPartListModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await session
            .Query<ContentItem, ContentItemIndex>(index =>
                index.ContentType == CrestContentPartListMigrations.ContentPartListContentType && index.Latest)
            .ListAsync();

        return [.. items.Select(ToModel).OrderBy(list => list.DisplayText, StringComparer.CurrentCultureIgnoreCase)];
    }

    public async Task<CrestContentPartListModel?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var normalized = CrestContentPartListRules.NormalizeKey(key);
        if (normalized.Length == 0)
        {
            return null;
        }

        if (_byKey.TryGetValue(normalized, out var cached))
        {
            return cached;
        }

        var item = await FindListItemAsync(normalized);
        var model = item is null ? null : ToModel(item);
        _byKey[normalized] = model;
        return model;
    }

    public async Task<IReadOnlyList<CrestOptionModel>> GetSelectableAsync(string key, CancellationToken cancellationToken = default)
    {
        var list = await GetAsync(key, cancellationToken);
        return list is null ? [] : CrestContentPartListRules.Selectable(list.Options);
    }

    public async Task<string?> ResolveKeyAsync(string listKey, string optionContentItemId, CancellationToken cancellationToken = default)
    {
        var list = await GetAsync(listKey, cancellationToken);
        return list?.Options
            .FirstOrDefault(option => string.Equals(option.ContentItemId, optionContentItemId, StringComparison.OrdinalIgnoreCase))
            ?.Key;
    }

    public async Task<string?> ResolveContentItemIdAsync(string listKey, string optionKey, CancellationToken cancellationToken = default)
    {
        var list = await GetAsync(listKey, cancellationToken);
        var normalized = CrestContentPartListRules.NormalizeKey(optionKey);
        return list?.Options
            .FirstOrDefault(option => CrestContentPartListRules.KeyComparer.Equals(option.Key, normalized))
            ?.ContentItemId;
    }

    public async Task<CrestContentPartListModel> SeedAsync(CrestContentPartListSeed seed, CancellationToken cancellationToken = default)
    {
        var listKey = CrestContentPartListRules.NormalizeKey(seed.Key);
        if (listKey.Length == 0)
        {
            throw new ArgumentException("A seeded content part list needs a key.", nameof(seed));
        }

        var listItem = await FindListItemAsync(listKey)
            ?? await CreateListItemAsync(listKey, seed.DisplayText, CrestOptionSources.Module);

        // Module locks are the developer's contract, re-asserted on every reseed -
        // unlike sort or labels there is no tenant edit of them to preserve (the API
        // refuses to touch a Module lock). Asserted, never cleared: dropping a lock
        // from a seed downgrades to whatever the tenant state says.
        var currentPart = listItem.As<CrestContentPartListPart>();
        var wantsDataLock = seed.DataLock && currentPart?.DataLock != CrestContentPartListLockSources.Module;
        var wantsEditLock = seed.EditLock && currentPart?.EditLock != CrestContentPartListLockSources.Module;
        if (wantsDataLock || wantsEditLock)
        {
            listItem.Alter<CrestContentPartListPart>(part =>
            {
                part.DataLock = seed.DataLock ? CrestContentPartListLockSources.Module : part.DataLock;
                part.EditLock = seed.EditLock ? CrestContentPartListLockSources.Module : part.EditLock;
            });
            await contentManager.UpdateAsync(listItem);
            await contentManager.PublishAsync(listItem);
        }

        // Existing options are matched by key and left untouched, so a tenant's
        // relabel/hide survives every reseed.
        var existingOptions = ToModel(listItem).Options;
        var plan = CrestContentPartListRules.PlanSeed(seed, existingOptions);
        if (plan.HasChanges)
        {
            // Seeds that don't set explicit positions get sequential ones in seed
            // order, appended after what the list already holds - the manual order
            // (and the Position index column) starts meaningful instead of all-zero.
            var nextPosition = existingOptions.Length == 0 ? 0 : existingOptions.Max(option => option.Position) + 1;
            foreach (var addition in plan.Additions)
            {
                var position = addition.Position != 0 ? addition.Position : nextPosition++;
                await AppendOptionAsync(listItem, addition.Key, addition.DisplayText, position, CrestOptionSources.Module, addition.Category, addition.DisplayTextPlural, addition.Value);
            }

            await contentManager.UpdateAsync(listItem);
            await contentManager.PublishAsync(listItem);
        }

        _byKey.Remove(listKey);
        return ToModel(listItem);
    }

    public async Task<CrestContentPartListModel> CreateListAsync(string key, string displayText, CancellationToken cancellationToken = default)
    {
        var normalized = CrestContentPartListRules.NormalizeKey(key);
        var existing = await ListAsync(cancellationToken);
        var validation = CrestContentPartListRules.ValidateKey(normalized, existing.Select(list => list.Key));
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Error);
        }

        var item = await CreateListItemAsync(normalized, displayText, CrestOptionSources.Tenant);
        _byKey.Remove(normalized);
        return ToModel(item);
    }

    public async Task<CrestOptionModel> AddOptionAsync(string listKey, string optionKey, string displayText, int position = 0, string? category = null, string? displayTextPlural = null, string? value = null, CancellationToken cancellationToken = default)
    {
        var listItem = await RequireListItemAsync(listKey);
        var validation = CrestContentPartListRules.ValidateKey(optionKey, ToModel(listItem).Options.Select(option => option.Key));
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Error);
        }

        var normalized = CrestContentPartListRules.NormalizeKey(optionKey);
        var option = await AppendOptionAsync(listItem, normalized, displayText, position, CrestOptionSources.Tenant, category, displayTextPlural, value);
        await contentManager.UpdateAsync(listItem);
        await contentManager.PublishAsync(listItem);
        _byKey.Remove(CrestContentPartListRules.NormalizeKey(listKey));

        return ToOptionModel(option);
    }

    public async Task<CrestOptionModel> UpdateOptionAsync(string listKey, string optionKey, string? displayText, int? position, bool? hidden, string? category = null, string? displayTextPlural = null, string? value = null, CancellationToken cancellationToken = default)
    {
        var listItem = await RequireListItemAsync(listKey);
        var normalized = CrestContentPartListRules.NormalizeKey(optionKey);
        // The mutation MUST happen inside Alter. As<T>() materializes a part from the
        // item's JSON, so options fetched outside Alter are deserialized copies -
        // changing them updates nothing, and the save silently succeeds having written
        // the original values back.
        ContentItem? option = null;

        listItem.Alter<CrestContentPartListPart>(part =>
        {
            option = part.Options.FirstOrDefault(candidate =>
                CrestContentPartListRules.KeyComparer.Equals(CrestContentPartListRules.NormalizeKey(candidate.As<CrestOptionPart>()?.Key), normalized));

            if (option is null)
            {
                return;
            }

            if (displayText is not null)
            {
                option.DisplayText = displayText;
                option.Alter<ContentPart>("TitlePart", titlePart => titlePart.Content.Title = displayText);
            }

            if (position is not null || hidden is not null || category is not null || displayTextPlural is not null || value is not null)
            {
                option.Alter<CrestOptionPart>(optionPart =>
                {
                    optionPart.Position = position ?? optionPart.Position;
                    optionPart.Hidden = hidden ?? optionPart.Hidden;
                    optionPart.Category = category is null
                        ? optionPart.Category
                        : CrestContentPartListRules.NormalizeCategory(category);
                    // Null leaves the plural alone; a blank one CLEARS it (back to the
                    // singular fallback) - there is no meaningful "empty plural".
                    optionPart.DisplayTextPlural = displayTextPlural is null
                        ? optionPart.DisplayTextPlural
                        : (string.IsNullOrWhiteSpace(displayTextPlural) ? null : displayTextPlural.Trim());
                    // Same null/blank contract for the machine value.
                    optionPart.Value = value is null
                        ? optionPart.Value
                        : (string.IsNullOrWhiteSpace(value) ? null : value.Trim());
                });
            }
        });

        if (option is null)
        {
            throw new InvalidOperationException($"Content part list '{listKey}' has no option '{optionKey}'.");
        }

        await contentManager.UpdateAsync(listItem);
        await contentManager.PublishAsync(listItem);
        _byKey.Remove(CrestContentPartListRules.NormalizeKey(listKey));

        return ToOptionModel(option);
    }

    public async Task<CrestContentPartListModel> ReorderOptionsAsync(string key, IReadOnlyList<string> orderedKeys, CancellationToken cancellationToken = default)
    {
        var listItem = await RequireListItemAsync(key);

        var validation = CrestContentPartListRules.ValidateReorder(
            ToModel(listItem).Options.Select(option => option.Key), orderedKeys);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Error);
        }

        var positionByKey = orderedKeys
            .Select((optionKey, index) => (Key: CrestContentPartListRules.NormalizeKey(optionKey), Position: index))
            .ToDictionary(entry => entry.Key, entry => entry.Position, CrestContentPartListRules.KeyComparer);

        // Mutation must happen inside Alter - see UpdateOptionAsync's comment.
        listItem.Alter<CrestContentPartListPart>(part =>
        {
            foreach (var option in part.Options)
            {
                var optionKey = CrestContentPartListRules.NormalizeKey(option.As<CrestOptionPart>()?.Key);
                if (positionByKey.TryGetValue(optionKey, out var position))
                {
                    option.Alter<CrestOptionPart>(optionPart => optionPart.Position = position);
                }
            }
        });

        await contentManager.UpdateAsync(listItem);
        await contentManager.PublishAsync(listItem);
        _byKey.Remove(CrestContentPartListRules.NormalizeKey(key));

        return ToModel(listItem);
    }

    public async Task<CrestContentPartListModel> UpdateListLocksAsync(string key, bool? dataLock, bool? editLock, CancellationToken cancellationToken = default)
    {
        var listItem = await RequireListItemAsync(key);

        listItem.Alter<CrestContentPartListPart>(part =>
        {
            part.DataLock = ApplyTenantLock(part.DataLock, dataLock, "categories & data");
            part.EditLock = ApplyTenantLock(part.EditLock, editLock, "editing");
        });

        await contentManager.UpdateAsync(listItem);
        await contentManager.PublishAsync(listItem);
        _byKey.Remove(CrestContentPartListRules.NormalizeKey(key));

        return ToModel(listItem);
    }

    // One mechanism, two authorities: this path is the TENANT authority, so a
    // Module-placed lock is out of reach in both directions.
    private static string ApplyTenantLock(string current, bool? wanted, string lockName)
    {
        if (wanted is null)
        {
            return current;
        }

        if (string.Equals(current, CrestContentPartListLockSources.Module, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The {lockName} lock on this list was placed by the owning module and cannot be changed from the tenant.");
        }

        return wanted.Value ? CrestContentPartListLockSources.Tenant : CrestContentPartListLockSources.None;
    }

    public async Task DeleteListAsync(string key, CancellationToken cancellationToken = default)
    {
        var listItem = await RequireListItemAsync(key);

        // A module-seeded list's key is that module's contract with code; deleting it
        // would break the consuming module until the next reseed silently recreated an
        // empty list. Tenants hide module options instead.
        var source = listItem.As<CrestContentPartListPart>()?.Source ?? CrestOptionSources.Tenant;
        if (string.Equals(source, CrestOptionSources.Module, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The content part list '{key}' is provided by a module and cannot be deleted. Hide its options instead.");
        }

        // The options live inside this item's own document, so removing the list
        // removes them with it - there are no orphan Option items to sweep.
        await contentManager.RemoveAsync(listItem);
        _byKey.Remove(CrestContentPartListRules.NormalizeKey(key));
    }

    public async Task AttachToContentTypeAsync(string listKey, string contentType, string fieldName, string? displayName = null, CancellationToken cancellationToken = default)
    {
        var listItem = await RequireListItemAsync(listKey);

        var sourceKey = CrestOptionSourceKeys.ForContentPartList(CrestContentPartListRules.NormalizeKey(listKey));

        // WithField updates an EXISTING field's settings but keeps its original type,
        // so converting a field that used to be (say) a TextField needs it removed
        // first - otherwise the settings say "option picker" while the field is still
        // text. Stored content is untouched by this: the field's own data stays in the
        // item's document, which is what lets readers fall back to the previous shape
        // for content written before the conversion.
        var existing = await contentDefinitionManager.GetPartDefinitionAsync(contentType);
        var current = existing?.Fields.FirstOrDefault(field =>
            string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase));

        var position = current?.Settings?["ContentPartFieldSettings"]?["Position"]?.ToString();

        if (current is not null && !string.Equals(current.FieldDefinition?.Name, nameof(OptionPickerField), StringComparison.Ordinal))
        {
            await contentDefinitionManager.AlterPartDefinitionAsync(contentType, part => part.RemoveField(fieldName));
        }

        await contentDefinitionManager.AlterPartDefinitionAsync(contentType, part => part
            .WithField(fieldName, field =>
            {
                field
                    .OfType(nameof(OptionPickerField))
                    .WithDisplayName(displayName ?? fieldName)
                    .MergeSettings<OptionPickerFieldSettings>(settings =>
                    {
                        settings.SourceKey = sourceKey;
                        settings.Multiple = false;
                    });

                // Keep the field where the editor already put it.
                if (!string.IsNullOrWhiteSpace(position))
                {
                    field.WithPosition(position);
                }
            }));
    }

    private async Task<ContentItem?> FindListItemAsync(string normalizedKey)
    {
        var items = await session
            .Query<ContentItem, ContentItemIndex>(index =>
                index.ContentType == CrestContentPartListMigrations.ContentPartListContentType && index.Latest)
            .ListAsync();

        return items.FirstOrDefault(item =>
            CrestContentPartListRules.KeyComparer.Equals(
                CrestContentPartListRules.NormalizeKey(item.As<CrestContentPartListPart>()?.Key),
                normalizedKey));
    }

    private async Task<ContentItem> RequireListItemAsync(string listKey)
    {
        var normalized = CrestContentPartListRules.NormalizeKey(listKey);
        return await FindListItemAsync(normalized)
            ?? throw new InvalidOperationException($"There is no content part list with the key '{listKey}'.");
    }

    private async Task<ContentItem> CreateListItemAsync(string key, string displayText, string source)
    {
        var item = await contentManager.NewAsync(CrestContentPartListMigrations.ContentPartListContentType);
        item.DisplayText = displayText;
        item.Alter<ContentPart>("TitlePart", part => part.Content.Title = displayText);
        item.Alter<CrestContentPartListPart>(part =>
        {
            part.Key = key;
            part.Source = source;
            part.OptionContentType = CrestContentPartListMigrations.OptionContentType;
        });

        await contentManager.CreateAsync(item, VersionOptions.Draft);
        await contentManager.PublishAsync(item);
        return item;
    }

    private async Task<ContentItem> AppendOptionAsync(ContentItem listItem, string key, string displayText, int position, string source, string? category = null, string? displayTextPlural = null, string? value = null)
    {
        var option = await contentManager.NewAsync(CrestContentPartListMigrations.OptionContentType);
        option.DisplayText = displayText;
        option.Alter<ContentPart>("TitlePart", part => part.Content.Title = displayText);
        option.Alter<CrestOptionPart>(part =>
        {
            part.Key = key;
            part.Source = source;
            part.Position = position;
            part.Category = CrestContentPartListRules.NormalizeCategory(category);
            part.DisplayTextPlural = string.IsNullOrWhiteSpace(displayTextPlural) ? null : displayTextPlural.Trim();
            part.Value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            part.ContentPartListContentItemId = listItem.ContentItemId;
        });

        listItem.Alter<CrestContentPartListPart>(part => part.Options.Add(option));
        return option;
    }

    private static CrestContentPartListModel ToModel(ContentItem item)
    {
        var listPart = item.As<CrestContentPartListPart>();
        var options = listPart?.Options ?? [];

        return new CrestContentPartListModel(
            item.ContentItemId,
            CrestContentPartListRules.NormalizeKey(listPart?.Key),
            item.DisplayText ?? string.Empty,
            listPart?.Source ?? CrestOptionSources.Tenant,
            string.IsNullOrEmpty(listPart?.DataLock) ? CrestContentPartListLockSources.None : listPart.DataLock,
            string.IsNullOrEmpty(listPart?.EditLock) ? CrestContentPartListLockSources.None : listPart.EditLock,
            [.. CrestContentPartListRules.Ordered(options.Select(ToOptionModel))]);
    }

    private static CrestOptionModel ToOptionModel(ContentItem option)
    {
        var part = option.As<CrestOptionPart>();
        return new CrestOptionModel(
            option.ContentItemId,
            CrestContentPartListRules.NormalizeKey(part?.Key),
            option.DisplayText ?? string.Empty,
            part?.Source ?? CrestOptionSources.Tenant,
            part?.Hidden ?? false,
            part?.Position ?? 0,
            CrestContentPartListRules.NormalizeCategory(part?.Category),
            string.IsNullOrWhiteSpace(part?.DisplayTextPlural) ? null : part.DisplayTextPlural.Trim(),
            string.IsNullOrWhiteSpace(part?.Value) ? null : part.Value.Trim());
    }
}
