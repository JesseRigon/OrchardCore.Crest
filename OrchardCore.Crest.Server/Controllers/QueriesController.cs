using System.Text.Json.Nodes;
using Crest.Services;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Queries;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/queries")]
public sealed class CrestQueriesController(ICrestRequestAccess requestAccess) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CrestQueryCatalog>> ListAsync([FromQuery] string? search = null)
    {
        var access = await requestAccess.AuthorizeAsync(User, Permissions.ManageQueries);
        if (access is null) return Forbid();

        var queries = await access.GetRequiredService<IQueryManager>().ListQueriesAsync(new QueryContext { Name = search });
        var sources = access.GetRequiredService<IEnumerable<IQuerySource>>()
            .Select(source => source.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(new CrestQueryCatalog(queries.OrderBy(query => query.Name, StringComparer.OrdinalIgnoreCase).Select(CrestQuery.From).ToArray(), sources));
    }

    [HttpPost]
    public async Task<ActionResult<CrestQuery>> CreateAsync([FromBody] CrestQueryWrite write)
    {
        var access = await requestAccess.AuthorizeAsync(User, Permissions.ManageQueries);
        if (access is null) return Forbid();
        if (string.IsNullOrWhiteSpace(write.Name) || string.IsNullOrWhiteSpace(write.Source)) return BadRequest("A query name and source are required.");

        var manager = access.GetRequiredService<IQueryManager>();
        if (await manager.GetQueryAsync(write.Name) is not null) return Conflict("A query with that name already exists.");

        var query = await manager.NewAsync(write.Source);
        if (query is null) return BadRequest("The selected query source is not available for this tenant.");

        Apply(query, write);
        await manager.UpdateAsync(query);
        return Ok(CrestQuery.From(query));
    }

    [HttpPut("{name}")]
    public async Task<ActionResult<CrestQuery>> UpdateAsync(string name, [FromBody] CrestQueryWrite write)
    {
        var access = await requestAccess.AuthorizeAsync(User, Permissions.ManageQueries);
        if (access is null) return Forbid();
        if (string.IsNullOrWhiteSpace(write.Name) || string.IsNullOrWhiteSpace(write.Source)) return BadRequest("A query name and source are required.");

        var manager = access.GetRequiredService<IQueryManager>();
        var query = await manager.GetQueryAsync(name);
        if (query is null) return NotFound();
        if (!string.Equals(name, write.Name, StringComparison.OrdinalIgnoreCase) && await manager.GetQueryAsync(write.Name) is not null) return Conflict("A query with that name already exists.");
        if (!access.GetRequiredService<IEnumerable<IQuerySource>>().Any(source => string.Equals(source.Name, write.Source, StringComparison.OrdinalIgnoreCase))) return BadRequest("The selected query source is not available for this tenant.");

        Apply(query, write);
        if (!string.Equals(name, write.Name, StringComparison.OrdinalIgnoreCase)) await manager.DeleteQueryAsync(name);
        await manager.UpdateAsync(query);
        return Ok(CrestQuery.From(query));
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> DeleteAsync(string name)
    {
        var access = await requestAccess.AuthorizeAsync(User, Permissions.ManageQueries);
        if (access is null) return Forbid();
        return await access.GetRequiredService<IQueryManager>().DeleteQueryAsync(name) ? NoContent() : NotFound();
    }

    [HttpPost("delete")]
    public async Task<IActionResult> DeleteManyAsync([FromBody] CrestQueryNames request)
    {
        var access = await requestAccess.AuthorizeAsync(User, Permissions.ManageQueries);
        if (access is null) return Forbid();
        var names = request.Names?.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        if (names.Length == 0) return BadRequest("Select at least one query.");
        return await access.GetRequiredService<IQueryManager>().DeleteQueryAsync(names) ? NoContent() : NotFound();
    }

    private static void Apply(Query query, CrestQueryWrite write)
    {
        query.Name = write.Name.Trim();
        query.Source = write.Source.Trim();
        query.Schema = write.Schema?.Trim();
        query.ReturnContentItems = write.ReturnContentItems;
        query.Properties = write.Properties?.DeepClone() as JsonObject ?? [];
    }
}

public sealed record CrestQueryCatalog(CrestQuery[] Queries, string[] Sources);
public sealed record CrestQuery(string Name, string Source, string? Schema, bool ReturnContentItems, JsonObject Properties)
{
    public static CrestQuery From(Query query) => new(query.Name, query.Source, query.Schema, query.ReturnContentItems, query.Properties.DeepClone() as JsonObject ?? []);
}
public sealed record CrestQueryWrite(string Name, string Source, string? Schema, bool ReturnContentItems, JsonObject? Properties);
public sealed record CrestQueryNames(string[]? Names);
