using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Contents;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using System.Text.Json.Nodes;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/content-types")]
public sealed class ContentTypesController(IContentDefinitionManager contentDefinitionManager, IAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ContentType[]>> List()
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes)) return Forbid();
        var definitions = await contentDefinitionManager.ListTypeDefinitionsAsync();
        return Ok(definitions.Select(ContentType.From).ToArray());
    }

    [HttpGet("{contentType}")]
    public async Task<ActionResult<ContentType>> Get(string contentType)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes)) return Forbid();
        var definition = await contentDefinitionManager.GetTypeDefinitionAsync(contentType);
        return definition is null ? NotFound() : Ok(ContentType.From(definition));
    }
}

public sealed record ContentType(
    string Name,
    string DisplayName,
    JsonObject Settings,
    ContentTypePart[] Parts)
{
    public static ContentType From(ContentTypeDefinition source) => new(
        source.Name,
        source.DisplayName,
        source.Settings,
        source.Parts.Select(ContentTypePart.From).ToArray());
}

public sealed record ContentTypePart(
    string Name,
    JsonObject Settings,
    ContentPart Part)
{
    public static ContentTypePart From(ContentTypePartDefinition source) => new(
        source.Name,
        source.Settings,
        ContentPart.From(source.PartDefinition));
}

public sealed record ContentPart(
    string Name,
    JsonObject Settings,
    ContentPartField[] Fields)
{
    public static ContentPart From(ContentPartDefinition source) => new(
        source.Name,
        source.Settings,
        source.Fields.Select(ContentPartField.From).ToArray());
}

public sealed record ContentPartField(
    string Name,
    JsonObject Settings,
    ContentField Field)
{
    public static ContentPartField From(ContentPartFieldDefinition source) => new(
        source.Name,
        source.Settings,
        new ContentField(source.FieldDefinition.Name));
}

public sealed record ContentField(string Name);
