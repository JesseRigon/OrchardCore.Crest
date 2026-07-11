using Microsoft.AspNetCore.Mvc;
using OrchardCore;

namespace BlazingOrchard.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/blazing/content-items")]
public sealed class ContentItemsController(IOrchardHelper orchardHelper) : ControllerBase
{
    [HttpGet("by-handle/{handle}")]
    public async Task<ActionResult<ContentItem>> GetByHandle(string handle)
    {
        var contentItem = await orchardHelper.GetContentItemByHandleAsync(handle);
        return contentItem is null ? NotFound() : Ok(ContentItem.From(contentItem));
    }
}

public sealed record ContentItem(
    string ContentItemId,
    string ContentItemVersionId,
    string ContentType,
    string DisplayText,
    bool Published,
    bool Latest,
    DateTime? CreatedUtc,
    DateTime? ModifiedUtc,
    DateTime? PublishedUtc,
    string Owner,
    string Author,
    object Content)
{
    public static ContentItem From(OrchardCore.ContentManagement.ContentItem source) => new(
        source.ContentItemId,
        source.ContentItemVersionId,
        source.ContentType,
        source.DisplayText,
        source.Published,
        source.Latest,
        source.CreatedUtc,
        source.ModifiedUtc,
        source.PublishedUtc,
        source.Owner,
        source.Author,
        source.Content);
}
