using Crest.Regions.Models;

namespace Crest.Regions.Services;

/// <summary>
/// Point-in-polygon: which boundary nodes (districts that do not follow the hierarchy)
/// a point falls in. A provider seam like icons and tax: the built-in implementation
/// tests the global store's MultiPolygons in-process; external providers can be
/// registered by the host alongside or instead.
/// </summary>
public interface IGeoBoundaryResolver
{
    string Name { get; }
    int Priority { get; }
    /// <summary>Node ids of every boundary containing the point, restricted to the given country's nodes when known. Null means "cannot say".</summary>
    Task<IReadOnlyList<string>?> ResolveAsync(double latitude, double longitude, string? country, CancellationToken cancellationToken = default);
}

/// <summary>Address → point. No built-in exists; external providers register here.</summary>
public interface IGeocoder
{
    string Name { get; }
    int Priority { get; }
    Task<(double Latitude, double Longitude)?> GeocodeAsync(AddressInput address, CancellationToken cancellationToken = default);
}

/// <summary>Fans out over the registered resolvers, highest priority first; the first that can say, says.</summary>
public interface IGeoLocator
{
    Task<IReadOnlyList<string>> BoundariesAsync(double latitude, double longitude, string? country, CancellationToken cancellationToken = default);
    Task<(double Latitude, double Longitude)?> GeocodeAsync(AddressInput address, CancellationToken cancellationToken = default);
}

public sealed class CompositeGeoLocator(IEnumerable<IGeoBoundaryResolver> boundaryResolvers, IEnumerable<IGeocoder> geocoders) : IGeoLocator
{
    public async Task<IReadOnlyList<string>> BoundariesAsync(double latitude, double longitude, string? country, CancellationToken cancellationToken = default)
    {
        foreach (var resolver in boundaryResolvers.OrderByDescending(resolver => resolver.Priority))
        {
            var found = await resolver.ResolveAsync(latitude, longitude, country, cancellationToken);
            if (found is not null)
            {
                return found;
            }
        }

        return [];
    }

    public async Task<(double Latitude, double Longitude)?> GeocodeAsync(AddressInput address, CancellationToken cancellationToken = default)
    {
        foreach (var geocoder in geocoders.OrderByDescending(geocoder => geocoder.Priority))
        {
            var point = await geocoder.GeocodeAsync(address, cancellationToken);
            if (point is not null)
            {
                return point;
            }
        }

        return null;
    }
}
