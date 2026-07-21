using System.Text.Json.Nodes;

namespace Crest.Components.Models;

public sealed record CrestModelSource(
    string Name,
    string? ContentType,
    string Version,
    CrestModel[] Items);

public sealed record CrestModel(
    CrestContentItem? ContentItem,
    JsonNode? Content,
    CrestModel[]? Items = null,
    string[]? Classes = null,
    Dictionary<string, string>? Attributes = null,
    CrestShapeMetadata? Metadata = null,
    Dictionary<string, CrestZone>? Zones = null,
    Dictionary<string, CrestModelSource>? Sources = null,
    CrestShapeDiagnostic[]? Diagnostics = null)
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

public sealed record CrestContentItem(
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
    CrestContentItemMetadata? Metadata);

public sealed record CrestContentItemMetadata(
    string ContentType,
    string DisplayName,
    CrestContentPartMetadata[] Parts);

public sealed record CrestContentPartMetadata(
    string Name,
    string DisplayName,
    CrestContentFieldMetadata[] Fields,
    JsonNode? Settings);

public sealed record CrestContentFieldMetadata(
    string Name,
    string FieldType,
    string Label,
    bool Required,
    string Editor,
    string? Position,
    JsonNode? Settings);

public sealed record CrestShapeMetadata(
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

public sealed record CrestZone(CrestModel[] Items);

public sealed record CrestShapeDiagnostic(
    string Path,
    string? SourceType,
    string Action,
    string Reason);
