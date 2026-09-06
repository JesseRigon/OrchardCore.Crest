using System.Globalization;
using System.Text.Json.Nodes;

namespace Crest.Services;

/// <summary>
/// Reads and shapes custom data field values on an option content item. Field values
/// live in the item's document under [PartName][FieldName] in the stock field shapes
/// (TextField stores Text, Numeric/Boolean store Value); the management grid and the
/// picker provider both speak strings, so this is the one place that knows the JSON.
/// Pure over JsonNode, so the shape contract is unit-testable without content items.
/// </summary>
public static class CrestOptionFieldAccessor
{
    /// <summary>Reads a field's value as a string: [partName][fieldName], unwrapping
    /// the common Text/Value shapes. Null when the part, field or value is absent.</summary>
    public static string? ReadValue(JsonNode? content, string partName, string fieldName)
    {
        var field = content?[partName]?[fieldName];
        if (field is null)
        {
            return null;
        }

        var value = (field["Text"] ?? field["Value"] ?? field)?.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>The property the field type stores its value under - "Text" for
    /// TextField and anything unknown, "Value" for Numeric/Boolean.</summary>
    public static string ValueProperty(string fieldType) => fieldType switch
    {
        "NumericField" or "BooleanField" => "Value",
        _ => "Text",
    };

    /// <summary>
    /// Builds the JSON to store for a field, typed by field type so other consumers
    /// read proper JSON, not stringified numbers: NumericField parses to a number,
    /// BooleanField to a bool, everything else stores the string. A blank raw value
    /// returns null - the field CLEARS, mirroring the plural/Value contract.
    /// Throws when a Numeric/Boolean value does not parse: silently storing the
    /// string would hand a typed reader a value it cannot use.
    /// </summary>
    public static JsonObject? ToFieldNode(string fieldType, string fieldName, string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        JsonNode node = fieldType switch
        {
            "NumericField" => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                ? JsonValue.Create(number)
                : throw new InvalidOperationException($"'{value}' is not a number, which the field '{fieldName}' requires."),
            "BooleanField" => bool.TryParse(value, out var flag)
                ? JsonValue.Create(flag)
                : throw new InvalidOperationException($"'{value}' is not true or false, which the field '{fieldName}' requires."),
            _ => JsonValue.Create(value),
        };

        return new JsonObject { [ValueProperty(fieldType)] = node };
    }
}
