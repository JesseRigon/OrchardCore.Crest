using Crest.Models;
using Crest.Permissions;
using Crest.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Controllers;

// The management API behind Content Part Lists. The SCREEN lives in this module's
// blazor-wasm project; Fruitful modules only declare and consume sets, they never own
// these editors. The provider-generic picker endpoints (api/crest/option-sources/*)
// live in Crest.Server with the IOptionSourceProvider abstraction itself.
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/content-part-lists")]
public sealed class ContentPartListsController(
    ICrestContentPartListService contentPartLists,
    IMeasurementPreferenceService measurementPreferences,
    IAuthorizationService authorizationService) : ControllerBase
{
    /// <summary>
    /// The culture-to-units dataset: which measurement system the culture uses and
    /// the preferred global.uom unit key per dimension. Defaults to the request's
    /// resolved culture; pass ?culture= to ask for another. The seam localization or
    /// tenant settings can later adapt.
    /// </summary>
    [HttpGet("uom/preferences")]
    public async Task<ActionResult<MeasurementPreferences>> GetUomPreferencesAsync([FromQuery] string? culture = null)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok(measurementPreferences.Resolve(culture));
    }

    [HttpGet]
    public async Task<ActionResult<CrestContentPartListModel[]>> ListAsync()
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok((await contentPartLists.ListAsync(HttpContext.RequestAborted)).ToArray());
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<CrestContentPartListModel>> GetAsync(string key)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var list = await contentPartLists.GetAsync(key, HttpContext.RequestAborted);
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

        return Ok((await contentPartLists.GetSelectableAsync(key, HttpContext.RequestAborted)).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<CrestContentPartListModel>> CreateAsync([FromBody] CreateContentPartListRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        try
        {
            return Ok(await contentPartLists.CreateListAsync(request.Key, request.DisplayText, HttpContext.RequestAborted));
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

        var list = await contentPartLists.GetAsync(key, HttpContext.RequestAborted);
        if (list is not null)
        {
            if (CrestContentPartListRules.LockActive(list.EditLock))
            {
                return LockedProblem("Options cannot be added while the list is locked for editing.");
            }

            // Adds stay allowed under a data lock, but the category vocabulary is
            // frozen: logic keyed on the category set must cover tenant additions.
            var categoryValidation = CrestContentPartListRules.ValidateAddedCategory(list, request.Category);
            if (!categoryValidation.IsValid)
            {
                return LockedProblem(categoryValidation.Error!);
            }
        }

        try
        {
            return Ok(await contentPartLists.AddOptionAsync(key, request.Key, request.DisplayText, request.Position, request.Category, request.DisplayTextPlural, request.Value, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Rewrites the list's manual order: Position becomes the index in the
    /// posted key array, which must name every option exactly once. Reordering is
    /// display-level, so a data lock permits it; an edit lock does not.</summary>
    [HttpPut("{key}/order")]
    public async Task<ActionResult<CrestContentPartListModel>> ReorderAsync(string key, [FromBody] ReorderOptionsRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        if (await IsEditLockedAsync(key))
        {
            return LockedProblem("The list is locked for editing.");
        }

        try
        {
            return Ok(await contentPartLists.ReorderOptionsAsync(key, request.Keys ?? [], HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Places or lifts tenant-authority locks on a list. Module-placed
    /// locks are refused in both directions. Super admin only.</summary>
    [HttpPut("{key}/locks")]
    public async Task<ActionResult<CrestContentPartListModel>> UpdateLocksAsync(string key, [FromBody] UpdateContentPartListLocksRequest request)
    {
        if (!await IsAuthorizedAsync(CrestContentPartListPermissions.LockContentPartLists))
        {
            return Forbid();
        }

        try
        {
            return Ok(await contentPartLists.UpdateListLocksAsync(key, request.DataLock, request.EditLock, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Deletes a tenant-owned list. Module-seeded lists are refused - their
    /// keys are the owning module's contract, so they can only be hidden.</summary>
    [HttpDelete("{key}")]
    public async Task<IActionResult> DeleteAsync(string key)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        if (await IsEditLockedAsync(key))
        {
            return LockedProblem("The list is locked for editing and cannot be deleted.");
        }

        try
        {
            await contentPartLists.DeleteListAsync(key, HttpContext.RequestAborted);
            return NoContent();
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

        var list = await contentPartLists.GetAsync(key, HttpContext.RequestAborted);
        if (list is not null)
        {
            if (CrestContentPartListRules.LockActive(list.EditLock))
            {
                return LockedProblem("The list is locked for editing.");
            }

            // The data lock freezes the MACHINE surface - category assignments and
            // machine values; relabel/hide/reposition are display-level and stay open.
            if (request.Category is not null && CrestContentPartListRules.LockActive(list.DataLock))
            {
                return LockedProblem("This list's categories are locked and cannot be reassigned.");
            }

            if (request.Value is not null && CrestContentPartListRules.LockActive(list.DataLock))
            {
                return LockedProblem("This list's machine values are locked and cannot be changed.");
            }
        }

        try
        {
            return Ok(await contentPartLists.UpdateOptionAsync(key, optionKey, request.DisplayText, request.Position, request.Hidden, request.Category, request.DisplayTextPlural, request.Value, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{key}/attach")]
    public async Task<IActionResult> AttachAsync(string key, [FromBody] AttachContentPartListRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        try
        {
            await contentPartLists.AttachToContentTypeAsync(key, request.ContentType, request.FieldName, request.DisplayName, HttpContext.RequestAborted);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private async Task<bool> IsEditLockedAsync(string key)
    {
        var list = await contentPartLists.GetAsync(key, HttpContext.RequestAborted);
        return list is not null && CrestContentPartListRules.LockActive(list.EditLock);
    }

    private ObjectResult LockedProblem(string message) =>
        Problem(message, statusCode: StatusCodes.Status409Conflict);

    private Task<bool> CanViewAsync() => IsAuthorizedAsync(CrestContentPartListPermissions.ViewContentPartLists);

    private Task<bool> CanManageAsync() => IsAuthorizedAsync(CrestContentPartListPermissions.ManageContentPartLists);

    private async Task<bool> IsAuthorizedAsync(OrchardCore.Security.Permissions.Permission permission) =>
        await authorizationService.AuthorizeAsync(User, permission);
}

public sealed record CreateContentPartListRequest(string Key, string DisplayText);

public sealed record AddOptionRequest(string Key, string DisplayText, int Position = 0, string? Category = null, string? DisplayTextPlural = null, string? Value = null);

/// <summary>Null leaves a value unchanged; a BLANK DisplayTextPlural or Value clears
/// it (plural falls back to the singular; a cleared Value means the key is the
/// option's only machine datum).</summary>
public sealed record UpdateOptionRequest(string? DisplayText, int? Position, bool? Hidden, string? Category = null, string? DisplayTextPlural = null, string? Value = null);

/// <summary>Every option key in the desired manual order.</summary>
public sealed record ReorderOptionsRequest(IReadOnlyList<string>? Keys);

/// <summary>Tenant-authority lock changes; null leaves a lock as it is.</summary>
public sealed record UpdateContentPartListLocksRequest(bool? DataLock, bool? EditLock);

public sealed record AttachContentPartListRequest(string ContentType, string FieldName, string? DisplayName);
