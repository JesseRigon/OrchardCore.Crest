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

// Option Lists are stored as content items whose CrestOptionListPart contains the
// Option items directly, so tenants edit them like any other content and Orchard's own
// version history records who changed what - no parallel override store, and no
// dependency on the Taxonomies module.
public sealed class CrestOptionListService(
    ISession session,
    IContentManager contentManager,
    IContentDefinitionManager contentDefinitionManager) : ICrestOptionListService
{
    // Per-request memo only. Sets are read repeatedly while resolving a document's
    // lines, but they are content items a tenant may edit at any time, so caching
    // beyond the request would need invalidation on save; scoped memoization gets the
    // hot-path win without that risk.
    private readonly Dictionary<string, CrestOptionListModel?> _byKey = new(CrestOptionListRules.KeyComparer);

    public async Task<IReadOnlyList<CrestOptionListModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await session
            .Query<ContentItem, ContentItemIndex>(index =>
                index.ContentType == CrestOptionListMigrations.OptionListContentType && index.Latest)
            .ListAsync();

        return [.. items.Select(ToModel).OrderBy(list => list.DisplayText, StringComparer.CurrentCultureIgnoreCase)];
    }

    public async Task<CrestOptionListModel?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var normalized = CrestOptionListRules.NormalizeKey(key);
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
        return list is null ? [] : CrestOptionListRules.Selectable(list.Options);
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
        var normalized = CrestOptionListRules.NormalizeKey(optionKey);
        return list?.Options
            .FirstOrDefault(option => CrestOptionListRules.KeyComparer.Equals(option.Key, normalized))
            ?.ContentItemId;
    }

    public async Task<CrestOptionListModel> SeedAsync(CrestOptionListSeed seed, CancellationToken cancellationToken = default)
    {
        var listKey = CrestOptionListRules.NormalizeKey(seed.Key);
        if (listKey.Length == 0)
        {
            throw new ArgumentException("A seeded option list needs a key.", nameof(seed));
        }

        var listItem = await FindListItemAsync(listKey)
            ?? await CreateListItemAsync(listKey, seed.DisplayText, CrestOptionSources.Module);

        // Existing options are matched by key and left untouched, so a tenant's
        // relabel/hide survives every reseed.
        var plan = CrestOptionListRules.PlanSeed(seed, ToModel(listItem).Options);
        if (plan.HasChanges)
        {
            foreach (var addition in plan.Additions)
            {
                await AppendOptionAsync(listItem, addition.Key, addition.DisplayText, addition.Position, CrestOptionSources.Module);
            }

            await contentManager.UpdateAsync(listItem);
            await contentManager.PublishAsync(listItem);
        }

        _byKey.Remove(listKey);
        return ToModel(listItem);
    }

    public async Task<CrestOptionListModel> CreateListAsync(string key, string displayText, CancellationToken cancellationToken = default)
    {
        var normalized = CrestOptionListRules.NormalizeKey(key);
        var existing = await ListAsync(cancellationToken);
        var validation = CrestOptionListRules.ValidateKey(normalized, existing.Select(list => list.Key));
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Error);
        }

        var item = await CreateListItemAsync(normalized, displayText, CrestOptionSources.Tenant);
        _byKey.Remove(normalized);
        return ToModel(item);
    }

    public async Task<CrestOptionModel> AddOptionAsync(string listKey, string optionKey, string displayText, int position = 0, CancellationToken cancellationToken = default)
    {
        var listItem = await RequireListItemAsync(listKey);
        var validation = CrestOptionListRules.ValidateKey(optionKey, ToModel(listItem).Options.Select(option => option.Key));
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Error);
        }

        var normalized = CrestOptionListRules.NormalizeKey(optionKey);
        var option = await AppendOptionAsync(listItem, normalized, displayText, position, CrestOptionSources.Tenant);
        await contentManager.UpdateAsync(listItem);
        await contentManager.PublishAsync(listItem);
        _byKey.Remove(CrestOptionListRules.NormalizeKey(listKey));

        return ToOptionModel(option);
    }

    public async Task<CrestOptionModel> UpdateOptionAsync(string listKey, string optionKey, string? displayText, int? position, bool? hidden, CancellationToken cancellationToken = default)
    {
        var listItem = await RequireListItemAsync(listKey);
        var normalized = CrestOptionListRules.NormalizeKey(optionKey);
        var listPart = listItem.As<CrestOptionListPart>()
            ?? throw new InvalidOperationException($"Option list '{listKey}' has no option list part.");

        var option = listPart.Options.FirstOrDefault(candidate =>
            CrestOptionListRules.KeyComparer.Equals(CrestOptionListRules.NormalizeKey(candidate.As<CrestOptionPart>()?.Key), normalized))
            ?? throw new InvalidOperationException($"Option list '{listKey}' has no option '{optionKey}'.");

        if (displayText is not null)
        {
            option.DisplayText = displayText;
            option.Alter<ContentPart>("TitlePart", part => part.Content.Title = displayText);
        }

        if (position is not null || hidden is not null)
        {
            option.Alter<CrestOptionPart>(part =>
            {
                part.Position = position ?? part.Position;
                part.Hidden = hidden ?? part.Hidden;
            });
        }

        listItem.Alter<CrestOptionListPart>(part => part.Options = listPart.Options);
        await contentManager.UpdateAsync(listItem);
        await contentManager.PublishAsync(listItem);
        _byKey.Remove(CrestOptionListRules.NormalizeKey(listKey));

        return ToOptionModel(option);
    }

    public async Task AttachToContentTypeAsync(string listKey, string contentType, string fieldName, string? displayName = null, CancellationToken cancellationToken = default)
    {
        var listItem = await RequireListItemAsync(listKey);

        var sourceKey = CrestOptionSourceKeys.ForOptionList(CrestOptionListRules.NormalizeKey(listKey));

        await contentDefinitionManager.AlterPartDefinitionAsync(contentType, part => part
            .WithField(fieldName, field => field
                .OfType(nameof(OptionPickerField))
                .WithDisplayName(displayName ?? fieldName)
                .MergeSettings<OptionPickerFieldSettings>(settings =>
                {
                    settings.SourceKey = sourceKey;
                    settings.Multiple = false;
                })));
    }

    private async Task<ContentItem?> FindListItemAsync(string normalizedKey)
    {
        var items = await session
            .Query<ContentItem, ContentItemIndex>(index =>
                index.ContentType == CrestOptionListMigrations.OptionListContentType && index.Latest)
            .ListAsync();

        return items.FirstOrDefault(item =>
            CrestOptionListRules.KeyComparer.Equals(
                CrestOptionListRules.NormalizeKey(item.As<CrestOptionListPart>()?.Key),
                normalizedKey));
    }

    private async Task<ContentItem> RequireListItemAsync(string listKey)
    {
        var normalized = CrestOptionListRules.NormalizeKey(listKey);
        return await FindListItemAsync(normalized)
            ?? throw new InvalidOperationException($"There is no option list with the key '{listKey}'.");
    }

    private async Task<ContentItem> CreateListItemAsync(string key, string displayText, string source)
    {
        var item = await contentManager.NewAsync(CrestOptionListMigrations.OptionListContentType);
        item.DisplayText = displayText;
        item.Alter<ContentPart>("TitlePart", part => part.Content.Title = displayText);
        item.Alter<CrestOptionListPart>(part =>
        {
            part.Key = key;
            part.Source = source;
            part.OptionContentType = CrestOptionListMigrations.OptionContentType;
        });

        await contentManager.CreateAsync(item, VersionOptions.Draft);
        await contentManager.PublishAsync(item);
        return item;
    }

    private async Task<ContentItem> AppendOptionAsync(ContentItem listItem, string key, string displayText, int position, string source)
    {
        var option = await contentManager.NewAsync(CrestOptionListMigrations.OptionContentType);
        option.DisplayText = displayText;
        option.Alter<ContentPart>("TitlePart", part => part.Content.Title = displayText);
        option.Alter<CrestOptionPart>(part =>
        {
            part.Key = key;
            part.Source = source;
            part.Position = position;
            part.OptionListContentItemId = listItem.ContentItemId;
        });

        listItem.Alter<CrestOptionListPart>(part => part.Options.Add(option));
        return option;
    }

    private static CrestOptionListModel ToModel(ContentItem item)
    {
        var listPart = item.As<CrestOptionListPart>();
        var options = listPart?.Options ?? [];

        return new CrestOptionListModel(
            item.ContentItemId,
            CrestOptionListRules.NormalizeKey(listPart?.Key),
            item.DisplayText ?? string.Empty,
            listPart?.Source ?? CrestOptionSources.Tenant,
            [.. CrestOptionListRules.Ordered(options.Select(ToOptionModel))]);
    }

    private static CrestOptionModel ToOptionModel(ContentItem option)
    {
        var part = option.As<CrestOptionPart>();
        return new CrestOptionModel(
            option.ContentItemId,
            CrestOptionListRules.NormalizeKey(part?.Key),
            option.DisplayText ?? string.Empty,
            part?.Source ?? CrestOptionSources.Tenant,
            part?.Hidden ?? false,
            part?.Position ?? 0);
    }
}
