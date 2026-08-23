using System.Text.Json.Nodes;
using Crest.Services;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Queries;
using QueriesPermissions = OrchardCore.Queries.Permissions;

namespace Crest.ViewModels;

public sealed record CrestQueryCatalog(CrestQuery[] Queries, string[] Sources);

public sealed record CrestQuery(string Name, string Source, string? Schema, bool ReturnContentItems, JsonObject Properties)
{
    public static CrestQuery From(Query query) => new(query.Name, query.Source, query.Schema, query.ReturnContentItems, query.Properties.DeepClone() as JsonObject ?? []);
}

public sealed record CrestQueryWrite(string Name, string Source, string? Schema, bool ReturnContentItems, JsonObject? Properties);

public sealed record CrestQueryNames(string[]? Names);
