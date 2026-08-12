using Crest.Services;
using Microsoft.AspNetCore.Http;

namespace Crest.Routing;

// The built artifact: read-only once constructed, one instance per (active Admin theme id,
// active Site theme id) pair for a shell - see DefaultRouteComponentTableManager for the
// caching/keying policy. Matching reuses CrestRouteAuthorizationService's existing
// segment-tokenized template grammar rather than inventing a second one.
public sealed class RouteComponentTable(IReadOnlyList<RouteComponentEntry> entries)
{
    public IReadOnlyList<RouteComponentEntry> Entries { get; } = entries;

    public bool TryMatch(PathString path, out RouteComponentEntry entry)
    {
        foreach (var candidate in Entries)
        {
            if (CrestRouteAuthorizationService.Matches(candidate.RoutePattern, path.Value))
            {
                entry = candidate;
                return true;
            }
        }

        entry = null!;
        return false;
    }

    public RouteComponentEntry? DefaultLanding(RouteBucket bucket) =>
        Entries.FirstOrDefault(candidate => candidate.IsDefaultLanding && candidate.Bucket == bucket);
}
