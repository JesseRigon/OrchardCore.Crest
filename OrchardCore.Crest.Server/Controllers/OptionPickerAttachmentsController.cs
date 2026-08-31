using Crest.Fields;
using Crest.Models;
using Crest.Services;
using Crest.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Contents;

namespace Crest.Controllers;

// The admin surface behind "connect this field to an option source": reads and
// writes a field's OptionPickerFieldSettings - the INSTANCE layer, where source,
// displayed columns, sort, search, filters/dependencies, required and the rest live.
// Editing is definition work (it rewrites the part definition), so it is gated by
// the stock content-definition permission, and there is deliberately no per-user
// override surface: what the admin configures here is the behaviour for everyone.
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/option-picker/attachment")]
public sealed class OptionPickerAttachmentsController(
    IContentDefinitionManager contentDefinitionManager,
    IEnumerable<IOptionSourceProvider> sourceProviders,
    IAuthorizationService authorizationService) : ControllerBase
{
    /// <summary>The field's current picker binding. 200 with IsOptionPicker=false
    /// for a field of another type (the PUT would convert it); 200 with
    /// FieldExists=false for a name not yet on the part.</summary>
    [HttpGet]
    public async Task<ActionResult<OptionPickerAttachmentModel>> GetAsync(
        [FromQuery] string part, [FromQuery] string field)
    {
        if (!await authorizationService.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes))
        {
            return Forbid();
        }

        var partDefinition = await contentDefinitionManager.GetPartDefinitionAsync(part);
        var fieldDefinition = partDefinition?.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, field, StringComparison.OrdinalIgnoreCase));

        if (fieldDefinition is null)
        {
            return Ok(new OptionPickerAttachmentModel(false, null, false, new OptionPickerFieldSettings()));
        }

        var isPicker = string.Equals(fieldDefinition.FieldDefinition?.Name, nameof(OptionPickerField), StringComparison.Ordinal);
        return Ok(new OptionPickerAttachmentModel(
            true,
            fieldDefinition.FieldDefinition?.Name,
            isPicker,
            fieldDefinition.GetSettings<OptionPickerFieldSettings>() ?? new OptionPickerFieldSettings()));
    }

    /// <summary>
    /// Binds a field to an option source and writes its full instance settings.
    /// Creates the field when absent; converts it when it has another type (stored
    /// content is untouched - the field's data stays in each item's document).
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> PutAsync([FromBody] OptionPickerAttachmentRequest request)
    {
        if (!await authorizationService.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Part) || string.IsNullOrWhiteSpace(request.Field))
        {
            return Problem("A part and a field name are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var settings = request.Settings ?? new OptionPickerFieldSettings();
        var (providerKey, _) = CrestOptionSourceKeys.Split(settings.SourceKey);
        if (!sourceProviders.Any(provider => string.Equals(provider.Key, providerKey, StringComparison.OrdinalIgnoreCase)))
        {
            return Problem($"There is no option source provider named '{providerKey}'.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Same conversion rule as the content-part-lists attach path: WithField keeps an
        // existing field's TYPE, so a field of another type is removed first -
        // otherwise the settings would say "option picker" while the field stays
        // what it was.
        var existing = await contentDefinitionManager.GetPartDefinitionAsync(request.Part);
        var current = existing?.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, request.Field, StringComparison.OrdinalIgnoreCase));

        var position = current?.Settings?["ContentPartFieldSettings"]?["Position"]?.ToString();

        if (current is not null && !string.Equals(current.FieldDefinition?.Name, nameof(OptionPickerField), StringComparison.Ordinal))
        {
            await contentDefinitionManager.AlterPartDefinitionAsync(request.Part, part => part.RemoveField(request.Field));
        }

        await contentDefinitionManager.AlterPartDefinitionAsync(request.Part, part => part
            .WithField(request.Field, field =>
            {
                field
                    .OfType(nameof(OptionPickerField))
                    .WithDisplayName(request.DisplayName
                        ?? current?.Settings?["ContentPartFieldSettings"]?["DisplayName"]?.ToString()
                        ?? request.Field)
                    // Full replacement, property by property: MergeSettings merges
                    // JSON, and a merged array would keep rows the admin deleted.
                    .MergeSettings<OptionPickerFieldSettings>(target =>
                    {
                        target.SourceKey = settings.SourceKey;
                        target.Multiple = settings.Multiple;
                        target.Required = settings.Required;
                        target.Placeholder = settings.Placeholder ?? string.Empty;
                        target.Columns = settings.Columns ?? [];
                        target.DisplayTemplate = settings.DisplayTemplate;
                        target.SearchColumns = settings.SearchColumns ?? [];
                        target.SortColumns = settings.SortColumns ?? [];
                        target.Filters = settings.Filters ?? [];
                    });

                if (!string.IsNullOrWhiteSpace(position))
                {
                    field.WithPosition(position);
                }
            }));

        return NoContent();
    }

    /// <summary>Removes an option picker FIELD from the part definition. Only picker
    /// fields - removing other field types is general definition editing, out of this
    /// controller's scope. Stored content is untouched (data stays in each item's
    /// document).</summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteAsync([FromQuery] string part, [FromQuery] string field)
    {
        if (!await authorizationService.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes))
        {
            return Forbid();
        }

        var partDefinition = await contentDefinitionManager.GetPartDefinitionAsync(part);
        var fieldDefinition = partDefinition?.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, field, StringComparison.OrdinalIgnoreCase));

        if (fieldDefinition is null)
        {
            return NotFound();
        }

        if (!string.Equals(fieldDefinition.FieldDefinition?.Name, nameof(OptionPickerField), StringComparison.Ordinal))
        {
            return Problem("Only option picker fields can be removed here.", statusCode: StatusCodes.Status400BadRequest);
        }

        await contentDefinitionManager.AlterPartDefinitionAsync(part, definition => definition.RemoveField(field));
        return NoContent();
    }
}

/// <summary>A field's current picker binding, as the settings editor reads it.</summary>
public sealed record OptionPickerAttachmentModel(
    bool FieldExists,
    string? FieldType,
    bool IsOptionPicker,
    OptionPickerFieldSettings Settings);

/// <summary>The settings editor's save payload. Part is the part DEFINITION name -
/// for fields directly on a type, Orchard's implicit part shares the type's name.</summary>
public sealed record OptionPickerAttachmentRequest(
    string Part,
    string Field,
    string? DisplayName,
    OptionPickerFieldSettings? Settings);
