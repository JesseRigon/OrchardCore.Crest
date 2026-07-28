using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Represents the common <see cref="CrestTemplateForm{TItem}" /> API used by
/// its items. Injected as a cascading property in <see cref="ICrestFormComponent" />.
/// </summary>
public interface ICrestForm
{
    /// <summary>
    /// Adds the specified component to the form.
    /// </summary>
    /// <param name="component">The component to add to the form.</param>
    void AddComponent(ICrestFormComponent component);

    /// <summary>
    /// Removes the component from the form.
    /// </summary>
    /// <param name="component">The component to remove from the form.</param>
    void RemoveComponent(ICrestFormComponent component);

    /// <summary>
    /// Finds a form component by its name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The component whose <see cref="ICrestFormComponent.Name" /> equals to <paramref name="name" />; <c>null</c> if such a component is not found.</returns>
    ICrestFormComponent FindComponent(string name);
}

