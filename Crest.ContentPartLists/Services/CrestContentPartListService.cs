using System.Text.Json.Dynamic;
using System.Text.Json.Nodes;
using Crest.Fields;
using Crest.Migrations;
using Crest.Models;
using Crest.Settings;
using OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
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

    // Custom-field descriptors per option content type, memoized alongside _byKey for
    // the same reason: the type definition is read for every model projection.
    private readonly Dictionary<string, CrestOptionFieldModel[]> _fieldsByType = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<CrestContentPartListModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await session
            .Query<ContentItem, ContentItemIndex>(index =>
                index.ContentType == CrestContentPartListMigrations.ContentPartListContentType && index.Latest)
            .ListAsync();

        var models = new List<CrestContentPartListModel>();
        foreach (var item in items)
        {
            models.Add(ToModel(item, await GetOptionFieldsAsync(item)));
        }

        return [.. models.OrderBy(list => list.DisplayText, StringComparer.CurrentCultureIgnoreCase)];
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
        var model = item is null ? null : ToModel(item, await GetOptionFieldsAsync(item));
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
        return ToModel(listItem, await GetOptionFieldsAsync(listItem));
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
        return ToModel(item, await GetOptionFieldsAsync(item));
    }

    public async Task<CrestOptionModel> AddOptionAsync(string listKey, string optionKey, string displayText, int position = 0, string? category = null, string? displayTextPlural = null, string? value = null, IReadOnlyDictionary<string, string?>? fields = null, CancellationToken cancellationToken = default)
    {
        var listItem = await RequireListItemAsync(listKey);
        var validation = CrestContentPartListRules.ValidateKey(optionKey, ToModel(listItem).Options.Select(option => option.Key));
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Error);
        }

        var descriptors = await GetOptionFieldsAsync(listItem);
        var normalized = CrestContentPartListRules.NormalizeKey(optionKey);
        var option = await AppendOptionAsync(listItem, normalized, displayText, position, CrestOptionSources.Tenant, category, displayTextPlural, value);
        if (fields is { Count: > 0 })
        {
            WriteFields(option, fields, descriptors);
        }

        await contentManager.UpdateAsync(listItem);
        await contentManager.PublishAsync(listItem);
        _byKey.Remove(CrestContentPartListRules.NormalizeKey(listKey));

        return ToOptionModel(option, descriptors);
    }

    public async Task<CrestOptionModel> UpdateOptionAsync(string listKey, string optionKey, string? displayText, int? position, bool? hidden, string? category = null, string? displayTextPlural = null, string? value = null, IReadOnlyDictionary<string, string?>? fields = null, CancellationToken cancellationToken = default)
    {
        var listItem = await RequireListItemAsync(listKey);
        var descriptors = await GetOptionFieldsAsync(listItem);
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

            if (fields is { Count: > 0 })
            {
                WriteFields(option, fields, descriptors);
            }
        });

        if (option is null)
        {
            throw new InvalidOperationException($"Content part list '{listKey}' has no option '{optionKey}'.");
        }

        await contentManager.UpdateAsync(listItem);
        await contentManager.PublishAsync(listItem);
        _byKey.Remove(CrestContentPartListRules.NormalizeKey(listKey));

        return ToOptionModel(option, descriptors);
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

        return ToModel(listItem, await GetOptionFieldsAsync(listItem));
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

        return ToModel(listItem, await GetOptionFieldsAsync(listItem));
    }

    public async Task<CrestContentPartListModel> SetOptionFieldLockAsync(string listKey, string fieldName, bool dataLocked, CancellationToken cancellationToken = default)
    {
        var listItem = await RequireListItemAsync(listKey);
        var descriptors = await GetOptionFieldsAsync(listItem);
        var field = descriptors.FirstOrDefault(candidate => string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"The option type has no custom field named '{fieldName}'.");

        // The designation lives ON the field definition (the option type is the
        // list's), so it travels with the field the way any field setting does.
        await contentDefinitionManager.AlterPartDefinitionAsync(field.PartName, part => part
            .WithField(field.Name, builder => builder
                .MergeSettings<CrestOptionFieldSettings>(settings => settings.DataLocked = dataLocked)));

        _fieldsByType.Clear();
        _byKey.Remove(CrestContentPartListRules.NormalizeKey(listKey));
        return ToModel(listItem, await GetOptionFieldsAsync(listItem));
    }

    public async Task<IReadOnlyList<string>> GetOptionContentTypesAsync(CancellationToken cancellationToken = default)
    {
        // Eligible option types are the ones carrying CrestOptionPart - the part
        // that holds Key/Category/Position/etc.; without it an option cannot
        // round-trip through this service at all.
        var definitions = await contentDefinitionManager.ListTypeDefinitionsAsync();
        return [.. definitions
            .Where(definition => definition.Parts.Any(part =>
                string.Equals(part.PartDefinition.Name, nameof(CrestOptionPart), StringComparison.Ordinal)))
            .Select(definition => definition.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<CrestContentPartListModel> SetOptionContentTypeAsync(string listKey, string contentType, CancellationToken cancellationToken = default)
    {
        var listItem = await RequireListItemAsync(listKey);
        var typeName = contentType?.Trim() ?? string.Empty;

        var eligible = await GetOptionContentTypesAsync(cancellationToken);
        if (!eligible.Contains(typeName, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"'{typeName}' is not an option content type - it must exist and carry {nameof(CrestOptionPart)}.");
        }

        // Existing options keep their stored content type; retargeting a populated
        // list would orphan their data behind the new type's field surface and make
        // stale field names fail on update. Migrate-or-recreate is a deliberate,
        // separate decision - refuse the silent version.
        var options = listItem.As<CrestContentPartListPart>()?.Options ?? [];
        var mismatched = options.FirstOrDefault(option => !string.Equals(option.ContentType, typeName, StringComparison.OrdinalIgnoreCase));
        if (mismatched is not null)
        {
            throw new InvalidOperationException(
                $"The list already has options of type '{mismatched.ContentType}'. The option type can only be set while the list is empty (or already of that type).");
        }

        listItem.Alter<CrestContentPartListPart>(part => part.OptionContentType = typeName);
        await contentManager.UpdateAsync(listItem);
        await contentManager.PublishAsync(listItem);

        _fieldsByType.Clear();
        _byKey.Remove(CrestContentPartListRules.NormalizeKey(listKey));
        return ToModel(listItem, await GetOptionFieldsAsync(listItem));
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

    public async Task AttachToContentTypeAsync(string listKey, string contentType, string fieldName, string? displayName = null, Action<OptionPickerFieldSettings>? configure = null, CancellationToken cancellationToken = default)
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
                        // Caller-shaped settings (cascade filters, sort columns, ...)
                        // apply after the defaults, and on every reattach - the
                        // declaring migration owns them, so they are re-asserted.
                        configure?.Invoke(settings);
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
        // New options take the LIST's option content type (custom data fields live
        // on it); the shared Option type is only the blank-value fallback.
        var optionType = listItem.As<CrestContentPartListPart>()?.OptionContentType;
        var option = await contentManager.NewAsync(
            string.IsNullOrWhiteSpace(optionType) ? CrestContentPartListMigrations.OptionContentType : optionType);
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

    // The list's custom-field descriptors: every field on every part of its option
    // content type, with the admin's per-field lock designation. Null-safe on the
    // type name because pre-existing lists always used the shared Option type.
    private async Task<CrestOptionFieldModel[]> GetOptionFieldsAsync(ContentItem listItem)
    {
        var optionContentType = listItem.As<CrestContentPartListPart>()?.OptionContentType;
        var typeName = string.IsNullOrWhiteSpace(optionContentType)
            ? CrestContentPartListMigrations.OptionContentType
            : optionContentType;

        if (_fieldsByType.TryGetValue(typeName, out var cached))
        {
            return cached;
        }

        var type = await contentDefinitionManager.GetTypeDefinitionAsync(typeName);
        CrestOptionFieldModel[] fields = type is null
            ? []
            :
            [
                .. type.Parts.SelectMany(typePart => typePart.PartDefinition.Fields.Select(field => new CrestOptionFieldModel(
                    field.Name,
                    field.GetSettings<ContentPartFieldSettings>()?.DisplayName is { Length: > 0 } displayName ? displayName : field.Name,
                    typePart.PartDefinition.Name,
                    field.FieldDefinition.Name,
                    field.GetSettings<CrestOptionFieldSettings>()?.DataLocked ?? false))),
            ];

        _fieldsByType[typeName] = fields;
        return fields;
    }

    // Writes custom field values onto the option item. Must run where the option is
    // LIVE - inside the list's Alter for updates (see UpdateOptionAsync's comment),
    // or on a freshly created option before the list saves.
    private static void WriteFields(ContentItem option, IReadOnlyDictionary<string, string?> values, CrestOptionFieldModel[] descriptors)
    {
        foreach (var (name, raw) in values)
        {
            var field = descriptors.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"The option type has no custom field named '{name}'.");

            // Blank clears (ToFieldNode returns null), same contract as plural/Value.
            var node = CrestOptionFieldAccessor.ToFieldNode(field.Type, field.Name, raw);
            option.Alter<ContentPart>(field.PartName, part => part.Content[field.Name] = node);
        }
    }

    private static CrestContentPartListModel ToModel(ContentItem item, CrestOptionFieldModel[]? fields = null)
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
            [.. CrestContentPartListRules.Ordered(options.Select(option => ToOptionModel(option, fields)))],
            fields ?? [],
            string.IsNullOrWhiteSpace(listPart?.OptionContentType)
                ? CrestContentPartListMigrations.OptionContentType
                : listPart.OptionContentType);
    }

    private static CrestOptionModel ToOptionModel(ContentItem option, CrestOptionFieldModel[]? fields = null)
    {
        var part = option.As<CrestOptionPart>();

        IReadOnlyDictionary<string, string?>? fieldValues = null;
        if (fields is { Length: > 0 })
        {
            // Same one-shot conversion as the content-item provider: Content is a
            // dynamic view over the item's JSON, so read it as a JsonObject once.
            JsonObject? document = option.Content is JsonDynamicObject dynamicObject
                ? (JsonObject)dynamicObject
                : option.Content as JsonObject;

            fieldValues = fields.ToDictionary(
                field => field.Name,
                field => CrestOptionFieldAccessor.ReadValue(document, field.PartName, field.Name),
                StringComparer.OrdinalIgnoreCase);
        }

        return new CrestOptionModel(
            option.ContentItemId,
            CrestContentPartListRules.NormalizeKey(part?.Key),
            option.DisplayText ?? string.Empty,
            part?.Source ?? CrestOptionSources.Tenant,
            part?.Hidden ?? false,
            part?.Position ?? 0,
            CrestContentPartListRules.NormalizeCategory(part?.Category),
            string.IsNullOrWhiteSpace(part?.DisplayTextPlural) ? null : part.DisplayTextPlural.Trim(),
            string.IsNullOrWhiteSpace(part?.Value) ? null : part.Value.Trim(),
            fieldValues);
    }
}
