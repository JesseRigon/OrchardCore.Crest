namespace Crest.Routing;

// Scans Admin's own client assembly, plus every module-contributed *.BlazorWasm
// assembly (e.g. Accounting.BlazorWasm), for @page-attributed components - the same
// two-convention set Startup.cs's ThemeOwnerMetadata stamping and AddAdditionalAssemblies
// force-load already use, so a module's pages are reachable through the gate instead of
// only rendering (kept in sync deliberately, see Startup.cs's Configure comment).
// Bucket is the fixed RouteBucket.Admin value, not a theme-id lookup - see
// ThemeOwnerMetadata's comment for why a raw theme-id comparison is the wrong question.
public sealed class AdminRouteComponentTableProvider : IRouteComponentTableProvider
{
    public RouteBucket Bucket => RouteBucket.Admin;

    public IEnumerable<RouteComponentEntry> GetRouteComponents()
    {
        var adminAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name is { } name
                && (name == "OrchardCore.Crest.Admin.Client" || name.EndsWith(".BlazorWasm", StringComparison.Ordinal)));

        return adminAssemblies.SelectMany(assembly =>
            AssemblyRouteComponentScanner.Scan(assembly, Bucket, defaultLandingRoutePattern: "/Dashboard"));
    }
}
