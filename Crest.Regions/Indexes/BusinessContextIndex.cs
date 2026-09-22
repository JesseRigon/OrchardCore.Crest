using Crest.Regions.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace Crest.Regions.Indexes;

public sealed class BusinessContextIndex : MapIndex
{
    public string ContentItemId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? DefaultCountry { get; set; }
    public bool Enabled { get; set; }
    public bool Published { get; set; }
    public bool Latest { get; set; }
}

public sealed class BusinessContextIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<BusinessContextIndex>()
            .Map(contentItem =>
            {
                if (contentItem.ContentType != RegionsConstants.ContentTypes.BusinessContext || (!contentItem.Published && !contentItem.Latest))
                {
                    return null;
                }

                var part = contentItem.As<CrestBusinessContextPart>();
                if (part is null)
                {
                    return null;
                }

                return new BusinessContextIndex
                {
                    ContentItemId = contentItem.ContentItemId,
                    Key = part.Key,
                    DefaultCountry = part.DefaultCountry,
                    Enabled = part.Enabled,
                    Published = contentItem.Published,
                    Latest = contentItem.Latest,
                };
            });
    }
}
