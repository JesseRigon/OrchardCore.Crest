namespace Crest.Regions.Models;

// The geo tree (plans/regions-and-locations.md). One generic DAG of numbered levels
// labelled per country; level 1 is always the country. Standard nodes live in the global
// store and are read-only to tenants; tenant additions and presentation overrides live
// in the tenant's own store as the overlay documents below.

public static class GeoNodeKinds
{
    /// <summary>A level in a country's hierarchy: state, county, commune...</summary>
    public const string Level = "Level";

    /// <summary>Outside the strict hierarchy: the EU, a transit district, a sales territory. A node may belong to any number of these.</summary>
    public const string Grouping = "Grouping";
}

public static class GeoSources
{
    public const string Standard = "Standard";
    public const string Tenant = "Tenant";
}

/// <summary>A standard node, in the global store. Node ids are stable strings that survive data versions (US, US-IL, US-IL-031).</summary>
public sealed class GeoNode
{
    public long Id { get; set; }
    public string NodeId { get; set; } = string.Empty;
    /// <summary>Level-1 node id this node belongs to. A country's own row carries itself.</summary>
    public string Country { get; set; } = string.Empty;
    /// <summary>1 for the country; deeper as the country's addressing map dictates. 0 for groupings.</summary>
    public int Level { get; set; }
    public string Kind { get; set; } = GeoNodeKinds.Level;
    public string Name { get; set; } = string.Empty;
    /// <summary>The source standard's code for it: ISO 3166-2, FIPS, INSEE.</summary>
    public string? Code { get; set; }
    /// <summary>Postal abbreviation or short form, when the country has one (IL, ON).</summary>
    public string? Abbreviation { get; set; }
    /// <summary>Hierarchy parents. Usually one; several when the node straddles (Kansas City, four counties).</summary>
    public List<string> ParentIds { get; set; } = [];
    /// <summary>Grouping memberships.</summary>
    public List<string> GroupIds { get; set; } = [];
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

/// <summary>A separate, uniform record: a code maps to one or MORE nodes, since a ZIP can straddle counties.</summary>
public sealed class GeoPostalCode
{
    public long Id { get; set; }
    public string Country { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public List<string> NodeIds { get; set; } = [];
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

/// <summary>Geometry for point-in-polygon, its own document so it never rides on the node. Always a MultiPolygon: geographies are non-contiguous.</summary>
public sealed class GeoBoundary
{
    public long Id { get; set; }
    public string NodeId { get; set; } = string.Empty;
    /// <summary>"WKT" or "GeoJSON".</summary>
    public string Format { get; set; } = "WKT";
    public string Geometry { get; set; } = string.Empty;
}

// ---- Tenant overlay (tenant store) ----

/// <summary>A tenant-added node, always parented onto standard or tenant nodes. Never edits a standard one.</summary>
public sealed class GeoTenantNode
{
    public long Id { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int Level { get; set; }
    public string Kind { get; set; } = GeoNodeKinds.Level;
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Abbreviation { get; set; }
    public List<string> ParentIds { get; set; } = [];
    public List<string> GroupIds { get; set; } = [];
    /// <summary>Set when a standard-data version bump removed a parent this node referenced. Flagged, never dropped.</summary>
    public bool Orphaned { get; set; }
}

/// <summary>Presentation override of a standard node: label, hidden. Never data.</summary>
public sealed class GeoNodeOverride
{
    public long Id { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string? Label { get; set; }
    public bool Hidden { get; set; }
}

// ---- The merged view every consumer reads ----

public sealed record GeoNodeModel(
    string NodeId,
    string Country,
    int Level,
    string Kind,
    string Name,
    string? Code,
    string? Abbreviation,
    IReadOnlyList<string> ParentIds,
    IReadOnlyList<string> GroupIds,
    string Source,
    bool Hidden = false,
    double? Latitude = null,
    double? Longitude = null);

public sealed record GeoPostalCodeModel(string Country, string Code, IReadOnlyList<string> NodeIds, double? Latitude, double? Longitude);

/// <summary>An address's resolved geo stack: exactly one node per level, country down.</summary>
public sealed record GeoStack(IReadOnlyList<GeoNodeModel> Nodes)
{
    public static readonly GeoStack Empty = new([]);
    public GeoNodeModel? Country => Nodes.FirstOrDefault(node => node.Level == 1);
    public GeoNodeModel? AtLevel(int level) => Nodes.FirstOrDefault(node => node.Level == level);
    public IReadOnlyList<string> NodeIds => Nodes.Select(node => node.NodeId).ToArray();
}
