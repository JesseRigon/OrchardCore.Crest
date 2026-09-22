using Crest.Global;
using Crest.Regions.Indexes;
using Crest.Regions.Models;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using YesSql;
using YesSql.Services;

namespace Crest.Regions.Services;

/// <summary>
/// The built-in point-in-polygon: loads GeoBoundary MultiPolygons lazily from the global
/// store and tests with NetTopologySuite, in-process, no network. Only grouping nodes of
/// the point's country are tested, so a boundary set is never enumerated wholesale.
/// </summary>
public sealed class NetTopologySuiteBoundaryResolver(ICrestGlobalStore globalStore) : IGeoBoundaryResolver
{
    private static readonly WKTReader WktReader = new();

    public string Name => "NetTopologySuite";
    public int Priority => 0;

    public async Task<IReadOnlyList<string>?> ResolveAsync(double latitude, double longitude, string? country, CancellationToken cancellationToken = default)
    {
        var candidates = await globalStore.ReadAsync(async (session, ct) =>
        {
            var query = string.IsNullOrWhiteSpace(country)
                ? session.Query<GeoNode, GeoNodeIndex>(index => index.Kind == GeoNodeKinds.Grouping)
                : session.Query<GeoNode, GeoNodeIndex>(index => index.Kind == GeoNodeKinds.Grouping && index.Country == country);
            var groupings = await query.ListAsync(ct);
            var ids = groupings.Select(node => node.NodeId).ToArray();
            return ids.Length == 0 ? [] : await session.Query<GeoBoundary, GeoBoundaryIndex>(index => index.NodeId.IsIn(ids)).ListAsync(ct);
        }, cancellationToken);

        var point = new Point(longitude, latitude);
        var inside = new List<string>();
        foreach (var boundary in candidates)
        {
            Geometry geometry;
            try
            {
                // WKT only in the built-in; GeoJSON boundaries need NetTopologySuite.IO.GeoJSON, an
                // external-provider concern. A GeoJSON row is skipped, never misread.
                if (!boundary.Format.Equals("WKT", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                geometry = WktReader.Read(boundary.Geometry);
            }
            catch
            {
                // A malformed boundary is a data defect; it cannot make an address wrong, only unmatched.
                continue;
            }

            if (geometry.Contains(point))
            {
                inside.Add(boundary.NodeId);
            }
        }

        return inside;
    }
}
