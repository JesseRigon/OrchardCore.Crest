namespace Crest.Blazor;

// Name -> compiled component Type lookup for CrestBlazorComponentShapeBindingResolver.
// Deliberately not tenant-scoped: the default catalog is shared across all tenants for
// maximal reuse - a tenant-custom component store is a separate, later extension of this
// contract, not a different one.
public interface ICrestBlazorComponentRegistry
{
    bool TryResolve(string componentName, out Type componentType);
}
