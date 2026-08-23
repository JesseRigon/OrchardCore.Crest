using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents;
using OrchardCore.Security.Permissions;
using YesSql;

namespace Crest.ViewModels;

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
