using Crest.ContentGroups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Contents;
using OrchardCore.ContentTypes;
using OrchardCore.Environment.Shell.Scope;

namespace Crest.Controllers;

public sealed record CreateContentGroupRequest(string Key, string DisplayName, int Position = 0);
public sealed record UpdateContentGroupRequest(string? DisplayName, int? Position, bool? Hidden);
public sealed record AddContentGroupEntryRequest(string Kind, string Key);
public sealed record ContentGroupsSettingsRequest(bool AutoMenuPages);

/// <summary>
/// Content groups: reading is gated like the content-items list (ListContent);
/// reshaping groups is a definition-level act (EditContentTypes). Every write that can
/// change the menu (when auto pages are on) re-syncs the materialized menu as a
/// DEFERRED task, for the read-after-write reason documented on the per-type toggle.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/content-groups")]
public sealed class ContentGroupsController(
    CrestContentGroupService groups,
    IAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CrestContentGroupModel>>> ListAsync()
    {
        if (!await authorization.AuthorizeAsync(User, CommonPermissions.ListContent)) return Forbid();

        return Ok(await groups.ListAsync(HttpContext.RequestAborted));
    }

    [HttpGet("settings")]
    public async Task<ActionResult<CrestContentGroupsSettings>> GetSettingsAsync()
    {
        if (!await authorization.AuthorizeAsync(User, CommonPermissions.ListContent)) return Forbid();

        return Ok(await groups.GetSettingsAsync());
    }

    [HttpPut("settings")]
    public async Task<ActionResult<CrestContentGroupsSettings>> PutSettingsAsync(ContentGroupsSettingsRequest request)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        var settings = await groups.UpdateSettingsAsync(request.AutoMenuPages);
        DeferMenuSync();
        return Ok(settings);
    }

    [HttpPost]
    public Task<ActionResult<CrestContentGroupModel>> CreateAsync(CreateContentGroupRequest request)
        => WriteAsync(() => groups.CreateGroupAsync(request.Key, request.DisplayName, request.Position, HttpContext.RequestAborted));

    [HttpPut("{key}")]
    public Task<ActionResult<CrestContentGroupModel>> UpdateAsync(string key, UpdateContentGroupRequest request)
        => WriteAsync(() => groups.UpdateGroupAsync(key, request.DisplayName, request.Position, request.Hidden, HttpContext.RequestAborted));

    [HttpPost("{key}/entries")]
    public Task<ActionResult<CrestContentGroupModel>> AddEntryAsync(string key, AddContentGroupEntryRequest request)
        => WriteAsync(() => groups.AddEntryAsync(key, request.Kind, request.Key, HttpContext.RequestAborted));

    [HttpDelete("{key}/entries/{kind}/{entryKey}")]
    public async Task<ActionResult<CrestContentGroupModel>> RemoveEntryAsync(string key, string kind, string entryKey)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        var group = await groups.RemoveEntryAsync(key, kind, entryKey, HttpContext.RequestAborted);
        if (group is null) return NotFound();

        DeferMenuSync();
        return Ok(group);
    }

    [HttpDelete("{key}")]
    public async Task<IActionResult> DeleteAsync(string key)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        if (!await groups.DeleteGroupAsync(key, HttpContext.RequestAborted)) return NotFound();

        DeferMenuSync();
        return NoContent();
    }

    private async Task<ActionResult<CrestContentGroupModel>> WriteAsync(Func<Task<CrestContentGroupModel>> write)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        try
        {
            var group = await write();
            DeferMenuSync();
            return Ok(group);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    private void DeferMenuSync()
    {
        var actionContext = ControllerContext;
        ShellScope.AddDeferredTask(scope =>
            scope.ServiceProvider
                .GetRequiredService<Crest.Services.CrestProviderMenuSyncService>()
                .SyncAsync(actionContext));
    }
}
