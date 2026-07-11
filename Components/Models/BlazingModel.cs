using System.Text.Json.Nodes;

namespace BlazingOrchard.Components.Models;

public sealed record BlazingModelSource(
    string Name,
    string? ContentType,
    string Version,
    BlazingModel[] Items);

public sealed record BlazingModel(
    BlazingContentItem? ContentItem,
    JsonNode? Content,
    BlazingModel[]? Items = null,
    string[]? Classes = null,
    Dictionary<string, string>? Attributes = null,
    BlazingShapeMetadata? Metadata = null,
    Dictionary<string, BlazingZone>? Zones = null,
    Dictionary<string, BlazingModelSource>? Sources = null,
    BlazingShapeDiagnostic[]? Diagnostics = null)
{
    public string? GetText(string partName, string fieldName)
    {
        return ContentItem?.Content?[partName]?[fieldName]?["Text"]?.GetValue<string>();
    }

    public decimal? GetNumber(string partName, string fieldName)
    {
        var node = ContentItem?.Content?[partName]?[fieldName]?["Value"];
        return node is null ? null : node.GetValue<decimal>();
    }

    public string GetPickerIds(string partName, string fieldName)
    {
        var ids = ContentItem?.Content?[partName]?[fieldName]?["ContentItemIds"]?.AsArray();
        return ids is null
            ? string.Empty
            : string.Join(", ", ids.Select(id => id?.GetValue<string>()).Where(id => !string.IsNullOrWhiteSpace(id)));
    }
}

public sealed record BlazingContentItem(
    string? ContentItemId,
    string? ContentItemVersionId,
    string? ContentType,
    string? DisplayText,
    bool Latest,
    bool Published,
    DateTime? CreatedUtc,
    DateTime? ModifiedUtc,
    DateTime? PublishedUtc,
    string? Owner,
    string? Author,
    JsonNode? Content,
    string? EditUrl,
    BlazingContentItemMetadata? Metadata);

public sealed record BlazingContentItemMetadata(
    string ContentType,
    string DisplayName,
    BlazingContentPartMetadata[] Parts);

public sealed record BlazingContentPartMetadata(
    string Name,
    string DisplayName,
    BlazingContentFieldMetadata[] Fields,
    JsonNode? Settings);

public sealed record BlazingContentFieldMetadata(
    string Name,
    string FieldType,
    string Label,
    bool Required,
    string Editor,
    string? Position,
    JsonNode? Settings);

public sealed record BlazingShapeMetadata(
    string? Shape = null,
    string? Type = null,
    string? DisplayType = null,
    string? Name = null,
    string? Position = null,
    string? Differentiator = null,
    string? Id = null,
    string? TagName = null,
    string[]? Alternates = null,
    string[]? Wrappers = null);

public sealed record BlazingZone(BlazingModel[] Items);

public sealed record BlazingShapeDiagnostic(
    string Path,
    string? SourceType,
    string Action,
    string Reason);
