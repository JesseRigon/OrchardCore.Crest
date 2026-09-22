using Crest.Regions.Models;
using YesSql.Indexes;

namespace Crest.Regions.Indexes;

// ---- Global store ----

public sealed class GeoNodeIndex : MapIndex
{
    public string NodeId { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int Level { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
}

/// <summary>One row per (node, parent): the DAG's edges, walkable in both directions.</summary>
public sealed class GeoNodeParentIndex : MapIndex
{
    public string NodeId { get; set; } = string.Empty;
    public string ParentId { get; set; } = string.Empty;
    /// <summary>"Parent" for hierarchy, "Group" for grouping membership.</summary>
    public string Relation { get; set; } = GeoRelations.Parent;
}

public static class GeoRelations
{
    public const string Parent = "Parent";
    public const string Group = "Group";
}

public sealed class GeoPostalCodeIndex : MapIndex
{
    public string Country { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public sealed class GeoPostalCodeNodeIndex : MapIndex
{
    public string Country { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
}

public sealed class GeoBoundaryIndex : MapIndex
{
    public string NodeId { get; set; } = string.Empty;
}

public sealed class AddressingMapIndex : MapIndex
{
    public string Country { get; set; } = string.Empty;
}

public sealed class GeoNodeIndexProvider : IndexProvider<GeoNode>
{
    public override void Describe(DescribeContext<GeoNode> context)
    {
        context.For<GeoNodeIndex>().Map(node => new GeoNodeIndex
        {
            NodeId = node.NodeId,
            Country = node.Country,
            Level = node.Level,
            Kind = node.Kind,
            Name = node.Name,
            Code = node.Code,
        });

        context.For<GeoNodeParentIndex>().Map(node =>
            node.ParentIds.Select(parent => new GeoNodeParentIndex { NodeId = node.NodeId, ParentId = parent, Relation = GeoRelations.Parent })
                .Concat(node.GroupIds.Select(group => new GeoNodeParentIndex { NodeId = node.NodeId, ParentId = group, Relation = GeoRelations.Group }))
                .ToArray());
    }
}

public sealed class GeoPostalCodeIndexProvider : IndexProvider<GeoPostalCode>
{
    public override void Describe(DescribeContext<GeoPostalCode> context)
    {
        context.For<GeoPostalCodeIndex>().Map(code => new GeoPostalCodeIndex { Country = code.Country, Code = code.Code });
        context.For<GeoPostalCodeNodeIndex>().Map(code =>
            code.NodeIds.Select(node => new GeoPostalCodeNodeIndex { Country = code.Country, Code = code.Code, NodeId = node }).ToArray());
    }
}

public sealed class GeoBoundaryIndexProvider : IndexProvider<GeoBoundary>
{
    public override void Describe(DescribeContext<GeoBoundary> context) =>
        context.For<GeoBoundaryIndex>().Map(boundary => new GeoBoundaryIndex { NodeId = boundary.NodeId });
}

public sealed class AddressingMapIndexProvider : IndexProvider<AddressingMap>
{
    public override void Describe(DescribeContext<AddressingMap> context) =>
        context.For<AddressingMapIndex>().Map(map => new AddressingMapIndex { Country = map.Country });
}

// ---- Tenant store ----

public sealed class GeoTenantNodeIndex : MapIndex
{
    public string NodeId { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int Level { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Orphaned { get; set; }
}

public sealed class GeoTenantNodeParentIndex : MapIndex
{
    public string NodeId { get; set; } = string.Empty;
    public string ParentId { get; set; } = string.Empty;
    public string Relation { get; set; } = GeoRelations.Parent;
}

public sealed class GeoNodeOverrideIndex : MapIndex
{
    public string NodeId { get; set; } = string.Empty;
}

public sealed class GeoTenantNodeIndexProvider : IndexProvider<GeoTenantNode>
{
    public override void Describe(DescribeContext<GeoTenantNode> context)
    {
        context.For<GeoTenantNodeIndex>().Map(node => new GeoTenantNodeIndex
        {
            NodeId = node.NodeId,
            Country = node.Country,
            Level = node.Level,
            Kind = node.Kind,
            Name = node.Name,
            Orphaned = node.Orphaned,
        });
        context.For<GeoTenantNodeParentIndex>().Map(node =>
            node.ParentIds.Select(parent => new GeoTenantNodeParentIndex { NodeId = node.NodeId, ParentId = parent, Relation = GeoRelations.Parent })
                .Concat(node.GroupIds.Select(group => new GeoTenantNodeParentIndex { NodeId = node.NodeId, ParentId = group, Relation = GeoRelations.Group }))
                .ToArray());
    }
}

public sealed class GeoNodeOverrideIndexProvider : IndexProvider<GeoNodeOverride>
{
    public override void Describe(DescribeContext<GeoNodeOverride> context) =>
        context.For<GeoNodeOverrideIndex>().Map(item => new GeoNodeOverrideIndex { NodeId = item.NodeId });
}
