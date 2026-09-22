using Crest.Global;
using Crest.Regions.Global;
using Crest.Regions.Indexes;
using Crest.Regions.Models;
using YesSql;
using YesSql.Services;

namespace Crest.Regions.Services;

public sealed class GeoService(ICrestGlobalStore globalStore, ICrestGlobalCache cache, ISession tenantSession) : IGeoService
{
    // Standard nodes are read a country at a time through the host cache: a country's
    // whole tree (thousands of rows at most) is one version-keyed entry, so the hot path
    // never queries the global store per node.
    private static string CountryOf(string nodeId)
    {
        var dash = nodeId.IndexOf('-');
        return dash > 0 ? nodeId[..dash] : nodeId;
    }

    private Task<IReadOnlyDictionary<string, GeoNode>> StandardCountryAsync(string country, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync<IReadOnlyDictionary<string, GeoNode>>($"geo:nodes:{country}", async ct =>
        {
            var rows = await globalStore.ReadAsync(
                (session, innerCt) => session.Query<GeoNode, GeoNodeIndex>(index => index.Country == country).ListAsync(innerCt),
                ct);
            return rows.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        }, cancellationToken);

    // Per-request memo: address resolution asks for the same handful of nodes repeatedly.
    private readonly Dictionary<string, GeoNodeModel?> _nodes = new(StringComparer.Ordinal);

    public async Task<GeoNodeModel?> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (_nodes.TryGetValue(nodeId, out var cached))
        {
            return cached;
        }

        var found = await GetNodesAsync([nodeId], cancellationToken);
        return found.GetValueOrDefault(nodeId);
    }

    public async Task<IReadOnlyDictionary<string, GeoNodeModel>> GetNodesAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
    {
        var wanted = nodeIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        var result = new Dictionary<string, GeoNodeModel>(StringComparer.Ordinal);
        var missing = new List<string>();
        foreach (var id in wanted)
        {
            if (_nodes.TryGetValue(id, out var cached))
            {
                if (cached is not null)
                {
                    result[id] = cached;
                }
            }
            else
            {
                missing.Add(id);
            }
        }

        if (missing.Count == 0)
        {
            return result;
        }

        var standard = new List<GeoNode>();
        foreach (var group in missing.GroupBy(CountryOf, StringComparer.Ordinal))
        {
            var country = await StandardCountryAsync(group.Key, cancellationToken);
            standard.AddRange(group.Select(id => country.GetValueOrDefault(id)).Where(node => node is not null).Select(node => node!));
        }

        var tenant = await tenantSession.Query<GeoTenantNode, GeoTenantNodeIndex>(index => index.NodeId.IsIn(missing)).ListAsync(cancellationToken);
        var overrides = (await tenantSession.Query<GeoNodeOverride, GeoNodeOverrideIndex>(index => index.NodeId.IsIn(missing)).ListAsync(cancellationToken))
            .ToDictionary(item => item.NodeId, StringComparer.Ordinal);

        foreach (var node in standard)
        {
            result[node.NodeId] = Merge(ToModel(node), overrides.GetValueOrDefault(node.NodeId));
        }

        foreach (var node in tenant)
        {
            // A tenant node never shadows a standard one with the same id; ids are namespaced on write.
            result.TryAdd(node.NodeId, Merge(ToModel(node), overrides.GetValueOrDefault(node.NodeId)));
        }

        foreach (var id in missing)
        {
            _nodes[id] = result.GetValueOrDefault(id);
        }

        return result;
    }

    public async Task<IReadOnlyList<GeoNodeModel>> GetChildrenAsync(string parentId, CancellationToken cancellationToken = default)
    {
        var country = await StandardCountryAsync(CountryOf(parentId), cancellationToken);
        var standardIds = country.Values.Where(node => node.ParentIds.Contains(parentId, StringComparer.Ordinal)).Select(node => node.NodeId);
        var tenantIds = await tenantSession.QueryIndex<GeoTenantNodeParentIndex>(index => index.ParentId == parentId && index.Relation == GeoRelations.Parent).ListAsync(cancellationToken);
        var ids = standardIds.Concat(tenantIds.Select(row => row.NodeId));
        var nodes = await GetNodesAsync(ids, cancellationToken);
        return Ordered(nodes.Values);
    }

    public async Task<IReadOnlyList<GeoNodeModel>> GetLevelAsync(string country, int level, CancellationToken cancellationToken = default)
    {
        var standard = (await StandardCountryAsync(country, cancellationToken)).Values.Where(node => node.Level == level);
        var tenant = await tenantSession.Query<GeoTenantNode, GeoTenantNodeIndex>(index => index.Country == country && index.Level == level).ListAsync(cancellationToken);
        var ids = standard.Select(node => node.NodeId).Concat(tenant.Select(node => node.NodeId));
        var nodes = await GetNodesAsync(ids, cancellationToken);
        return Ordered(nodes.Values.Where(node => !node.Hidden));
    }

    public async Task<IReadOnlyList<GeoNodeModel>> GetAncestorsAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var ordered = new List<GeoNodeModel>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { nodeId };
        var frontier = new List<string> { nodeId };

        while (frontier.Count > 0)
        {
            var current = await GetNodesAsync(frontier, cancellationToken);
            var next = new List<string>();
            foreach (var id in frontier)
            {
                if (!current.TryGetValue(id, out var node))
                {
                    continue;
                }

                foreach (var parent in node.ParentIds)
                {
                    if (seen.Add(parent))
                    {
                        next.Add(parent);
                    }
                }
            }

            var parents = await GetNodesAsync(next, cancellationToken);
            ordered.AddRange(next.Select(id => parents.GetValueOrDefault(id)).Where(node => node is not null).Select(node => node!));
            frontier = next;
        }

        return ordered;
    }

    public async Task<IReadOnlyList<GeoNodeModel>> GetGroupsAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var self = await GetNodeAsync(nodeId, cancellationToken);
        if (self is null)
        {
            return [];
        }

        var chain = new List<GeoNodeModel> { self };
        chain.AddRange(await GetAncestorsAsync(nodeId, cancellationToken));
        var groupIds = chain.SelectMany(node => node.GroupIds).Distinct(StringComparer.Ordinal);
        var groups = await GetNodesAsync(groupIds, cancellationToken);
        return Ordered(groups.Values);
    }

    public async Task<GeoStack> ResolveStackAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
    {
        var ids = nodeIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0)
        {
            return GeoStack.Empty;
        }

        var nodes = await GetNodesAsync(ids, cancellationToken);
        var missing = ids.Where(id => !nodes.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
        {
            throw new GeoStackException($"Unknown geo node(s): {string.Join(", ", missing)}.");
        }

        var ordered = nodes.Values.Where(node => node.Kind == GeoNodeKinds.Level).OrderBy(node => node.Level).ToArray();
        var duplicateLevel = ordered.GroupBy(node => node.Level).FirstOrDefault(group => group.Count() > 1);
        if (duplicateLevel is not null)
        {
            throw new GeoStackException($"An address has exactly one node per level; level {duplicateLevel.Key} was given {duplicateLevel.Count()} ({string.Join(", ", duplicateLevel.Select(node => node.NodeId))}).");
        }

        if (ordered.Length > 0 && ordered[0].Level != 1)
        {
            throw new GeoStackException("A geo stack starts at the country (level 1).");
        }

        for (var i = 1; i < ordered.Length; i++)
        {
            var above = ordered[i - 1];
            var node = ordered[i];
            if (!node.ParentIds.Contains(above.NodeId, StringComparer.Ordinal))
            {
                // Gaps are allowed (a country may not use level 3 for this address); the
                // node just has to sit somewhere beneath the previous one.
                var ancestors = await GetAncestorsAsync(node.NodeId, cancellationToken);
                if (!ancestors.Any(ancestor => ancestor.NodeId == above.NodeId))
                {
                    throw new GeoStackException($"'{node.NodeId}' is not within '{above.NodeId}'.");
                }
            }
        }

        return new GeoStack(ordered);
    }

    public async Task<GeoPostalCodeModel?> GetPostalCodeAsync(string country, string code, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToUpperInvariant().Replace(" ", string.Empty);
        var found = await cache.GetOrCreateAsync($"geo:postal:{country}:{normalized}", ct => globalStore.ReadAsync(
            (session, innerCt) => session.Query<GeoPostalCode, GeoPostalCodeIndex>(index => index.Country == country && index.Code == normalized).FirstOrDefaultAsync(innerCt),
            ct), cancellationToken);
        return found is null ? null : new GeoPostalCodeModel(found.Country, found.Code, found.NodeIds, found.Latitude, found.Longitude);
    }

    public async Task<AddressingMap> GetAddressingMapAsync(string country, CancellationToken cancellationToken = default)
    {
        var map = await cache.GetOrCreateAsync($"geo:map:{country}", ct => globalStore.ReadAsync(
            (session, innerCt) => session.Query<AddressingMap, AddressingMapIndex>(index => index.Country == country).FirstOrDefaultAsync(innerCt),
            ct), cancellationToken);
        map ??= await cache.GetOrCreateAsync($"geo:map:{AddressingMapDefaults.AnyCountry}", ct => globalStore.ReadAsync(
            (session, innerCt) => session.Query<AddressingMap, AddressingMapIndex>(index => index.Country == AddressingMapDefaults.AnyCountry).FirstOrDefaultAsync(innerCt),
            ct), cancellationToken);
        return map ?? throw new InvalidOperationException("The global store has no addressing maps; the Crest.Regions geo schema has not been applied.");
    }

    public async Task<GeoNodeModel> AddTenantNodeAsync(GeoTenantNode node, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(node.NodeId))
        {
            throw new ArgumentException("A node id is required.", nameof(node));
        }

        if (!node.NodeId.StartsWith(TenantNodePrefix, StringComparison.Ordinal))
        {
            // Tenant ids are namespaced so they can never collide with a standard id
            // arriving in a later data version.
            node.NodeId = TenantNodePrefix + node.NodeId;
        }

        if (await GetNodeAsync(node.NodeId, cancellationToken) is not null)
        {
            throw new InvalidOperationException($"Geo node '{node.NodeId}' already exists.");
        }

        var parents = await GetNodesAsync(node.ParentIds.Concat(node.GroupIds), cancellationToken);
        var unknown = node.ParentIds.Concat(node.GroupIds).Where(id => !parents.ContainsKey(id)).ToArray();
        if (unknown.Length > 0)
        {
            throw new InvalidOperationException($"Unknown parent node(s): {string.Join(", ", unknown)}.");
        }

        // Cycle check: a new node's parents cannot be the node itself or any of its
        // descendants. A brand-new node has no descendants, so this only trips on
        // self-parenting - but the same check runs on every re-parent (below), where it
        // is load-bearing.
        await EnsureAcyclicAsync(node.NodeId, node.ParentIds.Concat(node.GroupIds), cancellationToken);

        if (node.Level == 0 && node.Kind == GeoNodeKinds.Level)
        {
            var parentLevel = parents.Values.Where(parent => parent.Kind == GeoNodeKinds.Level).Select(parent => parent.Level).DefaultIfEmpty(0).Max();
            node.Level = parentLevel + 1;
        }

        if (string.IsNullOrWhiteSpace(node.Country))
        {
            node.Country = parents.Values.Select(parent => parent.Country).FirstOrDefault(country => !string.IsNullOrEmpty(country)) ?? string.Empty;
        }

        await tenantSession.SaveAsync(node);
        _nodes.Remove(node.NodeId);
        return ToModel(node);
    }

    public async Task SetOverrideAsync(string nodeId, string? label, bool hidden, CancellationToken cancellationToken = default)
    {
        if (await GetNodeAsync(nodeId, cancellationToken) is null)
        {
            throw new InvalidOperationException($"Unknown geo node '{nodeId}'.");
        }

        var existing = await tenantSession.Query<GeoNodeOverride, GeoNodeOverrideIndex>(index => index.NodeId == nodeId).FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(label) && !hidden)
        {
            if (existing is not null)
            {
                tenantSession.Delete(existing);
            }
        }
        else
        {
            existing ??= new GeoNodeOverride { NodeId = nodeId };
            existing.Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
            existing.Hidden = hidden;
            await tenantSession.SaveAsync(existing);
        }

        _nodes.Remove(nodeId);
    }

    public const string TenantNodePrefix = "tenant:";

    private async Task EnsureAcyclicAsync(string nodeId, IEnumerable<string> proposedParents, CancellationToken cancellationToken)
    {
        foreach (var parent in proposedParents)
        {
            if (string.Equals(parent, nodeId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Geo node '{nodeId}' cannot be its own parent.");
            }

            var ancestors = await GetAncestorsAsync(parent, cancellationToken);
            if (ancestors.Any(ancestor => string.Equals(ancestor.NodeId, nodeId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"Parenting '{nodeId}' under '{parent}' would create a cycle: '{parent}' is already beneath it.");
            }
        }
    }

    private static GeoNodeModel Merge(GeoNodeModel node, GeoNodeOverride? item) =>
        item is null ? node : node with { Name = item.Label ?? node.Name, Hidden = item.Hidden };

    private static GeoNodeModel ToModel(GeoNode node) =>
        new(node.NodeId, node.Country, node.Level, node.Kind, node.Name, node.Code, node.Abbreviation, node.ParentIds, node.GroupIds, GeoSources.Standard, false, node.Latitude, node.Longitude);

    private static GeoNodeModel ToModel(GeoTenantNode node) =>
        new(node.NodeId, node.Country, node.Level, node.Kind, node.Name, node.Code, node.Abbreviation, node.ParentIds, node.GroupIds, GeoSources.Tenant);

    private static IReadOnlyList<GeoNodeModel> Ordered(IEnumerable<GeoNodeModel> nodes) =>
        nodes.OrderBy(node => node.Level).ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase).ToArray();
}
