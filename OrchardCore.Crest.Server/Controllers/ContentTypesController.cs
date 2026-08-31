using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.Contents;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using System.Text.Json.Nodes;
using Crest.ViewModels;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/content-types")]
public sealed class ContentTypesController(
    IContentDefinitionManager contentDefinitionManager,
    IOptions<ContentOptions> contentOptions,
    IAuthorizationService authorization) : ControllerBase
{
    /// <summary>The registered content field types a field can be - what the
    /// definition screens offer in their field-type dropdown.</summary>
    [HttpGet("field-types")]
    public async Task<ActionResult<string[]>> ListFieldTypes()
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes)) return Forbid();
        return Ok(contentOptions.Value.ContentFieldOptions
            .Select(option => option.Type.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray());
    }

    /// <summary>
    /// Sets a field's TYPE (creating the field when absent). Conversion removes and
    /// re-adds the field definition, keeping its position and display name; stored
    /// content is untouched - each item's data stays in its own document, which is
    /// what lets readers fall back to the previous shape. Converting TO an option
    /// picker normally goes through the picker settings editor instead, so the
    /// binding is configured in the same step.
    /// </summary>
    [HttpPut("fields")]
    public async Task<IActionResult> SetFieldType([FromBody] SetFieldTypeRequest request)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        if (string.IsNullOrWhiteSpace(request.Part) || string.IsNullOrWhiteSpace(request.Field) || string.IsNullOrWhiteSpace(request.FieldType))
        {
            return Problem("A part, a field name and a field type are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!contentOptions.Value.ContentFieldOptions.Any(option => string.Equals(option.Type.Name, request.FieldType, StringComparison.Ordinal)))
        {
            return Problem($"'{request.FieldType}' is not a registered field type.", statusCode: StatusCodes.Status400BadRequest);
        }

        var existing = await contentDefinitionManager.GetPartDefinitionAsync(request.Part);
        var current = existing?.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, request.Field, StringComparison.OrdinalIgnoreCase));

        if (string.Equals(current?.FieldDefinition?.Name, request.FieldType, StringComparison.Ordinal))
        {
            return NoContent();
        }

        var position = current?.Settings?["ContentPartFieldSettings"]?["Position"]?.ToString();
        var displayName = request.DisplayName
            ?? current?.Settings?["ContentPartFieldSettings"]?["DisplayName"]?.ToString()
            ?? request.Field;

        if (current is not null)
        {
            await contentDefinitionManager.AlterPartDefinitionAsync(request.Part, part => part.RemoveField(request.Field));
        }

        await contentDefinitionManager.AlterPartDefinitionAsync(request.Part, part => part
            .WithField(request.Field, field =>
            {
                field.OfType(request.FieldType).WithDisplayName(displayName);
                if (!string.IsNullOrWhiteSpace(position))
                {
                    field.WithPosition(position);
                }
            }));

        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<ContentType[]>> List()
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes)) return Forbid();
        var definitions = await contentDefinitionManager.ListTypeDefinitionsAsync();
        return Ok(definitions.Select(ContentType.From).ToArray());
    }

    /// <summary>
    /// Every part DEFINITION, attached to a type or not, with its fields and the
    /// types using it. The Content Parts screen edits fields HERE because a part is
    /// shared: a field change lands on every type carrying the part.
    /// </summary>
    [HttpGet("parts")]
    public async Task<ActionResult<ContentPartDefinitionModel[]>> ListParts()
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes)) return Forbid();

        var types = (await contentDefinitionManager.ListTypeDefinitionsAsync()).ToArray();
        var usage = types
            .SelectMany(type => type.Parts.Select(part => (Part: part.PartDefinition.Name, Type: type.Name)))
            .GroupBy(entry => entry.Part, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Type).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);

        var parts = await contentDefinitionManager.ListPartDefinitionsAsync();
        return Ok(parts
            .Select(part => new ContentPartDefinitionModel(
                part.Name,
                Crest.ViewModels.ContentPart.From(part),
                usage.TryGetValue(part.Name, out var usedBy) ? usedBy : []))
            .OrderBy(part => part.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    /// <summary>
    /// Attaches a part to a type, or updates the ATTACHMENT's instance settings
    /// (display name, description, position) when it is already attached. The part's
    /// own fields are not touched here - those belong to the part definition and are
    /// edited on the Content Parts screen, because they are shared across every type
    /// carrying the part.
    /// </summary>
    [HttpPut("{contentType}/parts")]
    public async Task<IActionResult> AttachPart(string contentType, [FromBody] AttachPartRequest request)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        if (string.IsNullOrWhiteSpace(request.Part))
        {
            return Problem("A part name is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var typeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(contentType);
        if (typeDefinition is null)
        {
            return NotFound();
        }

        // The part must exist as a definition (or be the type's own implicit part) -
        // silently creating an empty definition from a typo helps no one.
        var partDefinition = await contentDefinitionManager.GetPartDefinitionAsync(request.Part);
        if (partDefinition is null && !string.Equals(request.Part, contentType, StringComparison.OrdinalIgnoreCase))
        {
            return Problem($"There is no content part named '{request.Part}'.", statusCode: StatusCodes.Status400BadRequest);
        }

        await contentDefinitionManager.AlterTypeDefinitionAsync(contentType, type => type
            .WithPart(request.Part, part =>
            {
                if (request.DisplayName is not null)
                {
                    part.WithDisplayName(request.DisplayName);
                }

                if (request.Description is not null)
                {
                    part.WithDescription(request.Description);
                }

                if (!string.IsNullOrWhiteSpace(request.Position))
                {
                    part.WithPosition(request.Position);
                }
            }));

        return NoContent();
    }

    /// <summary>Detaches a part from a type. The part definition and its fields
    /// survive - other types may carry it, and stored content stays in each item's
    /// document.</summary>
    [HttpDelete("{contentType}/parts/{partName}")]
    public async Task<IActionResult> DetachPart(string contentType, string partName)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        var typeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(contentType);
        var attached = typeDefinition?.Parts.FirstOrDefault(part =>
            string.Equals(part.Name, partName, StringComparison.OrdinalIgnoreCase));
        if (attached is null)
        {
            return NotFound();
        }

        await contentDefinitionManager.AlterTypeDefinitionAsync(contentType, type => type.RemovePart(partName));
        return NoContent();
    }

    [HttpGet("{contentType}")]
    public async Task<ActionResult<ContentType>> Get(string contentType)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes)) return Forbid();
        var definition = await contentDefinitionManager.GetTypeDefinitionAsync(contentType);
        return definition is null ? NotFound() : Ok(ContentType.From(definition));
    }
}

/// <summary>Field-type change payload. Part is the part DEFINITION name; a field
/// that does not exist yet is created.</summary>
public sealed record SetFieldTypeRequest(string Part, string Field, string FieldType, string? DisplayName = null);

/// <summary>A part DEFINITION with its fields and the types carrying it.</summary>
public sealed record ContentPartDefinitionModel(string Name, Crest.ViewModels.ContentPart Part, string[] UsedByTypes);

/// <summary>Attach a part to a type / update the attachment's instance settings.
/// Null leaves a setting unchanged.</summary>
public sealed record AttachPartRequest(string Part, string? DisplayName = null, string? Description = null, string? Position = null);
