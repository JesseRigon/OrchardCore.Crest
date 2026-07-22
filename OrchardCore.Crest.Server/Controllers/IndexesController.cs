using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Crest.Services;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;

namespace Crest.Controllers;

[ApiController, AutoValidateAntiforgeryToken, Route("api/crest/indexes")]
public sealed class CrestIndexesController(ICrestRequestAccess requestAccess) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CrestIndex[]>> ListAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, IndexingPermissions.ManageIndexes);
        if (access is null) return Forbid();
        var profiles = access.GetRequiredService<IIndexProfileStore>();
        var items = await profiles.GetAllAsync();
        return Ok(items.OrderBy(profile => profile.Name).Select(CrestIndex.From).ToArray());
    }

    [HttpPost("{id}/rebuild")]
    public async Task<ActionResult<CrestIndex>> RebuildAsync(string id)
    {
        var access = await requestAccess.AuthorizeAsync(User, IndexingPermissions.ManageIndexes);
        if (access is null) return Forbid();
        var profiles = access.GetRequiredService<IIndexProfileStore>();
        var profile = await profiles.FindByIdAsync(id);
        if (profile is null) return NotFound();
        var manager = access.GetRequiredService<IServiceProvider>().GetKeyedService<IIndexManager>(profile.ProviderName);
        if (manager is null) return Conflict($"No index manager is registered for provider '{profile.ProviderName}'.");
        return await manager.RebuildAsync(profile) ? Ok(CrestIndex.From(profile)) : BadRequest("The index could not be rebuilt.");
    }
}

public sealed record CrestIndex(string Id, string? Name, string? Provider, string? IndexName, string? Type, string? CreatedUtc)
{ public static CrestIndex From(IndexProfile profile) => new(profile.Id, profile.Name, profile.ProviderName, profile.IndexName, profile.Type, profile.CreatedUtc.ToString("O")); }
