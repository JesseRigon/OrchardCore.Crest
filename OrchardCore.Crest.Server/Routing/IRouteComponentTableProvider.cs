namespace Crest.Routing;

// One implementation per Blazor theme (Admin, Site, ...). Each theme is the sole authority
// on its own routes - nobody hand-maintains a central route list anywhere else. Mirrors
// IShapeTableProvider's role for shapes: this is the "scan my own assembly by convention"
// contract, not a registration list.
public interface IRouteComponentTableProvider
{
    RouteBucket Bucket { get; }

    IEnumerable<RouteComponentEntry> GetRouteComponents();
}
