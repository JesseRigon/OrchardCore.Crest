using Crest.Regions.Models;

namespace Crest.Regions.Services;

/// <summary>
/// The merged view of the geo tree: standard nodes from the global store with the tenant's
/// overlay (additions, label/hidden overrides) applied. Node ids are the only handle
/// anything else stores.
/// </summary>
public interface IGeoService
{
    Task<GeoNodeModel?> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, GeoNodeModel>> GetNodesAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default);

    /// <summary>Hierarchy children of a node (not grouping members).</summary>
    Task<IReadOnlyList<GeoNodeModel>> GetChildrenAsync(string parentId, CancellationToken cancellationToken = default);

    /// <summary>Every level-N node of a country, for list-typed address fields.</summary>
    Task<IReadOnlyList<GeoNodeModel>> GetLevelAsync(string country, int level, CancellationToken cancellationToken = default);

    /// <summary>All hierarchy ancestors of a node along every parent path, nearest first. Terminates because the graph is acyclic by construction.</summary>
    Task<IReadOnlyList<GeoNodeModel>> GetAncestorsAsync(string nodeId, CancellationToken cancellationToken = default);

    /// <summary>Grouping nodes the node belongs to, directly or through any ancestor.</summary>
    Task<IReadOnlyList<GeoNodeModel>> GetGroupsAsync(string nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the given node ids form ONE stack - one node per level and each a
    /// child of the one above - and returns it ordered country-down. Throws
    /// <see cref="GeoStackException"/> when a node is missing, two share a level, or a
    /// node is not beneath its predecessor; an address with a multi-parent node must name
    /// the parent explicitly, so an ambiguous chain is an error, never a guess.
    /// </summary>
    Task<GeoStack> ResolveStackAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default);

    Task<GeoPostalCodeModel?> GetPostalCodeAsync(string country, string code, CancellationToken cancellationToken = default);

    Task<AddressingMap> GetAddressingMapAsync(string country, CancellationToken cancellationToken = default);

    // ---- Tenant overlay writes ----

    /// <summary>Adds a tenant node parented onto existing nodes. Refuses cycles: none of the parents may be the node or its descendant.</summary>
    Task<GeoNodeModel> AddTenantNodeAsync(GeoTenantNode node, CancellationToken cancellationToken = default);

    Task SetOverrideAsync(string nodeId, string? label, bool hidden, CancellationToken cancellationToken = default);
}

public sealed class GeoStackException(string message) : InvalidOperationException(message);
