using Microsoft.AspNetCore.Components;

namespace Crest.Routing;

// Shared scan step behind every IRouteComponentTableProvider: read the [Route] attribute
// @page compiles to, straight off the component type via reflection. This is the
// registration point itself (per agents.md's "no invented path literals" rule) - nobody
// re-types a route string anywhere else; every other piece of code that needs to know a
// route consults the resulting RouteComponentEntry, not a second, hand-typed constant.
public static class AssemblyRouteComponentScanner
{
    public static IEnumerable<RouteComponentEntry> Scan(System.Reflection.Assembly assembly, RouteBucket bucket, string? defaultLandingRoutePattern = null)
    {
        foreach (var type in assembly.GetExportedTypes())
        {
            if (!typeof(IComponent).IsAssignableFrom(type))
            {
                continue;
            }

            foreach (var routeAttribute in type.GetCustomAttributes(typeof(RouteAttribute), inherit: false).Cast<RouteAttribute>())
            {
                yield return new RouteComponentEntry(
                    routeAttribute.Template,
                    type,
                    bucket,
                    IsDefaultLanding: defaultLandingRoutePattern is not null
                        && string.Equals(routeAttribute.Template, defaultLandingRoutePattern, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
