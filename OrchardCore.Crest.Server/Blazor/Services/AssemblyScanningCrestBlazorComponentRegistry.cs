using System.Collections.Concurrent;
using System.Reflection;
using Crest.Components.Blazor;
using Microsoft.AspNetCore.Components;

namespace Crest.Blazor;

// Scans a fixed set of assemblies once (at first use, not per-request) for types carrying
// [CrestBlazorComponent], building a name -> Type map. Resolved once and cached until the
// process restarts - deliberately not weak/sliding-expiration like
// LiquidTemplateManager.GetCachedTemplate, since component types don't change per-request
// the way tenant template text can (a new/changed component requires a module deploy or
// app restart, not a per-request re-scan).
//
// Singleton by design: the catalog is shared across all tenants (see
// ICrestBlazorComponentRegistry's own comment) - there is no per-tenant variant to keep
// separate copies of. Tenant-custom components (an open question, see the plan doc) will
// need their own resolution path layered on top of this, not a fork of it.
public sealed class AssemblyScanningCrestBlazorComponentRegistry : ICrestBlazorComponentRegistry
{
    private readonly Lazy<IReadOnlyDictionary<string, Type>> _components;

    public AssemblyScanningCrestBlazorComponentRegistry(IEnumerable<Assembly> assembliesToScan)
    {
        var assemblies = assembliesToScan.Distinct().ToArray();
        _components = new Lazy<IReadOnlyDictionary<string, Type>>(() => Scan(assemblies));
    }

    public bool TryResolve(string componentName, out Type componentType)
        => _components.Value.TryGetValue(componentName, out componentType);

    private static IReadOnlyDictionary<string, Type> Scan(IReadOnlyList<Assembly> assemblies)
    {
        var map = new ConcurrentDictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).ToArray()!;
            }

            foreach (var type in types)
            {
                if (!typeof(IComponent).IsAssignableFrom(type))
                {
                    continue;
                }

                var marker = type.GetCustomAttribute<CrestBlazorComponentAttribute>();
                if (marker is null)
                {
                    continue;
                }

                map[marker.Name] = type;
            }
        }

        return map;
    }
}
