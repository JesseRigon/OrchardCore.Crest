using System.Reflection;
using System.Text.Json;
using Crest.Global;
using Crest.Regions.Indexes;
using Crest.Regions.Models;
using Crest.Regions.Services;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;

namespace Crest.Regions.Global;

/// <summary>
/// The geo tree's global-store schema: index tables plus the standard-data loader. Fresh-
/// install repeatable: a new store gets the tables and the data in one pass; a re-run
/// against a populated store upserts by node id and is a no-op when nothing changed.
/// Bump <see cref="Version"/> with the data files to reload.
/// </summary>
public sealed class GeoGlobalSchema : ICrestGlobalSchema
{
    public string Name => "Crest.Regions.Geo";
    public int Version => 2;

    public IEnumerable<IIndexProvider> IndexProviders() =>
    [
        new GeoNodeIndexProvider(),
        new GeoPostalCodeIndexProvider(),
        new GeoBoundaryIndexProvider(),
        new AddressingMapIndexProvider(),
    ];

    public async Task ApplyAsync(ICrestGlobalSchemaContext context, int fromVersion, CancellationToken cancellationToken)
    {
        if (fromVersion < 1)
        {
            await CreateTablesAsync(context.SchemaBuilder);
        }

        // Order matters only for parents: countries and subdivisions, then US counties from
        // the Census gazetteer, then the US extras that parent onto counties.
        var existing = (await context.Session.Query<GeoNode, GeoNodeIndex>().ListAsync(cancellationToken))
            .ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        await UpsertNodesAsync(context.Session, existing, ReadData<GeoNodesFile>("geo-nodes.json").Nodes);
        await UpsertNodesAsync(context.Session, existing, ReadCounties());
        var us = ReadData<GeoNodesFile>("geo-nodes-us.json");
        await UpsertNodesAsync(context.Session, existing, us.Nodes);
        await ApplyGroupMembershipsAsync(context.Session, existing, us.GroupMemberships);
        await LoadAddressingMapsAsync(context.Session, cancellationToken);
    }

    private static async Task CreateTablesAsync(ISchemaBuilder schema)
    {
        await schema.CreateMapIndexTableAsync<GeoNodeIndex>(table => table
            .Column<string>(nameof(GeoNodeIndex.NodeId), column => column.WithLength(64))
            .Column<string>(nameof(GeoNodeIndex.Country), column => column.WithLength(8))
            .Column<int>(nameof(GeoNodeIndex.Level))
            .Column<string>(nameof(GeoNodeIndex.Kind), column => column.WithLength(16))
            .Column<string>(nameof(GeoNodeIndex.Name), column => column.WithLength(255))
            .Column<string>(nameof(GeoNodeIndex.Code), column => column.Nullable().WithLength(64)));
        await schema.AlterIndexTableAsync<GeoNodeIndex>(table => table.CreateIndex("IDX_GeoNodeIndex_NodeId", "DocumentId", "NodeId"));
        await schema.AlterIndexTableAsync<GeoNodeIndex>(table => table.CreateIndex("IDX_GeoNodeIndex_CountryLevel", "DocumentId", "Country", "Level"));

        await schema.CreateMapIndexTableAsync<GeoNodeParentIndex>(table => table
            .Column<string>(nameof(GeoNodeParentIndex.NodeId), column => column.WithLength(64))
            .Column<string>(nameof(GeoNodeParentIndex.ParentId), column => column.WithLength(64))
            .Column<string>(nameof(GeoNodeParentIndex.Relation), column => column.WithLength(8)));
        await schema.AlterIndexTableAsync<GeoNodeParentIndex>(table => table.CreateIndex("IDX_GeoNodeParentIndex_Node", "DocumentId", "NodeId"));
        await schema.AlterIndexTableAsync<GeoNodeParentIndex>(table => table.CreateIndex("IDX_GeoNodeParentIndex_Parent", "DocumentId", "ParentId", "Relation"));

        await schema.CreateMapIndexTableAsync<GeoPostalCodeIndex>(table => table
            .Column<string>(nameof(GeoPostalCodeIndex.Country), column => column.WithLength(8))
            .Column<string>(nameof(GeoPostalCodeIndex.Code), column => column.WithLength(32)));
        await schema.AlterIndexTableAsync<GeoPostalCodeIndex>(table => table.CreateIndex("IDX_GeoPostalCodeIndex_CountryCode", "DocumentId", "Country", "Code"));

        await schema.CreateMapIndexTableAsync<GeoPostalCodeNodeIndex>(table => table
            .Column<string>(nameof(GeoPostalCodeNodeIndex.Country), column => column.WithLength(8))
            .Column<string>(nameof(GeoPostalCodeNodeIndex.Code), column => column.WithLength(32))
            .Column<string>(nameof(GeoPostalCodeNodeIndex.NodeId), column => column.WithLength(64)));
        await schema.AlterIndexTableAsync<GeoPostalCodeNodeIndex>(table => table.CreateIndex("IDX_GeoPostalCodeNodeIndex_Node", "DocumentId", "NodeId"));

        await schema.CreateMapIndexTableAsync<GeoBoundaryIndex>(table => table
            .Column<string>(nameof(GeoBoundaryIndex.NodeId), column => column.WithLength(64)));
        await schema.AlterIndexTableAsync<GeoBoundaryIndex>(table => table.CreateIndex("IDX_GeoBoundaryIndex_Node", "DocumentId", "NodeId"));

        await schema.CreateMapIndexTableAsync<AddressingMapIndex>(table => table
            .Column<string>(nameof(AddressingMapIndex.Country), column => column.WithLength(8)));
        await schema.AlterIndexTableAsync<AddressingMapIndex>(table => table.CreateIndex("IDX_AddressingMapIndex_Country", "DocumentId", "Country"));
    }

    private static async Task UpsertNodesAsync(ISession session, Dictionary<string, GeoNode> existing, IEnumerable<GeoNode> nodes)
    {
        foreach (var incoming in nodes)
        {
            if (existing.TryGetValue(incoming.NodeId, out var current))
            {
                if (SameNode(current, incoming))
                {
                    continue;
                }

                current.Country = incoming.Country;
                current.Level = incoming.Level;
                current.Kind = incoming.Kind;
                current.Name = incoming.Name;
                current.Code = incoming.Code;
                current.Abbreviation = incoming.Abbreviation;
                current.ParentIds = [.. incoming.ParentIds];
                // Group membership is additive: a later file may have added groups.
                current.GroupIds = [.. current.GroupIds.Union(incoming.GroupIds, StringComparer.Ordinal)];
                current.Latitude = incoming.Latitude;
                current.Longitude = incoming.Longitude;
                await session.SaveAsync(current);
            }
            else
            {
                await session.SaveAsync(incoming);
                existing[incoming.NodeId] = incoming;
            }
        }
    }

    private static async Task ApplyGroupMembershipsAsync(ISession session, Dictionary<string, GeoNode> existing, IEnumerable<GroupMembership> memberships)
    {
        foreach (var membership in memberships)
        {
            foreach (var nodeId in membership.NodeIds)
            {
                if (existing.TryGetValue(nodeId, out var node) && !node.GroupIds.Contains(membership.GroupId, StringComparer.Ordinal))
                {
                    node.GroupIds.Add(membership.GroupId);
                    await session.SaveAsync(node);
                }
            }
        }
    }

    private static IEnumerable<GeoNode> ReadCounties()
    {
        using var reader = new StreamReader(OpenData("us-counties-gazetteer-2024.txt"));
        foreach (var node in CensusGazetteerParser.ReadCounties(reader))
        {
            yield return node;
        }
    }

    private static bool SameNode(GeoNode a, GeoNode b) =>
        a.Country == b.Country && a.Level == b.Level && a.Kind == b.Kind && a.Name == b.Name && a.Code == b.Code
        && a.Abbreviation == b.Abbreviation && a.ParentIds.SequenceEqual(b.ParentIds) && a.GroupIds.SequenceEqual(b.GroupIds)
        && a.Latitude == b.Latitude && a.Longitude == b.Longitude;

    private static async Task LoadAddressingMapsAsync(ISession session, CancellationToken cancellationToken)
    {
        var file = ReadData<AddressingMapsFile>("addressing-maps.json");
        var existing = (await session.Query<AddressingMap, AddressingMapIndex>().ListAsync(cancellationToken))
            .ToDictionary(map => map.Country, StringComparer.Ordinal);

        foreach (var incoming in file.Maps)
        {
            if (incoming.PostalCodePattern is null && file.PostalPatterns.TryGetValue(incoming.Country, out var pattern))
            {
                incoming.PostalCodePattern = pattern;
            }

            if (existing.TryGetValue(incoming.Country, out var current))
            {
                current.Levels = incoming.Levels;
                current.Fields = incoming.Fields;
                current.PostalCodePattern = incoming.PostalCodePattern;
                current.PostalCodeLabel = incoming.PostalCodeLabel;
                current.LocalityLevel = incoming.LocalityLevel;
                await session.SaveAsync(current);
            }
            else
            {
                await session.SaveAsync(incoming);
            }
        }

        // Countries with a postal pattern but no authored map get the generic map plus their pattern.
        var generic = file.Maps.FirstOrDefault(map => map.Country == AddressingMapDefaults.AnyCountry);
        if (generic is null)
        {
            return;
        }

        foreach (var (country, pattern) in file.PostalPatterns)
        {
            if (existing.ContainsKey(country) || file.Maps.Any(map => map.Country == country))
            {
                continue;
            }

            await session.SaveAsync(new AddressingMap
            {
                Country = country,
                Levels = generic.Levels.Select(level => new AddressingLevel { Level = level.Level, Label = level.Label, IsList = level.IsList, Required = level.Required, OnForm = level.OnForm }).ToList(),
                Fields = generic.Fields.Select(field => new AddressingField { Field = field.Field, Label = field.Label, Order = field.Order, Required = field.Required }).ToList(),
                PostalCodePattern = pattern,
                PostalCodeLabel = generic.PostalCodeLabel,
                LocalityLevel = generic.LocalityLevel,
            });
        }
    }

    private static T ReadData<T>(string fileName)
    {
        using var stream = OpenData(fileName);
        return JsonSerializer.Deserialize<T>(stream, JsonOptions) ?? throw new InvalidOperationException($"Geo data file '{fileName}' is empty.");
    }

    private static Stream OpenData(string fileName)
    {
        var assembly = typeof(GeoGlobalSchema).Assembly;
        var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(name => IsDataFile(name, fileName))
            ?? throw new InvalidOperationException($"Embedded geo data file '{fileName}' is missing from {assembly.GetName().Name}.");
        return assembly.GetManifestResourceStream(resourceName)!;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class GeoNodesFile
    {
        public int Version { get; set; }
        public List<GeoNode> Nodes { get; set; } = [];
        public List<GroupMembership> GroupMemberships { get; set; } = [];
    }

    private sealed class GroupMembership
    {
        public string GroupId { get; set; } = string.Empty;
        public List<string> NodeIds { get; set; } = [];
    }

    // OrchardCore.Module.Targets embeds files with ">" as the folder separator
    // ("Crest.Regions.Data>geo-nodes.json"); a plain SDK embed uses "."; match either.
    private static bool IsDataFile(string resourceName, string fileName) =>
        resourceName.EndsWith(">" + fileName, StringComparison.Ordinal)
        || resourceName.EndsWith("." + fileName, StringComparison.Ordinal)
        || string.Equals(resourceName, fileName, StringComparison.Ordinal);

    private sealed class AddressingMapsFile
    {
        public int Version { get; set; }
        public List<AddressingMap> Maps { get; set; } = [];
        public Dictionary<string, string> PostalPatterns { get; set; } = new(StringComparer.Ordinal);
    }
}

public static class AddressingMapDefaults
{
    /// <summary>The map used for a country that has no authored one.</summary>
    public const string AnyCountry = "*";
}
