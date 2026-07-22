using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Templates;
using OrchardCore.Templates.Models;
using OrchardCore.Templates.Services;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/templates")]
public sealed class CrestTemplatesController(TemplatesManager manager, IAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CrestTemplate[]>> ListAsync()
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ManageTemplates)) return Forbid();
        var document = await manager.GetTemplatesDocumentAsync();
        return Ok(document.Templates.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => new CrestTemplate(x.Key, x.Value.Description, x.Value.Content)).ToArray());
    }
    [HttpPut("{name}")]
    public async Task<ActionResult<CrestTemplate>> SaveAsync(string name, [FromBody] CrestTemplateWrite request)
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ManageTemplates)) return Forbid();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("A template name is required.");
        var template = new Template { Description = request.Description?.Trim(), Content = request.Content ?? string.Empty };
        await manager.UpdateTemplateAsync(name, template);
        return Ok(new CrestTemplate(name, template.Description, template.Content));
    }
    [HttpDelete("{name}")]
    public async Task<IActionResult> DeleteAsync(string name)
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ManageTemplates)) return Forbid();
        await manager.RemoveTemplateAsync(name); return NoContent();
    }
}
public sealed record CrestTemplate(string Name, string? Description, string Content);
public sealed record CrestTemplateWrite(string? Description, string? Content);
