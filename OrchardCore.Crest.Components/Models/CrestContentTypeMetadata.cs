using System.Text.Json.Nodes;

namespace Crest.Components.Models;

/// <summary>
/// Builds the editor's <see cref="CrestContentItemMetadata"/> from a content-type
/// definition as served by the api/crest/content-types API - the content-items API
/// returns raw items without editor metadata, so the definition-driven form derives
/// its shape from the type definition instead.
/// </summary>
public static class CrestContentTypeMetadata
{
    public static CrestContentItemMetadata? FromTypeDefinition(JsonNode? definition)
    {
        if (definition is null)
        {
            return null;
        }

        var contentType = definition["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var parts = new List<CrestContentPartMetadata>();
        foreach (var partEntry in definition["parts"] as JsonArray ?? new JsonArray())
        {
            if (partEntry is null)
            {
                continue;
            }

            var partName = partEntry["name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(partName))
            {
                continue;
            }

            var part = partEntry["part"];
            var partDisplayName = part?["settings"]?["ContentPartSettings"]?["DisplayName"]?.GetValue<string>();

            var fields = new List<CrestContentFieldMetadata>();
            foreach (var fieldEntry in part?["fields"] as JsonArray ?? new JsonArray())
            {
                if (fieldEntry is null)
                {
                    continue;
                }

                var fieldName = fieldEntry["name"]?.GetValue<string>();
                var fieldType = fieldEntry["field"]?["name"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(fieldType))
                {
                    continue;
                }

                var settings = fieldEntry["settings"];
                var common = settings?["ContentPartFieldSettings"];
                fields.Add(new CrestContentFieldMetadata(
                    fieldName,
                    fieldType,
                    common?["DisplayName"]?.GetValue<string>() ?? fieldName,
                    IsRequired(settings),
                    common?["Editor"]?.GetValue<string>() ?? string.Empty,
                    common?["Position"]?.GetValue<string>(),
                    settings?.DeepClone()));
            }

            parts.Add(new CrestContentPartMetadata(
                partName,
                partDisplayName ?? partName,
                [.. fields],
                partEntry["settings"]?.DeepClone()));
        }

        return new CrestContentItemMetadata(
            contentType,
            definition["displayName"]?.GetValue<string>() ?? contentType,
            [.. parts]);
    }

    // Field-type-specific settings blocks (TextFieldSettings, NumericFieldSettings...)
    // each carry their own Required flag; any of them marks the field required.
    private static bool IsRequired(JsonNode? settings)
    {
        if (settings is not JsonObject settingsObject)
        {
            return false;
        }

        foreach (var (_, value) in settingsObject)
        {
            if (value?["Required"] is JsonValue required && required.TryGetValue<bool>(out var isRequired) && isRequired)
            {
                return true;
            }
        }

        return false;
    }
}
