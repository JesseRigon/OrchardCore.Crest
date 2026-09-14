namespace Crest.Routing;

// One entry per @page-attributed Blazor component belonging to a routing bucket.
// RoutePattern is always read back from the component's own [Route]/@page literal via
// reflection (see IRouteComponentTableProvider) - never re-typed as a second string
// constant anywhere else, per the standing "no invented path literals" rule (agents.md).
// Bucket is the two-value RouteBucket (Admin/Site), not a raw theme id - see
// ThemeOwnerMetadata's comment for why a theme-id comparison can never correctly decide
// "is this bucket active" (a tenant's admin theme and site theme are both active at once).
// AllowsAnonymous is read off the component's own [AllowAnonymous] attribute (the same
// ASP.NET marker MVC uses): such a page is served without the admin shell's login
// redirect or route authorization, and rendered shell-less - the seam a module uses for
// a public surface that lives in the admin bucket (a member portal's login/registration
// pages, say) without Crest knowing anything about that module's parties or classes.
public sealed record RouteComponentEntry(
    string RoutePattern,
    Type ComponentType,
    RouteBucket Bucket,
    bool IsDefaultLanding = false,
    bool AllowsAnonymous = false);
