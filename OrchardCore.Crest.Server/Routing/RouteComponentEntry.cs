namespace Crest.Routing;

// One entry per @page-attributed Blazor component belonging to a routing bucket.
// RoutePattern is always read back from the component's own [Route]/@page literal via
// reflection (see IRouteComponentTableProvider) - never re-typed as a second string
// constant anywhere else, per the standing "no invented path literals" rule (agents.md).
// Bucket is the two-value RouteBucket (Admin/Site), not a raw theme id - see
// ThemeOwnerMetadata's comment for why a theme-id comparison can never correctly decide
// "is this bucket active" (a tenant's admin theme and site theme are both active at once).
public sealed record RouteComponentEntry(
    string RoutePattern,
    Type ComponentType,
    RouteBucket Bucket,
    bool IsDefaultLanding = false);
