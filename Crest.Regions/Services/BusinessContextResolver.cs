using Crest.Regions.Indexes;
using Crest.Regions.Models;
using OrchardCore.ContentManagement;
using YesSql;

namespace Crest.Regions.Services;

public sealed class BusinessContextResolver(ISession session, IContentManager contentManager) : IBusinessContextResolver
{
    public async Task<BusinessContextModel?> ResolveAsync(ContentItem? carrier, CancellationToken cancellationToken = default)
    {
        var id = carrier?.As<CrestBusinessContextReferencePart>()?.BusinessContextId;
        return string.IsNullOrWhiteSpace(id) ? null : await GetAsync(id, cancellationToken);
    }

    public async Task<BusinessContextModel?> GetAsync(string contentItemId, CancellationToken cancellationToken = default)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Published);
        return item is null ? null : ToModel(item);
    }

    public async Task<BusinessContextModel?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        var normalized = key.Trim();
        var item = await session
            .Query<ContentItem, BusinessContextIndex>(index => index.Key == normalized && index.Published)
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? null : ToModel(item);
    }

    public async Task<IReadOnlyList<BusinessContextModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await session
            .Query<ContentItem, BusinessContextIndex>(index => index.Published)
            .ListAsync(cancellationToken);
        return items.Select(ToModel).OrderBy(context => context.DisplayText, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static BusinessContextModel ToModel(ContentItem item)
    {
        var part = item.As<CrestBusinessContextPart>() ?? new CrestBusinessContextPart();
        return new BusinessContextModel(
            item.ContentItemId,
            part.Key,
            item.DisplayText ?? part.Key,
            part.DefaultCountry,
            part.MeasurementSystem,
            part.TimeZone,
            part.Language,
            part.Enabled);
    }
}
