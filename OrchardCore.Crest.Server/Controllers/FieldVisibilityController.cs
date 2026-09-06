using Crest.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents;

namespace Crest.Controllers;

/// <summary>
/// Reads and writes a field's <see cref="CrestFieldVisibilitySettings"/> — the
/// declarative show/hide condition any field type can carry. Definition work,
/// gated exactly like the other definition editors; the settings live on the
/// PART's field definition, so a condition applies on every type carrying the
/// part (same sharing rule as the field itself).
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/field-visibility")]
public sealed class FieldVisibilityController(
    IContentDefinitionManager contentDefinitionManager,
    IAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<FieldVisibilityModel>> GetAsync([FromQuery] string part, [FromQuery] string field)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes)) return Forbid();

        var definition = await FindFieldAsync(part, field);
        return definition is null
            ? Ok(new FieldVisibilityModel(false, new CrestFieldVisibilitySettings()))
            : Ok(new FieldVisibilityModel(true, definition.GetSettings<CrestFieldVisibilitySettings>() ?? new CrestFieldVisibilitySettings()));
    }

    /// <summary>Replaces the condition wholesale (a blank Path clears it) — a JSON
    /// merge would resurrect removed keys.</summary>
    [HttpPut]
    public async Task<IActionResult> PutAsync([FromBody] FieldVisibilityRequest request)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        if (string.IsNullOrWhiteSpace(request.Part) || string.IsNullOrWhiteSpace(request.Field))
        {
            return Problem("A part and a field name are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var definition = await FindFieldAsync(request.Part, request.Field);
        if (definition is null)
        {
            return NotFound();
        }

        var incoming = request.Settings ?? new CrestFieldVisibilitySettings();
        var hasCondition = !string.IsNullOrWhiteSpace(incoming.Path);
        if (hasCondition && !CrestFieldVisibilityOperators.All.Contains(incoming.Operator, StringComparer.OrdinalIgnoreCase))
        {
            return Problem($"'{incoming.Operator}' is not a visibility operator.", statusCode: StatusCodes.Status400BadRequest);
        }

        await contentDefinitionManager.AlterPartDefinitionAsync(request.Part, part => part
            .WithField(request.Field, field => field
                .MergeSettings<CrestFieldVisibilitySettings>(settings =>
                {
                    settings.Path = hasCondition ? incoming.Path!.Trim() : null;
                    settings.Operator = hasCondition ? incoming.Operator : CrestFieldVisibilityOperators.Any;
                    settings.Keys = hasCondition
                        ? [.. (incoming.Keys ?? []).Where(key => !string.IsNullOrWhiteSpace(key)).Select(key => key.Trim())]
                        : [];
                })));

        return NoContent();
    }

    private async Task<ContentPartFieldDefinition?> FindFieldAsync(string part, string field)
    {
        var definition = await contentDefinitionManager.GetPartDefinitionAsync(part);
        return definition?.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, field, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>FieldExists distinguishes "no condition yet" from "no such field".</summary>
public sealed record FieldVisibilityModel(bool FieldExists, CrestFieldVisibilitySettings Settings);

public sealed record FieldVisibilityRequest(string Part, string Field, CrestFieldVisibilitySettings? Settings);
