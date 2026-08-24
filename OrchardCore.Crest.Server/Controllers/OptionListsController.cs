using Crest.Models;
using Crest.Permissions;
using Crest.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Controllers;

// The management API behind Option Lists. The SCREENS live in Crest.Admin's wasm
// pages; Fruitful modules only declare and consume sets, they never own these editors.
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/option-lists")]
public sealed class OptionListsController(
    ICrestOptionListService optionLists,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CrestOptionListModel[]>> ListAsync()
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok((await optionLists.ListAsync(HttpContext.RequestAborted)).ToArray());
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<CrestOptionListModel>> GetAsync(string key)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var list = await optionLists.GetAsync(key, HttpContext.RequestAborted);
        return list is null ? NotFound() : Ok(list);
    }

    /// <summary>The options an editor should offer: ordered, hidden ones dropped.</summary>
    [HttpGet("{key}/selectable")]
    public async Task<ActionResult<CrestOptionModel[]>> GetSelectableAsync(string key)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok((await optionLists.GetSelectableAsync(key, HttpContext.RequestAborted)).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<CrestOptionListModel>> CreateAsync([FromBody] CreateOptionListRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        try
        {
            return Ok(await optionLists.CreateListAsync(request.Key, request.DisplayText, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{key}/options")]
    public async Task<ActionResult<CrestOptionModel>> AddOptionAsync(string key, [FromBody] AddOptionRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        try
        {
            return Ok(await optionLists.AddOptionAsync(key, request.Key, request.DisplayText, request.Position, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    // The technical key is deliberately not updatable - it is the contract module
    // code matches on. Tenants relabel, reorder and hide instead.
    [HttpPut("{key}/options/{optionKey}")]
    public async Task<ActionResult<CrestOptionModel>> UpdateOptionAsync(string key, string optionKey, [FromBody] UpdateOptionRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        try
        {
            return Ok(await optionLists.UpdateOptionAsync(key, optionKey, request.DisplayText, request.Position, request.Hidden, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{key}/attach")]
    public async Task<IActionResult> AttachAsync(string key, [FromBody] AttachOptionListRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        try
        {
            await optionLists.AttachToContentTypeAsync(key, request.ContentType, request.FieldName, request.DisplayName, HttpContext.RequestAborted);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private Task<bool> CanViewAsync() => IsAuthorizedAsync(CrestOptionListPermissions.ViewOptionLists);

    private Task<bool> CanManageAsync() => IsAuthorizedAsync(CrestOptionListPermissions.ManageOptionLists);

    private async Task<bool> IsAuthorizedAsync(OrchardCore.Security.Permissions.Permission permission) =>
        await authorizationService.AuthorizeAsync(User, permission);
}

public sealed record CreateOptionListRequest(string Key, string DisplayText);

public sealed record AddOptionRequest(string Key, string DisplayText, int Position = 0);

public sealed record UpdateOptionRequest(string? DisplayText, int? Position, bool? Hidden);

public sealed record AttachOptionListRequest(string ContentType, string FieldName, string? DisplayName);
