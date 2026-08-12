namespace Crest.Routing;

// Which routing bucket a Blazor endpoint belongs to. Deliberately NOT a raw theme-id
// string: routing only ever needs to answer "is this the currently-active Admin shell,
// or the Site shell" - never "which literal theme id is active." A tenant always has
// exactly one active admin theme AND one active site theme AT THE SAME TIME (they are
// independent Orchard settings, not mutually exclusive), so comparing a component's own
// theme id against "is it either of the two active theme ids" can never disambiguate an
// Admin-vs-Site collision (e.g. both Admin/Home.razor and Site/Home.razor declaring
// @page "/") - both sides of that OR are simultaneously true by construction. See
// RouteGateMatcherPolicy for how RouteBucket.Admin candidates are actually gated (via
// BlazorAdminThemeMiddleware's own shell-selection signal, not a theme-id comparison).
public enum RouteBucket
{
    Admin,
    Site,
}

// Attached to a Blazor endpoint via an endpoint convention (see Startup.Configure),
// sourced from the matching RouteComponentEntry.ThemeId at the moment the endpoint is
// mapped, mapped down to the two-value Bucket - never a second, re-typed theme id
// literal. RouteGateMatcherPolicy is the sole consumer: it vetoes a matched Admin-bucket
// candidate for any request BlazorAdminThemeMiddleware did not itself route to the admin
// shell.
public sealed record ThemeOwnerMetadata(RouteBucket Bucket);
