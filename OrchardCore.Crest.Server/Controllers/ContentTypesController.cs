using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Contents;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using System.Text.Json.Nodes;
using Crest.ViewModels;

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
