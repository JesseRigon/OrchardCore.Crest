using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents;
using OrchardCore.Security.Permissions;
using YesSql;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/content-items")]
public sealed class ContentItemsController(
    IOrchardHelper orchardHelper,
    ISession session,
    IContentManager contentManager,
    IAuthorizationService authorizationService) : ControllerBase
{
    private static readonly string[] DefaultViewFields =
    [
        nameof(OrchardCore.ContentManagement.ContentItem.DisplayText),
        nameof(OrchardCore.ContentManagement.ContentItem.ContentType),
    ];

    [HttpGet]
    public async Task<ActionResult<ContentItemListResult>> ListAsync(
        [FromQuery] string? contentType = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.ListContent))
        {
            return Forbid();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = session.Query<OrchardCore.ContentManagement.ContentItem, ContentItemIndex>()
            .With<ContentItemIndex>(index => index.Latest);

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            query = query.With<ContentItemIndex>(index => index.ContentType == contentType);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.With<ContentItemIndex>(index => index.DisplayText.Contains(search));
        }

        if (string.Equals(status, "published", StringComparison.OrdinalIgnoreCase))
        {
            query = query.With<ContentItemIndex>(index => index.Published);
        }
        else if (string.Equals(status, "draft", StringComparison.OrdinalIgnoreCase))
        {
            query = query.With<ContentItemIndex>(index => !index.Published);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(index => index.ModifiedUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ListAsync();

        return Ok(new ContentItemListResult(items.Select(ContentItem.From).ToArray(), total, page, pageSize));
    }

    [HttpPost("{contentItemId}/publish")]
    public Task<IActionResult> PublishAsync(string contentItemId) => ChangePublicationAsync(contentItemId, publish: true);

    [HttpPost("{contentItemId}/unpublish")]
    public Task<IActionResult> UnpublishAsync(string contentItemId) => ChangePublicationAsync(contentItemId, publish: false);

    [HttpGet("{contentItemId}")]
    public async Task<ActionResult<ContentItem>> GetAsync(string contentItemId)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (item is null) return NotFound();
        return await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, item)
            ? Ok(ContentItem.From(item))
            : Forbid();
    }

    // Unlike GetAsync above (full item, gated on EditContent - the content-editor
    // shape Admin's Api.cs already depends on), this is for any caller reading a
    // published item's data for display: anonymous Site visitors, logged-in members,
    // or admins - all through the same action, with the RESPONSE SHAPE driven by
    // what's requested (?parts=) and what that specific caller is permitted to see,
    // not by which endpoint they called. An admin who can view the item gets the same
    // fields back as anyone else who can view it; this isn't a "public" vs "admin"
    // endpoint, it's permission-shaped for whoever's asking.
    //
    // Today every requested field checks the same item-level CommonPermissions.ViewContent
    // - OrchardCore has no built-in per-part permission (see plans/permissions.md for
    // why, and what real per-part permissions would require). That means "permitted"
    // currently means "can view this item at all," not yet differentiated per part -
    // each field's check is already its own step below so a real per-part permission
    // can replace CommonPermissions.ViewContent there later without restructuring this
    // action.
    [HttpGet("{contentItemId}/view")]
    public async Task<ActionResult<Dictionary<string, object?>>> ViewAsync(
        string contentItemId,
        [FromQuery] string? parts = null)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Published);
        if (item is null)
        {
            return NotFound();
        }

        var requestedFields = string.IsNullOrWhiteSpace(parts)
            ? DefaultViewFields
            : parts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = new Dictionary<string, object?>();
        foreach (var field in requestedFields)
        {
            if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent, item))
            {
                continue;
            }

            result[field] = field switch
            {
                nameof(OrchardCore.ContentManagement.ContentItem.DisplayText) => item.DisplayText,
                nameof(OrchardCore.ContentManagement.ContentItem.ContentType) => item.ContentType,
                _ => item.Content[field],
            };
        }

        return result.Count == 0 ? Forbid() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ContentItem>> CreateAsync([FromBody] ContentItemWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ContentType) || !await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent))
        {
            return Forbid();
        }

        var item = await contentManager.NewAsync(request.ContentType);
        item.DisplayText = request.DisplayText?.Trim() ?? string.Empty;
        ReplaceContent(item, request.Content);
        var created = await contentManager.CreateAsync(item, VersionOptions.Draft);
        if (!created) return Conflict();
        if (request.Publish && !await contentManager.PublishAsync(item)) return Conflict();
        return Ok(ContentItem.From(item));
    }

    [HttpPut("{contentItemId}")]
    public async Task<ActionResult<ContentItem>> UpdateAsync(string contentItemId, [FromBody] ContentItemWriteRequest request)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (item is null) return NotFound();
        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, item)) return Forbid();

        item.DisplayText = request.DisplayText?.Trim() ?? string.Empty;
        ReplaceContent(item, request.Content);
        await contentManager.UpdateAsync(item);
        if (request.Publish && !item.Published && !await contentManager.PublishAsync(item)) return Conflict();
        return Ok(ContentItem.From(item));
    }

    [HttpDelete("{contentItemId}")]
    public async Task<IActionResult> DeleteAsync(string contentItemId)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (item is null)
        {
            return NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.DeleteContent, item))
        {
            return Forbid();
        }

        return await contentManager.RemoveAsync(item) ? NoContent() : Conflict();
    }

    [HttpGet("by-handle/{handle}")]
    public async Task<ActionResult<ContentItem>> GetByHandle(string handle)
    {
        var contentItem = await orchardHelper.GetContentItemByHandleAsync(handle);
        if (contentItem is null) return NotFound();
        return await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, contentItem)
            ? Ok(ContentItem.From(contentItem))
            : Forbid();
    }

    private async Task<IActionResult> ChangePublicationAsync(string contentItemId, bool publish)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (item is null)
        {
            return NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.PublishContent, item))
        {
            return Forbid();
        }

        var changed = publish
            ? await contentManager.PublishAsync(item)
            : await contentManager.UnpublishAsync(item);
        return changed ? NoContent() : Conflict();
    }

    private static void ReplaceContent(OrchardCore.ContentManagement.ContentItem item, JsonObject? content)
    {
        item.Content.Clear();
        if (content is null) return;
        foreach (var property in content)
        {
            item.Content[property.Key] = property.Value?.DeepClone();
        }
    }
}

public sealed record ContentItemListResult(ContentItem[] Items, int Total, int Page, int PageSize);
public sealed record ContentItemWriteRequest(string ContentType, string? DisplayText, JsonObject? Content, bool Publish);

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
