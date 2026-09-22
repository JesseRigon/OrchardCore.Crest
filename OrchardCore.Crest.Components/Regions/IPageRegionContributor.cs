using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace Crest.Components.Regions;

/// <summary>
/// The UI-injection seam (plans/taxes.md › UI injection): a page declares named regions
/// with <see cref="CrestPageRegion"/>; a DOWNSTREAM module contributes a component to a
/// region key without the page's module referencing it - the page-region analogue of
/// IRouteComponentTableProvider. Nothing registered renders nothing, which is the correct
/// disabled behaviour. Region keys belong to the module owning the page, in its Domain.
/// </summary>
public interface IPageRegionContributor
{
    string RegionKey { get; }
    int Order { get; }
    /// <summary>The component to render; it receives the region's context as the parameter named <see cref="CrestPageRegion.ContextParameterName"/>.</summary>
    Type ComponentType { get; }
}

/// <summary>Discovers contributors across the loaded module assemblies. Configured once at client startup from the module assembly registry.</summary>
public static class PageRegionRegistry
{
    private static IReadOnlyList<IPageRegionContributor> _contributors = [];

    public static void Configure(IEnumerable<Assembly> assemblies)
    {
        var found = new List<IPageRegionContributor>();
        foreach (var assembly in assemblies.Append(typeof(PageRegionRegistry).Assembly).Distinct())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(type => type is not null).Select(type => type!).ToArray();
            }

            foreach (var type in types)
            {
                if (type.IsAbstract || !typeof(IPageRegionContributor).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) is null)
                {
                    continue;
                }

                if (Activator.CreateInstance(type) is IPageRegionContributor contributor)
                {
                    found.Add(contributor);
                }
            }
        }

        _contributors = found.OrderBy(contributor => contributor.Order).ToArray();
    }

    public static IReadOnlyList<IPageRegionContributor> For(string regionKey) =>
        _contributors.Where(contributor => string.Equals(contributor.RegionKey, regionKey, StringComparison.Ordinal)).ToArray();
}
