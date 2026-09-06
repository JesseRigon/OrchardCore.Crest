using System.Text.Json.Dynamic;
using System.Text.Json.Nodes;
using Crest.Settings;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace Crest.Services;

/// <summary>
/// Applies visibility conditions at SAVE time: a field whose condition is false has
/// its value CLEARED (user ruling 2026-09-02) - the editor hides it live, but only
/// the server can guarantee a document never keeps a stale dependent value (an
/// un-voided invoice must not silently keep its void reason). Runs to a fixpoint,
/// because clearing one field can turn another field's condition false in the same
/// save (A visible-when B, B visible-when C).
/// </summary>
public sealed class CrestFieldVisibilityEnforcer(
    IContentDefinitionManager contentDefinitionManager,
    OptionParentValueResolver parentValues)
{
    public async Task ClearHiddenFieldsAsync(ContentItem item, CancellationToken cancellationToken = default)
    {
        var type = await contentDefinitionManager.GetTypeDefinitionAsync(item.ContentType);
        if (type is null)
        {
            return;
        }

        var conditioned = new List<(string PartName, string FieldName, CrestFieldVisibilitySettings Settings)>();
        foreach (var typePart in type.Parts)
        {
            foreach (var field in typePart.PartDefinition.Fields)
            {
                var settings = field.GetSettings<CrestFieldVisibilitySettings>();
                if (settings is { HasCondition: true })
                {
                    conditioned.Add((typePart.Name, field.Name, settings));
                }
            }
        }

        if (conditioned.Count == 0)
        {
            return;
        }

        // Fixpoint: each pass may clear a field that another condition reads. Bounded
        // by the number of conditioned fields - a longer chain than that is a cycle,
        // and a cycle's remaining members legitimately keep their values.
        for (var pass = 0; pass < conditioned.Count; pass++)
        {
            var clearedAny = false;

            foreach (var (partName, fieldName, settings) in conditioned)
            {
                JsonObject? content = item.Content is JsonDynamicObject dynamicObject
                    ? (JsonObject)dynamicObject
                    : item.Content as JsonObject;

                if (content?[partName]?[fieldName] is null)
                {
                    continue;
                }

                // A bare "Field" path addresses the type's implicit part; the stored
                // JSON nests it under the part name, so qualify before reading.
                var contentPath = settings.Path!.Contains('.') ? settings.Path! : $"{item.ContentType}.{settings.Path}";
                IReadOnlyList<string> raw = [];
                if (OptionFilterResolver.StateFromContent(content, [contentPath]).TryGetValue(contentPath, out var values))
                {
                    raw = values;
                }

                var resolved = await parentValues.ResolveAsync(item.ContentType, settings.Path!, null, raw, cancellationToken);

                if (!CrestFieldVisibilityRules.IsVisible(settings, resolved))
                {
                    // Mutate the ROOT document directly - the same write style the
                    // save path itself uses. Alter<ContentPart> is unusable here:
                    // GetOrCreate can serve an element CACHED before the save
                    // replaced the part's subtree, so a mutation through it lands on
                    // a detached object and Apply's merge quietly resurrects the
                    // stale value.
                    if (content?[partName] is JsonObject partObject && partObject.Remove(fieldName))
                    {
                        clearedAny = true;
                    }
                }
            }

            if (!clearedAny)
            {
                return;
            }
        }
    }
}
