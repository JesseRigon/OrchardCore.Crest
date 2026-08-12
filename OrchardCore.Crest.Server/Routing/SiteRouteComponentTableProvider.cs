namespace Crest.Routing;

// Scans Site's own client assembly for @page-attributed components. Bucket is the fixed
// RouteBucket.Site value - see ThemeOwnerMetadata's comment for why routing works in
// terms of the two-value bucket rather than a raw theme id.
public sealed class SiteRouteComponentTableProvider : IRouteComponentTableProvider
{
    public RouteBucket Bucket => RouteBucket.Site;

    public IEnumerable<RouteComponentEntry> GetRouteComponents()
    {
        var siteClientAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => assembly.GetName().Name == "OrchardCore.Crest.Site.Client");

        return siteClientAssembly is null
            ? []
            : AssemblyRouteComponentScanner.Scan(siteClientAssembly, Bucket, defaultLandingRoutePattern: "/");
    }
}
