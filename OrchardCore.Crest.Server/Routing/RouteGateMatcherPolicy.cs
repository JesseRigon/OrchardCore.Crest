using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing; // MatcherPolicy itself lives directly in this namespace
using Microsoft.AspNetCore.Routing.Matching; // IEndpointSelectorPolicy/CandidateSet live here
using Microsoft.Extensions.Logging;
using Crest; // CrestBlazorHosting.ShellBasePathItem

namespace Crest.Routing;

// The gate: vetoes a matched Blazor endpoint whose ThemeOwnerMetadata.Bucket isn't the
// bucket actually serving this request. Runs AFTER endpoint matching
// (IEndpointSelectorPolicy), so the Endpoint/RouteEndpoint.Metadata that Blazor's own SDK
// produced (render-mode negotiation, boot-config metadata) is never touched,
// reconstructed, or reflected over - only whether a given already-matched candidate is
// allowed to win is decided here. This is the documented ASP.NET Core pattern for "veto a
// match based on custom per-request state" (Microsoft's own "A/B Testing Migrated
// Endpoints" migration guide uses the same shape) - not a DynamicRouteValueTransformer
// (wrong pipeline stage, designed for rewriting/generating candidates, not vetoing
// already-generated ones) and not a filtering wrapper around
// RazorComponentEndpointDataSource<App> (internal-shaped, would require re-deriving
// undocumented caching/change-token semantics). See docs/BlazorWeb.md's "Route
// reachability" section for the full research and reasoning.
//
// Bucket disambiguation - NOT a theme-id comparison. Earlier versions of this policy
// invalidated a candidate whose ThemeOwnerMetadata.ThemeId wasn't "the tenant's active
// admin theme OR the tenant's active site theme". That is always true for BOTH buckets at
// once: a tenant's active admin theme and active site theme are independent, simultaneous
// settings, not mutually exclusive alternatives. Confirmed empirically: a clean-tenant GET
// / matched both Admin/Home.razor's endpoint (@page "/", deliberately base-relative - see
// that file's own header comment) and Site/Home.razor's endpoint, and BOTH evaluated as
// "active theme" under the old OR check, leaving two valid "/" candidates and throwing
// AmbiguousMatchException. The two endpoints are not actually ambiguous to a human: which
// one should win is entirely decided by whether BlazorAdminThemeMiddleware routed this
// specific request to the admin shell (it rewrites the request path AND stashes
// CrestBlazorHosting.ShellBasePathItem before endpoint routing ever runs) - the exact
// same signal Components/App.razor itself reads to decide which document (Admin vs Site)
// to render. So: an Admin-bucket candidate is valid iff the middleware marked this
// request as the admin shell; a Site-bucket candidate is valid iff it did NOT (Site is
// the fallback bucket for every request the middleware didn't claim - see
// BlazorAdminThemeMiddleware's IsPageRequest/isAdminRoute/isLoginRoute gating, which
// never even runs for a bare "/" request). The two are deliberately mutually exclusive,
// mirroring exactly the _isAdminShell branch in Components/App.razor - a request that
// the middleware rewrote into "/" (e.g. a bare "/Admin" with no further segments) must
// only ever leave the Admin candidate standing, never both.
public sealed class RouteGateMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
{
    // Runs before Blazor's own render-mode negotiation policies, so a vetoed candidate
    // never reaches that later stage.
    public override int Order => -1000;

    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints) =>
        endpoints.Any(endpoint => endpoint.Metadata.GetMetadata<ThemeOwnerMetadata>() is not null);

    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        // The admin shell marker BlazorAdminThemeMiddleware stashes before UseRouting()
        // runs - the same HttpContext.Items key Components/App.razor reads to pick which
        // document to render. No theme-service calls needed here: whether Admin's bucket
        // is allowed to win is entirely a function of the middleware's own routing
        // decision, already made once per request.
        var isAdminShellRequest = httpContext.Items.TryGetValue(CrestBlazorHosting.ShellBasePathItem, out var shellBasePath)
            && shellBasePath is string { Length: > 0 };

        var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger<RouteGateMatcherPolicy>();

        for (var index = 0; index < candidates.Count; index++)
        {
            if (!candidates.IsValidCandidate(index))
            {
                continue;
            }

            var themeOwner = candidates[index].Endpoint?.Metadata.GetMetadata<ThemeOwnerMetadata>();
            if (themeOwner is null)
            {
                continue;
            }

            var isValidForBucket = themeOwner.Bucket switch
            {
                RouteBucket.Admin => isAdminShellRequest,
                RouteBucket.Site => !isAdminShellRequest,
                _ => false,
            };

            if (!isValidForBucket)
            {
                logger.LogDebug(
                    "RouteGateMatcherPolicy vetoed candidate {DisplayName} (bucket {Bucket}) for {Path}: isAdminShellRequest={IsAdminShellRequest}.",
                    candidates[index].Endpoint.DisplayName,
                    themeOwner.Bucket,
                    httpContext.Request.Path,
                    isAdminShellRequest);
                candidates.SetValidity(index, false);
            }
        }

        return Task.CompletedTask;
    }
}
