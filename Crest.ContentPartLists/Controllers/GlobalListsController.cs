using Crest.Global;
using Crest.Global.Lists;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Environment.Shell;
using YesSql;

namespace Crest.Controllers;

/// <summary>
/// Super-tenant editing of the STANDARD rows of global lists (plans/global.md › Who can
/// write). Every action requires both the permission and the Default shell: an ordinary
/// tenant gets 404, not 403, because there is no route to this there, not a hidden one.
/// Host rows survive data-file reloads; Standard rows are the loader's.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route(GlobalListsRoutes.Api)]
public sealed class GlobalListsController(
    ICrestGlobalStore globalStore,
    ShellSettings shellSettings,
    IAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GlobalList>>> ListAsync(CancellationToken cancellationToken)
    {
        if (await GateAsync() is { } gate)
        {
            return gate;
        }

        var lists = await globalStore.ReadAsync((session, ct) => session.Query<GlobalList, GlobalListIndex>().ListAsync(ct), cancellationToken);
        return lists.OrderBy(list => list.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<GlobalList>> GetAsync(string key, CancellationToken cancellationToken)
    {
        if (await GateAsync() is { } gate)
        {
            return gate;
        }

        var list = await globalStore.ReadAsync((session, ct) => session.Query<GlobalList, GlobalListIndex>(index => index.Key == key).FirstOrDefaultAsync(ct), cancellationToken);
        return list is null ? NotFound() : list;
    }

    /// <summary>Adds or updates a Host option. Standard options are the data file's and are refused; correct them in the next data version.</summary>
    [HttpPut("{key}/options/{optionKey}")]
    public async Task<ActionResult<GlobalOption>> UpsertOptionAsync(string key, string optionKey, [FromBody] GlobalOptionWrite write, CancellationToken cancellationToken)
    {
        if (await GateAsync() is { } gate)
        {
            return gate;
        }

        GlobalOption? result = null;
        string? refused = null;
        await globalStore.WriteAsync(async (session, ct) =>
        {
            var list = await session.Query<GlobalList, GlobalListIndex>(index => index.Key == key).FirstOrDefaultAsync(ct);
            if (list is null)
            {
                refused = $"No global list '{key}'.";
                return;
            }

            var option = list.Options.FirstOrDefault(candidate => string.Equals(candidate.Key, optionKey, StringComparison.OrdinalIgnoreCase));
            if (option is { Source: GlobalOptionSources.Standard })
            {
                refused = $"'{optionKey}' is a Standard row of '{key}'; it is corrected in the next data version, not edited here.";
                return;
            }

            option ??= new GlobalOption { Key = optionKey.Trim(), Source = GlobalOptionSources.Host };
            if (!list.Options.Contains(option))
            {
                list.Options.Add(option);
            }

            option.DisplayText = write.DisplayText;
            option.DisplayTextPlural = write.DisplayTextPlural;
            option.Value = write.Value;
            option.Category = write.Category;
            option.Position = write.Position ?? option.Position;
            await session.SaveAsync(list);
            result = option;
        }, cancellationToken);

        return refused is not null ? Conflict(refused) : result!;
    }

    [HttpDelete("{key}/options/{optionKey}")]
    public async Task<IActionResult> DeleteOptionAsync(string key, string optionKey, CancellationToken cancellationToken)
    {
        if (await GateAsync() is { } gate)
        {
            return gate;
        }

        string? refused = null;
        await globalStore.WriteAsync(async (session, ct) =>
        {
            var list = await session.Query<GlobalList, GlobalListIndex>(index => index.Key == key).FirstOrDefaultAsync(ct);
            var option = list?.Options.FirstOrDefault(candidate => string.Equals(candidate.Key, optionKey, StringComparison.OrdinalIgnoreCase));
            if (list is null || option is null)
            {
                refused = "Not found.";
                return;
            }

            if (option.Source == GlobalOptionSources.Standard)
            {
                refused = "Standard rows are not deleted here.";
                return;
            }

            list.Options.Remove(option);
            await session.SaveAsync(list);
        }, cancellationToken);

        return refused is null ? NoContent() : Conflict(refused);
    }

    private async Task<ActionResult?> GateAsync()
    {
        if (!shellSettings.IsDefaultShell())
        {
            return NotFound();
        }

        return await authorization.AuthorizeAsync(User, CrestGlobalPermissions.ManageGlobalReferenceData) ? null : Forbid();
    }

    public sealed record GlobalOptionWrite(string DisplayText, string? DisplayTextPlural, string? Value, string? Category, int? Position);
}

public static class GlobalListsRoutes
{
    public const string Api = "api/crest/global/lists";
}
